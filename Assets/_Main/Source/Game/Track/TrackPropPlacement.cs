using System;
using UnityEngine;

namespace MatchRacers
{
    [Serializable]
    public struct TrackPropPlacement
    {
        [Tooltip("Yerleştirilecek dekor türü. Katalogdaki prefab bu enum ile bulunur.")]
        [SerializeField] private ETrackProp m_Prop;
        [Tooltip("Dekorun yolun hangi tarafına konacağı: uzak, yakın, iki taraf ya da merkez. Kamera uzak tarafa baktığı için asıl görünen o taraftır.")]
        [SerializeField] private ETrackSide m_Side;
        [Tooltip("Dizinin yol boyunca başladığı mesafe (metre).")]
        [SerializeField] private float m_Distance;
        [Tooltip("Yol kenarından dışarı doğru kayma (metre). Bariyerlerde küçük, ağaçlarda büyük tutulur.")]
        [SerializeField] private float m_EdgeOffset;
        [Tooltip("Dekorun yerden yüksekliği (metre). Üst üste dizmek için kullanılır.")]
        [SerializeField] private float m_Height;
        [Tooltip("Dizideki kopya sayısı.")]
        [SerializeField] private int m_Count;
        [Tooltip("İki kopya arasındaki mesafe (metre).")]
        [SerializeField] private float m_Spacing;
        [Tooltip("Dekorun kendi ekseninde dönüşü (derece).")]
        [SerializeField] private float m_Yaw;
        [Tooltip("Dekorun ölçek çarpanı. Katalogdaki temel ölçekle çarpılır.")]
        [SerializeField] private float m_Scale;

        public ETrackProp Prop => m_Prop;
        public ETrackSide Side => m_Side;
        public float Distance => m_Distance;
        public float EdgeOffset => m_EdgeOffset;
        public float Height => m_Height;
        public int Count => m_Count < 1 ? 1 : m_Count;
        public float Spacing => m_Spacing < 0.1f ? 0.1f : m_Spacing;
        public float Yaw => m_Yaw;
        public float Scale => m_Scale <= 0f ? 1f : m_Scale;

        public float GetDistance(int index)
        {
            return m_Distance + Spacing * index;
        }

        public static TrackPropPlacement Create(ETrackProp prop, ETrackSide side, float distance, float edgeOffset,
            int count, float spacing)
        {
            return new TrackPropPlacement
            {
                m_Prop = prop,
                m_Side = side,
                m_Distance = distance,
                m_EdgeOffset = edgeOffset,
                m_Count = count,
                m_Spacing = spacing,
                m_Yaw = 0f,
                m_Scale = 1f
            };
        }
    }
}
