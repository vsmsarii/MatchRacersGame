using CasualKit.Core;
using CasualKit.UI;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CasualKit.Bootstrap
{
    public static class ServiceInstaller
    {
        public static GameContext CreateContext(GameObject host, string saveFileName)
        {
            GameLoop loop = new GameLoop();
            ServiceProvider services = new ServiceProvider(loop);
            GameContext context = new GameContext(loop, services);

            GameRunner runner = host.GetComponent<GameRunner>();
            if (runner == null)
                runner = host.AddComponent<GameRunner>();

            runner.Bind(context);
            Register(services, saveFileName);
            return context;
        }

        public static void Register(ServiceProvider services, string saveFileName)
        {
            ITimeService time = new SystemTimeService();
            IAssetProvider assets = new AssetProvider();
            ISaveService save = new SaveService(saveFileName, time);
            AnalyticsSystem analytics = new AnalyticsSystem();
            analytics.Register(new AnalyticsLog());
            ISceneLoadingUi loadingUi = new SceneLoadingUi();

            services.Register<ITimeService>(time);
            services.Register<IAssetProvider>(assets);
            services.Register<IGameObjectPool>(new GameObjectPool(assets));
            services.Register<ISceneLoadingUi>(loadingUi);
            services.Register<ISceneService>(new SceneService(loadingUi));
            services.Register<IInputService>(new InputService(assets));
            services.Register<IAudioService>(new AudioSystem(assets));
            services.Register<ISaveService>(save);
            services.Register<IWalletService>(new WalletService(save));
            services.Register<IIAPService>(new IAPService(assets, save));
            services.Register<IAnalyticsSystem>(analytics);
            services.Register<IAdsService>(new NullAdsService());
            services.Register<IRemoteConfig>(new NullRemoteConfig());
            services.Register<IHaptics>(new DeviceHaptics());
        }

        public static async UniTask<bool> InitializeRuntimeAsync(GameContext context, bool bindUi)
        {
            IAssetProvider assets = context.Services.Get<IAssetProvider>();
            IInputService input = context.Services.Get<IInputService>();
            IAudioService audio = context.Services.Get<IAudioService>();
            IIAPService iap = context.Services.Get<IIAPService>();
            ISaveService save = context.Services.Get<ISaveService>();
            IRemoteConfig remoteConfig = context.Services.Get<IRemoteConfig>();

            await assets.InitializeAsync(context.CancellationToken);
            await input.InitializeAsync(context.CancellationToken);
            await audio.InitializeAsync(context.CancellationToken);
            await iap.InitializeAsync(context.CancellationToken);
            await remoteConfig.FetchAsync(context.CancellationToken);

            GlobalSettingsSO globalSettings = await assets.LoadAsset<GlobalSettingsSO>(
                AddressableKeys.GlobalSettings,
                context.CancellationToken);
            if (globalSettings != null)
            {
                context.GlobalSettings = globalSettings;
                Application.targetFrameRate = globalSettings.TargetFrameRate;
                save.ConfigureHearts(globalSettings.DefaultHeartCount, globalSettings.HeartRefillMinutes);
            }
            else
            {
                EditorLog.Error("GlobalSettings asset missing.");
            }

            UnityEngine.Object manifestAsset = await assets.LoadAsset<UnityEngine.Object>(
                AddressableKeys.LevelManifest,
                context.CancellationToken);
            ILevelCatalog levelCatalog = manifestAsset as ILevelCatalog;
            if (levelCatalog != null)
                context.LevelCatalog = levelCatalog;
            else
                EditorLog.Error("LevelManifest asset missing.");

            if (!bindUi)
                return globalSettings != null && levelCatalog != null;

            UIPanelCatalogSO catalog = await assets.LoadAsset<UIPanelCatalogSO>(
                AddressableKeys.UiPanelCatalog,
                context.CancellationToken);
            UiEventSystem.Ensure();
            UIRoot uiRoot = Object.FindAnyObjectByType<UIRoot>();
            if (uiRoot != null)
                uiRoot.Initialize(assets, catalog);
            else
                EditorLog.Error("UIRoot missing.");

            return globalSettings != null && levelCatalog != null;
        }
    }
}
