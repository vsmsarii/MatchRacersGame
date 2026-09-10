using Cysharp.Threading.Tasks;
using CasualKit.Core;
using CasualKit.UI;
using UnityEngine;

namespace CasualKit.Bootstrap
{
    public sealed class BootMenu : MonoBehaviour
    {
        private GameContext m_Context;
        private IAPCatalogSO m_IapCatalog;

        private void Awake()
        {
            if (GameRunner.Instance == null)
            {
                EditorLog.Error("GameRunner missing. Play from Init.");
                return;
            }

            m_Context = GameRunner.Instance.Context;
            EB.Presentation.Add<UIPanelOpened>(OnPanelOpened);
        }

        private void Start()
        {
            if (m_Context == null)
                return;

            RunAsync().Forget();
        }

        private async UniTaskVoid RunAsync()
        {
            IAssetProvider assets = m_Context.Services.Get<IAssetProvider>();
            IIAPService iap = m_Context.Services.Get<IIAPService>();
            IAudioService audio = m_Context.Services.Get<IAudioService>();

            m_IapCatalog = await assets.LoadAsset<IAPCatalogSO>(AddressableKeys.IapCatalog, m_Context.CancellationToken);
            await iap.InitializeAsync(m_Context.CancellationToken);
            audio.PlayMusic(EAudioName.MusicMenu);

            EB.Presentation.Invoke(new OpenUIPanelEvent(EUIPanel.MainMenu, 0));
        }

        private void OnPanelOpened(UIPanelOpened opened)
        {
            if (opened.Instance == null)
                return;

            if (opened.Panel == EUIPanel.MainMenu)
            {
                MainMenuCanvasUI ui = opened.Instance.GetComponent<MainMenuCanvasUI>();
                if (ui == null)
                    return;

                ISaveService save = m_Context.Services.Get<ISaveService>();
                save.RefreshHearts();
                int levelIndex = save.CurrentLevelIndex;
                int lastLevelIndex = m_Context.LevelCatalog != null
                    ? m_Context.LevelCatalog.LastLevelIndex
                    : levelIndex;
                if (levelIndex > lastLevelIndex)
                    levelIndex = lastLevelIndex;

                ui.Bind(
                    save,
                    levelIndex + 1,
                    save.GetTotalScore(),
                    () => Play(levelIndex).Forget(),
                    OpenShop,
                    OpenSettings);
                UIPanels.SetLoadingProgress(1f);
                UIPanels.HideLoading();
                return;
            }

            if (opened.Panel == EUIPanel.Shop)
            {
                ShopCanvasUI shop = opened.Instance.GetComponent<ShopCanvasUI>();
                if (shop == null)
                    return;

                shop.Bind(
                    m_IapCatalog,
                    m_Context.Services.Get<IIAPService>(),
                    _ => true,
                    () => UIPanels.Close(EUIPanel.Shop));
                return;
            }

            if (opened.Panel != EUIPanel.Settings)
                return;

            SettingsCanvasUI settings = opened.Instance.GetComponent<SettingsCanvasUI>();
            if (settings == null)
                return;

            settings.Bind(
                m_Context.Services.Get<IAudioService>(),
                () => UIPanels.Close(EUIPanel.Settings));
        }

        private void OpenShop()
        {
            m_Context.Services.Get<IAudioService>().Play(EAudioName.SfxUiClick);
            EB.Presentation.Invoke(new OpenUIPanelEvent(EUIPanel.Shop, 1));
        }

        private void OpenSettings()
        {
            m_Context.Services.Get<IAudioService>().Play(EAudioName.SfxUiClick);
            EB.Presentation.Invoke(new OpenUIPanelEvent(EUIPanel.Settings, 1));
        }

        private async UniTaskVoid Play(int levelIndex)
        {
            ISaveService save = m_Context.Services.Get<ISaveService>();
            save.RefreshHearts();
            if (save.Hearts <= 0)
                return;

            m_Context.GameplayLevelIndex = levelIndex;
            ISceneService scenes = m_Context.Services.Get<ISceneService>();
            await scenes.Load(ESceneName.Gameplay, m_Context.CancellationToken);
        }

        private void OnDestroy()
        {
            EB.Presentation.Remove<UIPanelOpened>(OnPanelOpened);
        }
    }
}
