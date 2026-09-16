using System.Threading;
using CasualKit.Core;
using CasualKit.UI;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CasualKit.Bootstrap
{
    public sealed class BootGameplay : MonoBehaviour
    {
        private GameContext m_Context;
        private CancellationTokenSource m_Cts;
        private IGameSession m_Session;
        private int m_LevelIndex;
        private bool m_SettingsOpen;

        private void Awake()
        {
            if (GameRunner.Instance == null)
            {
                EditorLog.Error("GameRunner missing. Play from Init.");
                return;
            }

            m_Context = GameRunner.Instance.Context;
            m_Cts = CancellationTokenSource.CreateLinkedTokenSource(m_Context.CancellationToken);
        }

        private void Start()
        {
            if (m_Context == null)
                return;

            RunAsync().Forget();
        }

        private async UniTaskVoid RunAsync()
        {
            CancellationToken token = m_Cts.Token;
            ISaveService save = m_Context.Services.Get<ISaveService>();

            m_LevelIndex = m_Context.GameplayLevelIndex >= 0 ? m_Context.GameplayLevelIndex : save.CurrentLevelIndex;
            if (m_Context.LevelCatalog != null && m_LevelIndex > m_Context.LevelCatalog.LastLevelIndex)
                m_LevelIndex = m_Context.LevelCatalog.LastLevelIndex;
            if (m_LevelIndex < 0)
                m_LevelIndex = 0;

            UIPanels.SetLoadingProgress(0.75f);
            m_Session = GameSessionFactory.Create(m_Context);
            bool loaded = await m_Session.LoadAsync(m_LevelIndex, token);
            if (token.IsCancellationRequested)
                return;

            if (!loaded)
            {
                UIPanels.HideLoading();
                return;
            }

            EB.Presentation.Add<UIPanelOpened>(OnPanelOpened);
            EB.Presentation.Add<ApplicationPauseChanged>(OnApplicationPauseChanged);
            UIPanels.SetLoadingProgress(1f);
            EB.Presentation.Invoke(new OpenUIPanelEvent(EUIPanel.Gameplay, 0));
        }

        private void OnPanelOpened(UIPanelOpened opened)
        {
            if (opened.Instance == null)
                return;

            if (opened.Panel == EUIPanel.Gameplay)
            {
                if (m_Session != null)
                    m_Session.BindHud(opened.Instance, OpenSettings);

                UIPanels.HideLoading();
                return;
            }

            if (opened.Panel != EUIPanel.Settings)
                return;

            SettingsCanvasUI settings = opened.Instance.GetComponent<SettingsCanvasUI>();
            if (settings != null)
                settings.Bind(m_Context.Services.Get<IAudioService>(), CloseSettings);
        }

        private void OpenSettings()
        {
            m_Context.Services.Get<IAudioService>().Play(EAudioName.SfxUiClick);
            ShowPausePanel();
        }

        private void ShowPausePanel()
        {
            if (m_SettingsOpen)
                return;

            m_SettingsOpen = true;
            if (m_Session != null)
                m_Session.Pause();

            EB.Presentation.Invoke(new OpenUIPanelEvent(EUIPanel.Settings, 1, additive: true));
        }

        private void CloseSettings()
        {
            m_SettingsOpen = false;
            UIPanels.Close(EUIPanel.Settings);
            if (m_Session != null)
                m_Session.Resume();
        }

        private void OnApplicationPauseChanged(ApplicationPauseChanged evt)
        {
            if (m_Session == null || !m_Session.IsLoaded)
                return;

            if (evt.Paused)
            {
                m_Session.Pause();
                return;
            }

            ShowPausePanel();
        }

        private void OnDisable()
        {
            Shutdown();
        }

        private void OnDestroy()
        {
            Shutdown();
        }

        private void Shutdown()
        {
            EB.Presentation.Remove<UIPanelOpened>(OnPanelOpened);
            EB.Presentation.Remove<ApplicationPauseChanged>(OnApplicationPauseChanged);
            EB.Presentation.Invoke(new CloseAllUIPanelsEvent(1));

            if (m_Cts != null)
            {
                m_Cts.Cancel();
                m_Cts.Dispose();
                m_Cts = null;
            }

            m_SettingsOpen = false;
            if (m_Session != null)
            {
                m_Session.Teardown();
                m_Session = null;
            }
        }
    }
}
