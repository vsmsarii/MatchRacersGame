using UnityEngine;

namespace MatchRacers
{
    [CreateAssetMenu(fileName = "RaceConfig", menuName = "MatchRacers/Race Config")]
    public sealed class RaceConfigSO : ScriptableObject
    {
        public const int CarCount = 8;
        public const int RivalCount = CarCount - 1;

        [Header("Race")]
        [SerializeField, Min(0.1f)] private float m_BaseSpeed = 12f;
        [SerializeField, Min(1f)] private float m_RaceLengthMeters = 1000f;
        [SerializeField, Min(0f)] private float m_CountdownSeconds = 3f;

        [Header("Track")]
        [SerializeField, Min(0.5f)] private float m_LaneWidthMeters = 2.4f;
        [SerializeField, Min(0f)] private float m_RunoutMeters = 60f;
        [SerializeField, Min(0f)] private float m_RoadShoulderMeters = 3f;

        [Header("Track Shape")]
        [SerializeField] private Vector3[] m_Waypoints;

        [Header("Camera")]
        [SerializeField] private bool m_CarsMoveLeftToRight = true;
        [SerializeField, Min(1f)] private float m_CameraSideDistance = 30f;
        [SerializeField] private float m_CameraHeight = 7.5f;
        [SerializeField] private float m_CameraLookAhead = 6f;
        [SerializeField, Min(0.1f)] private float m_CameraFollowSmoothing = 5f;
        [SerializeField, Range(20f, 90f)] private float m_CameraFieldOfView = 52f;
        [SerializeField, Range(0f, 12f)] private float m_CameraBuffFovGain = 2.25f;
        [SerializeField, Range(0f, 1f)] private float m_CameraPackBias = 0.35f;
        [SerializeField, Range(0f, 20f)] private float m_CameraBuffPunch = 5f;

        [Header("Car View")]
        [SerializeField, Min(0.1f)] private float m_LaneChangeSmoothing = 6f;
        [SerializeField] private float m_BankPerBuffLevel = 1.8f;
        [SerializeField, Min(0.05f)] private float m_WheelRadiusMeters = 0.34f;
        [SerializeField, Min(0f)] private float m_BuffTrailSeconds = 0.35f;
        [SerializeField, Min(0f)] private float m_PlayerMarkerHeight = 2.6f;

        [Header("Simulation")]
        [SerializeField, Min(1)] private int m_FixedStepHz = 120;

        [Header("Target Order Mode")]
        [SerializeField, Min(1f)] private float m_TargetSeparationMeters = 12f;
        [SerializeField, Min(0f)] private float m_TargetMarginStartMeters = 6f;
        [SerializeField, Min(0f)] private float m_TargetMarginAheadEndMeters = 48f;
        [SerializeField, Min(0f)] private float m_TargetMarginBehindEndMeters = 16f;
        [SerializeField, Range(0f, 1f)] private float m_TargetAttackLockProgress = 0.65f;
        [SerializeField, Min(0f)] private float m_TargetAttackLockGapMeters = 60f;
        [SerializeField, Range(0.05f, 1f)] private float m_TargetMarginRampEnd = 0.75f;
        [SerializeField, Range(0f, 0.6f)] private float m_TargetSlotJitter = 0.22f;
        [SerializeField, Range(0.5f, 1f)] private float m_TargetPaceMin = 0.75f;
        [SerializeField, Range(1f, 1.6f)] private float m_TargetPaceMax = 1.3f;
        [SerializeField, Min(0.01f)] private float m_TargetPaceRatePerSecond = 0.4f;
        [SerializeField, Min(0.01f)] private float m_TargetPlayerSpeedEmaSeconds = 2f;
        [SerializeField, Min(0.01f)] private float m_TargetBiasResponse = 6f;

        [Header("Telemetry")]
        [SerializeField, Min(0f)] private float m_OvertakeConfirmSeconds = 0.5f;

