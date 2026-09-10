using UnityEngine;

namespace MatchRacers
{
    public sealed class CatchUpBalancer : IPaceController
    {
        private readonly RaceConfigSO m_Config;
        private readonly float[] m_SmoothedGap;
        private readonly float[] m_Current;

        private bool m_Seeded;

        public CatchUpBalancer(RaceConfigSO config, int carCount)
        {
            m_Config = config;
            m_SmoothedGap = new float[carCount];
            m_Current = new float[carCount];
        }

        public float GetSmoothedGap(int carIndex)
        {
            return carIndex >= 0 && carIndex < m_SmoothedGap.Length ? m_SmoothedGap[carIndex] : 0f;
        }

        public void Reset(CarState[] cars, uint seed, int targetPosition)
        {
            m_Seeded = false;

            for (int i = 0; i < m_Current.Length; i++)
            {
                m_SmoothedGap[i] = 0f;
                m_Current[i] = 1f;
                cars[i].BalanceMultiplier = 1f;
                cars[i].PaceBias = 0f;
            }
        }

        public void Step(CarState[] cars, int playerCarIndex, float raceTime, float playerLastBuffTime, float deltaTime)
        {
            if (!m_Config.BalanceEnabled)
                return;

            CarState player = cars[playerCarIndex];
            float emaAlpha = 1f - Mathf.Exp(-deltaTime / m_Config.GapEmaSeconds);
            float deadZone = m_Config.GapDeadZoneMeters;
            float saturation = m_Config.GapSaturationMeters;
            float maxStep = m_Config.BalanceRatePerSecond * deltaTime;

            bool playerRecentlyActive = raceTime - playerLastBuffTime <= m_Config.PassivePlayerGraceSeconds;

            for (int i = 0; i < cars.Length; i++)
            {
                CarState car = cars[i];

                if (i == playerCarIndex || car.Finished)
                {
                    car.BalanceMultiplier = 1f;
                    continue;
                }

                float rawGap = player.Distance - car.Distance;
                m_SmoothedGap[i] = m_Seeded
                    ? m_SmoothedGap[i] + (rawGap - m_SmoothedGap[i]) * emaAlpha
                    : rawGap;

                if (car.HasActiveBuff)
                {
                    car.BalanceMultiplier = 1f;
                    continue;
                }

                float target = ComputeTarget(m_SmoothedGap[i], deadZone, saturation, playerRecentlyActive);
                m_Current[i] += Mathf.Clamp(target - m_Current[i], -maxStep, maxStep);
                m_Current[i] = Mathf.Clamp(m_Current[i], m_Config.BalanceMinMultiplier, m_Config.BalanceMaxMultiplier);
                car.BalanceMultiplier = m_Current[i];
            }

            m_Seeded = true;
        }

        private float ComputeTarget(float smoothedGap, float deadZone, float saturation, bool playerRecentlyActive)
        {
            float magnitude = Mathf.Clamp01((Mathf.Abs(smoothedGap) - deadZone) / (saturation - deadZone));
            if (magnitude <= 0f)
                return 1f;

            if (smoothedGap > 0f)
                return 1f + magnitude * (m_Config.BalanceMaxMultiplier - 1f);

            if (!playerRecentlyActive)
                return 1f;

            return 1f - magnitude * (1f - m_Config.BalanceMinMultiplier);
        }
    }
}
