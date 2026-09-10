using UnityEngine;

namespace MatchRacers
{
    public sealed class AiAgent : IRaceAgent
    {
        private const float FullTankRatio = 0.98f;

        private readonly int m_CarIndex;
        private readonly AiProfileSO m_Profile;
        private readonly BuffTableSO m_BuffTable;

        private Xorshift m_Rng;
        private float m_PhaseOffset;
        private float m_NextDecisionTime;
        private float m_FireTime;
        private int m_PendingKey;
        private EAiDecisionState m_State = EAiDecisionState.Idle;
        private int m_LastFiredKey;
        private float m_LastScore;

        public int CarIndex => m_CarIndex;
        public AiProfileSO Profile => m_Profile;
        public EAiDecisionState State => m_State;
        public int LastFiredKey => m_LastFiredKey;
        public float LastScore => m_LastScore;

        public AiAgent(int carIndex, AiProfileSO profile, BuffTableSO buffTable)
        {
            m_CarIndex = carIndex;
            m_Profile = profile;
            m_BuffTable = buffTable;
        }

        public void Reset(uint seed)
        {
            m_Rng = new Xorshift(seed);
            m_PhaseOffset = m_Rng.Range(0f, m_Profile.DecisionIntervalMax);
            m_NextDecisionTime = 0f;
            m_FireTime = -1f;
            m_PendingKey = 0;
            m_LastFiredKey = 0;
            m_LastScore = 0f;
            m_State = EAiDecisionState.Idle;
        }

        public int PollBuffKey(IRaceView view, float stepTime, float stepDelta)
        {
            if (view.State != ERaceState.Racing)
            {
                m_NextDecisionTime = stepTime + m_PhaseOffset;
                m_State = EAiDecisionState.Idle;
                return 0;
            }

            CarState car = view.GetCar(m_CarIndex);
            if (car == null || car.Finished)
            {
                m_State = EAiDecisionState.Idle;
                return 0;
            }

            if (m_PendingKey != 0)
            {
                if (stepTime < m_FireTime)
                {
                    m_State = EAiDecisionState.Reacting;
                    return 0;
                }

                int key = m_PendingKey;
                m_PendingKey = 0;
                m_LastFiredKey = key;
                m_State = EAiDecisionState.Waiting;
                ScheduleNextDecision(stepTime);
                return key;
            }

            if (stepTime < m_NextDecisionTime)
                return 0;

            Evaluate(view, car, stepTime);
            return 0;
        }

        private void Evaluate(IRaceView view, CarState car, float stepTime)
        {
            ScheduleNextDecision(stepTime);

            if (car.HasActiveBuff || car.CooldownStepsRemaining > 0)
            {
                m_State = EAiDecisionState.Busy;
                return;
            }

            float progress = Mathf.Clamp01(car.Distance / view.RaceLengthMeters);
            float reserve = m_Profile.EnergyReserveRatio * (1f - progress) * m_BuffTable.EnergyMax;
            float usableEnergy = car.Energy - reserve;
            if (usableEnergy <= 0f)
            {
                m_State = EAiDecisionState.Saving;
                return;
            }

            bool hasAheadTarget = view.TryGetCarAhead(m_CarIndex, out _, out float gapAhead);
            bool hasBehindTarget = view.TryGetCarBehind(m_CarIndex, out _, out float gapBehind);

            if (m_Profile.RequiresNearbyRival && !HasRivalInRange(hasAheadTarget, gapAhead, hasBehindTarget, gapBehind))
            {
                m_State = EAiDecisionState.NoTarget;
                return;
            }

            float score = m_Profile.EvaluateAggression(progress);

            if (hasAheadTarget && gapAhead <= m_Profile.EngagementRangeMeters)
                score += m_Profile.OvertakeUrgency * (1f - score);

            bool comfortableLead = !hasAheadTarget && hasBehindTarget && gapBehind >= m_Profile.ComfortGapMeters;
            if (comfortableLead)
                score *= 1f - m_Profile.LeadCaution;

            if (car.Energy >= m_BuffTable.EnergyMax * FullTankRatio)
                score = Mathf.Max(score, m_Profile.OverflowUrgency);

            if (car.PaceBias > 0f)
                score += (1f - score) * car.PaceBias;
            else if (car.PaceBias < 0f)
                score *= 1f + car.PaceBias;

            m_LastScore = score;

            if (m_Rng.NextFloat() > score)
            {
                m_State = EAiDecisionState.Saving;
                return;
            }

            int key = PickKey(usableEnergy);
            if (key == 0)
            {
                m_State = EAiDecisionState.Saving;
                return;
            }

            m_PendingKey = key;
            m_FireTime = stepTime + m_Rng.Range(m_Profile.ReactionDelayMin, m_Profile.ReactionDelayMax);
            m_State = EAiDecisionState.Reacting;
        }

        private bool HasRivalInRange(bool hasAhead, float gapAhead, bool hasBehind, float gapBehind)
        {
            float range = m_Profile.EngagementRangeMeters;

            if (hasAhead && gapAhead <= range)
                return true;

            return hasBehind && gapBehind <= range;
        }

        private int PickKey(float usableEnergy)
        {
            int min = m_Profile.PreferredKeyMin;
            int max = m_Profile.PreferredKeyMax;
            int highestAffordable = 0;

            for (int key = max; key >= min; key--)
            {
                if (m_BuffTable.TryGetEnergyCost(key, out float cost) && cost <= usableEnergy)
                {
                    highestAffordable = key;
                    break;
                }
            }

            if (highestAffordable < min)
                return 0;

            return m_Rng.Range(min, highestAffordable + 1);
        }

        private void ScheduleNextDecision(float stepTime)
        {
            float interval = m_Rng.Range(m_Profile.DecisionIntervalMin, m_Profile.DecisionIntervalMax);
            float jitter = m_Profile.IntervalJitter;
            interval *= 1f + m_Rng.Range(-jitter, jitter);
            m_NextDecisionTime = stepTime + Mathf.Max(0.05f, interval);
        }
    }
}
