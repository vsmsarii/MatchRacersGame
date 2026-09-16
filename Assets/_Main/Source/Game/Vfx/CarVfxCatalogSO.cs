using System;
using UnityEngine;

namespace MatchRacers
{
    [Serializable]
    public struct CarVfxEntry
    {
        [Tooltip("Bu satırın tanımladığı araç efekti: rölanti dumanı, seyir alevi ya da nitro alevi.")]
        [SerializeField] private ECarVfx m_Effect;
        [Tooltip("Efektin prefabı. Havuzdan çekilir, spawn noktasına takılır ve istenmeyince havuza iade edilir.")]
        [SerializeField] private GameObject m_Prefab;
        [Tooltip("Efektin hedef ölçeği. Nitro alevinde seviye başına artış ayrıca uygulanır.")]
        [SerializeField, Min(0f)] private float m_Scale;
        [Tooltip("Efektin sıfırdan hedef ölçeğe çıkma süresi (saniye).")]
        [SerializeField, Min(0.01f)] private float m_GrowSeconds;
        [Tooltip("Efektin hedef ölçekten sıfıra inme süresi (saniye).")]
        [SerializeField, Min(0.01f)] private float m_ShrinkSeconds;
        [Tooltip("Yükleme sırasında havuza hazırlanacak kopya sayısı. Kod bunu toplam spawn noktası sayısıyla karşılaştırıp büyüğünü kullanır; ilk nitroda takılma yaşanmaması için vardır.")]
        [SerializeField, Min(0)] private int m_Warmup;
        [Tooltip("Açıkken parçacıklar aracın uzayında simüle edilir ve araçla birlikte taşınır. Kapalıyken parçacıklar çıktıkları yerde kalır: hızlı araçta alev noktalı bir ize dönüşür.")]
        [SerializeField] private bool m_FollowCar;
        [Tooltip("Efekt açılırken hedefin ne kadar üstüne çıkacağı. Nitro alevinde 0.1 verilirse ölçek 0'dan 1.1'e çıkıp 1.0'a oturur.")]
        [SerializeField, Min(0f)] private float m_Overshoot;
        [Tooltip("Aşan ölçeğin hedefe oturma süresi (saniye).")]
        [SerializeField, Min(0.01f)] private float m_SettleSeconds;
        [Tooltip("Açıkken efekt ilk oynatıldığı anda bir ömür boyu simüle edilmiş gibi belirir. Rölanti dumanında kapalıdır; açık olduğunda duman sahne yüklenince havaya fırlamış görünüyordu.")]
        [SerializeField] private bool m_Prewarm;

        public CarVfxEntry(ECarVfx effect, float scale, float growSeconds, float shrinkSeconds, int warmup, bool followCar,
            float overshoot = 0f, float settleSeconds = 0.15f, bool prewarm = false)
        {
            m_Prewarm = prewarm;
            m_Effect = effect;
            m_Prefab = null;
            m_Scale = scale;
            m_GrowSeconds = growSeconds;
            m_ShrinkSeconds = shrinkSeconds;
            m_Warmup = warmup;
            m_FollowCar = followCar;
            m_Overshoot = overshoot;
            m_SettleSeconds = settleSeconds;
        }

        public ECarVfx Effect => m_Effect;
        public GameObject Prefab => m_Prefab;
        public float Scale => m_Scale < 0f ? 0f : m_Scale;
        public float GrowSeconds => m_GrowSeconds < 0.01f ? 0.01f : m_GrowSeconds;
        public float ShrinkSeconds => m_ShrinkSeconds < 0.01f ? 0.01f : m_ShrinkSeconds;
        public int Warmup => m_Warmup < 0 ? 0 : m_Warmup;
        public bool FollowCar => m_FollowCar;
        public bool Prewarm => m_Prewarm;
        public float Overshoot => m_Overshoot < 0f ? 0f : m_Overshoot;
        public float SettleSeconds => m_SettleSeconds < 0.01f ? 0.01f : m_SettleSeconds;
    }

