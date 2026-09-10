using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace CasualKit.UI
{
    [CreateAssetMenu(fileName = "UIPanelCatalog", menuName = "CasualKit/UI Panel Catalog")]
    public sealed class UIPanelCatalogSO : ScriptableObject
    {
        [Serializable]
        private struct UIPanelCatalogEntry
        {
            [SerializeField] private EUIPanel m_Panel;
            [SerializeField] private AssetReferenceGameObject m_Prefab;

            public EUIPanel Panel => m_Panel;
            public AssetReferenceGameObject Prefab => m_Prefab;
        }

        [SerializeField] private UIPanelCatalogEntry[] m_Entries;

        public bool TryGetPrefab(EUIPanel panel, out AssetReferenceGameObject prefab)
        {
            for (int index = 0; index < m_Entries.Length; index++)
            {
                if (m_Entries[index].Panel != panel)
                    continue;

                prefab = m_Entries[index].Prefab;
                return prefab != null && prefab.RuntimeKeyIsValid();
            }

            prefab = null;
            return false;
        }
    }
}
