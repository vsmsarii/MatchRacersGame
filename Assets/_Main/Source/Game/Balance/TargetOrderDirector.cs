using UnityEngine;

namespace MatchRacers
{
    public sealed class TargetOrderDirector : IPaceController
    {
        private const float MinElapsedSeconds = 0.35f;
        private const float MinTimeToFinish = 0.4f;
        private const float ChallengeAttackTimeoutSeconds = 1f;
        private const float ChallengeRetrySeconds = 0.5f;
        private const float ChallengeMinPassMeters = 5f;
        private const float MinSideGapMeters = 5f;
        private const float ComebackAbortGapMeters = 15f;
        private const float ComebackUrgentMarginSeconds = 1f;

        private readonly RaceConfigSO m_Config;
        private readonly int m_CarCount;
        private readonly float[] m_SlotJitter;
        private readonly float[] m_Current;
        private readonly float[] m_Previous;
        private readonly float[] m_TargetGap;
        private readonly bool[] m_IsAheadSide;
        private readonly int[] m_Ahead;
        private readonly int[] m_Behind;
        private readonly ETargetChallengePhase[] m_Phase;
        private readonly float[] m_PhaseTimer;
        private readonly float[] m_ChallengeGoal;
        private readonly int[] m_ChallengeKey;
        private readonly int[] m_AcceptedAtStart;
        private readonly float[] m_Headroom;
        private readonly float[] m_AheadMargin;
        private readonly float[] m_WanderPhase;
        private readonly float[] m_WanderRate;
        private readonly float[] m_BandScale;
        private readonly float[] m_LaunchPace;
        private readonly float[] m_Spread;
        private readonly int[] m_ByDistance;
        private readonly int[] m_Cluster;
        private readonly bool[] m_PushForward;
        private readonly bool[] m_PushBack;
        private readonly bool[] m_WantsSwap;
        private readonly float[] m_GlueTime;
        private readonly float[] m_SwapDistance;
        private readonly int[] m_NearRank;
        private readonly float[] m_ChaseTimer;
        private readonly float[] m_ChaseDelay;
        private readonly bool[] m_Settling;
        private bool m_LaunchSettled;

        private int m_TargetPosition = 1;
        private float m_PlayerAverageSpeed;
        private float m_MarginAhead;
        private float m_MarginBehind;
        private float m_Progress;
        private double m_PlayerBestFinish;
        private double m_PlayerWorstFinish;
        private Xorshift m_ChallengeRng;
        private float m_NextChallengeTime;
        private float m_NextComebackTime;
        private int m_ChallengeCount;
        private int m_ComebackCount;

        public int TargetPosition => m_TargetPosition;
        public float MarginAhead => m_MarginAhead;
        public float MarginBehind => m_MarginBehind;
        public double PlayerBestFinish => m_PlayerBestFinish;
        public double PlayerWorstFinish => m_PlayerWorstFinish;
        public int ChallengeCount => m_ChallengeCount;
        public int ComebackCount => m_ComebackCount;
        public bool IsLaunching => !m_LaunchSettled;

        public TargetOrderDirector(RaceConfigSO config, int carCount)
        {
            m_Config = config;
            m_CarCount = carCount;
            m_SlotJitter = new float[carCount];
            m_Current = new float[carCount];
            m_Previous = new float[carCount];
            m_TargetGap = new float[carCount];
            m_IsAheadSide = new bool[carCount];
            m_Ahead = new int[carCount];
            m_Behind = new int[carCount];
            m_Phase = new ETargetChallengePhase[carCount];
            m_PhaseTimer = new float[carCount];
            m_ChallengeGoal = new float[carCount];
            m_ChallengeKey = new int[carCount];
            m_AcceptedAtStart = new int[carCount];
            m_Headroom = new float[carCount];
            m_AheadMargin = new float[carCount];
            m_WanderPhase = new float[carCount];
            m_WanderRate = new float[carCount];
            m_BandScale = new float[carCount];
            m_LaunchPace = new float[carCount];
            m_Spread = new float[carCount];
            m_ByDistance = new int[carCount];
            m_Cluster = new int[carCount];
            m_PushForward = new bool[carCount];
            m_PushBack = new bool[carCount];
            m_WantsSwap = new bool[carCount];
            m_GlueTime = new float[carCount];
            m_SwapDistance = new float[carCount];
            m_NearRank = new int[carCount];
            m_ChaseTimer = new float[carCount];
            m_ChaseDelay = new float[carCount];
            m_Settling = new bool[carCount];
        }

        public float GetTargetGap(int carIndex)
        {
            return carIndex >= 0 && carIndex < m_TargetGap.Length ? m_TargetGap[carIndex] : 0f;
        }

        public bool IsAheadSide(int carIndex)
        {
            return carIndex >= 0 && carIndex < m_IsAheadSide.Length && m_IsAheadSide[carIndex];
        }

        public ETargetChallengePhase GetChallengePhase(int carIndex)
        {
            return carIndex >= 0 && carIndex < m_Phase.Length ? m_Phase[carIndex] : ETargetChallengePhase.None;
        }

        public bool IsSettling(int carIndex)
        {
            return carIndex >= 0 && carIndex < m_Settling.Length && m_Settling[carIndex];
        }

