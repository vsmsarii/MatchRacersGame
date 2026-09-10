using System;
using Cysharp.Threading.Tasks;
using CasualKit.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CasualKit.UI
{
    public sealed class ShopCanvasUI : MonoBehaviour
    {
        [SerializeField] private Button m_CloseButton;
        [SerializeField] private Transform m_Content;
        [SerializeField] private ShopItemView m_ItemPrefab;

        private IIAPService m_Iap;
        private IAPCatalogSO m_Catalog;
        private Func<IAPCatalogEntry, bool> m_IsVisible;
        private Action m_Close;

        private void Awake()
        {
            if (m_CloseButton != null)
                m_CloseButton.onClick.AddListener(OnCloseClicked);
        }

        public void Bind(
            IAPCatalogSO catalog,
            IIAPService iap,
            Func<IAPCatalogEntry, bool> isVisible,
            Action close)
        {
            m_Catalog = catalog;
            m_Iap = iap;
            m_IsVisible = isVisible;
            m_Close = close;
            Rebuild();
        }

        private void Rebuild()
        {
            if (m_Content == null)
                return;

            if (m_ItemPrefab == null)
                return;

            for (int i = m_Content.childCount - 1; i >= 0; i--)
                Destroy(m_Content.GetChild(i).gameObject);

            if (m_Catalog == null || m_Catalog.Entries == null)
                return;

            for (int i = 0; i < m_Catalog.Entries.Length; i++)
            {
                IAPCatalogEntry entry = m_Catalog.Entries[i];
                if (string.IsNullOrEmpty(entry.Key) || !IsVisible(entry))
                    continue;

                ShopItemView item = Instantiate(m_ItemPrefab, m_Content);
                item.Bind(entry, PurchaseAsync);
            }
        }

        private bool IsVisible(IAPCatalogEntry entry)
        {
            return m_IsVisible == null || m_IsVisible.Invoke(entry);
        }

        private async UniTask<bool> PurchaseAsync(string key)
        {
            if (m_Iap == null)
                return false;

            return await m_Iap.PurchaseAsync(key);
        }

        private void OnCloseClicked()
        {
            if (m_Close != null)
                m_Close.Invoke();
            else
                UIPanels.Close(EUIPanel.Shop);
        }
    }
}
