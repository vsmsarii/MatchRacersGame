using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CasualKit.UI
{
    public sealed class LoseCanvasUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text m_Title;
        [SerializeField] private TMP_Text m_Detail;
        [SerializeField] private Button m_RetryButton;
        [SerializeField] private Button m_MenuButton;

        private Action m_Retry;
        private Action m_Menu;

        private void Awake()
        {
            if (m_RetryButton != null)
                m_RetryButton.onClick.AddListener(OnRetryClicked);
            if (m_MenuButton != null)
                m_MenuButton.onClick.AddListener(OnMenuClicked);
        }

        public void Show(int score, int bestCombo, Action retry, Action menu)
        {
            m_Retry = retry;
            m_Menu = menu;

            if (m_Title != null)
                m_Title.text = "FAIL";
            if (m_Detail != null)
                m_Detail.text = "Score " + score + "   Best combo x" + bestCombo;

            if (m_RetryButton != null)
            {
                m_RetryButton.interactable = retry != null;
                TMP_Text retryLabel = m_RetryButton.GetComponentInChildren<TMP_Text>();
                if (retryLabel != null)
                    retryLabel.text = "Retry";
            }

            if (m_MenuButton != null)
                m_MenuButton.gameObject.SetActive(true);
        }

        private void OnRetryClicked()
        {
            if (m_Retry == null)
                return;

            Action retry = m_Retry;
            m_Retry = null;
            retry.Invoke();
        }

        private void OnMenuClicked()
        {
            if (m_Menu == null)
                return;

            Action menu = m_Menu;
            m_Menu = null;
            menu.Invoke();
        }
    }
}