        public void Reset(CarState[] cars, uint seed, int targetPosition)
        {
            m_TargetPosition = Mathf.Clamp(targetPosition, 1, m_CarCount);
            m_PlayerAverageSpeed = m_Config.BaseSpeed;
            m_MarginAhead = m_Config.TargetMarginStartMeters;
            m_MarginBehind = m_Config.TargetMarginStartMeters;
            m_Progress = 0f;
            m_PlayerBestFinish = 0.0;
            m_PlayerWorstFinish = 0.0;

            Xorshift rng = new Xorshift(seed ^ 0x9E3779B9u);

            int rivalCount = 0;
            for (int i = 0; i < m_CarCount; i++)
            {
                m_Current[i] = 1f;
                m_Previous[i] = 1f;
                m_TargetGap[i] = 0f;
                m_IsAheadSide[i] = false;
                m_SlotJitter[i] = rng.Range(-m_Config.TargetSlotJitter, m_Config.TargetSlotJitter);
                m_Phase[i] = ETargetChallengePhase.None;
                m_PhaseTimer[i] = 0f;
                m_ChallengeGoal[i] = 0f;
                m_ChallengeKey[i] = 0;
                m_AcceptedAtStart[i] = 0;
                m_Headroom[i] = 0f;
                m_AheadMargin[i] = 0f;
                m_LaunchPace[i] = 1f;
                m_Spread[i] = 0f;
                m_GlueTime[i] = 0f;
                m_SwapDistance[i] = 0f;
                m_Settling[i] = false;
                cars[i].BalanceMultiplier = 1f;
                cars[i].PaceBias = 0f;

                if (i != 0)
                    m_Ahead[rivalCount++] = i;
            }

            for (int i = rivalCount - 1; i > 0; i--)
            {
                int j = rng.Range(0, i + 1);
                (m_Ahead[i], m_Ahead[j]) = (m_Ahead[j], m_Ahead[i]);
            }

            int aheadCount = Mathf.Min(m_TargetPosition - 1, rivalCount);
            int behindCount = rivalCount - aheadCount;
            for (int k = 0; k < rivalCount; k++)
            {
                int carIndex = m_Ahead[k];
                m_IsAheadSide[carIndex] = k < aheadCount;

                float rank = k < aheadCount
                    ? (aheadCount - k) / (float)Mathf.Max(1, aheadCount)
                    : -(k - aheadCount + 1) / (float)Mathf.Max(1, behindCount);
                m_LaunchPace[carIndex] = 1f + m_Config.TargetLaunchSpread * rank;
            }

            m_ChallengeRng = new Xorshift(seed ^ 0x85EBCA6Bu);
            for (int i = 0; i < m_CarCount; i++)
            {
                float period = m_ChallengeRng.Range(m_Config.TargetWanderPeriodMinSeconds, m_Config.TargetWanderPeriodMaxSeconds);
                m_WanderPhase[i] = m_ChallengeRng.Range(0f, 2f * Mathf.PI);
                m_WanderRate[i] = 2f * Mathf.PI / Mathf.Max(0.5f, period);
                m_BandScale[i] = 1f - m_ChallengeRng.Range(0f, m_Config.TargetNearBandSpread);
                m_ChaseDelay[i] = m_ChallengeRng.Range(m_Config.TargetChaseReactionMinSeconds,
                    m_Config.TargetChaseReactionMaxSeconds);
                m_ChaseTimer[i] = 0f;
                m_NearRank[i] = 0;
            }

            m_ChallengeCount = 0;
            m_ComebackCount = 0;
            m_LaunchSettled = false;
            m_NextChallengeTime = m_Config.CountdownSeconds + NextChallengeInterval();
            m_NextComebackTime = m_Config.CountdownSeconds + NextComebackInterval();
        }

        public void Step(CarState[] cars, int playerCarIndex, float raceTime, float playerLastBuffTime, float deltaTime)
        {
            System.Array.Copy(m_Current, m_Previous, m_CarCount);

            CarState player = cars[playerCarIndex];
            player.BalanceMultiplier = 1f;
            player.PaceBias = 0f;

            float elapsed = Mathf.Max(MinElapsedSeconds, raceTime - m_Config.CountdownSeconds);
            float sample = player.Distance > 1f ? player.Distance / elapsed : m_Config.BaseSpeed;
            m_PlayerAverageSpeed = Mathf.Lerp(m_PlayerAverageSpeed, Mathf.Max(1f, sample),
                1f - Mathf.Exp(-deltaTime / m_Config.TargetPlayerSpeedEmaSeconds));

            m_Progress = Mathf.Clamp01(player.Distance / m_Config.RaceLengthMeters);
            float rampEnd = Mathf.Max(0.05f, m_Config.TargetMarginRampEnd);
            float ramp = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(m_Progress / rampEnd));
            m_MarginAhead = Mathf.Lerp(m_Config.TargetMarginStartMeters, m_Config.TargetMarginAheadEndMeters, ramp);
            m_MarginBehind = Mathf.Lerp(m_Config.TargetMarginStartMeters, m_Config.TargetMarginBehindEndMeters, ramp);

            int aheadCount = SortSide(cars, playerCarIndex, true, m_Ahead);
            int behindCount = SortSide(cars, playerCarIndex, false, m_Behind);

            for (int slot = 0; slot < aheadCount; slot++)
                m_NearRank[m_Ahead[slot]] = aheadCount - 1 - slot;

            for (int slot = 0; slot < behindCount; slot++)
                m_NearRank[m_Behind[slot]] = slot;

            UpdateChallenges(cars, player, raceTime, deltaTime, behindCount);
            UpdateComebacks(cars, player, raceTime, deltaTime, aheadCount);

