namespace CasualKit.Core
{
    public interface ITimeService : IService
    {
        long UnixUtc { get; }
    }
}
