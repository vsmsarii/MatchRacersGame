using UnityEngine;

namespace CasualKit.Core
{
    public sealed class GameRunner : MonoBehaviour
    {
        public static GameRunner Instance { get; private set; }

        private GameContext m_Context;

        public GameContext Context => m_Context;

        public void Bind(GameContext context)
        {
            m_Context = context;
            Instance = this;
        }

        private void Update()
        {
            m_Context?.GameLoop.Tick(Time.deltaTime);
        }

        private void FixedUpdate()
        {
            m_Context?.GameLoop.FixedTick(Time.fixedDeltaTime);
        }

        private void LateUpdate()
        {
            m_Context?.GameLoop.LateTick(Time.deltaTime);
        }

        private void OnApplicationPause(bool paused)
        {
            if (m_Context == null)
                return;

            if (paused)
                m_Context.Services.Get<ISaveService>().FlushPending();

            EB.Presentation.Invoke(new ApplicationPauseChanged(paused));
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            m_Context?.Dispose();
            m_Context = null;
        }
    }
}