            float remaining = m_Config.RaceLengthMeters - player.Distance;
            float timeToFinish = Mathf.Max(MinTimeToFinish, remaining / m_PlayerAverageSpeed);
            float correctionHorizon = Mathf.Min(timeToFinish, m_Config.TargetCorrectionHorizonSeconds);
            float aheadSeparation = m_Config.TargetSeparationMeters;
            float behindSeparation = m_Config.TargetBehindSeparationMeters;
            float maxStep = m_Config.TargetPaceRatePerSecond * deltaTime;

            for (int slot = 0; slot < aheadCount; slot++)
                PlanTargetGap(m_Ahead[slot], m_MarginAhead + aheadSeparation * (aheadCount - 1 - slot), true, raceTime);

            for (int slot = 0; slot < behindCount; slot++)
                PlanTargetGap(m_Behind[slot], -m_MarginBehind - behindSeparation * slot, false, raceTime);

            SpreadTargetGaps(aheadCount, behindCount);
            UpdateSpacing(cars, player, deltaTime);

            bool launching = raceTime - m_Config.CountdownSeconds < m_Config.TargetLaunchSeconds;
            if (!launching && !m_LaunchSettled)
            {
                m_LaunchSettled = true;
                for (int i = 0; i < m_CarCount; i++)
                    m_Settling[i] = !cars[i].IsPlayer;
            }

            for (int slot = 0; slot < aheadCount; slot++)
                Drive(cars[m_Ahead[slot]], player, correctionHorizon, maxStep, launching);

            for (int slot = 0; slot < behindCount; slot++)
                Drive(cars[m_Behind[slot]], player, correctionHorizon, maxStep, launching);

            ApplyEnvelope(cars, player, raceTime, maxStep);
        }

        private void PlanTargetGap(int carIndex, float rawTargetGap, bool aheadSide, float raceTime)
        {
            if (m_Phase[carIndex] != ETargetChallengePhase.None && m_Phase[carIndex] != ETargetChallengePhase.Fallback)
            {
                m_TargetGap[carIndex] = m_ChallengeGoal[carIndex];
                return;
            }

            float wander = m_Config.TargetWanderMeters *
                           Mathf.Sin(m_WanderPhase[carIndex] + m_WanderRate[carIndex] * raceTime);
            float targetGap = rawTargetGap * (1f + m_SlotJitter[carIndex]) + wander;
            m_TargetGap[carIndex] = aheadSide
                ? Mathf.Max(MinSideGapMeters, targetGap)
                : Mathf.Min(-MinSideGapMeters, targetGap);
        }

        private void SpreadTargetGaps(int aheadCount, int behindCount)
        {
            float spacing = m_Config.TargetRivalSpacingMeters;

            bool hasPrevious = false;
            float previous = 0f;
            for (int slot = aheadCount - 1; slot >= 0; slot--)
            {
                int i = m_Ahead[slot];
                if (m_Phase[i] != ETargetChallengePhase.None)
                    continue;

                if (hasPrevious)
                    m_TargetGap[i] = Mathf.Max(m_TargetGap[i], previous + spacing);

                previous = m_TargetGap[i];
                hasPrevious = true;
            }

            hasPrevious = false;
            for (int slot = 0; slot < behindCount; slot++)
            {
                int i = m_Behind[slot];
                if (m_Phase[i] != ETargetChallengePhase.None)
                    continue;

                if (hasPrevious)
                    m_TargetGap[i] = Mathf.Min(m_TargetGap[i], previous - spacing);

                previous = m_TargetGap[i];
                hasPrevious = true;
            }
        }

        private void Drive(CarState car, CarState player, float timeToFinish, float maxStep, bool launching)
        {
            if (car.Finished)
            {
                car.BalanceMultiplier = 1f;
                car.PaceBias = 0f;
                car.NearGateWeight = 0f;
                return;
            }

            int i = car.CarIndex;
            car.NearGateWeight = GetNearBand(car, player, out float low, out float high);

            ETargetChallengePhase phase = m_Phase[i];
            float targetGap = m_TargetGap[i];

            float gap = car.Distance - player.Distance;
            float requiredSpeed = m_PlayerAverageSpeed + (targetGap - gap) / timeToFinish;
            float desired = requiredSpeed / Mathf.Max(0.001f, m_Config.BaseSpeed * car.BaseSpeedMultiplier);

            car.PaceBias = Mathf.Clamp((desired - 1f) * m_Config.TargetBiasResponse, -1f, 1f);

            bool holdingStation = m_Progress >= m_Config.TargetAttackLockProgress &&
                                  Mathf.Abs(gap) < m_Config.TargetAttackLockGapMeters;
            if (holdingStation || phase == ETargetChallengePhase.Lead || phase == ETargetChallengePhase.Fallback ||
                phase == ETargetChallengePhase.Return)
                car.PaceBias = -1f;

            if (phase == ETargetChallengePhase.Fallback || phase == ETargetChallengePhase.Return)
            {
                float neutral = player.BaseSpeedMultiplier / Mathf.Max(0.001f, car.BaseSpeedMultiplier);
                float drift = Mathf.Clamp(neutral * (1f - m_Config.TargetChallengeFallbackPace),
                    m_Config.TargetPaceMin, m_Config.TargetPaceMax);
                desired = phase == ETargetChallengePhase.Fallback ? drift : Mathf.Min(desired, drift);
                low = Mathf.Min(low, drift);
            }
            else if (phase == ETargetChallengePhase.None && launching)
            {
                desired = m_LaunchPace[i];
                low = m_Config.TargetPaceMin;
                high = m_Config.TargetPaceMax;
            }
            else if (phase == ETargetChallengePhase.None && m_Spread[i] != 0f)
            {
                float inGate = Mathf.Lerp(low, high, (m_Spread[i] + 1f) * 0.5f);
                float outside = Mathf.Clamp(desired, low, high) * (1f + m_Spread[i] * m_Config.TargetRivalSpacingPace);
                desired = Mathf.Lerp(outside, inGate, car.NearGateWeight);
            }

            float currentLow = m_Settling[i] ? Mathf.Min(low, m_Current[i]) : low;
            float currentHigh = m_Settling[i] ? Mathf.Max(high, m_Current[i]) : high;

            if (car.HasActiveBuff)
            {
                car.BalanceMultiplier = 1f;
                m_Current[i] = Mathf.Clamp(m_Current[i], currentLow, currentHigh);
                return;
            }

            desired = Mathf.Clamp(desired, low, high);
            m_Current[i] += Mathf.Clamp(desired - m_Current[i], -maxStep, maxStep);
            m_Current[i] = Mathf.Clamp(m_Current[i], currentLow, currentHigh);
            car.BalanceMultiplier = m_Current[i];

            if (m_Settling[i] && m_Current[i] >= low && m_Current[i] <= high)
                m_Settling[i] = false;
        }

