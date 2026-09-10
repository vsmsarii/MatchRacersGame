using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CasualKit.Core
{
    public sealed class AudioSystem : Service, IAudioService
    {
        private const string MusicMuteKey = "casualkit.audio.music.mute";
        private const string SoundMuteKey = "casualkit.audio.sound.mute";

        private readonly IAssetProvider m_Assets;
        private readonly Dictionary<EAudioName, AudioClip> m_Clips = new Dictionary<EAudioName, AudioClip>();
        private GameObject m_Root;
        private AudioSource m_SfxSource;
        private AudioSource m_MusicSource;
        private EAudioName m_CurrentMusic;
        private bool m_MusicMuted;
        private bool m_SoundMuted;

        public bool MusicMuted => m_MusicMuted;
        public bool SoundMuted => m_SoundMuted;

        public AudioSystem(IAssetProvider assets)
        {
            m_Assets = assets;
        }

        public async UniTask InitializeAsync(CancellationToken cancellationToken = default)
        {
            AudioCatalogSO catalog = await m_Assets.LoadAsset<AudioCatalogSO>(AddressableKeys.AudioCatalog, cancellationToken);
            m_Clips.Clear();
            if (catalog == null || catalog.Entries == null)
                return;

            for (int i = 0; i < catalog.Entries.Length; i++)
            {
                AudioCatalogEntry entry = catalog.Entries[i];
                if (entry.Name == EAudioName.None || entry.Clip == null)
                    continue;

                m_Clips[entry.Name] = entry.Clip;
            }
        }

        public void Play(EAudioName name)
        {
            if (m_SoundMuted || m_SfxSource == null || name == EAudioName.None)
                return;

            if (!m_Clips.TryGetValue(name, out AudioClip clip) || clip == null)
                return;

            m_SfxSource.PlayOneShot(clip);
        }

        public void PlayMusic(EAudioName name)
        {
            if (m_MusicSource == null || name == EAudioName.None)
                return;

            if (m_CurrentMusic == name && m_MusicSource.isPlaying)
            {
                m_MusicSource.mute = m_MusicMuted;
                return;
            }

            if (!m_Clips.TryGetValue(name, out AudioClip clip) || clip == null)
            {
                StopMusic();
                return;
            }

            m_CurrentMusic = name;
            m_MusicSource.clip = clip;
            m_MusicSource.loop = true;
            m_MusicSource.mute = m_MusicMuted;
            m_MusicSource.Play();
        }

        public void StopMusic()
        {
            m_CurrentMusic = EAudioName.None;
            if (m_MusicSource == null)
                return;

            m_MusicSource.Stop();
            m_MusicSource.clip = null;
        }

        public void SetMusicMuted(bool muted)
        {
            m_MusicMuted = muted;
            PlayerPrefs.SetInt(MusicMuteKey, muted ? 1 : 0);
            PlayerPrefs.Save();
            if (m_MusicSource != null)
                m_MusicSource.mute = muted;
        }

        public void SetSoundMuted(bool muted)
        {
            m_SoundMuted = muted;
            PlayerPrefs.SetInt(SoundMuteKey, muted ? 1 : 0);
            PlayerPrefs.Save();
            if (m_SfxSource != null)
                m_SfxSource.mute = muted;
        }

        protected override void OnInitialize()
        {
            m_MusicMuted = PlayerPrefs.GetInt(MusicMuteKey, 0) == 1;
            m_SoundMuted = PlayerPrefs.GetInt(SoundMuteKey, 0) == 1;

            m_Root = new GameObject("[Audio]");
            Object.DontDestroyOnLoad(m_Root);
            m_SfxSource = m_Root.AddComponent<AudioSource>();
            m_SfxSource.playOnAwake = false;
            m_SfxSource.spatialBlend = 0f;
            m_SfxSource.mute = m_SoundMuted;

            m_MusicSource = m_Root.AddComponent<AudioSource>();
            m_MusicSource.playOnAwake = false;
            m_MusicSource.spatialBlend = 0f;
            m_MusicSource.loop = true;
            m_MusicSource.mute = m_MusicMuted;
        }

        protected override void OnDispose()
        {
            m_Clips.Clear();
            if (m_Root != null)
                Object.Destroy(m_Root);

            m_Root = null;
            m_SfxSource = null;
            m_MusicSource = null;
        }
    }
}
