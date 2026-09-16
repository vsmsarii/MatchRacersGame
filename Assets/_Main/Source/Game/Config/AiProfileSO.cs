using UnityEngine;

namespace MatchRacers
{
    [CreateAssetMenu(fileName = "AiProfile", menuName = "MatchRacers/AI Profile")]
    public sealed class AiProfileSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Profilin okunabilir adı. Debug panelinde ve test çıktılarında bu ad görünür.")]
        [SerializeField] private string m_DisplayName = "Profile";

        [Header("Decision Timing")]
        [Tooltip("Rakibin iki nitro kararı arasında bekleyeceği en kısa süre (saniye). Bu aralık bilerek enerji dolum süresinin üstünde tutulur; aksi halde rakip enerjisi dolar dolmaz refleksle basar ve oyuncunun kopyası gibi davranır.")]
        [SerializeField, Min(0.05f)] private float m_DecisionIntervalMin = 2.5f;
        [Tooltip("İki nitro kararı arasındaki en uzun süre (saniye). Min ile arasındaki açıklık, rakibin ne kadar öngörülemez olduğunu belirler.")]
        [SerializeField, Min(0.05f)] private float m_DecisionIntervalMax = 4f;
        [Tooltip("Karar aralığına eklenen rastgele sapma oranı. Yedi rakibin kararlarının aynı anda tetiklenmesini engeller.")]
        [SerializeField, Range(0f, 1f)] private float m_IntervalJitter = 0.15f;
        [Tooltip("Karar verildikten sonra tuşa basılana kadar geçen en kısa tepki gecikmesi (saniye). İnsan benzeri bir gecikme bırakır.")]
        [SerializeField, Min(0f)] private float m_ReactionDelayMin = 0.15f;
        [Tooltip("Tepki gecikmesinin üst sınırı (saniye). Aynı karede toplu atak oluşmasını dağıtır.")]
        [SerializeField, Min(0f)] private float m_ReactionDelayMax = 0.45f;

        [Header("Buff Choice")]
        [Tooltip("Rakibin tercih ettiği en düşük nitro seviyesi (1-5).")]
        [SerializeField, Range(BuffTableSO.MinKey, BuffTableSO.MaxKey)] private int m_PreferredKeyMin = 3;
        [Tooltip("Rakibin tercih ettiği en yüksek nitro seviyesi (1-5). Enerji yeterse bu aralıkta seçim yapar.")]
        [SerializeField, Range(BuffTableSO.MinKey, BuffTableSO.MaxKey)] private int m_PreferredKeyMax = 4;
        [Tooltip("Rakibin dokunmadan sakladığı enerji oranı. Yüksek değer temkinli, düşük değer savurgan bir rakip üretir.")]
        [SerializeField, Range(0f, 1f)] private float m_EnergyReserveRatio;
        [Tooltip("Enerji tavana yaklaştığında atak isteğinin ne kadar artacağı. Enerjinin boşa taşmasını engeller.")]
        [SerializeField, Range(0f, 1f)] private float m_OverflowUrgency = 0.6f;

        [Header("Attack Window")]
        [Tooltip("Yarış ilerlemesine göre atak isteği çarpanı (0 başlangıç, 1 bitiş). Erken atakçı profili başta, finalde risk alan profili sonda yükselir.")]
        [SerializeField] private AnimationCurve m_AggressionOverProgress = AnimationCurve.Linear(0f, 1f, 1f, 1f);

        [Header("Opportunism")]
        [Tooltip("Açıkken rakip yalnız yakınında mücadele edecek biri varsa atak yapar. Fırsat kollayan profiller için.")]
        [SerializeField] private bool m_RequiresNearbyRival;
        [Tooltip("Yakınında kimse yokken atak isteğinin çarpanı. Sıfıra yakın değer, yalnız kalan rakibi boşa nitro yakmaktan alıkoyar.")]
        [SerializeField, Range(0.02f, 1f)] private float m_IsolatedAttackScale = 0.25f;
        [Tooltip("Bir rakibi yakın saymak için gereken mesafe (metre). Yakınlık kararlarının eşiği.")]
        [SerializeField, Min(1f)] private float m_EngagementRangeMeters = 25f;

        [Header("Situational")]
        [Tooltip("Hemen önünde bir araç varken atak isteğinin artışı. Sollama anlarını üretir.")]
        [SerializeField, Range(0f, 1f)] private float m_OvertakeUrgency = 0.35f;
        [Tooltip("Öndeyken atak isteğinin düşüşü. Lider rakibin gereksiz nitro yakmasını engeller.")]
        [SerializeField, Range(0f, 1f)] private float m_LeadCaution = 0.3f;
        [Tooltip("Rakibin rahat saydığı öndeki boşluk (metre). Bu kadar açtıysa atak isteği azalır.")]
        [SerializeField, Min(0f)] private float m_ComfortGapMeters = 40f;

        [Header("Pace")]
        [Tooltip("Rakibin taban hızındaki rastgele sapma oranı. Sekiz aracın yarış boyunca aynı hizada gitmesini engelleyen ilk katmandır; oyuncuya uygulanmaz.")]
        [SerializeField, Range(0f, 0.2f)] private float m_BaseSpeedVariance = 0.02f;

        public string DisplayName => string.IsNullOrEmpty(m_DisplayName) ? name : m_DisplayName;

        public float DecisionIntervalMin => Mathf.Min(m_DecisionIntervalMin, m_DecisionIntervalMax);
        public float DecisionIntervalMax => Mathf.Max(m_DecisionIntervalMin, m_DecisionIntervalMax);
        public float IntervalJitter => Mathf.Clamp01(m_IntervalJitter);
        public float ReactionDelayMin => Mathf.Min(m_ReactionDelayMin, m_ReactionDelayMax);
        public float ReactionDelayMax => Mathf.Max(m_ReactionDelayMin, m_ReactionDelayMax);

        public int PreferredKeyMin => Mathf.Min(m_PreferredKeyMin, m_PreferredKeyMax);
        public int PreferredKeyMax => Mathf.Max(m_PreferredKeyMin, m_PreferredKeyMax);
        public float EnergyReserveRatio => Mathf.Clamp01(m_EnergyReserveRatio);
        public float OverflowUrgency => Mathf.Clamp01(m_OverflowUrgency);

        public bool RequiresNearbyRival => m_RequiresNearbyRival;
        public float IsolatedAttackScale => Mathf.Clamp(m_IsolatedAttackScale, 0.02f, 1f);
        public float EngagementRangeMeters => m_EngagementRangeMeters < 1f ? 1f : m_EngagementRangeMeters;

        public float OvertakeUrgency => Mathf.Clamp01(m_OvertakeUrgency);
        public float LeadCaution => Mathf.Clamp01(m_LeadCaution);
        public float ComfortGapMeters => m_ComfortGapMeters < 0f ? 0f : m_ComfortGapMeters;

        public float BaseSpeedVariance => Mathf.Clamp(m_BaseSpeedVariance, 0f, 0.2f);

        public float EvaluateAggression(float raceProgress)
        {
            if (m_AggressionOverProgress == null || m_AggressionOverProgress.length == 0)
                return 1f;

            return Mathf.Clamp01(m_AggressionOverProgress.Evaluate(Mathf.Clamp01(raceProgress)));
        }
    }
}