        private void UpdateSpacing(CarState[] cars, CarState player, float deltaTime)
        {
            int count = 0;
            for (int i = 0; i < m_CarCount; i++)
            {
                m_PushForward[i] = false;
                m_PushBack[i] = false;
                m_WantsSwap[i] = false;
                m_Spread[i] = 0f;

                if (!cars[i].Finished)
                    m_ByDistance[count++] = i;
            }

            SortByDistance(cars, m_ByDistance, count);

            float spacing = m_Config.TargetRivalSpacingMeters;
            int start = 0;
            for (int k = 1; k <= count; k++)
            {
                if (k < count)
                {
                    CarState front = cars[m_ByDistance[k - 1]];
                    CarState rear = cars[m_ByDistance[k]];
                    if (front.Distance - rear.Distance < spacing && !front.HasActiveBuff && !rear.HasActiveBuff)
                        continue;
                }

                SpreadCluster(cars, start, k);
                start = k;
            }

            for (int i = 0; i < m_CarCount; i++)
            {
                CarState car = cars[i];
                if (car.IsPlayer)
                    continue;

                m_GlueTime[i] = m_WantsSwap[i] ? m_GlueTime[i] + deltaTime : 0f;

                if (m_GlueTime[i] < m_Config.TargetRivalGlueSeconds || m_IsAheadSide[i] ||
                    m_Phase[i] != ETargetChallengePhase.None || !m_PushBack[i] ||
                    car.Distance - player.Distance <= -MinSideGapMeters)
                    continue;

                m_Phase[i] = ETargetChallengePhase.Fallback;
                m_PhaseTimer[i] = 0f;
                m_GlueTime[i] = 0f;
            }
        }

        private void SpreadCluster(CarState[] cars, int start, int end)
        {
            int size = end - start;
            if (size < 2)
                return;

            float stuckSpeed = m_Config.TargetRivalStuckSpeed;
            for (int k = start; k + 1 < end; k++)
            {
                CarState front = cars[m_ByDistance[k]];
                CarState rear = cars[m_ByDistance[k + 1]];

                if (!IsSpacingFree(front) || !IsSpacingFree(rear) || DesiredGap(front) >= DesiredGap(rear))
                    continue;

                m_PushBack[front.CarIndex] = true;
                m_PushForward[rear.CarIndex] = true;

                if (Mathf.Abs(front.Speed - rear.Speed) > stuckSpeed)
                    continue;

                m_WantsSwap[front.CarIndex] = true;
                m_WantsSwap[rear.CarIndex] = true;
                m_SwapDistance[rear.CarIndex] = front.Distance - rear.Distance;
            }

            for (int k = 0; k < size; k++)
                m_Cluster[k] = m_ByDistance[start + k];

            for (int k = 1; k < size; k++)
            {
                int carIndex = m_Cluster[k];
                float wants = DesiredGap(cars[carIndex]);
                int j = k - 1;

                while (j >= 0 && DesiredGap(cars[m_Cluster[j]]) < wants)
                {
                    m_Cluster[j + 1] = m_Cluster[j];
                    j--;
                }

                m_Cluster[j + 1] = carIndex;
            }

            for (int rank = 0; rank < size; rank++)
            {
                int carIndex = m_Cluster[rank];
                if (cars[carIndex].IsPlayer || m_Phase[carIndex] != ETargetChallengePhase.None)
                    continue;

                m_Spread[carIndex] = 1f - 2f * rank / (size - 1);
            }
        }

        private float DesiredGap(CarState car)
        {
            return car.IsPlayer ? 0f : m_TargetGap[car.CarIndex];
        }

        private bool IsSpacingFree(CarState car)
        {
            return car.IsPlayer || m_Phase[car.CarIndex] == ETargetChallengePhase.None;
        }