    [CreateAssetMenu(fileName = "CarVfxCatalog", menuName = "MatchRacers/Car Vfx Catalog")]
    public sealed class CarVfxCatalogSO : ScriptableObject
    {
        public const string AddressPrefix = "vfx.car.";

        [Header("Effects")]
        [Tooltip("Araç efektlerinin tablosu. Spawn noktaları araç prefablarındaki CarVfxRig bileşeninde tanımlanır.")]
        [SerializeField] private CarVfxEntry[] m_Entries =
        {
            new CarVfxEntry(ECarVfx.IdleSmoke, 1.22f, 0.4f, 0.3f, 8, false),
            new CarVfxEntry(ECarVfx.CruiseFlame, 0.2f, 0.2f, 0.15f, 8, true),
            new CarVfxEntry(ECarVfx.NitroFlame, 1.0f, 0.12f, 0.25f, 8, true, 0.1f, 0.15f),
        };

        [Header("Nitro Scaling")]
        [Tooltip("Nitro alevinin görünmeye başladığı seviye. 1 tuşu hız vermediği için alev de çıkmaz.")]
        [SerializeField, Range(BuffTableSO.MinKey, BuffTableSO.MaxKey)] private int m_NitroBaseLevel = 2;
        [Tooltip("Nitro seviyesi başına alev ölçeğine eklenen artış. Seviyelerin görsel ayrışmasını sağlar.")]
        [SerializeField] private float m_NitroScalePerLevel = 0.1f;

        [Header("Motion")]
        [Tooltip("Aracın hareket ediyor sayılması için gereken hız (m/sn). Bunun altında rölanti dumanı, üstünde seyir alevi görünür.")]
        [SerializeField, Min(0f)] private float m_MovingSpeedThreshold = 0.5f;

        [Header("Culling")]
        [Tooltip("Kamera kırpması için araç çevresinde varsayılan yarıçap (metre). Görüş dışındaki araçların efektleri havuza iade edilir.")]
        [SerializeField, Min(0.1f)] private float m_CullRadius = 3.5f;

        public int EntryCount => m_Entries != null ? m_Entries.Length : 0;
        public int NitroBaseLevel => Mathf.Clamp(m_NitroBaseLevel, BuffTableSO.MinKey, BuffTableSO.MaxKey);
        public float NitroScalePerLevel => m_NitroScalePerLevel;
        public float MovingSpeedThreshold => m_MovingSpeedThreshold < 0f ? 0f : m_MovingSpeedThreshold;
        public float CullRadius => m_CullRadius < 0.1f ? 0.1f : m_CullRadius;

        public static string GetAddress(ECarVfx effect)
        {
            return AddressPrefix + effect;
        }

        public static bool IsWanted(ECarVfx effect, bool moving, bool nitro)
        {
            switch (effect)
            {
                case ECarVfx.IdleSmoke: return !moving;
                case ECarVfx.CruiseFlame: return moving && !nitro;
                case ECarVfx.NitroFlame: return nitro;
                default: return false;
            }
        }

        public CarVfxEntry GetEntry(int index)
        {
            return m_Entries[index];
        }

        public bool TryGet(ECarVfx effect, out CarVfxEntry entry)
        {
            entry = default;
            if (m_Entries == null || effect == ECarVfx.None)
                return false;

            for (int i = 0; i < m_Entries.Length; i++)
            {
                if (m_Entries[i].Effect != effect)
                    continue;

                entry = m_Entries[i];
                return true;
            }

            return false;
        }

        public bool IsNitro(int activeBuffKey)
        {
            return activeBuffKey >= NitroBaseLevel;
        }

        public float GetTargetScale(ECarVfx effect, int activeBuffKey)
        {
            if (!TryGet(effect, out CarVfxEntry entry))
                return 0f;

            if (effect != ECarVfx.NitroFlame)
                return entry.Scale;

            if (!IsNitro(activeBuffKey))
                return 0f;

            return Mathf.Max(0f, entry.Scale + m_NitroScalePerLevel * (activeBuffKey - NitroBaseLevel));
        }
    }
}
