namespace CasualKit.Core
{
    public sealed class WalletService : Service, IWalletService
    {
        private readonly ISaveService m_Save;

        public WalletService(ISaveService save)
        {
            m_Save = save;
        }

        public int SoftCurrency => m_Save.SoftCurrency;

        public void Add(int amount)
        {
            m_Save.AddSoftCurrency(amount);
        }

        public bool TrySpend(int amount)
        {
            return m_Save.TrySpendSoftCurrency(amount);
        }
    }
}
