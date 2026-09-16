using System;
using UnityEngine;

namespace MatchRacers
{
    [Serializable]
    public struct BuffLevelEntry
    {
        [Tooltip("Bu satırın tanımladığı tuş (1-5).")]
        [SerializeField, Range(BuffTableSO.MinKey, BuffTableSO.MaxKey)] private int m_Key;
        [Tooltip("Tuşun enerji maliyeti. Sözleşme gereği 1 tuşu bedavadır; üst seviyeler bilerek pahalıdır ki sürekli 5 basmak tek strateji olmasın.")]
        [SerializeField, Min(0f)] private float m_EnergyCost;
        [Tooltip("Bu tuşun kendi bekleme süresi (saniye). Ortak beklemeden bağımsızdır, aynı tuşu arka arkaya kullanmayı sınırlar.")]
        [SerializeField, Min(0f)] private float m_CooldownSeconds;

        public BuffLevelEntry(int key, float energyCost, float cooldownSeconds)
        {
            m_Key = key;
            m_EnergyCost = energyCost;
            m_CooldownSeconds = cooldownSeconds;
        }

        public int Key => Mathf.Clamp(m_Key, BuffTableSO.MinKey, BuffTableSO.MaxKey);
        public float EnergyCost => m_EnergyCost < 0f ? 0f : m_EnergyCost;
        public float CooldownSeconds => m_CooldownSeconds < 0f ? 0f : m_CooldownSeconds;
    }

    [CreateAssetMenu(fileName = "BuffTable", menuName = "MatchRacers/Buff Table")]
    public sealed class BuffTableSO : ScriptableObject
    {
        public const int MinKey = 1;
        public const int MaxKey = 5;

        [Header("Contract")]
        [Tooltip("Bir nitronun etki penceresi. Case sözleşmesi 1.00 saniyedir; değiştirmek sözleşmeyi bozar ve testler bu değere göre doğrular.")]
        [SerializeField, Min(0.01f)] private float m_WindowSeconds = 1f;

        [Header("Launch")]
        [Tooltip("Nitroya basmakla hızlanmanın başlaması arasındaki gecikme. VFX ateşlenmesiyle hareketin eş zamanlı okunması için var. Pencereyi yemez, sayaç gecikme bitince başlar.")]
        [SerializeField, Min(0f)] private float m_LaunchDelaySeconds = 0.06f;
        [Tooltip("Gecikme boyunca uygulanan taban hız çarpanı. 1 yazılırsa yavaşlama olmaz; 0.92 aracın hafifçe geri oturup fırlaması hissini verir.")]
        [SerializeField, Range(0.5f, 1f)] private float m_LaunchDipMultiplier = 0.92f;

        [Header("Resource")]
        [Tooltip("Enerji havuzunun tavanı. Oyuncunun kaç güçlü hamleyi arka arkaya yapabileceğini belirler.")]
        [SerializeField, Min(0f)] private float m_EnergyMax = 120f;
        [Tooltip("Saniyede dolan enerji. Maliyetlerle birlikte sürdürülebilir nitro seviyesini belirleyen asıl koldur.")]
        [SerializeField, Min(0f)] private float m_EnergyRegenPerSecond = 5f;
        [Tooltip("Herhangi bir nitrodan sonra bütün tuşlara uygulanan ortak bekleme (saniye).")]
        [SerializeField, Min(0f)] private float m_GlobalCooldownSeconds = 0.4f;

        [Header("Levels")]
        [Tooltip("Tuş başına maliyet ve bekleme tablosu. Hız çarpanı (k × taban hız) burada değil kodda sabittir; case sözleşmesi olduğu için yanlışlıkla değiştirilemesin diye dışarı açılmadı.")]
        [SerializeField] private BuffLevelEntry[] m_Levels =
        {
            new BuffLevelEntry(1, 0f, 2f),
            new BuffLevelEntry(2, 24f, 6f),
            new BuffLevelEntry(3, 46f, 11f),
            new BuffLevelEntry(4, 66f, 16f),
            new BuffLevelEntry(5, 84f, 24f),
        };

        public float WindowSeconds => m_WindowSeconds < 0.01f ? 0.01f : m_WindowSeconds;
        public float LaunchDelaySeconds => m_LaunchDelaySeconds < 0f ? 0f : m_LaunchDelaySeconds;
        public float LaunchDipMultiplier => Mathf.Clamp(m_LaunchDipMultiplier, 0.5f, 1f);
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

        public int GetLaunchSteps(int stepHz)
        {
            if (stepHz < 1)
                return 0;

            int steps = Mathf.RoundToInt(LaunchDelaySeconds * stepHz);
            return steps < 0 ? 0 : steps;
        }

        public bool TryGetEnergyCost(int key, out float energyCost)
        {
            return TryGetLevel(key, out energyCost, out _);
        }

        public bool TryGetLevel(int key, out float energyCost, out float cooldownSeconds)
        {
            energyCost = 0f;
            cooldownSeconds = 0f;

            if (!IsValidKey(key) || m_Levels == null)
                return false;

            for (int i = 0; i < m_Levels.Length; i++)
            {
                if (m_Levels[i].Key != key)
                    continue;

                energyCost = m_Levels[i].EnergyCost;
                cooldownSeconds = m_Levels[i].CooldownSeconds;
                return true;
            }

            return false;
        }

        public int GetKeyCooldownSteps(int key, int stepHz)
        {
            if (!TryGetLevel(key, out _, out float cooldownSeconds))
                return 0;

            int steps = Mathf.RoundToInt(cooldownSeconds * stepHz);
            return steps < 0 ? 0 : steps;
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
