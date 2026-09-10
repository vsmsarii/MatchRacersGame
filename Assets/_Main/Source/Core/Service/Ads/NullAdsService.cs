using System.Threading;
using Cysharp.Threading.Tasks;

namespace CasualKit.Core
{
    public sealed class NullAdsService : Service, IAdsService
    {
        public bool IsRewardedReady => false;

        public UniTask<bool> ShowRewardedAsync(CancellationToken cancellationToken = default)
        {
            return UniTask.FromResult(false);
        }

        public void ShowInterstitial()
        {
        }
    }
}
