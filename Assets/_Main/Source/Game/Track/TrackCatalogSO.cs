using UnityEngine;

namespace MatchRacers
{
    [CreateAssetMenu(fileName = "TrackCatalog", menuName = "MatchRacers/Track Catalog")]
    public sealed class TrackCatalogSO : ScriptableObject
    {
        [Header("Tracks")]
        [Tooltip("Oyuna girerken listelenecek pistler. Track Editor'deki Submit To Track List bu diziye ekler; sıra, panelde görünen sıradır.")]
        [SerializeField] private TrackLayoutSO[] m_Tracks = new TrackLayoutSO[0];

        public int Count => m_Tracks != null ? m_Tracks.Length : 0;

        public TrackLayoutSO GetTrack(int index)
        {
            return index >= 0 && index < Count ? m_Tracks[index] : null;
        }

        public int IndexOf(TrackLayoutSO layout)
        {
            if (layout == null)
                return -1;

            for (int i = 0; i < Count; i++)
            {
                if (m_Tracks[i] == layout)
                    return i;
            }

            return -1;
        }
    }
}
