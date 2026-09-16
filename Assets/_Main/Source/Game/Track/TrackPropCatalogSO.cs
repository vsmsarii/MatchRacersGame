using System;
using UnityEngine;

namespace MatchRacers
{
    [Serializable]
    public struct TrackPropEntry
    {
        [Tooltip("Bu satırın tanımladığı dekor türü.")]
        [SerializeField] private ETrackProp m_Prop;
        [Tooltip("Dekorun prefabı. Oyunda bu prefablar GameObject olarak yaratılmaz, tek çizimde toplu olarak çizilir.")]
        [SerializeField] private GameObject m_Prefab;
        [Tooltip("Prefabın kendi duruş düzeltmesi (derece). Modelin ileri yönü yanlışsa burada düzeltilir.")]
        [SerializeField] private Vector3 m_LocalEuler;
        [Tooltip("Prefabın temel ölçeği. Yerleştirmedeki ölçek bununla çarpılır.")]
        [SerializeField] private float m_LocalScale;
        [Tooltip("Prefabın yerden yükseklik düzeltmesi (metre). Modelin pivotu tabanında değilse gerekir.")]
        [SerializeField] private float m_HeightOffset;
        [Tooltip("Açıkken uzak tarafa konan kopyalar 180 derece çevrilir; bariyer gibi tek yöne bakan modeller yola doğru bakar.")]
        [SerializeField] private bool m_MirrorOnFarSide;

        public ETrackProp Prop => m_Prop;
        public GameObject Prefab => m_Prefab;
        public Vector3 LocalEuler => m_LocalEuler;
        public float LocalScale => m_LocalScale <= 0f ? 1f : m_LocalScale;
        public float HeightOffset => m_HeightOffset;
        public bool MirrorOnFarSide => m_MirrorOnFarSide;
    }

    [CreateAssetMenu(fileName = "TrackPropCatalog", menuName = "MatchRacers/Track Prop Catalog")]
    public sealed class TrackPropCatalogSO : ScriptableObject
    {
        [Header("Props")]
        [Tooltip("Pist editöründe kullanılabilecek bütün dekorların listesi.")]
        [SerializeField] private TrackPropEntry[] m_Entries;

        public bool TryGet(ETrackProp prop, out TrackPropEntry entry)
        {
            entry = default;
            if (m_Entries == null || prop == ETrackProp.None)
                return false;

            for (int i = 0; i < m_Entries.Length; i++)
            {
                if (m_Entries[i].Prop != prop || m_Entries[i].Prefab == null)
                    continue;

                entry = m_Entries[i];
                return true;
            }

            return false;
        }
    }
}
