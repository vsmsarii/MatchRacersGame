using UnityEngine;

namespace MatchRacers
{
    public sealed class RaceCameraRig
    {
        private readonly Camera m_Camera;
        private readonly IRaceView m_Race;
        private readonly RacePath m_Path;
        private readonly RaceConfigSO m_Config;

        private float m_FocusDistance;
        private float m_FieldOfView;
        private float m_Punch;
        private bool m_Initialized;

        public Camera Camera => m_Camera;

        public void Punch(int buffKey)
        {
            float amount = m_Config.CameraBuffPunch * Mathf.Clamp01((buffKey - 1) / 4f);
            if (amount > m_Punch)
                m_Punch = amount;
        }

        public RaceCameraRig(Camera camera, IRaceView race, RacePath path, RaceConfigSO config)
        {
            m_Camera = camera;
            m_Race = race;
            m_Path = path;
            m_Config = config;
            m_FieldOfView = config.CameraFieldOfView;
        }

        public void Sync(float deltaTime)
        {
            CarState player = m_Race.GetCar(m_Race.PlayerCarIndex);
            if (player == null)
                return;

            float packCenter = GetPackCenterDistance();
            float target = Mathf.Lerp(player.Distance, packCenter, m_Config.CameraPackBias);

            if (!m_Initialized)
            {
                m_FocusDistance = target;
                m_Initialized = true;
            }
            else
            {
                m_FocusDistance = Mathf.Lerp(m_FocusDistance, target, 1f - Mathf.Exp(-m_Config.CameraFollowSmoothing * deltaTime));
            }

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
        }

        private float GetPackCenterDistance()
        {
            float sum = 0f;
            for (int i = 0; i < m_Race.CarCount; i++)
                sum += m_Race.GetCar(i).Distance;

            return sum / m_Race.CarCount;
        }
    }
}
