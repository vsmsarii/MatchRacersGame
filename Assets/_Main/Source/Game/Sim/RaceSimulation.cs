using CasualKit.Core;
using UnityEngine;

namespace MatchRacers
{
    public sealed class RaceSimulation : IRaceView
    {
        private const int MaxStepsPerAdvance = 64;

        private readonly RaceConfigSO m_Config;
        private readonly BuffSystem m_Buffs;
        private readonly CarState[] m_Cars;
        private readonly IRaceAgent[] m_Agents;
        private readonly RankingSystem m_Ranking;
        private readonly CatchUpBalancer m_Balancer;
        private readonly TargetOrderDirector m_Director;
        private IPaceController m_Pace;
        private readonly int[] m_FinishBuffer;
        private readonly double[] m_ExactDistance;
        private readonly int[] m_ConfirmedPosition;
        private readonly int[] m_PendingPosition;
        private readonly int[] m_PendingSteps;
        private int m_OvertakeConfirmSteps;

        private float m_Accumulator;
        private float m_Time;
        private int m_StepIndex;
        private int m_CountdownStepsRemaining;
        private int m_FinishedCount;
        private ERaceState m_State = ERaceState.Idle;
        private uint m_Seed;
        private ERaceMode m_Mode = ERaceMode.Free;
        private int m_TargetPosition = 1;
        private bool m_EmitEvents = true;
        private float m_PlayerLastBuffTime = float.NegativeInfinity;

        public int CarCount => m_Cars.Length;
        public int PlayerCarIndex => 0;
        public float RaceLengthMeters => m_Config.RaceLengthMeters;
        public float Time => m_Time;
        public ERaceState State => m_State;
        public int StepIndex => m_StepIndex;
        public uint Seed => m_Seed;
        public BuffSystem Buffs => m_Buffs;
        public RaceConfigSO Config => m_Config;
        public BuffTableSO BuffTable => m_Config.BuffTable;
        public float CountdownRemaining => m_CountdownStepsRemaining * m_Config.FixedDeltaTime;
        public float InterpolationAlpha => m_State == ERaceState.Racing
            ? Mathf.Clamp01(m_Accumulator / m_Config.FixedDeltaTime)
            : 1f;
        public float RenderTime => m_Time - (1f - InterpolationAlpha) * m_Config.FixedDeltaTime;
        public bool EmitEvents { get => m_EmitEvents; set => m_EmitEvents = value; }
        public CatchUpBalancer Balancer => m_Balancer;
        public TargetOrderDirector Director => m_Director;
        public ERaceMode Mode => m_Mode;
        public int TargetPosition => m_TargetPosition;
        public bool TargetMatched => m_Mode == ERaceMode.TargetOrder &&
            m_Cars[PlayerCarIndex].FinishOrder == m_TargetPosition;
        public float PlayerLastBuffTime => m_PlayerLastBuffTime;

        public RaceSimulation(RaceConfigSO config, IRaceAgent[] agents)
        {
            m_Config = config;
            m_Agents = agents;
            m_Buffs = new BuffSystem(config.BuffTable, config.FixedStepHz);
            m_Cars = new CarState[RaceConfigSO.CarCount];
            m_FinishBuffer = new int[RaceConfigSO.CarCount];
            m_ExactDistance = new double[RaceConfigSO.CarCount];
            m_ConfirmedPosition = new int[RaceConfigSO.CarCount];
            m_PendingPosition = new int[RaceConfigSO.CarCount];
            m_PendingSteps = new int[RaceConfigSO.CarCount];
            m_Ranking = new RankingSystem(RaceConfigSO.CarCount);
            m_Balancer = new CatchUpBalancer(config, RaceConfigSO.CarCount);
            m_Director = new TargetOrderDirector(config, RaceConfigSO.CarCount);
            m_Pace = m_Balancer;

            for (int i = 0; i < m_Cars.Length; i++)
            {
                m_Cars[i] = new CarState
                {
                    CarIndex = i,
                    LaneIndex = i,
                    IsPlayer = i == PlayerCarIndex
                };
            }
        }

        public IRaceAgent GetAgent(int carIndex)
        {
            return carIndex >= 0 && carIndex < m_Agents.Length ? m_Agents[carIndex] : null;
        }

        public CarState GetCar(int carIndex)
        {
            return carIndex >= 0 && carIndex < m_Cars.Length ? m_Cars[carIndex] : null;
        }

        public int GetPosition(int carIndex)
        {
            return m_Ranking.GetPosition(carIndex);
        }

        public int GetCarAtPosition(int position)
        {
            return m_Ranking.GetCarAtPosition(position);
        }

        public float RaceProgress
        {
            get
            {
                float leader = 0f;
                for (int i = 0; i < m_Cars.Length; i++)
                {
                    if (m_Cars[i].Distance > leader)
                        leader = m_Cars[i].Distance;
                }

                return Mathf.Clamp01(leader / m_Config.RaceLengthMeters);
            }
        }

