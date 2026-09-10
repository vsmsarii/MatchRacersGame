using System;
using UnityEngine;

namespace CasualKit.Core
{
    [Serializable]
    public struct IAPCatalogEntry
    {
        [SerializeField] private string m_Key;
        [SerializeField] private string m_DisplayName;
        [SerializeField] private string m_Description;
        [SerializeField] private Sprite m_Icon;
        [SerializeField] private string m_PriceLabel;
        [SerializeField] private string m_GooglePlayId;
        [SerializeField] private string m_AppStoreId;
        [SerializeField] private EIAPRewardType m_RewardType;
        [SerializeField] private int m_RewardAmount;

        public string Key => m_Key;
        public string DisplayName => m_DisplayName;
        public string Description => m_Description;
        public Sprite Icon => m_Icon;
        public string PriceLabel => m_PriceLabel;
        public string GooglePlayId => m_GooglePlayId;
        public string AppStoreId => m_AppStoreId;
        public EIAPRewardType RewardType => m_RewardType;
        public int RewardAmount => m_RewardAmount < 1 ? 1 : m_RewardAmount;
    }

    [CreateAssetMenu(fileName = "IAPCatalog", menuName = "CasualKit/IAP Catalog")]
    public sealed class IAPCatalogSO : ScriptableObject
    {
        [SerializeField] private IAPCatalogEntry[] m_Entries;

        public IAPCatalogEntry[] Entries => m_Entries;

        public bool TryGet(string key, out IAPCatalogEntry entry)
        {
            if (m_Entries == null || string.IsNullOrEmpty(key))
            {
                entry = default;
                return false;
            }

            for (int i = 0; i < m_Entries.Length; i++)
            {
                if (m_Entries[i].Key != key)
                    continue;

                entry = m_Entries[i];
                return true;
            }

            entry = default;
            return false;
        }
    }
}
