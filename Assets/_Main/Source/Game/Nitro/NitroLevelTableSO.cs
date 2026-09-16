using System;
using UnityEngine;

namespace MatchRacers
{
    [Serializable]
    public struct NitroLevelEntry
    {
        [Tooltip("Bu satırın tanımladığı nitro seviyesi (1-5).")]
        [SerializeField, Range(BuffTableSO.MinKey, BuffTableSO.MaxKey)] private int m_Key;
        [Tooltip("Seviyenin alev ve ışık rengi. Seviyelerin görsel olarak ayrışmasını sağlayan ilk ipucu.")]
        [SerializeField] private Color m_Color;
        [Tooltip("Particle emisyonunun çarpanı. Yüksek seviyelerde alev yoğunlaşır.")]
        [SerializeField, Min(0f)] private float m_EmissionScale;
        [Tooltip("Particle boyutunun çarpanı.")]
        [SerializeField, Min(0f)] private float m_SizeScale;
        [Tooltip("Aracın arkasında kalan hız izinin kalınlığı (metre).")]
        [SerializeField, Min(0f)] private float m_TrailWidth;
        [Tooltip("Seviyenin nitro sesi perdesi. Üst seviyeler daha tiz ve agresif duyulur.")]
        [SerializeField, Range(0.25f, 3f)] private float m_Pitch;
        [Tooltip("Seviyenin nitro sesi seviyesi.")]
        [SerializeField, Range(0f, 1f)] private float m_Volume;
        [Tooltip("Egzozdaki ışık şiddeti. 1 tuşu hız vermediği için orada sıfırdır.")]
        [SerializeField, Min(0f)] private float m_LightIntensity;
        [Tooltip("Bu seviyede kameranın sarsıntı genliği (metre). 1 tuşunda sıfırdır; üst seviyelerde hızın ağırlığını hissettirir.")]
        [SerializeField, Range(0f, 1f)] private float m_CameraShake;

        public NitroLevelEntry(int key, Color color, float emission, float size, float trail,
            float pitch, float volume, float light, float cameraShake = 0f)
        {
            m_Key = key;
            m_Color = color;
            m_EmissionScale = emission;
            m_SizeScale = size;
            m_TrailWidth = trail;
            m_Pitch = pitch;
            m_Volume = volume;
            m_LightIntensity = light;
            m_CameraShake = cameraShake;
        }

        public int Key => Mathf.Clamp(m_Key, BuffTableSO.MinKey, BuffTableSO.MaxKey);
        public Color Color => m_Color;
        public float EmissionScale => m_EmissionScale <= 0f ? 1f : m_EmissionScale;
        public float SizeScale => m_SizeScale <= 0f ? 1f : m_SizeScale;
        public float TrailWidth => m_TrailWidth;
        public float Pitch => Mathf.Clamp(m_Pitch, 0.25f, 3f);
        public float Volume => Mathf.Clamp01(m_Volume);
        public float LightIntensity => m_LightIntensity;
        public float CameraShake => Mathf.Clamp01(m_CameraShake);
    }

    [CreateAssetMenu(fileName = "NitroLevelTable", menuName = "MatchRacers/Nitro Level Table")]
    public sealed class NitroLevelTableSO : ScriptableObject
    {
        [Header("Levels")]
        [Tooltip("Seviye başına görsel ve işitsel değerler. Case'in 2-3-4-5 ayrışması gereksinimi bu tablodan karşılanır.")]
        [SerializeField] private NitroLevelEntry[] m_Levels =
        {
            new NitroLevelEntry(1, new Color(0.70f, 0.75f, 0.78f), 0.35f, 0.70f, 0.14f, 0.85f, 0.35f, 0.0f, 0.00f),
            new NitroLevelEntry(2, new Color(0.35f, 0.80f, 0.92f), 0.60f, 0.85f, 0.20f, 0.95f, 0.50f, 1.5f, 0.05f),
            new NitroLevelEntry(3, new Color(0.40f, 0.88f, 0.52f), 0.85f, 1.00f, 0.28f, 1.05f, 0.65f, 3.0f, 0.10f),
            new NitroLevelEntry(4, new Color(0.98f, 0.75f, 0.25f), 1.20f, 1.20f, 0.38f, 1.18f, 0.80f, 5.0f, 0.17f),
            new NitroLevelEntry(5, new Color(1.00f, 0.42f, 0.22f), 1.70f, 1.45f, 0.50f, 1.32f, 1.00f, 8.0f, 0.26f),
        };

        [Header("Launch")]
        [Tooltip("Nitroya basıldığında aracın görsel olarak ne kadar geri çekileceği (metre). Yalnız çizime uygulanır, gerçek mesafeyi değiştirmez.")]
        [SerializeField, Min(0f)] private float m_LaunchDipMeters = 0.25f;
        [Tooltip("Geri çekilmenin tamamlanma süresi (saniye).")]
        [SerializeField, Min(0.01f)] private float m_LaunchDipSeconds = 0.07f;
        [Tooltip("Geri çekilmeden normale dönüş süresi (saniye). Fırlama hissini bu dönüş verir.")]
        [SerializeField, Min(0.01f)] private float m_LaunchRecoverSeconds = 0.28f;

        [Header("Envelope")]
        [Tooltip("Nitro efektlerinin tam güce çıkma süresi. Kısa tutulur, çünkü giriş anı ani okunmalı.")]
        [SerializeField, Min(0.01f)] private float m_EntryRampSeconds = 0.08f;
        [Tooltip("Nitro bitince efektlerin sönme süresi. Uzun tutulursa hız bittiği halde efekt devam ediyormuş gibi görünür.")]
        [SerializeField, Min(0.01f)] private float m_ExitFadeSeconds = 0.45f;

        public float LaunchDipMeters => m_LaunchDipMeters < 0f ? 0f : m_LaunchDipMeters;
        public float LaunchDipSeconds => m_LaunchDipSeconds < 0.01f ? 0.01f : m_LaunchDipSeconds;
        public float LaunchRecoverSeconds => m_LaunchRecoverSeconds < 0.01f ? 0.01f : m_LaunchRecoverSeconds;
        public float EntryRampSeconds => m_EntryRampSeconds < 0.01f ? 0.01f : m_EntryRampSeconds;
        public float ExitFadeSeconds => m_ExitFadeSeconds < 0.01f ? 0.01f : m_ExitFadeSeconds;

        public NitroLevelEntry Get(int key)
        {
            if (m_Levels != null)
            {
                for (int i = 0; i < m_Levels.Length; i++)
                {
                    if (m_Levels[i].Key == key)
                        return m_Levels[i];
                }
            }

            return new NitroLevelEntry(key, Color.white, 1f, 1f, 0.2f, 1f, 0.6f, 2f);
        }
    }
}
