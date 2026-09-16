using System;
using UnityEngine;

namespace MatchRacers
{
    [Serializable]
    public struct TrackSegment
    {
        public const float MinLength = 0.1f;
        public const float MinRadius = 5f;

        [Tooltip("Parçanın türü: düz ya da viraj.")]
        [SerializeField] private ETrackSegmentType m_Type;
        [Tooltip("Düz parçanın uzunluğu (metre). Virajda kullanılmaz, yay uzunluğu açı ve yarıçaptan hesaplanır.")]
        [SerializeField] private float m_Length;
        [Tooltip("Virajın dönüş açısı (derece). Pozitif sağa, negatif sola döner.")]
        [SerializeField] private float m_Angle;
        [Tooltip("Virajın yarıçapı (metre). Küçük yarıçap keskin viraj demektir; aracın yandan görünümünde okunaklılığı düşürür.")]
        [SerializeField] private float m_Radius;
        [Tooltip("Parça boyunca kazanılan yükseklik (metre). Düz pistlerde 0 bırakılır.")]
        [SerializeField] private float m_Rise;

        public ETrackSegmentType Type => m_Type;
        public float Length => m_Length < MinLength ? MinLength : m_Length;
        public float Angle => Mathf.Clamp(m_Angle, -360f, 360f);
        public float Radius => m_Radius < MinRadius ? MinRadius : m_Radius;
        public float Rise => m_Rise;
        public bool IsTurn => m_Type == ETrackSegmentType.Turn;

        public float ArcLength => IsTurn ? Mathf.Abs(Angle) * Mathf.Deg2Rad * Radius : Length;
        public float PathLength => Mathf.Sqrt(ArcLength * ArcLength + m_Rise * m_Rise);

        public static TrackSegment Straight(float length, float rise = 0f)
        {
            return new TrackSegment
            {
                m_Type = ETrackSegmentType.Straight,
                m_Length = length,
                m_Angle = 90f,
                m_Radius = 40f,
                m_Rise = rise
            };
        }

        public static TrackSegment Turn(float angle, float radius, float rise = 0f)
        {
            return new TrackSegment
            {
                m_Type = ETrackSegmentType.Turn,
                m_Length = 100f,
                m_Angle = angle,
                m_Radius = radius,
                m_Rise = rise
            };
        }
    }
}
