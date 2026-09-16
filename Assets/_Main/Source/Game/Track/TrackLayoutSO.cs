using System.Collections.Generic;
using UnityEngine;

namespace MatchRacers
{
    [CreateAssetMenu(fileName = "TrackLayout", menuName = "MatchRacers/Track Layout")]
    public sealed class TrackLayoutSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Pistin oyundaki adı. Pist seçim panelinde bu ad görünür.")]
        [SerializeField] private string m_DisplayName = "Track";

        [Header("Race")]
        [Tooltip("Bu pistin yarış uzunluğu (metre). 0 bırakılırsa RaceConfig'teki varsayılan uzunluk kullanılır.")]
        [SerializeField, Min(0f)] private float m_RaceLengthMeters;

        [Header("Look")]
        [Tooltip("Pistin ortam ayarı: ışık, sis, gökyüzü, post-process, arka plan silüeti ve zemin. Boş bırakılırsa RaceConfig'teki varsayılan ortam kullanılır.")]
        [SerializeField] private RaceEnvironmentSO m_Environment;

        [Header("Music")]
        [Tooltip("Mod seçimi ekranında çalacak müzik. Boş bırakılırsa bu pistte lobi müziği çalmaz.")]
        [SerializeField] private AudioClip m_LobbyMusic;
        [Tooltip("Mod seçimi müziğinin seviyesi.")]
        [SerializeField, Range(0f, 1f)] private float m_LobbyMusicVolume = 0.8f;
        [Tooltip("Yarış başlayınca geçilecek müzik. Boş bırakılırsa bu pistte yarış müziği çalmaz.")]
        [SerializeField] private AudioClip m_RaceMusic;
        [Tooltip("Yarış müziğinin seviyesi.")]
        [SerializeField, Range(0f, 1f)] private float m_RaceMusicVolume = 0.8f;

        [Header("Origin")]
        [Tooltip("Pistin başlangıç noktasının dünya konumu.")]
        [SerializeField] private Vector3 m_StartPosition;
        [Tooltip("Başlangıçtaki yön (derece). 90 pozitif X yönüne bakar.")]
        [SerializeField] private float m_StartHeading = 90f;
        [Tooltip("Rotanın kaç metrede bir örnekleneceği. Küçük değer virajları yumuşatır, büyük değer bellek ve kurulum süresini düşürür.")]
        [SerializeField, Range(0.25f, 5f)] private float m_SampleSpacing = 1f;
        [Tooltip("Açıkken pist kapalı bir tur olur ve mesafe başa sarar. Kapalıyken pist bir yerde başlayıp biter.")]
        [SerializeField] private bool m_ClosedLoop;

        [Header("Segments")]
        [Tooltip("Pisti oluşturan düz ve viraj parçaları. Track Editor bu diziyi tutamaçlarla düzenler.")]
        [SerializeField] private TrackSegment[] m_Segments = { TrackSegment.Straight(1100f) };

        [Header("Props")]
        [Tooltip("Bu pistte kullanılabilecek dekor kataloğu.")]
        [SerializeField] private TrackPropCatalogSO m_PropCatalog;
        [Tooltip("Yol boyunca tekrar eden dekor dizileri. Straight pistinde 4400'ün üzerinde kopya vardır; hepsi tek çizimde toplanır.")]
        [SerializeField] private TrackPropPlacement[] m_Props = new TrackPropPlacement[0];

        [Header("Landmarks")]
        [Tooltip("Tek tek yerleştirilmiş objeler. Dekor dizilerinden farklı olarak her biri kendi konumuna sahiptir.")]
        [SerializeField] private TrackLandmark[] m_Landmarks = new TrackLandmark[0];

        [Header("Surface")]
        [Tooltip("Asfalt malzemesi. Boş bırakılırsa koyu gri bir malzeme üretilir.")]
        [SerializeField] private Material m_RoadMaterial;
        [Tooltip("Şerit çizgisi malzemesi.")]
        [SerializeField] private Material m_StripeMaterial;
        [Tooltip("Açıkken yol kenarına her 100 metrede bir işaret konur. Hız hissini ve kalan mesafeyi okumayı kolaylaştırır.")]
        [SerializeField] private bool m_DistanceMarkers = true;
        [Tooltip("Açıkken bitiş çizgisine damalı bir kapı kurulur.")]
        [SerializeField] private bool m_FinishGate = true;

        [Header("Ground")]
        [Tooltip("Açıkken pistin etrafına zemin düzlemi serilir. Kapatılırsa pistin altı boş görünür.")]
        [SerializeField] private bool m_Ground = true;
        [Tooltip("Zemin malzemesi. Ortamda zemin deseni açıksa desen bu malzemenin bir kopyasına işlenir; asıl malzeme değişmez.")]
        [SerializeField] private Material m_GroundMaterial;
        [Tooltip("Zeminin pist sınırlarından ne kadar taşacağı (metre). Küçük değer kamerada zeminin bittiği yeri gösterir.")]
        [SerializeField, Min(0f)] private float m_GroundMargin = 120f;
        [Tooltip("Zeminin yola göre yükseklik farkı (metre). Küçük bir negatif değer, zeminle yolun z-fighting yapmasını engeller.")]
        [SerializeField] private float m_GroundHeight = -0.05f;

        public string DisplayName => string.IsNullOrWhiteSpace(m_DisplayName) ? name : m_DisplayName;
        public float RaceLengthMeters => m_RaceLengthMeters < 0f ? 0f : m_RaceLengthMeters;
        public RaceEnvironmentSO Environment => m_Environment;

