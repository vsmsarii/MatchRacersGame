using UnityEngine;
using UnityEngine.Rendering;

namespace MatchRacers
{
    [CreateAssetMenu(fileName = "RaceEnvironment", menuName = "MatchRacers/Race Environment")]
    public sealed class RaceEnvironmentSO : ScriptableObject
    {
        [Header("Sun")]
        [SerializeField] private Vector3 m_SunEuler = new Vector3(48f, -35f, 0f);
        [SerializeField] private Color m_SunColor = new Color(1f, 0.97f, 0.9f);
        [SerializeField, Min(0f)] private float m_SunIntensity = 1.1f;
        [SerializeField] private LightShadows m_SunShadows = LightShadows.Soft;
        [SerializeField, Range(0f, 1f)] private float m_ShadowStrength = 0.65f;

        [Header("Ambient")]
        [SerializeField] private Color m_AmbientSky = new Color(0.32f, 0.36f, 0.42f);
        [SerializeField] private Color m_AmbientEquator = new Color(0.22f, 0.24f, 0.28f);
        [SerializeField] private Color m_AmbientGround = new Color(0.10f, 0.11f, 0.13f);

        [Header("Camera Background")]
        [SerializeField] private Color m_BackgroundColor = new Color(0.09f, 0.11f, 0.14f);

        [Header("Fog")]
        [SerializeField] private bool m_FogEnabled = true;
        [SerializeField] private Color m_FogColor = new Color(0.10f, 0.12f, 0.15f);
        [SerializeField, Min(0f)] private float m_FogStart = 120f;
        [SerializeField, Min(0f)] private float m_FogEnd = 420f;

        [Header("Post Process")]
        [SerializeField] private VolumeProfile m_VolumeProfile;
        [SerializeField, Min(0f)] private float m_VolumeWeight = 1f;

        public Vector3 SunEuler => m_SunEuler;
        public Color SunColor => m_SunColor;
        public float SunIntensity => m_SunIntensity < 0f ? 0f : m_SunIntensity;
        public LightShadows SunShadows => m_SunShadows;
        public float ShadowStrength => Mathf.Clamp01(m_ShadowStrength);

        public Color AmbientSky => m_AmbientSky;
        public Color AmbientEquator => m_AmbientEquator;
        public Color AmbientGround => m_AmbientGround;
        public Color BackgroundColor => m_BackgroundColor;

        public bool FogEnabled => m_FogEnabled;
        public Color FogColor => m_FogColor;
        public float FogStart => m_FogStart;
        public float FogEnd => Mathf.Max(m_FogEnd, m_FogStart + 1f);

        public VolumeProfile VolumeProfile => m_VolumeProfile;
        public float VolumeWeight => Mathf.Clamp01(m_VolumeWeight);
    }
}
