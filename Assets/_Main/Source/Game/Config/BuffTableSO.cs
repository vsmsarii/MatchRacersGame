using System;
using UnityEngine;

namespace MatchRacers
{
    [Serializable]
    public struct BuffLevelEntry
    {
        [SerializeField, Range(BuffTableSO.MinKey, BuffTableSO.MaxKey)] private int m_Key;
        [SerializeField, Min(0f)] private float m_EnergyCost;

        public BuffLevelEntry(int key, float energyCost)
        {
            m_Key = key;
            m_EnergyCost = energyCost;
        }

        public int Key => Mathf.Clamp(m_Key, BuffTableSO.MinKey, BuffTableSO.MaxKey);
        public float EnergyCost => m_EnergyCost < 0f ? 0f : m_EnergyCost;
    }

    [CreateAssetMenu(fileName = "BuffTable", menuName = "MatchRacers/Buff Table")]
    public sealed class BuffTableSO : ScriptableObject
    {
        public const int MinKey = 1;
        public const int MaxKey = 5;

        [Header("Contract")]
        [SerializeField, Min(0.01f)] private float m_WindowSeconds = 1f;

        [Header("Resource")]
        [SerializeField, Min(0f)] private float m_EnergyMax = 120f;
        [SerializeField, Min(0f)] private float m_EnergyRegenPerSecond = 5f;
        [SerializeField, Min(0f)] private float m_GlobalCooldownSeconds = 0.4f;

        [Header("Levels")]
        [SerializeField] private BuffLevelEntry[] m_Levels =
        {
            new BuffLevelEntry(1, 0f),
            new BuffLevelEntry(2, 22f),
            new BuffLevelEntry(3, 45f),
            new BuffLevelEntry(4, 72f),
            new BuffLevelEntry(5, 105f),
        };

        public float WindowSeconds => m_WindowSeconds < 0.01f ? 0.01f : m_WindowSeconds;
        public float EnergyMax => m_EnergyMax < 0f ? 0f : m_EnergyMax;
        public float EnergyRegenPerSecond => m_EnergyRegenPerSecond < 0f ? 0f : m_EnergyRegenPerSecond;
        public float GlobalCooldownSeconds => m_GlobalCooldownSeconds < 0f ? 0f : m_GlobalCooldownSeconds;
        public BuffLevelEntry[] Levels => m_Levels;

        public static bool IsValidKey(int key)
        {
            return key >= MinKey && key <= MaxKey;
        }

        public static float GetSpeedMultiplier(int key)
        {
            return IsValidKey(key) ? key : 1f;
        }

        public int GetWindowSteps(int stepHz)
        {
            if (stepHz < 1)
                return 1;

            int steps = Mathf.RoundToInt(WindowSeconds * stepHz);
            return steps < 1 ? 1 : steps;
        }

        public bool TryGetEnergyCost(int key, out float energyCost)
        {
            energyCost = 0f;
            if (!IsValidKey(key) || m_Levels == null)
                return false;

            for (int i = 0; i < m_Levels.Length; i++)
            {
                if (m_Levels[i].Key != key)
                    continue;

                energyCost = m_Levels[i].EnergyCost;
                return true;
            }

            return false;
        }

        public float GetSustainedMultiplier(int key)
        {
            if (!TryGetEnergyCost(key, out float energyCost) || energyCost <= 0f)
                return 1f;

            return 1f + (key - 1) * EnergyRegenPerSecond / energyCost;
        }

        public float GetRefillSeconds(int key)
        {
            if (!TryGetEnergyCost(key, out float energyCost) || EnergyRegenPerSecond <= 0f)
                return 0f;

            return energyCost / EnergyRegenPerSecond;
        }
    }
}