        private void TryCommandSwap(CarState car)
        {
            int i = car.CarIndex;
            if (m_GlueTime[i] < m_Config.TargetRivalGlueSeconds || !m_WantsSwap[i] || !m_PushForward[i] ||
                car.HasActiveBuff || car.CooldownStepsRemaining > 0)
                return;

            int key = ChooseAttackKey(car, m_SwapDistance[i], m_Config.TargetChallengeLeadMeters, car.MaxBuffKey, out _);
            if (key == 0)
                return;

            car.CommandedBuffKey = key;
            m_GlueTime[i] = 0f;
        }

        private bool TryCommandChase(CarState car, CarState player, int maxKey)
        {
            int i = car.CarIndex;
            float lag = m_TargetGap[i] - (car.Distance - player.Distance);
            if (!m_Config.TargetChaseEnabled || m_NearRank[i] >= m_Config.TargetChaseSlots ||
                m_Progress >= m_Config.TargetAttackLockProgress || lag < m_Config.TargetChaseLagMeters)
            {
                m_ChaseTimer[i] = 0f;
                return false;
            }

            m_ChaseTimer[i] += m_Config.FixedDeltaTime;
            if (m_ChaseTimer[i] < m_ChaseDelay[i] || car.HasActiveBuff || car.CooldownStepsRemaining > 0)
                return false;

            BuffTableSO table = m_Config.BuffTable;
            float gainPerLevel = m_Config.BaseSpeed * car.BaseSpeedMultiplier * table.WindowSeconds;
            float limit = lag + m_Config.TargetSlotNitroToleranceMeters;

            int best = 0;
            float bestError = float.MaxValue;
            for (int key = BuffTableSO.MinKey + 1; key <= maxKey; key++)
            {
                if (car.KeyCooldownSteps[key] > 0)
                    continue;

                if (!table.TryGetEnergyCost(key, out float cost) || car.Energy < cost)
                    continue;

                float gain = (key - 1) * gainPerLevel;
                if (gain > limit)
                    continue;

                float error = Mathf.Abs(gain - lag);
                if (error < bestError)
                {
                    bestError = error;
                    best = key;
                }
            }

            if (best == 0)
                return false;

            car.CommandedBuffKey = best;
            m_ChaseTimer[i] = 0f;
            return true;
        }

        private void UpdateChallenges(CarState[] cars, CarState player, float raceTime, float deltaTime, int behindCount)
        {
            int active = 0;
            for (int slot = 0; slot < behindCount; slot++)
            {
                CarState car = cars[m_Behind[slot]];
                if (m_Phase[car.CarIndex] != ETargetChallengePhase.None &&
                    AdvanceChallenge(car, player, raceTime, deltaTime))
                    active++;
            }

            if (!m_Config.TargetChallengeEnabled || behindCount == 0 || raceTime < m_NextChallengeTime ||
                active >= m_Config.TargetChallengeMaxConcurrent)
                return;

            if (m_Progress < m_Config.TargetChallengeStartProgress || m_Progress > m_Config.TargetChallengeEndProgress)
                return;

            int offset = m_ChallengeRng.Range(0, behindCount);
            for (int n = 0; n < behindCount; n++)
            {
                CarState car = cars[m_Behind[(offset + n) % behindCount]];
                if (!TryStartChallenge(car, player))
                    continue;

                m_ChallengeCount++;
                m_NextChallengeTime = raceTime + NextChallengeInterval();
                return;
            }

            m_NextChallengeTime = raceTime + ChallengeRetrySeconds;
        }

        private bool TryStartChallenge(CarState car, CarState player)
        {
            int i = car.CarIndex;
            if (m_Phase[i] != ETargetChallengePhase.None || car.Finished || car.HasActiveBuff ||
                car.CooldownStepsRemaining > 0)
                return false;

            float deficit = player.Distance - car.Distance;
            if (deficit <= 0f || deficit > m_Config.TargetChallengeRangeMeters)
                return false;

            if (m_Headroom[i] < m_Config.TargetChallengeMinHeadroomSeconds)
                return false;

            int key = ChooseAttackKey(car, deficit, m_Config.TargetChallengeLeadMeters, HeadroomKeyCap(i), out float gain);
            if (key == 0)
                return false;

            m_Phase[i] = ETargetChallengePhase.Attack;
            m_PhaseTimer[i] = 0f;
            m_ChallengeKey[i] = key;
            m_ChallengeGoal[i] = gain - deficit;
            m_AcceptedAtStart[i] = car.AcceptedBuffCount;
            return true;
        }

        private int HeadroomKeyCap(int carIndex)
        {
            float paceMin = m_Config.TargetPaceMin;
            int cap = BuffTableSO.MinKey;
            for (int key = BuffTableSO.MinKey + 1; key <= BuffTableSO.MaxKey; key++)
            {
                if (m_Headroom[carIndex] - (key - paceMin) / paceMin >= m_Config.TargetChallengeMinHeadroomSeconds)
                    cap = key;
            }

            return cap;
        }

        private int ChooseAttackKey(CarState car, float deficit, float lead, int maxKey, out float chosenGain)
        {
            BuffTableSO table = m_Config.BuffTable;
            float gainPerLevel = m_Config.BaseSpeed * car.BaseSpeedMultiplier * table.WindowSeconds;
            float wanted = deficit + lead;

            int best = 0;
            float bestError = float.MaxValue;
            chosenGain = 0f;

            for (int key = BuffTableSO.MinKey + 1; key <= maxKey; key++)
            {
                if (car.KeyCooldownSteps[key] > 0)
                    continue;

                if (!table.TryGetEnergyCost(key, out float cost) || car.Energy < cost)
                    continue;

                float gain = (key - 1) * gainPerLevel;
                if (gain < deficit + ChallengeMinPassMeters)
                    continue;

                float error = Mathf.Abs(gain - wanted);
                if (error < bestError)
                {
                    bestError = error;
                    best = key;
                    chosenGain = gain;
                }
            }

            return best;
        }

