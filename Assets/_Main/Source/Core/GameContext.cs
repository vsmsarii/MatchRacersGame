using System;
using System.Threading;

namespace CasualKit.Core
{
    public sealed class GameContext : IDisposable
    {
        public readonly GameLoop GameLoop;
        public readonly ServiceProvider Services;

        private readonly CancellationTokenSource m_Cts;
        private bool m_Disposed;

        public int GameplayLevelIndex = -1;
        public GlobalSettingsSO GlobalSettings;
        public ILevelCatalog LevelCatalog;

        public CancellationToken CancellationToken => m_Cts.Token;

        public GameContext(GameLoop gameLoop, ServiceProvider services)
        {
            GameLoop = gameLoop;
            Services = services;
            m_Cts = new CancellationTokenSource();
        }

        public void Dispose()
        {
            if (m_Disposed)
                return;

            m_Disposed = true;

            if (!m_Cts.IsCancellationRequested)
                m_Cts.Cancel();

            m_Cts.Dispose();
            Services.Dispose();
        }
    }
}
