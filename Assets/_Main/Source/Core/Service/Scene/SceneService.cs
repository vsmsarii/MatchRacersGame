using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CasualKit.Core
{
    public sealed class SceneService : Service, ISceneService
    {
        private const float SceneProgressWeight = 0.7f;

        private readonly ISceneLoadingUi m_Loading;

        public SceneService(ISceneLoadingUi loading)
        {
            m_Loading = loading;
        }

        public UniTask Load(ESceneName scene, CancellationToken cancellationToken = default)
        {
            if (!TryGetSceneName(scene, out string sceneName))
                return UniTask.CompletedTask;

            return LoadInternal(sceneName, cancellationToken);
        }

        public UniTask Reload(ESceneName scene, CancellationToken cancellationToken = default)
        {
            return Load(scene, cancellationToken);
        }

        private async UniTask LoadInternal(string sceneName, CancellationToken cancellationToken)
        {
            if (m_Loading != null)
                await m_Loading.ShowAsync(cancellationToken);

            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (operation == null)
            {
                EditorLog.Error("Scene is not in build settings: " + sceneName);
                return;
            }

            while (!operation.isDone)
            {
                float sceneProgress = Mathf.Clamp01(operation.progress / 0.9f);
                if (m_Loading != null)
                    m_Loading.SetProgress(sceneProgress * SceneProgressWeight);
                await UniTask.Yield(cancellationToken);
            }

            if (m_Loading != null)
                m_Loading.SetProgress(SceneProgressWeight);
        }

        private static bool TryGetSceneName(ESceneName scene, out string sceneName)
        {
            switch (scene)
            {
                case ESceneName.Init:
                    sceneName = "Init";
                    return true;
                case ESceneName.Menu:
                    sceneName = "Menu";
                    return true;
                case ESceneName.Gameplay:
                    sceneName = "Gameplay";
                    return true;
                default:
                    sceneName = null;
                    EditorLog.Error("Unmapped scene requested: " + scene);
                    return false;
            }
        }
    }
}