        public bool TryGetCarAhead(int carIndex, out int aheadCarIndex, out float gapMeters)
        {
            int position = m_Ranking.GetPosition(carIndex);
            aheadCarIndex = m_Ranking.GetCarAtPosition(position - 1);
            if (aheadCarIndex < 0)
            {
                gapMeters = 0f;
                return false;
            }

            gapMeters = m_Cars[aheadCarIndex].Distance - m_Cars[carIndex].Distance;
            return true;
        }

        public bool TryGetCarBehind(int carIndex, out int behindCarIndex, out float gapMeters)
        {
            int position = m_Ranking.GetPosition(carIndex);
            behindCarIndex = m_Ranking.GetCarAtPosition(position + 1);
            if (behindCarIndex < 0)
            {
                gapMeters = 0f;
                return false;
            }

            gapMeters = m_Cars[carIndex].Distance - m_Cars[behindCarIndex].Distance;
            return true;
        }

        public void Configure(ERaceMode mode, int targetPosition)
        {
            m_Mode = mode;
            m_TargetPosition = Mathf.Clamp(targetPosition, 1, RaceConfigSO.CarCount);
        }

        public void Reset(uint seed)
        {
            m_Seed = seed;
            m_Time = 0f;
            m_StepIndex = 0;
            m_Accumulator = 0f;
            m_FinishedCount = 0;
            m_CountdownStepsRemaining = Mathf.RoundToInt(m_Config.CountdownSeconds * m_Config.FixedStepHz);

            Xorshift rng = new Xorshift(seed);
            float startEnergy = m_Config.BuffTable.EnergyMax;

            for (int i = 0; i < m_Cars.Length; i++)
            {
                CarState car = m_Cars[i];
                car.Reset(startEnergy);
                m_ExactDistance[i] = 0d;

                float variance = 0f;
                if (!car.IsPlayer && m_Config.TryGetRivalProfile(i - 1, out AiProfileSO profile))
                    variance = profile.BaseSpeedVariance;

                car.BaseSpeedMultiplier = car.IsPlayer ? 1f : 1f + rng.Range(-variance, variance);
            }

            for (int i = 0; i < m_Agents.Length; i++)
            {
                if (m_Agents[i] != null)
                    m_Agents[i].Reset(rng.NextUInt());
            }

            m_PlayerLastBuffTime = float.NegativeInfinity;
            m_OvertakeConfirmSteps = Mathf.RoundToInt(m_Config.OvertakeConfirmSeconds * m_Config.FixedStepHz);
            m_Pace = m_Mode == ERaceMode.TargetOrder ? (IPaceController)m_Director : m_Balancer;
            m_Pace.Reset(m_Cars, seed, m_TargetPosition);
            m_Ranking.Rebuild(m_Cars);

            for (int i = 0; i < m_Cars.Length; i++)
            {
                m_ConfirmedPosition[i] = m_Ranking.GetPosition(i);
                m_PendingPosition[i] = m_ConfirmedPosition[i];
                m_PendingSteps[i] = 0;
            }

            SetState(m_CountdownStepsRemaining > 0 ? ERaceState.Countdown : ERaceState.Racing);
        }

        public void Advance(float deltaTime)
        {
            if (m_State != ERaceState.Countdown && m_State != ERaceState.Racing)
                return;

            m_Accumulator += deltaTime;
            float step = m_Config.FixedDeltaTime;
            int guard = 0;

            while (m_Accumulator >= step && guard < MaxStepsPerAdvance)
            {
                Step();
                m_Accumulator -= step;
                guard++;

                if (m_State != ERaceState.Countdown && m_State != ERaceState.Racing)
                    break;
            }
        }

        public void Step()
        {
            float dt = m_Config.FixedDeltaTime;

            if (m_State == ERaceState.Racing)
            {
                for (int i = 0; i < m_Cars.Length; i++)
                {
                    if (m_Cars[i].Finished)
                        continue;

                    m_Buffs.Regenerate(m_Cars[i], dt);
                    m_Buffs.TickCooldown(m_Cars[i]);
                }
            }

            PollAgents(dt);

            if (m_State == ERaceState.Countdown)
            {
                m_CountdownStepsRemaining--;
                m_StepIndex++;
                m_Time = m_StepIndex / (float)m_Config.FixedStepHz;

                if (m_CountdownStepsRemaining <= 0)
                    SetState(ERaceState.Racing);

                return;
            }

            m_Pace.Step(m_Cars, PlayerCarIndex, m_Time, m_PlayerLastBuffTime, dt);

            Integrate(dt);
            ExpireBuffWindows();

            m_Ranking.Rebuild(m_Cars);
            DetectOvertakes();

            m_StepIndex++;
            m_Time = m_StepIndex / (float)m_Config.FixedStepHz;

            if (m_FinishedCount >= m_Cars.Length)
                SetState(ERaceState.Finished);
        }

