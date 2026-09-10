namespace CasualKit.Core
{
    public abstract class Service : IService
    {
        public bool IsInitialized { get; private set; }

        public void Initialize()
        {
            if (IsInitialized)
                return;

            IsInitialized = true;
            OnInitialize();
        }

        public void Dispose()
        {
            if (!IsInitialized)
                return;

            OnDispose();
            IsInitialized = false;
        }

        protected virtual void OnInitialize(){}
        protected virtual void OnDispose(){}
    }
}