using System.Threading;
using Cysharp.Threading.Tasks;

namespace CasualKit.Core
{
    public interface IAdsService : IService
    {
        bool IsRewardedReady { get; }
        UniTask<bool> ShowRewardedAsync(CancellationToken cancellationToken = default);
        void ShowInterstitial();
    }
}
