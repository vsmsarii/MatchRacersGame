using System;
using UnityEngine;

namespace MatchRacers
{
    [Serializable]
    public struct TrackLandmark
    {
        [Tooltip("Objenin editördeki adı. Sahne hiyerarşisinde de bu ad görünür.")]
        [SerializeField] private string m_Label;
        [Tooltip("Yerleştirilen prefab.")]
        [SerializeField] private GameObject m_Prefab;
        [Tooltip("Objenin neye bağlı olduğu: dünya konumu ya da yola bağlı mesafe. Yola bağlıysa pist şekli değişince obje yolla birlikte taşınır.")]
        [SerializeField] private ELandmarkAnchor m_Anchor;
        [Tooltip("Dünya konumu. Yalnız dünya bağlı modda kullanılır.")]
        [SerializeField] private Vector3 m_Position;
        [Tooltip("Yola bağlı modda yol boyunca mesafe (metre).")]
        [SerializeField] private float m_Distance;
        [Tooltip("Yola bağlı modda yolun merkezine göre yanal kayma (metre).")]
        [SerializeField] private float m_Lateral;
        [Tooltip("Yola bağlı modda yerden yükseklik (metre).")]
        [SerializeField] private float m_Height;
        [Tooltip("Objenin dönüşü (derece).")]
        [SerializeField] private Vector3 m_Euler;
        [Tooltip("Objenin ölçeği.")]
        [SerializeField] private Vector3 m_Scale;

        public string Label => m_Label;
        public GameObject Prefab => m_Prefab;
        public ELandmarkAnchor Anchor => m_Anchor;
        public Vector3 Position => m_Position;
        public float Distance => m_Distance;
        public float Lateral => m_Lateral;
        public float Height => m_Height;
        public Vector3 Euler => m_Euler;
        public Vector3 Scale => m_Scale == Vector3.zero ? Vector3.one : m_Scale;

        public string DisplayName => !string.IsNullOrEmpty(m_Label)
            ? m_Label
            : (m_Prefab != null ? m_Prefab.name : "Landmark");

        public void Resolve(RacePath path, out Vector3 position, out Quaternion rotation)
        {
            if (m_Anchor == ELandmarkAnchor.World || path == null)
            {
                position = m_Position;
                rotation = Quaternion.Euler(m_Euler);
                return;
            }

            path.Evaluate(m_Distance, out Vector3 point, out Vector3 forward);
            position = point + RacePath.RightOf(forward) * m_Lateral + Vector3.up * m_Height;
            rotation = Quaternion.LookRotation(RacePath.Flatten(forward), Vector3.up) * Quaternion.Euler(m_Euler);
        }

        public static TrackLandmark AtWorld(string label, GameObject prefab, Vector3 position, Quaternion rotation,
            Vector3 scale)
        {
            return new TrackLandmark
            {
                m_Label = label,
                m_Prefab = prefab,
                m_Anchor = ELandmarkAnchor.World,
                m_Position = position,
                m_Euler = rotation.eulerAngles,
                m_Scale = scale
            };
        }

        public static TrackLandmark OnTrack(string label, GameObject prefab, RacePath path, Vector3 position,
            Quaternion rotation, Vector3 scale)
        {
            float distance = path.FindClosestDistance(position);
            path.Evaluate(distance, out Vector3 point, out Vector3 forward);
            Quaternion frame = Quaternion.LookRotation(RacePath.Flatten(forward), Vector3.up);

            return new TrackLandmark
            {
                m_Label = label,
                m_Prefab = prefab,
                m_Anchor = ELandmarkAnchor.Track,
                m_Position = position,
                m_Distance = distance,
                m_Lateral = Vector3.Dot(position - point, RacePath.RightOf(forward)),
                m_Height = position.y - point.y,
                m_Euler = (Quaternion.Inverse(frame) * rotation).eulerAngles,
                m_Scale = scale
            };
        }
    }
}
