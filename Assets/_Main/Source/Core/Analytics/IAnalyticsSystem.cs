namespace CasualKit.Core
{
    public interface IAnalyticsSystem : IService
    {
        void Register(IAnalytics analytics);
    }
}