        private void PollAgents(float dt)
        {
            for (int i = 0; i < m_Agents.Length && i < m_Cars.Length; i++)
            {
                IRaceAgent agent = m_Agents[i];
                if (agent == null)
                    continue;

                CarState car = m_Cars[i];
                if (car.Finished)
                    continue;

                int key = agent.PollBuffKey(this, m_Time, dt);
                if (key == 0)
                    continue;

                EBuffRejectReason reason = m_Buffs.TryActivate(car, key, m_State);
                if (reason == EBuffRejectReason.None)
                {
                    if (i == PlayerCarIndex)
                        m_PlayerLastBuffTime = m_Time;

                    Emit(new BuffAccepted(i, key, m_Time));
                    continue;
                }

                car.RejectedBuffCount++;
                Emit(new BuffRejected(i, key, reason, m_Time));
            }
        }

        private void Integrate(float dt)
        {
            float raceLength = m_Config.RaceLengthMeters;
            int newlyFinished = 0;

            for (int i = 0; i < m_Cars.Length; i++)
            {
                CarState car = m_Cars[i];
                car.PreviousDistance = car.Distance;

                if (car.Finished)
                    continue;

                float buffMultiplier = m_Buffs.GetSpeedMultiplier(car);
                car.Speed = m_Config.BaseSpeed * car.BaseSpeedMultiplier * buffMultiplier * car.BalanceMultiplier;

                double previousDistance = m_ExactDistance[i];
                double nextDistance = previousDistance + (double)car.Speed * dt;
                m_ExactDistance[i] = nextDistance;
                car.Distance = (float)nextDistance;

                if (nextDistance < raceLength)
                    continue;

                double travelled = nextDistance - previousDistance;
                double fraction = travelled > 0d ? (raceLength - previousDistance) / travelled : 0d;
                car.FinishTime = m_Time + (float)(System.Math.Clamp(fraction, 0d, 1d) * dt);
                car.Finished = true;
                m_FinishBuffer[newlyFinished++] = i;
            }

            AssignFinishOrders(newlyFinished);
        }

        private void AssignFinishOrders(int newlyFinished)
        {
            for (int i = 1; i < newlyFinished; i++)
            {
                int carIndex = m_FinishBuffer[i];
                int j = i - 1;
                while (j >= 0 && m_Cars[m_FinishBuffer[j]].FinishTime > m_Cars[carIndex].FinishTime)
                {
                    m_FinishBuffer[j + 1] = m_FinishBuffer[j];
                    j--;
                }

                m_FinishBuffer[j + 1] = carIndex;
            }

            for (int i = 0; i < newlyFinished; i++)
            {
                CarState car = m_Cars[m_FinishBuffer[i]];
                car.FinishOrder = ++m_FinishedCount;
                car.FinishSpeed = car.Speed;
                car.Speed = 0f;
                car.ActiveBuffKey = 0;
                car.BuffStepsRemaining = 0;
                car.LaunchStepsRemaining = 0;
                Emit(new CarFinished(car.CarIndex, car.FinishOrder, car.FinishTime));
            }
        }

        private void ExpireBuffWindows()
        {
            for (int i = 0; i < m_Cars.Length; i++)
            {
                if (m_Buffs.ConsumeWindowStep(m_Cars[i], out int expiredKey))
                    Emit(new BuffExpired(i, expiredKey, m_Time));
            }
        }

        private void DetectOvertakes()
        {
            for (int i = 0; i < m_Cars.Length; i++)
            {
                int now = m_Ranking.GetPosition(i);

                if (now == m_ConfirmedPosition[i])
                {
                    m_PendingPosition[i] = now;
                    m_PendingSteps[i] = 0;
                    continue;
                }

                if (now != m_PendingPosition[i])
                {
                    m_PendingPosition[i] = now;
                    m_PendingSteps[i] = 0;
                    continue;
                }

                m_PendingSteps[i]++;
                if (m_PendingSteps[i] < m_OvertakeConfirmSteps)
                    continue;

                int previous = m_ConfirmedPosition[i];
                m_ConfirmedPosition[i] = now;
                m_PendingSteps[i] = 0;

                if (now >= previous)
                    continue;

                int passed = m_Ranking.GetCarAtPosition(now + 1);
                if (passed >= 0)
                    Emit(new OvertakeOccurred(i, passed, m_Time));
            }
        }

        private void SetState(ERaceState state)
        {
            if (m_State == state)
                return;

            m_State = state;
            Emit(new RaceStateChanged(state, m_Time));
        }

        private void Emit<T>(T evt) where T : struct
        {
            if (m_EmitEvents)
                EB.Gameplay.Invoke(evt);
        }
    }
}
