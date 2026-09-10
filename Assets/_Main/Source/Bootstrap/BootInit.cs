using CasualKit.Core;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CasualKit.Bootstrap
{
    public sealed class BootInit : MonoBehaviour
    {
        private GameContext m_Context;

        private void Awake()
        {
            if (GameRunner.Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            DontDestroyOnLoad(gameObject);

            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            EditorLog.SetMinimumLevel(ELogLevel.Warning);
#endif

            m_Context = ServiceInstaller.CreateContext(gameObject, SaveService.DefaultFileName);
        }
        private void Start()
        {
            if (m_Context == null)
                return;

            BootAsync().Forget();
        }
        private async UniTaskVoid BootAsync()
        {
            await ServiceInstaller.InitializeRuntimeAsync(m_Context, true);

            ISaveService save = m_Context.Services.Get<ISaveService>();
            ISceneService scenes = m_Context.Services.Get<ISceneService>();
            ESceneName scene = save.HasCompletedFirstLevel ? ESceneName.Menu : ESceneName.Gameplay;
            
            await scenes.Load(scene, m_Context.CancellationToken);
        }
    }
}
