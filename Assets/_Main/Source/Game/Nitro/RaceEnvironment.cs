using UnityEngine;
using UnityEngine.Rendering;

namespace MatchRacers
{
    public sealed class RaceEnvironment
    {
        private readonly RaceEnvironmentSO m_Settings;

        private GameObject m_Root;
        private Light m_Sun;
        private Volume m_Volume;

        private AmbientMode m_PreviousAmbientMode;
        private Color m_PreviousSky;
        private Color m_PreviousEquator;
        private Color m_PreviousGround;
        private bool m_PreviousFog;
        private Color m_PreviousFogColor;
        private float m_PreviousFogStart;
        private float m_PreviousFogEnd;
        private FogMode m_PreviousFogMode;
        private bool m_Applied;

        public Light Sun => m_Sun;
        public Color BackgroundColor => m_Settings != null ? m_Settings.BackgroundColor : Color.black;

        public RaceEnvironment(RaceEnvironmentSO settings)
        {
            m_Settings = settings;
        }

        public void Apply()
        {
            if (m_Settings == null || m_Applied)
                return;

            m_Applied = true;
            m_Root = new GameObject("RaceEnvironment");

            CacheRenderSettings();

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = m_Settings.AmbientSky;
            RenderSettings.ambientEquatorColor = m_Settings.AmbientEquator;
            RenderSettings.ambientGroundColor = m_Settings.AmbientGround;

            RenderSettings.fog = m_Settings.FogEnabled;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = m_Settings.FogColor;
            RenderSettings.fogStartDistance = m_Settings.FogStart;
            RenderSettings.fogEndDistance = m_Settings.FogEnd;

            CreateSun();
            CreateVolume();
        }

        public void Dispose()
        {
            if (!m_Applied)
                return;

            m_Applied = false;

            RenderSettings.ambientMode = m_PreviousAmbientMode;
            RenderSettings.ambientSkyColor = m_PreviousSky;
            RenderSettings.ambientEquatorColor = m_PreviousEquator;
            RenderSettings.ambientGroundColor = m_PreviousGround;
            RenderSettings.fog = m_PreviousFog;
            RenderSettings.fogMode = m_PreviousFogMode;
            RenderSettings.fogColor = m_PreviousFogColor;
            RenderSettings.fogStartDistance = m_PreviousFogStart;
            RenderSettings.fogEndDistance = m_PreviousFogEnd;

            if (m_Root != null)
                Object.Destroy(m_Root);

            m_Root = null;
            m_Sun = null;
            m_Volume = null;
        }

        private void CacheRenderSettings()
        {
            m_PreviousAmbientMode = RenderSettings.ambientMode;
            m_PreviousSky = RenderSettings.ambientSkyColor;
            m_PreviousEquator = RenderSettings.ambientEquatorColor;
            m_PreviousGround = RenderSettings.ambientGroundColor;
            m_PreviousFog = RenderSettings.fog;
            m_PreviousFogMode = RenderSettings.fogMode;
            m_PreviousFogColor = RenderSettings.fogColor;
            m_PreviousFogStart = RenderSettings.fogStartDistance;
            m_PreviousFogEnd = RenderSettings.fogEndDistance;
        }

        private void CreateSun()
        {
            Light[] existing = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            for (int i = 0; i < existing.Length; i++)
            {
                if (existing[i].type == LightType.Directional && existing[i].isActiveAndEnabled)
                    existing[i].enabled = false;
            }

            GameObject host = new GameObject("RaceSun");
            host.transform.SetParent(m_Root.transform, false);
            host.transform.rotation = Quaternion.Euler(m_Settings.SunEuler);

            m_Sun = host.AddComponent<Light>();
            m_Sun.type = LightType.Directional;
            m_Sun.color = m_Settings.SunColor;
            m_Sun.intensity = m_Settings.SunIntensity;
            m_Sun.shadows = m_Settings.SunShadows;
            m_Sun.shadowStrength = m_Settings.ShadowStrength;
        }

        private void CreateVolume()
        {
            if (m_Settings.VolumeProfile == null)
                return;

            GameObject host = new GameObject("RaceVolume");
            host.transform.SetParent(m_Root.transform, false);

            m_Volume = host.AddComponent<Volume>();
            m_Volume.isGlobal = true;
            m_Volume.priority = 10f;
            m_Volume.weight = m_Settings.VolumeWeight;
            m_Volume.sharedProfile = m_Settings.VolumeProfile;
        }
    }
}
