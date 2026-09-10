using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CasualKit.Core
{
    public interface IGameSession
    {
        int LevelIndex { get; }
        bool IsLoaded { get; }
        bool ReturnToMenu { get; }

        UniTask<bool> LoadAsync(int levelIndex, CancellationToken cancellationToken);
        void BindHud(GameObject hudRoot, Action openSettings);
        void Pause();
        void Resume();
        void Teardown();
    }
}
