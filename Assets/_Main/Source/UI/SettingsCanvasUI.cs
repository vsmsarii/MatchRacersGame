using System;
using CasualKit.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CasualKit.UI
{
    public sealed class SettingsCanvasUI : MonoBehaviour
    {
        [SerializeField] private Button m_CloseButton;
        [SerializeField] private Button m_MusicButton;
        [SerializeField] private Button m_SoundButton;
        [SerializeField] private Button m_BackToMenuButton;
        [SerializeField] private TMP_Text m_MusicLabel;
        [SerializeField] private TMP_Text m_SoundLabel;

        private IAudioService m_Audio;
        private Action m_Close;
        private Action m_BackToMenu;

        private void Awake()
        {
            if (m_CloseButton != null)
                m_CloseButton.onClick.AddListener(OnCloseClicked);
            if (m_MusicButton != null)
                m_MusicButton.onClick.AddListener(OnMusicClicked);
            if (m_SoundButton != null)
                m_SoundButton.onClick.AddListener(OnSoundClicked);
            if (m_BackToMenuButton != null)
                m_BackToMenuButton.onClick.AddListener(OnBackToMenuClicked);
        }

        public void Bind(IAudioService audio, Action close, Action backToMenu = null)
        {
            m_Audio = audio;
            m_Close = close;
            m_BackToMenu = backToMenu;

            if (m_BackToMenuButton != null)
                m_BackToMenuButton.gameObject.SetActive(backToMenu != null);

            RefreshLabels();
        }

        private void RefreshLabels()
        {
            if (m_Audio == null)
                return;

            if (m_MusicLabel != null)
                m_MusicLabel.text = m_Audio.MusicMuted ? "MUSIC OFF" : "MUSIC ON";
            if (m_SoundLabel != null)
                m_SoundLabel.text = m_Audio.SoundMuted ? "SOUND OFF" : "SOUND ON";
        }

        private void OnMusicClicked()
        {
            if (m_Audio == null)
                return;

            m_Audio.SetMusicMuted(!m_Audio.MusicMuted);
            m_Audio.Play(EAudioName.SfxUiClick);
            RefreshLabels();
        }

        private void OnSoundClicked()
        {
            if (m_Audio == null)
                return;

            m_Audio.SetSoundMuted(!m_Audio.SoundMuted);
            if (!m_Audio.SoundMuted)
                m_Audio.Play(EAudioName.SfxUiClick);
            RefreshLabels();
        }

        private void OnCloseClicked()
        {
            if (m_Audio != null)
                m_Audio.Play(EAudioName.SfxUiClick);
            if (m_Close != null)
                m_Close.Invoke();
            else
                UIPanels.Close(EUIPanel.Settings);
        }

        private void OnBackToMenuClicked()
        {
            if (m_Audio != null)
                m_Audio.Play(EAudioName.SfxUiClick);
            if (m_BackToMenu != null)
                m_BackToMenu.Invoke();
        }
    }
}
