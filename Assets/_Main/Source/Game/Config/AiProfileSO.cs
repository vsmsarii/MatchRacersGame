using UnityEngine;

namespace MatchRacers
{
    [CreateAssetMenu(fileName = "AiProfile", menuName = "MatchRacers/AI Profile")]
    public sealed class AiProfileSO : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string m_DisplayName = "Profile";

        [Header("Decision Timing")]
        [SerializeField, Min(0.05f)] private float m_DecisionIntervalMin = 2.5f;
        [SerializeField, Min(0.05f)] private float m_DecisionIntervalMax = 4f;
        [SerializeField, Range(0f, 1f)] private float m_IntervalJitter = 0.15f;
        [SerializeField, Min(0f)] private float m_ReactionDelayMin = 0.15f;
        [SerializeField, Min(0f)] private float m_ReactionDelayMax = 0.45f;

        [Header("Buff Choice")]
        [SerializeField, Range(BuffTableSO.MinKey, BuffTableSO.MaxKey)] private int m_PreferredKeyMin = 3;
        [SerializeField, Range(BuffTableSO.MinKey, BuffTableSO.MaxKey)] private int m_PreferredKeyMax = 4;
        [SerializeField, Range(0f, 1f)] private float m_EnergyReserveRatio;
        [SerializeField, Range(0f, 1f)] private float m_OverflowUrgency = 0.6f;

        [Header("Attack Window")]
        [SerializeField] private AnimationCurve m_AggressionOverProgress = AnimationCurve.Linear(0f, 1f, 1f, 1f);

        [Header("Opportunism")]
        [SerializeField] private bool m_RequiresNearbyRival;
        [SerializeField, Min(1f)] private float m_EngagementRangeMeters = 25f;

        [Header("Situational")]
        [SerializeField, Range(0f, 1f)] private float m_OvertakeUrgency = 0.35f;
        [SerializeField, Range(0f, 1f)] private float m_LeadCaution = 0.3f;
        [SerializeField, Min(0f)] private float m_ComfortGapMeters = 40f;

        [Header("Pace")]
        [SerializeField, Range(0f, 0.2f)] private float m_BaseSpeedVariance = 0.02f;

        public string DisplayName => string.IsNullOrEmpty(m_DisplayName) ? name : m_DisplayName;

        public float DecisionIntervalMin => Mathf.Min(m_DecisionIntervalMin, m_DecisionIntervalMax);
        public float DecisionIntervalMax => Mathf.Max(m_DecisionIntervalMin, m_DecisionIntervalMax);
        public float IntervalJitter => Mathf.Clamp01(m_IntervalJitter);
        public float ReactionDelayMin => Mathf.Min(m_ReactionDelayMin, m_ReactionDelayMax);
        public float ReactionDelayMax => Mathf.Max(m_ReactionDelayMin, m_ReactionDelayMax);

        public int PreferredKeyMin => Mathf.Min(m_PreferredKeyMin, m_PreferredKeyMax);
        public int PreferredKeyMax => Mathf.Max(m_PreferredKeyMin, m_PreferredKeyMax);
        public float EnergyReserveRatio => Mathf.Clamp01(m_EnergyReserveRatio);
        public float OverflowUrgency => Mathf.Clamp01(m_OverflowUrgency);

        public bool RequiresNearbyRival => m_RequiresNearbyRival;
        public float EngagementRangeMeters => m_EngagementRangeMeters < 1f ? 1f : m_EngagementRangeMeters;

        public float OvertakeUrgency => Mathf.Clamp01(m_OvertakeUrgency);
        public float LeadCaution => Mathf.Clamp01(m_LeadCaution);
        public float ComfortGapMeters => m_ComfortGapMeters < 0f ? 0f : m_ComfortGapMeters;

        public float BaseSpeedVariance => Mathf.Clamp(m_BaseSpeedVariance, 0f, 0.2f);

        public float EvaluateAggression(float raceProgress)
        {
            if (m_AggressionOverProgress == null || m_AggressionOverProgress.length == 0)
                return 1f;

            return Mathf.Clamp01(m_AggressionOverProgress.Evaluate(Mathf.Clamp01(raceProgress)));
        }
    }
}
