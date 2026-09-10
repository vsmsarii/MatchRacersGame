using System;
using UnityEngine;

namespace CasualKit.Core
{
    [Serializable]
    public struct AudioCatalogEntry
    {
        [SerializeField] private EAudioName m_Name;
        [SerializeField] private AudioClip m_Clip;

        public EAudioName Name => m_Name;
        public AudioClip Clip => m_Clip;
    }

    [CreateAssetMenu(fileName = "AudioCatalog", menuName = "CasualKit/Audio Catalog")]
    public sealed class AudioCatalogSO : ScriptableObject
    {
        [SerializeField] private AudioCatalogEntry[] m_Entries;

        public AudioCatalogEntry[] Entries => m_Entries;

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
