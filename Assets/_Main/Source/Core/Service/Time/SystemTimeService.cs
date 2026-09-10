using System;

namespace CasualKit.Core
{
    public sealed class SystemTimeService : Service, ITimeService
    {
        public long UnixUtc => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}
