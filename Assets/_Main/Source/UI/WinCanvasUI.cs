using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CasualKit.UI
{
    public sealed class WinCanvasUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text m_Title;
        [SerializeField] private TMP_Text m_Detail;
        [SerializeField] private Button m_RetryButton;
        [SerializeField] private Button m_NextButton;

        private Action m_Continue;
        private bool m_ContinueRequested;

        private void Awake()
        {
            if (m_RetryButton != null)
                m_RetryButton.gameObject.SetActive(false);

            if (m_NextButton != null)
                m_NextButton.onClick.AddListener(OnContinueClicked);
        }

        public void Show(int score, int bestCombo, Action continueAction)
        {
            m_Continue = continueAction;
            m_ContinueRequested = false;
            if (m_Title != null)
                m_Title.text = "COMPLETE";
            if (m_Detail != null)
                m_Detail.text = "Score " + score + "   Best combo x" + bestCombo;
            if (m_RetryButton != null)
                m_RetryButton.gameObject.SetActive(false);
            if (m_NextButton != null)
            {
                m_NextButton.gameObject.SetActive(true);
                TMP_Text label = m_NextButton.GetComponentInChildren<TMP_Text>();
                if (label != null)
                    label.text = "CONTINUE";
            }
        }

        private void OnContinueClicked()
        {
            if (m_Continue == null || m_ContinueRequested)
                return;

            m_ContinueRequested = true;
            if (m_NextButton != null)
                m_NextButton.interactable = false;

            m_Continue.Invoke();
        }
    }
}
