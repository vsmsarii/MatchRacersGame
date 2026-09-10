using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CasualKit.Core
{
    public interface IAssetProvider : IService
    {
        UniTask InitializeAsync(CancellationToken cancellationToken = default);
        UniTask<T> LoadAsset<T>(string key, CancellationToken cancellationToken = default) where T : UnityEngine.Object;
        void ReleaseAsset(string key);
        UniTask<GameObject> Instantiate(string key, Transform parent = null, CancellationToken cancellationToken = default);
        void ReleaseInstance(GameObject instance);
    }
}