        private bool AdvanceChallenge(CarState car, CarState player, float raceTime, float deltaTime)
        {
            int i = car.CarIndex;
            if (car.Finished)
            {
                EndChallenge(i, raceTime);
                return false;
            }

            m_PhaseTimer[i] += deltaTime;
            float gap = car.Distance - player.Distance;

            switch (m_Phase[i])
            {
                case ETargetChallengePhase.Attack:
                    if (car.AcceptedBuffCount > m_AcceptedAtStart[i])
                    {
                        if (car.HasActiveBuff)
                            return true;

                        if (gap <= 0f)
                        {
                            EndChallenge(i, raceTime);
                            return false;
                        }

                        m_Phase[i] = ETargetChallengePhase.Lead;
                        m_PhaseTimer[i] = 0f;
                        m_ChallengeGoal[i] = gap;
                        return true;
                    }

                    if (m_PhaseTimer[i] > ChallengeAttackTimeoutSeconds)
                    {
                        EndChallenge(i, raceTime);
                        return false;
                    }

                    return true;

                case ETargetChallengePhase.Lead:
                    if (m_PhaseTimer[i] >= m_Config.TargetChallengeHoldSeconds)
                    {
                        m_Phase[i] = ETargetChallengePhase.Fallback;
                        m_PhaseTimer[i] = 0f;
                    }

                    return true;

                case ETargetChallengePhase.Fallback:
                    if (gap <= -m_Config.TargetNearInnerMeters)
                    {
                        EndChallenge(i, raceTime);
                        return false;
                    }

                    return true;
            }

            EndChallenge(i, raceTime);
            return false;
        }

        private void EndChallenge(int carIndex, float raceTime)
        {
            m_Settling[carIndex] = true;
            m_Phase[carIndex] = ETargetChallengePhase.None;
            m_PhaseTimer[carIndex] = 0f;
            m_NextChallengeTime = Mathf.Max(m_NextChallengeTime, raceTime + NextChallengeInterval());
        }

        private float NextChallengeInterval()
        {
            return m_ChallengeRng.Range(m_Config.TargetChallengeIntervalMinSeconds,
                m_Config.TargetChallengeIntervalMaxSeconds);
        }

        private void UpdateComebacks(CarState[] cars, CarState player, float raceTime, float deltaTime, int aheadCount)
        {
            int active = 0;
            for (int slot = 0; slot < aheadCount; slot++)
            {
                CarState car = cars[m_Ahead[slot]];
                if (m_Phase[car.CarIndex] != ETargetChallengePhase.None &&
                    AdvanceComeback(car, player, raceTime, deltaTime))
                    active++;
            }

            if (!m_Config.TargetComebackEnabled || aheadCount == 0 || raceTime < m_NextComebackTime ||
                active >= m_Config.TargetComebackMaxConcurrent)
                return;

            if (m_Progress < m_Config.TargetComebackStartProgress || m_Progress > m_Config.TargetComebackEndProgress)
                return;

            int offset = m_ChallengeRng.Range(0, aheadCount);
            for (int n = 0; n < aheadCount; n++)
            {
                CarState car = cars[m_Ahead[(offset + n) % aheadCount]];
                if (!TryStartComeback(car, player))
                    continue;

                m_ComebackCount++;
                m_NextComebackTime = raceTime + NextComebackInterval();
                return;
            }

            m_NextComebackTime = raceTime + ChallengeRetrySeconds;
        }

        private bool TryStartComeback(CarState car, CarState player)
        {
            int i = car.CarIndex;
            if (m_Phase[i] != ETargetChallengePhase.None || car.Finished || car.HasActiveBuff)
                return false;

            float gap = car.Distance - player.Distance;
            if (gap <= 0f || gap > m_Config.TargetComebackRangeMeters)
                return false;

            if (m_AheadMargin[i] < m_Config.TargetComebackMinMarginSeconds)
                return false;

            if (!m_Config.BuffTable.TryGetEnergyCost(BuffTableSO.MinKey + 1, out float cost) || car.Energy < cost)
                return false;

            m_Phase[i] = ETargetChallengePhase.Return;
            m_PhaseTimer[i] = 0f;
            m_ChallengeGoal[i] = -m_Config.TargetComebackBehindMeters;
            return true;
        }

