using System.Threading;
using Cysharp.Threading.Tasks;

namespace CasualKit.Core
{
    public interface ISceneService : IService
    {
        UniTask Load(ESceneName scene, CancellationToken cancellationToken = default);
        UniTask Reload(ESceneName scene, CancellationToken cancellationToken = default);
    }
}
