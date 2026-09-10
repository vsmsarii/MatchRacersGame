using System.Threading;
using Cysharp.Threading.Tasks;

namespace CasualKit.Core
{
    public sealed class NullRemoteConfig : Service, IRemoteConfig
    {
        public UniTask FetchAsync(CancellationToken cancellationToken = default)
        {
            return UniTask.CompletedTask;
        }

        public string GetString(string key, string fallback) => fallback;
        public int GetInt(string key, int fallback) => fallback;
        public float GetFloat(string key, float fallback) => fallback;
        public bool GetBool(string key, bool fallback) => fallback;
    }
}
