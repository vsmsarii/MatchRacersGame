using System;
using CasualKit.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CasualKit.UI
{
    public sealed class MainMenuCanvasUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text m_TotalScore;
        [SerializeField] private TMP_Text m_Hearts;
        [SerializeField] private TMP_Text m_HeartTimer;
        [SerializeField] private Button m_PlayButton;
        [SerializeField] private TMP_Text m_PlayLabel;
        [SerializeField] private Button m_ShopButton;
        [SerializeField] private Button m_SettingsButton;

        private Action m_Play;
        private Action m_Shop;
        private Action m_Settings;
        private ISaveService m_Save;
        private int m_LevelNumber = 1;
        private bool m_CanPlay;
        private bool m_PlayRequested;

        private int m_ShownHearts = int.MinValue;
        private int m_ShownMaxHearts = int.MinValue;
        private long m_ShownHeartTimerSeconds = long.MinValue;
        private int m_ShownHeartTimerMode = int.MinValue;
        private bool m_ShownCanPlay;
        private int m_ShownPlayLevel = int.MinValue;

        private void Awake()
        {
            if (m_PlayButton != null)
                m_PlayButton.onClick.AddListener(OnPlayClicked);
            if (m_ShopButton != null)
                m_ShopButton.onClick.AddListener(OnShopClicked);
            if (m_SettingsButton != null)
                m_SettingsButton.onClick.AddListener(OnSettingsClicked);
        }

        private void Update()
        {
            if (m_Save == null)
                return;

            RefreshHeartsUi();
        }

        public void Bind(
            ISaveService save,
            int levelNumber,
            int totalScore,
            Action play,
            Action shop,
            Action settings)
        {
            m_Save = save;
            m_Play = play;
            m_Shop = shop;
            m_Settings = settings;
            m_LevelNumber = levelNumber;
            m_PlayRequested = false;
            InvalidateShown();

            if (m_TotalScore != null)
                m_TotalScore.text = "Score " + totalScore;

            if (m_ShopButton != null)
                m_ShopButton.gameObject.SetActive(shop != null);
            if (m_SettingsButton != null)
                m_SettingsButton.gameObject.SetActive(settings != null);

            RefreshHeartsUi();
        }

        private void InvalidateShown()
        {
            m_ShownHearts = int.MinValue;
            m_ShownMaxHearts = int.MinValue;
            m_ShownHeartTimerSeconds = long.MinValue;
            m_ShownHeartTimerMode = int.MinValue;
            m_ShownPlayLevel = int.MinValue;
        }

        private void RefreshHeartsUi()
        {
            if (m_Save == null)
                return;

            m_Save.RefreshHearts();
            int hearts = m_Save.Hearts;
            int maxHearts = m_Save.MaxHearts;
            m_CanPlay = hearts > 0;

            if (m_Hearts != null && (hearts != m_ShownHearts || maxHearts != m_ShownMaxHearts))
            {
                m_ShownHearts = hearts;
                m_ShownMaxHearts = maxHearts;
                m_Hearts.text = FormatHeartsLabel(hearts, maxHearts);
            }

            if (m_HeartTimer != null)
            {
                long seconds = m_Save.SecondsUntilNextHeart;
                int mode;
                if (hearts >= maxHearts || seconds <= 0)
                    mode = hearts <= 0 ? 1 : 0;
                else
                    mode = 2;

                if (mode != m_ShownHeartTimerMode || (mode == 2 && seconds != m_ShownHeartTimerSeconds))
                {
                    m_ShownHeartTimerMode = mode;
                    m_ShownHeartTimerSeconds = seconds;
                    if (mode == 1)
                        m_HeartTimer.text = "No hearts - visit Shop";
                    else if (mode == 2)
                        m_HeartTimer.text = "Next heart " + FormatTime(seconds);
                    else
                        m_HeartTimer.text = string.Empty;
                }
            }

            if (m_CanPlay != m_ShownCanPlay || m_LevelNumber != m_ShownPlayLevel)
            {
                m_ShownCanPlay = m_CanPlay;
                m_ShownPlayLevel = m_LevelNumber;
                if (m_PlayButton != null)
                    m_PlayButton.interactable = m_CanPlay && !m_PlayRequested;
                if (m_PlayLabel != null)
                    m_PlayLabel.text = m_CanPlay ? "LEVEL " + m_LevelNumber : "NO HEARTS";
            }
        }

        private static string FormatHeartsLabel(int hearts, int maxHearts)
        {
            if (maxHearts <= 0)
                return "Hearts " + hearts;

            if (hearts > maxHearts)
                return "Hearts " + maxHearts + "(+" + (hearts - maxHearts) + ")";

            return "Hearts " + hearts + "/" + maxHearts;
        }

        private static string FormatTime(long totalSeconds)
        {
            long minutes = totalSeconds / 60;
            long seconds = totalSeconds % 60;
            return minutes.ToString("00") + ":" + seconds.ToString("00");
        }

        private void OnPlayClicked()
        {
            if (!m_CanPlay || m_Play == null || m_PlayRequested)
                return;

            m_PlayRequested = true;
            if (m_PlayButton != null)
                m_PlayButton.interactable = false;

            m_Play.Invoke();
        }

        private void OnShopClicked()
        {
            if (m_Shop != null)
                m_Shop.Invoke();
        }

        private void OnSettingsClicked()
        {
            if (m_Settings != null)
                m_Settings.Invoke();
        }
    }
}
