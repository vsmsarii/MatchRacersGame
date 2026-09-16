using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CasualKit.Core
{
    public interface IAudioService : IService
    {
        bool MusicMuted { get; }
        bool SoundMuted { get; }

        UniTask InitializeAsync(CancellationToken cancellationToken = default);
        bool HasClip(EAudioName name);
        void Play(EAudioName name);
        void Play(EAudioName name, float volume, float pitch);
        void SetLoop(int channel, EAudioName name, float volume, float pitch);
        void SetLoopAt(int channel, EAudioName name, float volume, float pitch, Vector3 position);
        void PlayAt(EAudioName name, Vector3 position, float volume, float pitch);
        void StopLoop(int channel);
        void PlayMusic(EAudioName name);
        void PlayMusic(AudioClip clip, float volume);
        void StopMusic();
        void SetMusicMuted(bool muted);
        void SetSoundMuted(bool muted);
    }
}
