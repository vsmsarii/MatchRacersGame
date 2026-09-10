using System.Threading;
using Cysharp.Threading.Tasks;

namespace CasualKit.Core
{
    public interface IRemoteConfig : IService
    {
        UniTask FetchAsync(CancellationToken cancellationToken = default);
        string GetString(string key, string fallback);
        int GetInt(string key, int fallback);
        float GetFloat(string key, float fallback);
        bool GetBool(string key, bool fallback);
    }
}
