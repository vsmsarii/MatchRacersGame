using System;
using UnityEngine;

namespace MatchRacers
{
    [Serializable]
    public struct RaceSoundEntry
    {
        [SerializeField] private ERaceSound m_Sound;
        [SerializeField] private AudioClip m_Clip;
        [SerializeField, Range(0f, 1f)] private float m_Volume;

        public ERaceSound Sound => m_Sound;
        public AudioClip Clip => m_Clip;
        public float Volume => m_Volume <= 0f ? 1f : m_Volume;
    }

    [CreateAssetMenu(fileName = "RaceAudioCatalog", menuName = "MatchRacers/Race Audio Catalog")]
    public sealed class RaceAudioCatalogSO : ScriptableObject
    {
        [Header("Sounds")]
        [SerializeField] private RaceSoundEntry[] m_Entries;

        [Header("Mixing")]
        [SerializeField, Range(0f, 1f)] private float m_MasterVolume = 0.8f;
        [SerializeField, Min(1)] private int m_OneShotVoices = 6;

        public float MasterVolume => Mathf.Clamp01(m_MasterVolume);
        public int OneShotVoices => m_OneShotVoices < 1 ? 1 : m_OneShotVoices;

        public bool TryGet(ERaceSound sound, out AudioClip clip, out float volume)
        {
            clip = null;
            volume = 1f;

            if (m_Entries == null || sound == ERaceSound.None)
                return false;

            for (int i = 0; i < m_Entries.Length; i++)
            {
                if (m_Entries[i].Sound != sound || m_Entries[i].Clip == null)
                    continue;

                clip = m_Entries[i].Clip;
                volume = m_Entries[i].Volume;
                return true;
            }

            return false;
        }
    }
}