        public bool TryGetMusic(bool racing, out AudioClip clip, out float volume)
        {
            clip = racing ? m_RaceMusic : m_LobbyMusic;
            volume = Mathf.Clamp01(racing ? m_RaceMusicVolume : m_LobbyMusicVolume);
            return clip != null;
        }

        public Vector3 StartPosition => m_StartPosition;
        public float StartHeading => m_StartHeading;
        public float SampleSpacing => Mathf.Clamp(m_SampleSpacing, 0.25f, 5f);
        public bool ClosedLoop => m_ClosedLoop;

        public float GetLoopGap()
        {
            GetSegmentStart(SegmentCount, out Vector3 end, out _);
            return Vector3.Distance(end, m_StartPosition);
        }

        public float GetLoopHeadingError()
        {
            GetSegmentStart(SegmentCount, out _, out float heading);
            return Mathf.Abs(Mathf.DeltaAngle(heading, m_StartHeading));
        }
        public int SegmentCount => m_Segments != null ? m_Segments.Length : 0;
        public TrackPropCatalogSO PropCatalog => m_PropCatalog;
        public int PropCount => m_Props != null ? m_Props.Length : 0;
        public int LandmarkCount => m_Landmarks != null ? m_Landmarks.Length : 0;
        public Material RoadMaterial => m_RoadMaterial;
        public Material StripeMaterial => m_StripeMaterial;
        public bool DistanceMarkers => m_DistanceMarkers;
        public bool FinishGate => m_FinishGate;
        public bool Ground => m_Ground;
        public Material GroundMaterial => m_GroundMaterial;
        public float GroundMargin => m_GroundMargin < 0f ? 0f : m_GroundMargin;
        public float GroundHeight => m_GroundHeight;

        public TrackSegment GetSegment(int index)
        {
            return m_Segments[index];
        }

        public TrackPropPlacement GetProp(int index)
        {
            return m_Props[index];
        }

        public TrackLandmark GetLandmark(int index)
        {
            return m_Landmarks[index];
        }

        public float TotalLength
        {
            get
            {
                float total = 0f;
                for (int i = 0; i < SegmentCount; i++)
                    total += m_Segments[i].PathLength;

                return total;
            }
        }

        public float GetSegmentStartDistance(int index)
        {
            float distance = 0f;
            for (int i = 0; i < index && i < SegmentCount; i++)
                distance += m_Segments[i].PathLength;

            return distance;
        }

        public void GetSegmentStart(int index, out Vector3 position, out float heading)
        {
            position = m_StartPosition;
            heading = m_StartHeading;

            for (int i = 0; i < index && i < SegmentCount; i++)
                Advance(m_Segments[i], ref position, ref heading);
        }

        public void BuildPolyline(List<Vector3> points)
        {
            points.Clear();

            Vector3 position = m_StartPosition;
            float heading = m_StartHeading;
            float spacing = SampleSpacing;
            points.Add(position);

            for (int i = 0; i < SegmentCount; i++)
            {
                TrackSegment segment = m_Segments[i];
                int steps = Mathf.Max(1, Mathf.CeilToInt(segment.ArcLength / spacing));
                float rise = segment.Rise / steps;

                if (!segment.IsTurn)
                {
                    Vector3 step = HeadingToDirection(heading) * (segment.Length / steps);
                    for (int s = 0; s < steps; s++)
                    {
                        position += step;
                        position.y += rise;
                        points.Add(position);
                    }

                    continue;
                }

                float delta = segment.Angle / steps;
                float chord = 2f * segment.Radius * Mathf.Sin(Mathf.Abs(delta) * 0.5f * Mathf.Deg2Rad);

                for (int s = 0; s < steps; s++)
                {
                    position += HeadingToDirection(heading + delta * 0.5f) * chord;
                    position.y += rise;
                    heading += delta;
                    points.Add(position);
                }
            }

            if (points.Count < 2)
                points.Add(position + HeadingToDirection(heading));
        }

        public static void Advance(TrackSegment segment, ref Vector3 position, ref float heading)
        {
            if (!segment.IsTurn)
            {
                position += HeadingToDirection(heading) * segment.Length;
                position.y += segment.Rise;
                return;
            }

            float sign = TurnSign(segment);
            Vector3 center = GetTurnCenter(position, heading, segment);
            float startY = position.y;

            heading += segment.Angle;
            position = center + HeadingToDirection(heading - 90f * sign) * segment.Radius;
            position.y = startY + segment.Rise;
        }

        public static Vector3 GetTurnCenter(Vector3 start, float heading, TrackSegment segment)
        {
            return start + HeadingToDirection(heading + 90f * TurnSign(segment)) * segment.Radius;
        }

        public static Vector3 GetSegmentMidpoint(TrackSegment segment, Vector3 start, float heading)
        {
            Vector3 lift = Vector3.up * (segment.Rise * 0.5f);

            if (!segment.IsTurn)
                return start + HeadingToDirection(heading) * (segment.Length * 0.5f) + lift;

            float sign = TurnSign(segment);
            Vector3 center = GetTurnCenter(start, heading, segment);
            return center + HeadingToDirection(heading - 90f * sign + segment.Angle * 0.5f) * segment.Radius + lift;
        }

        public static float TurnSign(TrackSegment segment)
        {
            return segment.Angle < 0f ? -1f : 1f;
        }

        public static Vector3 HeadingToDirection(float heading)
        {
            float radians = heading * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians));
        }
    }
}