        private bool AdvanceComeback(CarState car, CarState player, float raceTime, float deltaTime)
        {
            int i = car.CarIndex;
            if (car.Finished)
            {
                EndComeback(i, raceTime);
                return false;
            }

            m_PhaseTimer[i] += deltaTime;
            float gap = car.Distance - player.Distance;

            switch (m_Phase[i])
            {
                case ETargetChallengePhase.Return:
                    bool arrived = gap <= -m_Config.TargetComebackBehindMeters;
                    bool timedOut = m_PhaseTimer[i] >= m_Config.TargetComebackReturnTimeoutSeconds;
                    bool urgent = m_AheadMargin[i] < ComebackUrgentMarginSeconds;
                    if (!arrived && !timedOut && !urgent)
                        return true;

                    if (gap > ComebackAbortGapMeters)
                    {
                        EndComeback(i, raceTime);
                        return false;
                    }

                    int key = car.HasActiveBuff || car.CooldownStepsRemaining > 0
                        ? 0
                        : ChooseAttackKey(car, Mathf.Max(0f, -gap), m_Config.TargetComebackLeadMeters,
                            BuffTableSO.MaxKey, out _);
                    if (key == 0)
                    {
                        if (!timedOut && !urgent)
                            return true;

                        EndComeback(i, raceTime);
                        return false;
                    }

                    m_Settling[i] = true;
                    m_Phase[i] = ETargetChallengePhase.Attack;
                    m_PhaseTimer[i] = 0f;
                    m_ChallengeKey[i] = key;
                    m_ChallengeGoal[i] = gap + (key - 1) * m_Config.BaseSpeed * car.BaseSpeedMultiplier *
                        m_Config.BuffTable.WindowSeconds;
                    m_AcceptedAtStart[i] = car.AcceptedBuffCount;
                    return true;

                case ETargetChallengePhase.Attack:
                    if (car.AcceptedBuffCount > m_AcceptedAtStart[i])
                    {
                        if (car.HasActiveBuff)
                            return true;

                        EndComeback(i, raceTime);
                        return false;
                    }

                    if (m_PhaseTimer[i] > ChallengeAttackTimeoutSeconds)
                    {
                        EndComeback(i, raceTime);
                        return false;
                    }

                    return true;
            }

            EndComeback(i, raceTime);
            return false;
        }

        private void EndComeback(int carIndex, float raceTime)
        {
            m_Settling[carIndex] = true;
            m_Phase[carIndex] = ETargetChallengePhase.None;
            m_PhaseTimer[carIndex] = 0f;
            m_NextComebackTime = Mathf.Max(m_NextComebackTime, raceTime + NextComebackInterval());
        }

        private float NextComebackInterval()
        {
            return m_ChallengeRng.Range(m_Config.TargetComebackIntervalMinSeconds,
                m_Config.TargetComebackIntervalMaxSeconds);
        }

        private int GetSlotNitroCap(CarState car, CarState player, float baseSpeed, BuffTableSO table)
        {
            float room = m_TargetGap[car.CarIndex] - (car.Distance - player.Distance) +
                         m_Config.TargetSlotNitroToleranceMeters;
            float gainPerLevel = baseSpeed * car.BaseSpeedMultiplier * table.WindowSeconds;

            int cap = BuffTableSO.MinKey;
            for (int key = BuffTableSO.MinKey + 1; key <= BuffTableSO.MaxKey; key++)
            {
                if ((key - 1) * gainPerLevel <= room)
                    cap = key;
            }

            return cap;
        }

        private float GetNearBand(CarState car, CarState player, out float low, out float high)
        {
            float gap = Mathf.Abs(car.Distance - player.Distance);
            float inner = m_Config.TargetNearInnerMeters;
            float outer = m_Config.TargetNearOuterMeters;
            float weight = gap <= inner ? 1f : Mathf.SmoothStep(0f, 1f, (outer - gap) / (outer - inner));
            float neutral = player.BaseSpeedMultiplier / Mathf.Max(0.001f, car.BaseSpeedMultiplier);
            float band = m_Config.TargetNearPaceBand * m_BandScale[car.CarIndex];

            low = Mathf.Lerp(m_Config.TargetPaceMin, Mathf.Max(m_Config.TargetPaceMin, neutral * (1f - band)), weight);
            high = Mathf.Lerp(m_Config.TargetPaceMax, Mathf.Min(m_Config.TargetPaceMax, neutral * (1f + band)), weight);
            return weight;
        }

