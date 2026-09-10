using System;
using System.Threading;
using CasualKit.Core;
using CasualKit.UI;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CasualKit.Bootstrap
{
    public sealed class StubGameSession : IGameSession
    {
        private readonly GameContext m_Context;
        private int m_LevelIndex;
        private bool m_IsLoaded;

        public int LevelIndex => m_LevelIndex;
        public bool IsLoaded => m_IsLoaded;
        public bool ReturnToMenu => false;

        public StubGameSession(GameContext context)
        {
            m_Context = context;
        }

        public UniTask<bool> LoadAsync(int levelIndex, CancellationToken cancellationToken)
        {
            m_LevelIndex = levelIndex < 0 ? 0 : levelIndex;
            m_Context.Services.Get<IAudioService>().PlayMusic(EAudioName.MusicGameplay);
            m_IsLoaded = true;
            return UniTask.FromResult(true);
        }

        public void BindHud(GameObject hudRoot, Action openSettings)
        {
            if (hudRoot == null)
                return;

            GameplayCanvasUI hud = hudRoot.GetComponent<GameplayCanvasUI>();
            if (hud == null)
                hud = hudRoot.GetComponentInChildren<GameplayCanvasUI>();
            if (hud == null)
                return;

            hud.BindLevel(m_LevelIndex + 1);
            hud.BindSettings(openSettings);
        }

        public void Pause()
        {
        }

        public void Resume()
        {
        }

        public void Teardown()
        {
            m_IsLoaded = false;
        }
    }
}
