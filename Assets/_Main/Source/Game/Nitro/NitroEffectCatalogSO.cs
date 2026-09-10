using System;
using UnityEngine;

namespace MatchRacers
{
    [Serializable]
    public struct NitroEffectEntry
    {
        [SerializeField] private ENitroEffect m_Effect;
        [SerializeField] private GameObject m_Prefab;
        [SerializeField] private Vector3 m_LocalPosition;
        [SerializeField] private Vector3 m_LocalEuler;
        [SerializeField] private float m_LocalScale;

        public ENitroEffect Effect => m_Effect;
        public GameObject Prefab => m_Prefab;
        public Vector3 LocalPosition => m_LocalPosition;
        public Vector3 LocalEuler => m_LocalEuler;
        public float LocalScale => m_LocalScale <= 0f ? 1f : m_LocalScale;
    }

    [CreateAssetMenu(fileName = "NitroEffectCatalog", menuName = "MatchRacers/Nitro Effect Catalog")]
    public sealed class NitroEffectCatalogSO : ScriptableObject
    {
        [Header("Attachment")]
        [SerializeField] private string m_ExhaustNodeFilter = "_Pipe_";

        [Header("Effects")]
        [SerializeField] private NitroEffectEntry[] m_Entries;

        [Header("Budget")]
        [SerializeField, Min(1)] private int m_MaxParticlesPerSystem = 48;

        public string ExhaustNodeFilter => m_ExhaustNodeFilter;
        public int MaxParticlesPerSystem => m_MaxParticlesPerSystem < 1 ? 1 : m_MaxParticlesPerSystem;

        public bool TryGet(ENitroEffect effect, out NitroEffectEntry entry)
        {
            entry = default;
            if (m_Entries == null || effect == ENitroEffect.None)
                return false;

            for (int i = 0; i < m_Entries.Length; i++)
            {
                if (m_Entries[i].Effect != effect || m_Entries[i].Prefab == null)
                    continue;

                entry = m_Entries[i];
                return true;
            }

            return false;
        }
    }
}
