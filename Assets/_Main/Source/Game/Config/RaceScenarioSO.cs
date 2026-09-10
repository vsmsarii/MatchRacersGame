using System;
using UnityEngine;

namespace MatchRacers
{
    [Serializable]
    public struct ScenarioInputEntry
    {
        [SerializeField, Min(0f)] private float m_TimeSeconds;
        [SerializeField, Range(BuffTableSO.MinKey, BuffTableSO.MaxKey)] private int m_Key;

        public float TimeSeconds => m_TimeSeconds < 0f ? 0f : m_TimeSeconds;
        public int Key => Mathf.Clamp(m_Key, BuffTableSO.MinKey, BuffTableSO.MaxKey);
    }

    [CreateAssetMenu(fileName = "RaceScenario", menuName = "MatchRacers/Race Scenario")]
    public sealed class RaceScenarioSO : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string m_DisplayName = "Scenario";

        [Header("Seeds")]
        [SerializeField] private int[] m_Seeds = { 1337, 4242, 90210 };

        [Header("Player Input")]
        [SerializeField] private EScenarioInputMode m_InputMode = EScenarioInputMode.None;

        [SerializeField] private ScenarioInputEntry[] m_Timeline;

        [SerializeField, Range(BuffTableSO.MinKey, BuffTableSO.MaxKey)] private int m_RepeatKey = 5;
        [SerializeField, Min(0.01f)] private float m_RepeatIntervalSeconds = 0.2f;
        [SerializeField, Min(0f)] private float m_RepeatStartSeconds;
        [SerializeField, Min(0f)] private float m_RepeatEndSeconds = 999f;

        public string DisplayName => string.IsNullOrEmpty(m_DisplayName) ? name : m_DisplayName;
        public int[] Seeds => m_Seeds;
        public EScenarioInputMode InputMode => m_InputMode;
        public ScenarioInputEntry[] Timeline => m_Timeline;
        public int RepeatKey => Mathf.Clamp(m_RepeatKey, BuffTableSO.MinKey, BuffTableSO.MaxKey);
        public float RepeatIntervalSeconds => m_RepeatIntervalSeconds < 0.01f ? 0.01f : m_RepeatIntervalSeconds;
        public float RepeatStartSeconds => Mathf.Min(m_RepeatStartSeconds, m_RepeatEndSeconds);
        public float RepeatEndSeconds => Mathf.Max(m_RepeatStartSeconds, m_RepeatEndSeconds);
    }
}