        [Header("Balance")]
        [SerializeField] private bool m_BalanceEnabled = true;
        [SerializeField, Min(0.01f)] private float m_GapEmaSeconds = 4f;
        [SerializeField, Min(0f)] private float m_GapDeadZoneMeters = 25f;
        [SerializeField, Min(0.01f)] private float m_GapSaturationMeters = 120f;
        [SerializeField, Range(0.5f, 1f)] private float m_BalanceMinMultiplier = 0.98f;
        [SerializeField, Range(1f, 1.5f)] private float m_BalanceMaxMultiplier = 1.06f;
        [SerializeField, Min(0.001f)] private float m_BalanceRatePerSecond = 0.02f;
        [SerializeField, Min(0f)] private float m_PassivePlayerGraceSeconds = 20f;

        [Header("References")]
        [SerializeField] private BuffTableSO m_BuffTable;
        [SerializeField] private RaceCarCatalogSO m_CarCatalog;
        [SerializeField] private NitroEffectCatalogSO m_NitroEffects;
        [SerializeField] private NitroLevelTableSO m_NitroLevels;
        [SerializeField] private RaceAudioCatalogSO m_RaceAudio;
        [SerializeField] private RaceEnvironmentSO m_Environment;
        [SerializeField] private AiProfileSO[] m_RivalProfiles = new AiProfileSO[RivalCount];

        public float BaseSpeed => m_BaseSpeed < 0.1f ? 0.1f : m_BaseSpeed;
        public float RaceLengthMeters => m_RaceLengthMeters < 1f ? 1f : m_RaceLengthMeters;
        public float CountdownSeconds => m_CountdownSeconds < 0f ? 0f : m_CountdownSeconds;

        public float LaneWidthMeters => m_LaneWidthMeters < 0.5f ? 0.5f : m_LaneWidthMeters;
        public float RunoutMeters => m_RunoutMeters < 0f ? 0f : m_RunoutMeters;
        public float RoadShoulderMeters => m_RoadShoulderMeters < 0f ? 0f : m_RoadShoulderMeters;
        public float RoadWidthMeters => CarCount * LaneWidthMeters + 2f * RoadShoulderMeters;

        public float GetLaneOffset(int laneIndex)
        {
            return (laneIndex - (CarCount - 1) * 0.5f) * LaneWidthMeters * -ViewSideSign;
        }

        public Vector3[] Waypoints => m_Waypoints;
        public bool HasCustomPath => m_Waypoints != null && m_Waypoints.Length >= 2;

        public bool CarsMoveLeftToRight => m_CarsMoveLeftToRight;
        public float ViewSideSign => m_CarsMoveLeftToRight ? 1f : -1f;

        public float CameraSideDistance => m_CameraSideDistance < 1f ? 1f : m_CameraSideDistance;
        public float CameraHeight => m_CameraHeight;
        public float CameraLookAhead => m_CameraLookAhead;
        public float CameraFollowSmoothing => m_CameraFollowSmoothing < 0.1f ? 0.1f : m_CameraFollowSmoothing;
        public float CameraFieldOfView => Mathf.Clamp(m_CameraFieldOfView, 20f, 90f);
        public float CameraBuffFovGain => Mathf.Clamp(m_CameraBuffFovGain, 0f, 12f);
        public float CameraPackBias => Mathf.Clamp01(m_CameraPackBias);
        public float CameraBuffPunch => Mathf.Clamp(m_CameraBuffPunch, 0f, 20f);

        public float LaneChangeSmoothing => m_LaneChangeSmoothing < 0.1f ? 0.1f : m_LaneChangeSmoothing;
        public float BankPerBuffLevel => m_BankPerBuffLevel;
        public float WheelRadiusMeters => m_WheelRadiusMeters < 0.05f ? 0.05f : m_WheelRadiusMeters;
        public float BuffTrailSeconds => m_BuffTrailSeconds < 0f ? 0f : m_BuffTrailSeconds;
        public float PlayerMarkerHeight => m_PlayerMarkerHeight < 0f ? 0f : m_PlayerMarkerHeight;

        public int FixedStepHz => m_FixedStepHz < 1 ? 1 : m_FixedStepHz;
        public float FixedDeltaTime => 1f / FixedStepHz;

