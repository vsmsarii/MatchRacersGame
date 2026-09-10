namespace CasualKit.Core
{
    public interface IWalletService : IService
    {
        int SoftCurrency { get; }
        void Add(int amount);
        bool TrySpend(int amount);
    }
}
