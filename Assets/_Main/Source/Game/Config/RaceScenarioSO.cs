using System;
using UnityEngine;

namespace MatchRacers
{
    [Serializable]
    public struct ScenarioInputEntry
    {
        [Tooltip("Bu girdinin yarış başından itibaren kaçıncı saniyede uygulanacağı.")]
        [SerializeField, Min(0f)] private float m_TimeSeconds;
        [Tooltip("O anda basılacak tuş (1-5).")]
        [SerializeField, Range(BuffTableSO.MinKey, BuffTableSO.MaxKey)] private int m_Key;

        public float TimeSeconds => m_TimeSeconds < 0f ? 0f : m_TimeSeconds;
        public int Key => Mathf.Clamp(m_Key, BuffTableSO.MinKey, BuffTableSO.MaxKey);
    }

    [CreateAssetMenu(fileName = "RaceScenario", menuName = "MatchRacers/Race Scenario")]
    public sealed class RaceScenarioSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Senaryonun okunabilir adı. Test çıktılarında ve Race Lab tablosunda bu ad görünür.")]
        [SerializeField] private string m_DisplayName = "Scenario";

        [Header("Seeds")]
        [Tooltip("Bu senaryonun koşturulacağı seed listesi. Aynı seed aynı yarışı üretir; GDD en az üç seed ister.")]
        [SerializeField] private int[] m_Seeds = { 1337, 4242, 90210 };

        [Header("Player Input")]
        [Tooltip("Oyuncu girdisinin nasıl üretileceği: hiç basmama, zaman çizelgesi ya da tekrarlayan tuş.")]
        [SerializeField] private EScenarioInputMode m_InputMode = EScenarioInputMode.None;

        [Tooltip("Zaman çizelgesi modunda uygulanacak girdiler. Erken atak sonrası pasiflik gibi davranışlar buradan kurulur.")]
        [SerializeField] private ScenarioInputEntry[] m_Timeline;

        [Tooltip("Tekrar modunda sürekli basılacak tuş. Sürekli 5 denemesi senaryosu bununla kurulur.")]
        [SerializeField, Range(BuffTableSO.MinKey, BuffTableSO.MaxKey)] private int m_RepeatKey = 5;
        [Tooltip("Tekrar modunda iki basış arasındaki süre. Ret kurallarını zorlamak için bilerek bekleme süresinden kısa tutulur.")]
        [SerializeField, Min(0.01f)] private float m_RepeatIntervalSeconds = 0.2f;
        [Tooltip("Tekrarın başlayacağı yarış zamanı (saniye).")]
        [SerializeField, Min(0f)] private float m_RepeatStartSeconds;
        [Tooltip("Tekrarın biteceği yarış zamanı (saniye). Yarıştan uzun bir değer, yarış boyunca basmak demektir.")]
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
