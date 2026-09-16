using UnityEngine;
using UnityEngine.UI;

namespace CasualKit.UI
{
    public sealed class GameplayCanvasUI : MonoBehaviour
    {
        [SerializeField] private Button m_SettingsButton;

        private System.Action m_Settings;

        private void Awake()
        {
            if (m_SettingsButton != null)
                m_SettingsButton.onClick.AddListener(OnSettingsClicked);
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
    }
}
