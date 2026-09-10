using System;
using UnityEngine;

namespace MatchRacers
{
    [Serializable]
    public struct NitroLevelEntry
    {
        [SerializeField, Range(BuffTableSO.MinKey, BuffTableSO.MaxKey)] private int m_Key;
        [SerializeField] private Color m_Color;
        [SerializeField, Min(0f)] private float m_EmissionScale;
        [SerializeField, Min(0f)] private float m_SizeScale;
        [SerializeField, Min(0f)] private float m_TrailWidth;
        [SerializeField, Range(0.25f, 3f)] private float m_Pitch;
        [SerializeField, Range(0f, 1f)] private float m_Volume;
        [SerializeField, Min(0f)] private float m_LightIntensity;

        public NitroLevelEntry(int key, Color color, float emission, float size, float trail,
            float pitch, float volume, float light)
        {
            m_Key = key;
            m_Color = color;
            m_EmissionScale = emission;
            m_SizeScale = size;
            m_TrailWidth = trail;
            m_Pitch = pitch;
            m_Volume = volume;
            m_LightIntensity = light;
        }

        public int Key => Mathf.Clamp(m_Key, BuffTableSO.MinKey, BuffTableSO.MaxKey);
        public Color Color => m_Color;
        public float EmissionScale => m_EmissionScale <= 0f ? 1f : m_EmissionScale;
        public float SizeScale => m_SizeScale <= 0f ? 1f : m_SizeScale;
        public float TrailWidth => m_TrailWidth;
        public float Pitch => Mathf.Clamp(m_Pitch, 0.25f, 3f);
        public float Volume => Mathf.Clamp01(m_Volume);
        public float LightIntensity => m_LightIntensity;
    }

    [CreateAssetMenu(fileName = "NitroLevelTable", menuName = "MatchRacers/Nitro Level Table")]
    public sealed class NitroLevelTableSO : ScriptableObject
    {
        [Header("Levels")]
        [SerializeField] private NitroLevelEntry[] m_Levels =
        {
            new NitroLevelEntry(1, new Color(0.70f, 0.75f, 0.78f), 0.35f, 0.70f, 0.14f, 0.85f, 0.35f, 0.0f),
            new NitroLevelEntry(2, new Color(0.35f, 0.80f, 0.92f), 0.60f, 0.85f, 0.20f, 0.95f, 0.50f, 1.5f),
            new NitroLevelEntry(3, new Color(0.40f, 0.88f, 0.52f), 0.85f, 1.00f, 0.28f, 1.05f, 0.65f, 3.0f),
            new NitroLevelEntry(4, new Color(0.98f, 0.75f, 0.25f), 1.20f, 1.20f, 0.38f, 1.18f, 0.80f, 5.0f),
            new NitroLevelEntry(5, new Color(1.00f, 0.42f, 0.22f), 1.70f, 1.45f, 0.50f, 1.32f, 1.00f, 8.0f),
        };

        [Header("Envelope")]
        [SerializeField, Min(0.01f)] private float m_EntryRampSeconds = 0.08f;
        [SerializeField, Min(0.01f)] private float m_ExitFadeSeconds = 0.45f;

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
