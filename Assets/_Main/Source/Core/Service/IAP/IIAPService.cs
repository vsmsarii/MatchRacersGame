using System.Threading;
using Cysharp.Threading.Tasks;

namespace CasualKit.Core
{
    public interface IIAPService : IService
    {
        UniTask InitializeAsync(CancellationToken cancellationToken = default);
        UniTask<bool> PurchaseAsync(string productKey, CancellationToken cancellationToken = default);
    }
}
