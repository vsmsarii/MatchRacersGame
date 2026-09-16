using System;
using UnityEngine;

namespace CasualKit.Core
{
    [Serializable]
    public struct AudioCatalogEntry
    {
        [Tooltip("Bu satırın hangi sesi tanımladığı. Kod seslere bu enum ile erişir, katalogda olmayan ad sessizce atlanır.")]
        [SerializeField] private EAudioName m_Name;
        [Tooltip("Çalınacak ses dosyası. Boş bırakılırsa o ses hiç çalmaz; motor seslerinde kod bir alt kademeye düşer.")]
        [SerializeField] private AudioClip m_Clip;
        [Tooltip("Bu sesin katalog seviyesi. Çağıran koddan gelen seviye bununla çarpılır. 0 yazılırsa 1 kabul edilir.")]
        [SerializeField, Range(0f, 1f)] private float m_Volume;

        public EAudioName Name => m_Name;
        public AudioClip Clip => m_Clip;
        public float Volume => m_Volume <= 0f ? 1f : m_Volume;
    }

    [CreateAssetMenu(fileName = "AudioCatalog", menuName = "CasualKit/Audio Catalog")]
    public sealed class AudioCatalogSO : ScriptableObject
    {
        [Tooltip("Oyundaki seslerin listesi: arayüz, motor ve nitro. Yeni ses eklerken EAudioName değerini ve klibini buraya yaz.")]
        [SerializeField] private AudioCatalogEntry[] m_Entries;

        [Header("3D")]
        [Tooltip("Araç seslerinin ne kadar 3B çalacağı. 1 tam konumlu, yani soldaki araç soldan duyulur; 0 her sesi ortadan verir. Müzik ve arayüz sesleri bundan etkilenmez.")]
        [SerializeField, Range(0f, 1f)] private float m_SpatialBlend = 1f;
        [Tooltip("Sesin tam seviyede duyulduğu yarıçap (metre). Kamera araçlara yaklaşık 22 m uzakta olduğu için bunu küçültmek kendi aracının sesini de kısar.")]
        [SerializeField, Min(0.1f)] private float m_MinDistance = 25f;
        [Tooltip("Sesin tamamen söndüğü mesafe (metre). Bunun ötesindeki rakipler duyulmaz.")]
        [SerializeField, Min(0.2f)] private float m_MaxDistance = 260f;
        [Tooltip("3B seslerin stereo genişliği (derece). Küçük değer sert panlar, büyük değer sesi iki kanala yayar.")]
        [SerializeField, Range(0f, 360f)] private float m_Spread = 35f;
        [Tooltip("Yanından geçen aracın perde kayması. 0 kapalı, 1 gerçekçi. Yüksek değerler hızlı geçişte abartılı duyulur.")]
        [SerializeField, Range(0f, 5f)] private float m_DopplerLevel = 0.25f;

        [Header("Music")]
        [Tooltip("Bütün müziklerin ortak seviyesi. Tek tek klip seviyeleri bununla çarpılır.")]
        [SerializeField, Range(0f, 1f)] private float m_MusicVolume = 1f;
        [Tooltip("İki müzik arasındaki çapraz geçiş süresi. 0 yazılırsa geçiş anında olur.")]
        [SerializeField, Min(0f)] private float m_MusicFadeSeconds = 1f;

        public AudioCatalogEntry[] Entries => m_Entries;
        public float SpatialBlend => Mathf.Clamp01(m_SpatialBlend);
        public float MinDistance => Mathf.Max(0.1f, m_MinDistance);
        public float MaxDistance => Mathf.Max(MinDistance + 0.1f, m_MaxDistance);
        public float Spread => Mathf.Clamp(m_Spread, 0f, 360f);
        public float DopplerLevel => Mathf.Clamp(m_DopplerLevel, 0f, 5f);
        public float MusicVolume => Mathf.Clamp01(m_MusicVolume);
        public float MusicFadeSeconds => Mathf.Max(0f, m_MusicFadeSeconds);

        public bool TryGet(EAudioName name, out AudioClip clip)
        {
            if (m_Entries == null || name == EAudioName.None)
            {
                clip = null;
                return false;
            }

            for (int i = 0; i < m_Entries.Length; i++)
            {
                if (m_Entries[i].Name != name)
                    continue;

                clip = m_Entries[i].Clip;
                return clip != null;
            }

            clip = null;
            return false;
        }
    }
}
