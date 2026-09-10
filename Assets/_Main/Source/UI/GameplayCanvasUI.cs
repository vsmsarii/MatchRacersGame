using CasualKit.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CasualKit.UI
{
    public sealed class GameplayCanvasUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text m_Level;
        [SerializeField] private TMP_Text m_Score;
        [SerializeField] private Button m_SettingsButton;

        private System.Action m_Settings;

        private void Awake()
        {
            if (m_SettingsButton != null)
                m_SettingsButton.onClick.AddListener(OnSettingsClicked);
        }

        private void OnEnable()
        {
            EB.Presentation.Add<RunHudChanged>(OnHudChanged);
        }

        private void OnDisable()
        {
            EB.Presentation.Remove<RunHudChanged>(OnHudChanged);
        }

        public void BindLevel(int levelNumber)
        {
            if (m_Level == null)
                return;

            m_Level.text = "Level " + levelNumber;
        }

        public void BindSettings(System.Action settings)
        {
            m_Settings = settings;
            if (m_SettingsButton == null)
                return;

            m_SettingsButton.gameObject.SetActive(settings != null);
        }

        private void OnSettingsClicked()
        {
            if (m_Settings != null)
                m_Settings.Invoke();
        }

        private void OnHudChanged(RunHudChanged evt)
        {
            if (m_Score != null)
                m_Score.text = "Score " + evt.Score;
        }
    }
}
