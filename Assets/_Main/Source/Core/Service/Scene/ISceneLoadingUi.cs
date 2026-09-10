using System.Threading;
using Cysharp.Threading.Tasks;

namespace CasualKit.Core
{
    public interface ISceneLoadingUi : IService
    {
        UniTask ShowAsync(CancellationToken cancellationToken = default);
        void SetProgress(float progress);
        void Hide();
    }
}
