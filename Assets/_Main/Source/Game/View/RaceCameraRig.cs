using UnityEngine;

namespace MatchRacers
{
    public sealed class RaceCameraRig
    {
        private const float TeleportSpeed = 500f;

        private readonly Camera m_Camera;
        private readonly IRaceView m_Race;
        private RacePath m_Path;
        private readonly RaceConfigSO m_Config;
        private readonly CarView[] m_Views;

        private float m_FocusDistance;
        private float m_Lag;
        private float m_PackOffset;
        private float m_LastPlayerDistance;
        private float m_FieldOfView;
        private float m_Punch;
        private bool m_Initialized;
        private float m_ShakeKick;
        private float m_ShakeTime;

        public Camera Camera => m_Camera;
        public float FocusDistance => m_FocusDistance;

        public static float GetShakeAmplitude(float levelShake, float kick, bool nitroActive, float scale, float sustain)
        {
            float sustained = nitroActive ? levelShake * sustain : 0f;
            return Mathf.Max(0f, scale * (sustained + kick));
        }

        public void Snap()
        {
            m_Initialized = false;
            m_Punch = 0f;
            m_ShakeKick = 0f;
            m_FieldOfView = m_Config.CameraFieldOfView;
        }

        public void SetPath(RacePath path)
        {
            m_Path = path;
            Snap();
        }

        public void Punch(int buffKey)
        {
            float amount = m_Config.CameraBuffPunch * Mathf.Clamp01((buffKey - 1) / 4f);
            if (amount > m_Punch)
                m_Punch = amount;

            float kick = GetLevelShake(buffKey) * m_Config.CameraShakeKick;
            if (kick > m_ShakeKick)
                m_ShakeKick = kick;
        }

        public RaceCameraRig(Camera camera, IRaceView race, RacePath path, RaceConfigSO config, CarView[] views)
        {
            m_Camera = camera;
            m_Race = race;
            m_Path = path;
            m_Config = config;
            m_Views = views;
            m_FieldOfView = config.CameraFieldOfView;
        }

        public void Sync(float deltaTime)
        {
            CarState player = m_Race.GetCar(m_Race.PlayerCarIndex);
            if (player == null)
                return;

            float alpha = m_Race.InterpolationAlpha;
            float playerDistance = GetVisualDistance(m_Race.PlayerCarIndex, alpha);
            float packOffset = (GetPackCenterDistance(alpha) - playerDistance) * m_Config.CameraPackBias;
            float velocity = deltaTime > 0f ? (playerDistance - m_LastPlayerDistance) / deltaTime : 0f;
            m_LastPlayerDistance = playerDistance;

            if (!m_Initialized || Mathf.Abs(velocity) > TeleportSpeed)
            {
                m_Lag = 0f;
                m_PackOffset = packOffset;
                m_Initialized = true;
            }
            else
            {
                float smoothing = m_Config.CameraFollowSmoothing;
                float blend = 1f - Mathf.Exp(-smoothing * deltaTime);
                m_Lag = Mathf.Lerp(m_Lag, Mathf.Max(0f, velocity) / smoothing, blend);
                m_PackOffset = Mathf.Lerp(m_PackOffset, packOffset, blend);
            }

            m_FocusDistance = playerDistance + m_PackOffset - m_Lag;

            m_Path.Evaluate(m_FocusDistance, out Vector3 focus, out Vector3 forward);
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

            m_Camera.transform.position = focus
                                          + right * (m_Config.ViewSideSign * m_Config.CameraSideDistance)
                                          + Vector3.up * m_Config.CameraHeight;
            m_Camera.transform.rotation = Quaternion.LookRotation(
                (focus + forward * m_Config.CameraLookAhead) - m_Camera.transform.position, Vector3.up);

            float targetFov = m_Config.CameraFieldOfView;
            if (player.HasActiveBuff)
                targetFov += (player.ActiveBuffKey - 1) * m_Config.CameraBuffFovGain;

            m_FieldOfView = Mathf.Lerp(m_FieldOfView, targetFov, 1f - Mathf.Exp(-6f * deltaTime));
            m_Punch = Mathf.Lerp(m_Punch, 0f, 1f - Mathf.Exp(-7f * deltaTime));
            m_Camera.fieldOfView = m_FieldOfView + m_Punch;

            ApplyShake(player, deltaTime);
        }

        private void ApplyShake(CarState player, float deltaTime)
        {
            m_ShakeKick *= Mathf.Exp(-m_Config.CameraShakeKickDecay * deltaTime);
            if (m_ShakeKick < 0.0001f)
                m_ShakeKick = 0f;

            float levelShake = player.HasActiveBuff ? GetLevelShake(player.ActiveBuffKey) : 0f;
            float amplitude = GetShakeAmplitude(levelShake, m_ShakeKick, player.HasActiveBuff,
                m_Config.CameraShakeScale, m_Config.CameraShakeSustain);

            if (amplitude <= 0.0001f)
                return;

            m_ShakeTime += deltaTime * m_Config.CameraShakeFrequency;

            float x = Mathf.PerlinNoise(m_ShakeTime, 0.37f) * 2f - 1f;
            float y = Mathf.PerlinNoise(0.71f, m_ShakeTime) * 2f - 1f;
            float roll = Mathf.PerlinNoise(m_ShakeTime, 5.3f) * 2f - 1f;

            Transform view = m_Camera.transform;
            view.position += view.right * (x * amplitude) + view.up * (y * amplitude * 0.6f);
            view.rotation *= Quaternion.Euler(0f, 0f, roll * amplitude * m_Config.CameraShakeRollPerMeter);
        }

        private float GetLevelShake(int buffKey)
        {
            return m_Config.NitroLevels != null ? m_Config.NitroLevels.Get(buffKey).CameraShake : 0f;
        }

        private float GetPackCenterDistance(float alpha)
        {
            float sum = 0f;
            for (int i = 0; i < m_Race.CarCount; i++)
                sum += GetVisualDistance(i, alpha);

            return sum / m_Race.CarCount;
        }

        private float GetVisualDistance(int carIndex, float alpha)
        {
            if (m_Views != null && carIndex < m_Views.Length && m_Views[carIndex] != null)
                return m_Views[carIndex].VisualDistance;

            return m_Race.GetCar(carIndex).GetRenderDistance(alpha);
        }
    }
}
