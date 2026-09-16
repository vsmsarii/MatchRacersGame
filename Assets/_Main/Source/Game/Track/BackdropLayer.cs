using System;
using UnityEngine;

namespace MatchRacers
{
    [Serializable]
    public sealed class BackdropLayer
    {
        [Tooltip("Bu kademenin yol kenarından uzaklığı (metre). Yakın kademe ekranda daha çok yer kaplar ve hızlı akar.")]
        [SerializeField, Min(0f)] private float m_Offset = 40f;
        [Tooltip("Kademenin derinliği (metre). Binaların kalınlığını, tepelerin genişliğini belirler.")]
        [SerializeField, Min(1f)] private float m_Depth = 20f;
        [Tooltip("En alçak elemanın yüksekliği (metre). Kamera 10 m yükseklikte olduğu için bunun altındaki elemanların üstünden arka kademeler görünür.")]
        [SerializeField, Min(0f)] private float m_MinHeight = 8f;
        [Tooltip("En yüksek elemanın yüksekliği (metre).")]
        [SerializeField, Min(0f)] private float m_MaxHeight = 30f;
        [Tooltip("Şehir stilinde bina genişliği, tepe stilinde tepe dalga boyu (metre).")]
        [SerializeField, Min(1f)] private float m_Spacing = 18f;
        [Tooltip("Kademenin taban rengi. Uzak kademeler açık tutulup sisle harmanlanınca derinlik hissi oluşur.")]
        [SerializeField] private Color m_Color = new Color(0.08f, 0.08f, 0.12f);
        [Tooltip("Şehir stilinde yanan pencere oranı. 0 pencereyi tamamen kapatır; tepe stilinde kullanılmaz.")]
        [SerializeField, Range(0f, 1f)] private float m_Windows = 0.3f;

        public BackdropLayer()
        {
        }

        public BackdropLayer(float offset, float depth, float minHeight, float maxHeight, float spacing, Color color, float windows)
        {
            m_Offset = offset;
            m_Depth = depth;
            m_MinHeight = minHeight;
            m_MaxHeight = maxHeight;
            m_Spacing = spacing;
            m_Color = color;
            m_Windows = windows;
        }

        public float Offset => Mathf.Max(0f, m_Offset);
        public float Depth => Mathf.Max(1f, m_Depth);
        public float MinHeight => Mathf.Max(0f, Mathf.Min(m_MinHeight, m_MaxHeight));
        public float MaxHeight => Mathf.Max(0f, Mathf.Max(m_MinHeight, m_MaxHeight));
        public float Spacing => Mathf.Max(1f, m_Spacing);
        public Color Color => m_Color;
        public float Windows => Mathf.Clamp01(m_Windows);
    }
}
