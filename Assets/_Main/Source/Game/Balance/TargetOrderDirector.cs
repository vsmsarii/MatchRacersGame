using UnityEngine;

namespace MatchRacers
{
    public sealed class TargetOrderDirector : IPaceController
    {
        private const float MinElapsedSeconds = 0.35f;
        private const float MinTimeToFinish = 0.4f;

        private readonly RaceConfigSO m_Config;
        private readonly int m_CarCount;
        private readonly float[] m_SlotJitter;
        private readonly float[] m_Current;
        private readonly float[] m_TargetGap;
        private readonly bool[] m_IsAheadSide;
        private readonly int[] m_Ahead;
        private readonly int[] m_Behind;

        private int m_TargetPosition = 1;
        private float m_PlayerAverageSpeed;
        private float m_MarginAhead;
        private float m_MarginBehind;
        private float m_Progress;

        public int TargetPosition => m_TargetPosition;
        public float MarginAhead => m_MarginAhead;
        public float MarginBehind => m_MarginBehind;

        public TargetOrderDirector(RaceConfigSO config, int carCount)
        {
            m_Config = config;
            m_CarCount = carCount;
            m_SlotJitter = new float[carCount];
            m_Current = new float[carCount];
            m_TargetGap = new float[carCount];
            m_IsAheadSide = new bool[carCount];
            m_Ahead = new int[carCount];
            m_Behind = new int[carCount];
        }

        public float GetTargetGap(int carIndex)
        {
            return carIndex >= 0 && carIndex < m_TargetGap.Length ? m_TargetGap[carIndex] : 0f;
        }

        public bool IsAheadSide(int carIndex)
        {
            return carIndex >= 0 && carIndex < m_IsAheadSide.Length && m_IsAheadSide[carIndex];
        }

        public void Reset(CarState[] cars, uint seed, int targetPosition)
        {
            m_TargetPosition = Mathf.Clamp(targetPosition, 1, m_CarCount);
            m_PlayerAverageSpeed = m_Config.BaseSpeed;
            m_MarginAhead = m_Config.TargetMarginStartMeters;
            m_MarginBehind = m_Config.TargetMarginStartMeters;
            m_Progress = 0f;

            Xorshift rng = new Xorshift(seed ^ 0x9E3779B9u);

            int rivalCount = 0;
            for (int i = 0; i < m_CarCount; i++)
            {
                m_Current[i] = 1f;
                m_TargetGap[i] = 0f;
                m_IsAheadSide[i] = false;
                m_SlotJitter[i] = rng.Range(-m_Config.TargetSlotJitter, m_Config.TargetSlotJitter);
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

            int aheadCount = m_TargetPosition - 1;
            for (int i = 0; i < aheadCount && i < rivalCount; i++)
                m_IsAheadSide[m_Ahead[i]] = true;
        }

        public void Step(CarState[] cars, int playerCarIndex, float raceTime, float playerLastBuffTime, float deltaTime)
        {
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

            float remaining = m_Config.RaceLengthMeters - player.Distance;
            float timeToFinish = Mathf.Max(MinTimeToFinish, remaining / m_PlayerAverageSpeed);
            float separation = m_Config.TargetSeparationMeters;
            float maxStep = m_Config.TargetPaceRatePerSecond * deltaTime;

            for (int slot = 0; slot < aheadCount; slot++)
                Drive(cars[m_Ahead[slot]], player, m_MarginAhead + separation * (aheadCount - 1 - slot),
                    timeToFinish, maxStep);

            for (int slot = 0; slot < behindCount; slot++)
                Drive(cars[m_Behind[slot]], player, -m_MarginBehind - separation * slot,
                    timeToFinish, maxStep);
        }

        private void Drive(CarState car, CarState player, float rawTargetGap, float timeToFinish, float maxStep)
        {
            if (car.Finished)
            {
                car.BalanceMultiplier = 1f;
                car.PaceBias = 0f;
                return;
            }

            float targetGap = rawTargetGap * (1f + m_SlotJitter[car.CarIndex]);
            m_TargetGap[car.CarIndex] = targetGap;

            float gap = car.Distance - player.Distance;
            float requiredSpeed = m_PlayerAverageSpeed + (targetGap - gap) / timeToFinish;
            float desired = requiredSpeed / Mathf.Max(0.001f, m_Config.BaseSpeed * car.BaseSpeedMultiplier);

            car.PaceBias = Mathf.Clamp((desired - 1f) * m_Config.TargetBiasResponse, -1f, 1f);

            bool holdingStation = m_Progress >= m_Config.TargetAttackLockProgress &&
                                  Mathf.Abs(gap) < m_Config.TargetAttackLockGapMeters;
            if (holdingStation)
                car.PaceBias = -1f;

            if (car.HasActiveBuff)
            {
                car.BalanceMultiplier = 1f;
                return;
            }

            desired = Mathf.Clamp(desired, m_Config.TargetPaceMin, m_Config.TargetPaceMax);
            m_Current[car.CarIndex] += Mathf.Clamp(desired - m_Current[car.CarIndex], -maxStep, maxStep);
            m_Current[car.CarIndex] = Mathf.Clamp(m_Current[car.CarIndex],
                m_Config.TargetPaceMin, m_Config.TargetPaceMax);
            car.BalanceMultiplier = m_Current[car.CarIndex];
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

            return count;
        }
    }
}