        private void ApplyEnvelope(CarState[] cars, CarState player, float raceTime, float maxStep)
        {
            BuffTableSO table = m_Config.BuffTable;
            int stepHz = m_Config.FixedStepHz;
            float baseSpeed = m_Config.BaseSpeed;
            double now = raceTime;
            double raceLength = m_Config.RaceLengthMeters;
            double rate = m_Config.TargetPaceRatePerSecond;
            float paceMin = m_Config.TargetPaceMin;
            float paceMax = m_Config.TargetPaceMax;
            double tau = m_Config.TargetEnvelopeTauStart +
                         (m_Config.TargetEnvelopeTauEnd - m_Config.TargetEnvelopeTauStart) *
                         Mathf.SmoothStep(0f, 1f, m_Progress);

            m_PlayerBestFinish = TargetProjection.BestFinishTime(player, table, baseSpeed, stepHz,
                1.0, 1.0, 0.0, raceLength, now);
            m_PlayerWorstFinish = TargetProjection.SlowestFinishTime(player.Distance, player.ActiveBuffKey,
                player.BuffStepsRemaining / (double)stepHz, baseSpeed * player.BaseSpeedMultiplier,
                1.0, 1.0, 0.0, raceLength, now);

            for (int i = 0; i < cars.Length; i++)
            {
                if (i == player.CarIndex)
                    continue;

                CarState car = cars[i];
                car.CommandedBuffKey = 0;
                car.HoldBuffs = false;
                car.MaxBuffKey = BuffTableSO.MaxKey;
                car.PaceIntervention = 0;

                if (car.Finished)
                    continue;

                GetNearBand(car, player, out float low, out float high);

                if (m_IsAheadSide[i])
                {
                    double best = TargetProjection.BestFinishTime(car, table, baseSpeed, stepHz,
                        m_Current[i], paceMax, rate, raceLength, now);
                    double safeLine = m_PlayerBestFinish - tau - m_Config.TargetAheadSlackSeconds;
                    m_AheadMargin[i] = (float)(safeLine - best);

                    if (best <= safeLine)
                    {
                        ApplyComebackCommand(car, player, baseSpeed, table);
                        continue;
                    }

                    if (m_Phase[i] != ETargetChallengePhase.None)
                        EndComeback(i, raceTime);

                    bool aheadCritical = best > m_PlayerBestFinish - tau - m_Config.TargetAheadCriticalSeconds;
                    float cap = aheadCritical ? paceMax : high;

                    car.PaceBias = 1f;
                    car.PaceIntervention = aheadCritical ? 2 : 1;
                    m_Current[i] = Mathf.Max(Mathf.Min(cap, m_Previous[i] + maxStep), Mathf.Min(m_Current[i], cap));

                    if (!car.HasActiveBuff)
                        car.BalanceMultiplier = m_Current[i];

                    if (!car.HasActiveBuff && car.CooldownStepsRemaining <= 0)
                        car.CommandedBuffKey = TargetProjection.BestReadyKey(car, table);

                    continue;
                }

                double slowest = TargetProjection.SlowestFinishTime(car.Distance, car.ActiveBuffKey,
                    car.BuffStepsRemaining / (double)stepHz, baseSpeed * car.BaseSpeedMultiplier,
                    m_Current[i], paceMin, rate, raceLength, now);
                double headroom = slowest - (m_PlayerWorstFinish + tau);
                m_Headroom[i] = (float)headroom;

                int maxKey = 1;
                for (int key = 2; key <= BuffTableSO.MaxKey; key++)
                {
                    if (headroom - (key - paceMin) / paceMin >= m_Config.TargetBehindSlackSeconds)
                        maxKey = key;
                }

                car.MaxBuffKey = m_Config.TargetSlotNitroCap
                    ? Mathf.Min(maxKey, GetSlotNitroCap(car, player, baseSpeed, table))
                    : maxKey;

                if (headroom >= m_Config.TargetBehindSlackSeconds)
                {
                    ApplyChallengeCommand(car, player, maxKey);
                    continue;
                }

                if (m_Phase[i] == ETargetChallengePhase.Attack)
                    EndChallenge(i, raceTime);
                else if (m_Phase[i] == ETargetChallengePhase.Lead)
                    m_Phase[i] = ETargetChallengePhase.Fallback;

                bool behindCritical = headroom < m_Config.TargetBehindCriticalSeconds;
                float floor = behindCritical ? paceMin : low;

                car.HoldBuffs = true;
                car.PaceBias = -1f;
                car.PaceIntervention = behindCritical ? -2 : -1;
                m_Current[i] = Mathf.Min(Mathf.Max(floor, m_Previous[i] - maxStep), Mathf.Max(m_Current[i], floor));

                if (!car.HasActiveBuff)
                    car.BalanceMultiplier = m_Current[i];
            }
        }

        private void ApplyChallengeCommand(CarState car, CarState player, int envelopeMaxKey)
        {
            int i = car.CarIndex;
            ETargetChallengePhase phase = m_Phase[i];

            if (phase == ETargetChallengePhase.Lead || phase == ETargetChallengePhase.Fallback)
            {
                car.HoldBuffs = true;
                return;
            }

            if (phase == ETargetChallengePhase.None)
            {
                if (!TryCommandChase(car, player, Mathf.Min(envelopeMaxKey, car.MaxBuffKey)))
                    TryCommandSwap(car);
                return;
            }

            if (phase != ETargetChallengePhase.Attack || car.AcceptedBuffCount > m_AcceptedAtStart[i] ||
                car.HasActiveBuff || car.CooldownStepsRemaining > 0 || m_ChallengeKey[i] > envelopeMaxKey)
                return;

            car.CommandedBuffKey = m_ChallengeKey[i];
        }

        private void ApplyComebackCommand(CarState car, CarState player, float baseSpeed, BuffTableSO table)
        {
            int i = car.CarIndex;
            ETargetChallengePhase phase = m_Phase[i];

            if (phase == ETargetChallengePhase.Return)
            {
                car.HoldBuffs = true;
                return;
            }

            if (phase == ETargetChallengePhase.Attack)
            {
                if (car.AcceptedBuffCount == m_AcceptedAtStart[i] && !car.HasActiveBuff &&
                    car.CooldownStepsRemaining <= 0)
                    car.CommandedBuffKey = m_ChallengeKey[i];
                return;
            }

            if (m_Config.TargetAheadSlotNitroCap)
                car.MaxBuffKey = GetSlotNitroCap(car, player, baseSpeed, table);

            if (!TryCommandChase(car, player, car.MaxBuffKey))
                TryCommandSwap(car);
        }

        private int SortSide(CarState[] cars, int playerCarIndex, bool aheadSide, int[] buffer)
        {
            int count = 0;
            for (int i = 0; i < cars.Length; i++)
            {
                if (i == playerCarIndex || m_IsAheadSide[i] != aheadSide)
                    continue;

                buffer[count++] = i;
            }

            SortByDistance(cars, buffer, count);
            return count;
        }

        private static void SortByDistance(CarState[] cars, int[] buffer, int count)
        {
            for (int i = 1; i < count; i++)
            {
                int carIndex = buffer[i];
                int j = i - 1;

                while (j >= 0 && cars[buffer[j]].Distance < cars[carIndex].Distance)
                {
                    buffer[j + 1] = buffer[j];
                    j--;
                }

                buffer[j + 1] = carIndex;
            }
        }
    }
}
