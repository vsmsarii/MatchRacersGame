using System;
using Cysharp.Threading.Tasks;
using CasualKit.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CasualKit.UI
{
    public sealed class ShopItemView : MonoBehaviour
    {
        [SerializeField] private Image m_Icon;
        [SerializeField] private TMP_Text m_Title;
        [SerializeField] private TMP_Text m_Description;
        [SerializeField] private TMP_Text m_Price;
        [SerializeField] private Button m_BuyButton;

        private string m_ProductKey;
        private Func<string, UniTask<bool>> m_Purchase;

        public void Bind(IAPCatalogEntry entry, Func<string, UniTask<bool>> purchase)
        {
            m_ProductKey = entry.Key;
            m_Purchase = purchase;

            if (m_Icon != null)
            {
                m_Icon.sprite = entry.Icon;
                m_Icon.enabled = entry.Icon != null;
            }

            if (m_Title != null)
                m_Title.text = entry.DisplayName;

            if (m_Description != null)
                m_Description.text = entry.Description;

            if (m_Price != null)
                m_Price.text = entry.PriceLabel;

            if (m_BuyButton != null)
            {
                m_BuyButton.onClick.RemoveListener(OnBuyClicked);
                m_BuyButton.onClick.AddListener(OnBuyClicked);
            }
        }

        private void OnBuyClicked()
        {
            BuyAsync().Forget();
        }

        private async UniTaskVoid BuyAsync()
        {
            if (m_Purchase == null || string.IsNullOrEmpty(m_ProductKey))
                return;

            if (m_BuyButton != null)
                m_BuyButton.interactable = false;

            await m_Purchase.Invoke(m_ProductKey);

            if (m_BuyButton != null)
                m_BuyButton.interactable = true;
        }
    }
}
