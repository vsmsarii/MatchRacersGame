using System;
using CasualKit.Core;
using UnityEngine;

namespace MatchRacers
{
    [Serializable]
    public struct EngineAudioLayer
    {
        [Tooltip("Bu satırın tanımladığı motor kademesi. 0 duruş, 1 taban hız, 2-5 aktif nitro seviyeleri.")]
        [SerializeField, Range(0, BuffTableSO.MaxKey)] private int m_Tier;
        [Tooltip("Bu kademede çalacak ses. Katalogda karşılığı yoksa kod taban hız, o da yoksa rölanti klibine düşer.")]
        [SerializeField] private EAudioName m_Sound;
        [Tooltip("Kademenin perde değeri. Kademeler arasında yumuşak kaydırılır; 2-5'in işitsel olarak ayrışmasını sağlayan asıl değerdir.")]
        [SerializeField, Range(0.25f, 3f)] private float m_Pitch;
        [Tooltip("Kademenin ses seviyesi. Üst kademeler bilerek daha yüksektir.")]
        [SerializeField, Range(0f, 1f)] private float m_Volume;

        public EngineAudioLayer(int tier, EAudioName sound, float pitch, float volume)
        {
            m_Tier = tier;
            m_Sound = sound;
            m_Pitch = pitch;
            m_Volume = volume;
        }

        public int Tier => Mathf.Clamp(m_Tier, 0, BuffTableSO.MaxKey);
        public EAudioName Sound => m_Sound;
        public float Pitch => Mathf.Clamp(m_Pitch, 0.25f, 3f);
        public float Volume => Mathf.Clamp01(m_Volume);
    }

    [CreateAssetMenu(fileName = "EngineAudioTable", menuName = "MatchRacers/Engine Audio Table")]
    public sealed class EngineAudioTableSO : ScriptableObject
    {
        [Header("Tiers")]
        [Tooltip("Motor kademelerinin tablosu. Her kademe için ses, perde ve seviye burada tanımlanır.")]
        [SerializeField] private EngineAudioLayer[] m_Layers =
        {
            new EngineAudioLayer(0, EAudioName.EngineIdle, 0.75f, 0.35f),
            new EngineAudioLayer(1, EAudioName.EngineCruise, 1.00f, 0.50f),
            new EngineAudioLayer(2, EAudioName.EngineNitro2, 1.12f, 0.60f),
            new EngineAudioLayer(3, EAudioName.EngineNitro3, 1.25f, 0.70f),
            new EngineAudioLayer(4, EAudioName.EngineNitro4, 1.40f, 0.80f),
            new EngineAudioLayer(5, EAudioName.EngineNitro5, 1.55f, 0.90f),
        };

        [Header("Mix")]
        [Tooltip("Rakip araçların motor ve nitro sesinin ölçeği. Sekiz araç aynı anda çalarken karmaşayı önler; kendi aracın bundan etkilenmez.")]
        [SerializeField, Range(0f, 1f)] private float m_RivalVolume = 0.7f;

        [Header("Blend")]
        [Tooltip("Kademe değişiminde iki ses döngüsü arasındaki çapraz geçiş süresi. Kısa değer sert, uzun değer yayvan geçiş verir.")]
        [SerializeField, Min(0.01f)] private float m_CrossfadeSeconds = 0.25f;
        [Tooltip("Perdenin hedef değere saniyede ne kadar yaklaşacağı. Aynı klip iki kademede kullanılıyorsa hız hissini bu kaydırma üretir.")]
        [SerializeField, Min(0.01f)] private float m_PitchRate = 3f;

        public float RivalVolume => Mathf.Clamp01(m_RivalVolume);
        public float CrossfadeSeconds => m_CrossfadeSeconds < 0.01f ? 0.01f : m_CrossfadeSeconds;
        public float PitchRate => m_PitchRate < 0.01f ? 0.01f : m_PitchRate;

        public bool TryGetLayer(int tier, out EngineAudioLayer layer)
        {
            layer = default;
            if (m_Layers == null)
                return false;

            for (int i = 0; i < m_Layers.Length; i++)
            {
                if (m_Layers[i].Tier != tier)
                    continue;

                layer = m_Layers[i];
                return true;
            }

            return false;
        }
    }
}