        public float TargetSeparationMeters => m_TargetSeparationMeters < 1f ? 1f : m_TargetSeparationMeters;
        public float TargetMarginStartMeters => m_TargetMarginStartMeters < 0f ? 0f : m_TargetMarginStartMeters;
        public float TargetMarginAheadEndMeters => Mathf.Max(m_TargetMarginAheadEndMeters, TargetMarginStartMeters);
        public float TargetMarginBehindEndMeters => Mathf.Max(m_TargetMarginBehindEndMeters, TargetMarginStartMeters);
        public float TargetAttackLockProgress => Mathf.Clamp01(m_TargetAttackLockProgress);
        public float TargetAttackLockGapMeters => m_TargetAttackLockGapMeters < 0f ? 0f : m_TargetAttackLockGapMeters;
        public float TargetMarginRampEnd => Mathf.Clamp(m_TargetMarginRampEnd, 0.05f, 1f);
        public float TargetSlotJitter => Mathf.Clamp(m_TargetSlotJitter, 0f, 0.6f);
        public float TargetPaceMin => Mathf.Clamp(m_TargetPaceMin, 0.5f, 1f);
        public float TargetPaceMax => Mathf.Clamp(m_TargetPaceMax, 1f, 1.6f);
        public float TargetPaceRatePerSecond => m_TargetPaceRatePerSecond < 0.01f ? 0.01f : m_TargetPaceRatePerSecond;
        public float TargetPlayerSpeedEmaSeconds => m_TargetPlayerSpeedEmaSeconds < 0.01f ? 0.01f : m_TargetPlayerSpeedEmaSeconds;
        public float TargetBiasResponse => m_TargetBiasResponse < 0.01f ? 0.01f : m_TargetBiasResponse;

        public float OvertakeConfirmSeconds => m_OvertakeConfirmSeconds < 0f ? 0f : m_OvertakeConfirmSeconds;

        public bool BalanceEnabled => m_BalanceEnabled;
        public float GapEmaSeconds => m_GapEmaSeconds < 0.01f ? 0.01f : m_GapEmaSeconds;
        public float GapDeadZoneMeters => m_GapDeadZoneMeters < 0f ? 0f : m_GapDeadZoneMeters;
        public float GapSaturationMeters => Mathf.Max(m_GapSaturationMeters, GapDeadZoneMeters + 0.01f);
        public float BalanceMinMultiplier => Mathf.Clamp(m_BalanceMinMultiplier, 0.5f, 1f);
        public float BalanceMaxMultiplier => Mathf.Clamp(m_BalanceMaxMultiplier, 1f, 1.5f);
        public float BalanceRatePerSecond => m_BalanceRatePerSecond < 0.001f ? 0.001f : m_BalanceRatePerSecond;
        public float PassivePlayerGraceSeconds => m_PassivePlayerGraceSeconds < 0f ? 0f : m_PassivePlayerGraceSeconds;

        public BuffTableSO BuffTable => m_BuffTable;
        public RaceCarCatalogSO CarCatalog => m_CarCatalog;
        public NitroEffectCatalogSO NitroEffects => m_NitroEffects;
        public NitroLevelTableSO NitroLevels => m_NitroLevels;
        public RaceAudioCatalogSO RaceAudio => m_RaceAudio;
        public RaceEnvironmentSO Environment => m_Environment;
        public AiProfileSO[] RivalProfiles => m_RivalProfiles;

        public int BuffWindowSteps => m_BuffTable != null ? m_BuffTable.GetWindowSteps(FixedStepHz) : FixedStepHz;

        public float BaselineRaceSeconds => RaceLengthMeters / BaseSpeed;

        public bool TryGetRivalProfile(int rivalIndex, out AiProfileSO profile)
        {
            profile = null;
            if (m_RivalProfiles == null || rivalIndex < 0 || rivalIndex >= m_RivalProfiles.Length)
                return false;

            profile = m_RivalProfiles[rivalIndex];
            return profile != null;
        }
    }
}
