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
        private const int ClipVoiceCount = 8;

        private readonly IAssetProvider m_Assets;
        private readonly Dictionary<EAudioName, AudioClip> m_Clips = new Dictionary<EAudioName, AudioClip>();
        private readonly Dictionary<EAudioName, float> m_Volumes = new Dictionary<EAudioName, float>();
        private readonly List<AudioSource> m_ClipVoices = new List<AudioSource>();
        private readonly List<AudioSource> m_PointVoices = new List<AudioSource>();
        private readonly Dictionary<int, AudioSource> m_Loops = new Dictionary<int, AudioSource>();
        private GameObject m_Root;
        private AudioSource m_SfxSource;
        private AudioSource m_MusicSource;
        private AudioSource m_FadingSource;
        private CancellationTokenSource m_FadeCancellation;
        private float m_MusicTarget;
        private float m_MusicVolume = 1f;
        private float m_MusicFadeSeconds = 1f;
        private float m_SpatialBlend = 1f;
        private float m_MinDistance = 25f;
        private float m_MaxDistance = 260f;
        private float m_Spread = 35f;
        private float m_DopplerLevel = 0.25f;
        private bool m_MusicMuted;
        private bool m_SoundMuted;
        private int m_NextClipVoice;
        private int m_NextPointVoice;

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
            m_Volumes.Clear();
            if (catalog == null)
                return;

            m_MusicVolume = catalog.MusicVolume;
            m_MusicFadeSeconds = catalog.MusicFadeSeconds;
            m_SpatialBlend = catalog.SpatialBlend;
            m_MinDistance = catalog.MinDistance;
            m_MaxDistance = catalog.MaxDistance;
            m_Spread = catalog.Spread;
            m_DopplerLevel = catalog.DopplerLevel;
            if (catalog.Entries == null)
                return;

            for (int i = 0; i < catalog.Entries.Length; i++)
            {
                AudioCatalogEntry entry = catalog.Entries[i];
                if (entry.Name == EAudioName.None || entry.Clip == null)
                    continue;

                m_Clips[entry.Name] = entry.Clip;
                m_Volumes[entry.Name] = entry.Volume;
            }
        }

        public bool HasClip(EAudioName name)
        {
            return TryGetClip(name, out _);
        }

        public void Play(EAudioName name)
        {
            if (m_SoundMuted || m_SfxSource == null || !TryGetClip(name, out AudioClip clip))
                return;

            m_SfxSource.PlayOneShot(clip, GetVolume(name));
        }

        public void Play(EAudioName name, float volume, float pitch)
        {
            if (m_SoundMuted || m_Root == null || volume <= 0f || !TryGetClip(name, out AudioClip clip))
                return;

            AudioSource voice = NextClipVoice();
            voice.pitch = pitch;
            voice.volume = 1f;
            voice.PlayOneShot(clip, Mathf.Clamp01(volume * GetVolume(name)));
        }

        public void SetLoop(int channel, EAudioName name, float volume, float pitch)
        {
            SetLoop(channel, name, volume, pitch, false, Vector3.zero);
        }

        public void SetLoopAt(int channel, EAudioName name, float volume, float pitch, Vector3 position)
        {
            SetLoop(channel, name, volume, pitch, true, position);
        }

        public void PlayAt(EAudioName name, Vector3 position, float volume, float pitch)
        {
            if (m_SoundMuted || m_Root == null || volume <= 0f || !TryGetClip(name, out AudioClip clip))
                return;

            AudioSource voice = NextPointVoice();
            ApplySpatial(voice, true, position);
            voice.pitch = pitch;
            voice.volume = 1f;
            voice.PlayOneShot(clip, Mathf.Clamp01(volume * GetVolume(name)));
        }

        private void SetLoop(int channel, EAudioName name, float volume, float pitch, bool spatial, Vector3 position)
        {
            if (m_Root == null)
                return;

            AudioSource source = GetLoop(channel);

            if (!TryGetClip(name, out AudioClip clip))
            {
                source.Stop();
                source.clip = null;
                return;
            }

            ApplySpatial(source, spatial, position);
            source.volume = Mathf.Clamp01(volume * GetVolume(name));
            source.pitch = pitch;
            source.mute = m_SoundMuted;

            if (source.clip != clip)
            {
                source.clip = clip;
                source.Play();
            }
            else if (!source.isPlaying)
            {
                source.Play();
            }
        }

        public void StopLoop(int channel)
        {
            if (!m_Loops.TryGetValue(channel, out AudioSource source) || source == null)
                return;

            source.Stop();
            source.clip = null;
        }

        public void PlayMusic(EAudioName name)
        {
            if (m_MusicSource == null || name == EAudioName.None)
                return;

            if (!TryGetClip(name, out AudioClip clip))
            {
                StopMusic();
                return;
            }

            PlayMusic(clip, GetVolume(name));
        }

        public void PlayMusic(AudioClip clip, float volume)
        {
            if (m_MusicSource == null || m_FadingSource == null)
                return;

            if (clip == null)
            {
                StopMusic();
                return;
            }

            float target = Mathf.Clamp01(volume) * m_MusicVolume;
            if (m_MusicSource.clip == clip && m_MusicSource.isPlaying)
            {
                m_MusicTarget = target;
                m_MusicSource.mute = m_MusicMuted;
                if (m_FadeCancellation == null)
                    m_MusicSource.volume = target;

                return;
            }

            CancelFade();

            AudioSource outgoing = m_MusicSource;
            m_MusicSource = m_FadingSource;
            m_FadingSource = outgoing;
            m_MusicTarget = target;

            bool fade = m_MusicFadeSeconds > 0f && outgoing.isPlaying;
            m_MusicSource.loop = true;
            m_MusicSource.mute = m_MusicMuted;

            if (m_MusicSource.clip != clip || !m_MusicSource.isPlaying)
            {
                m_MusicSource.clip = clip;
                m_MusicSource.volume = fade ? 0f : target;
                m_MusicSource.Play();
            }

            if (!fade)
            {
                StopSource(outgoing);
                m_MusicSource.volume = target;
                return;
            }

            m_FadeCancellation = new CancellationTokenSource();
            CrossfadeAsync(outgoing, m_MusicSource, m_FadeCancellation).Forget();
        }

        public void StopMusic()
        {
            CancelFade();
            StopSource(m_MusicSource);
            StopSource(m_FadingSource);
        }

        public void SetMusicMuted(bool muted)
        {
            m_MusicMuted = muted;
            PlayerPrefs.SetInt(MusicMuteKey, muted ? 1 : 0);
            PlayerPrefs.Save();
            if (m_MusicSource != null)
                m_MusicSource.mute = muted;

            if (m_FadingSource != null)
                m_FadingSource.mute = muted;
        }

        public void SetSoundMuted(bool muted)
        {
            m_SoundMuted = muted;
            PlayerPrefs.SetInt(SoundMuteKey, muted ? 1 : 0);
            PlayerPrefs.Save();
            if (m_SfxSource != null)
                m_SfxSource.mute = muted;

            for (int i = 0; i < m_ClipVoices.Count; i++)
            {
                if (m_ClipVoices[i] != null)
                    m_ClipVoices[i].mute = muted;
            }

            for (int i = 0; i < m_PointVoices.Count; i++)
            {
                if (m_PointVoices[i] != null)
                    m_PointVoices[i].mute = muted;
            }

            foreach (AudioSource loop in m_Loops.Values)
            {
                if (loop != null)
                    loop.mute = muted;
            }
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

            m_MusicSource = CreateMusicSource();
            m_FadingSource = CreateMusicSource();
        }

        protected override void OnDispose()
        {
            CancelFade();
            m_Clips.Clear();
            m_Volumes.Clear();
            m_ClipVoices.Clear();
            m_PointVoices.Clear();
            m_Loops.Clear();
            m_NextClipVoice = 0;
            m_NextPointVoice = 0;
            if (m_Root != null)
                Object.Destroy(m_Root);

            m_Root = null;
            m_SfxSource = null;
            m_MusicSource = null;
            m_FadingSource = null;
        }

        private AudioSource CreateMusicSource()
        {
            AudioSource source = m_Root.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.loop = true;
            source.mute = m_MusicMuted;
            return source;
        }

        private async UniTaskVoid CrossfadeAsync(AudioSource outgoing, AudioSource incoming, CancellationTokenSource cancellation)
        {
            float outgoingStart = outgoing.volume;
            float incomingStart = incoming.volume;
            float elapsed = 0f;

            while (elapsed < m_MusicFadeSeconds)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);
                if (cancellation.IsCancellationRequested || outgoing == null || incoming == null)
                    return;

                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / m_MusicFadeSeconds);
                outgoing.volume = Mathf.Lerp(outgoingStart, 0f, t);
                incoming.volume = Mathf.Lerp(incomingStart, m_MusicTarget, t);
            }

            StopSource(outgoing);
            incoming.volume = m_MusicTarget;

            if (m_FadeCancellation == cancellation)
            {
                m_FadeCancellation.Dispose();
                m_FadeCancellation = null;
            }
        }

        private void CancelFade()
        {
            if (m_FadeCancellation == null)
                return;

            m_FadeCancellation.Cancel();
            m_FadeCancellation.Dispose();
            m_FadeCancellation = null;
        }

        private static void StopSource(AudioSource source)
        {
            if (source == null)
                return;

            source.Stop();
            source.clip = null;
        }

        private bool TryGetClip(EAudioName name, out AudioClip clip)
        {
            clip = null;
            return name != EAudioName.None && m_Clips.TryGetValue(name, out clip) && clip != null;
        }

        private float GetVolume(EAudioName name)
        {
            return m_Volumes.TryGetValue(name, out float volume) ? volume : 1f;
        }

        private AudioSource NextClipVoice()
        {
            if (m_ClipVoices.Count < ClipVoiceCount)
            {
                AudioSource created = CreateSource("Voice", false);
                m_ClipVoices.Add(created);
                return created;
            }

            AudioSource voice = m_ClipVoices[m_NextClipVoice];
            m_NextClipVoice = (m_NextClipVoice + 1) % m_ClipVoices.Count;
            return voice;
        }

        private AudioSource NextPointVoice()
        {
            if (m_PointVoices.Count < ClipVoiceCount)
            {
                AudioSource created = CreateSource("PointVoice", false);
                m_PointVoices.Add(created);
                return created;
            }

            AudioSource voice = m_PointVoices[m_NextPointVoice];
            m_NextPointVoice = (m_NextPointVoice + 1) % m_PointVoices.Count;
            return voice;
        }

        private void ApplySpatial(AudioSource source, bool spatial, Vector3 position)
        {
            if (!spatial)
            {
                source.transform.localPosition = Vector3.zero;
                source.spatialBlend = 0f;
                source.dopplerLevel = 0f;
                return;
            }

            source.transform.position = position;
            source.spatialBlend = m_SpatialBlend;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = m_MinDistance;
            source.maxDistance = m_MaxDistance;
            source.spread = m_Spread;
            source.dopplerLevel = m_DopplerLevel;
        }

        private AudioSource GetLoop(int channel)
        {
            if (m_Loops.TryGetValue(channel, out AudioSource source) && source != null)
                return source;

            source = CreateSource("Loop_" + channel, true);
            m_Loops[channel] = source;
            return source;
        }

        private AudioSource CreateSource(string sourceName, bool loop)
        {
            GameObject host = new GameObject(sourceName);
            host.transform.SetParent(m_Root.transform, false);

            AudioSource source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.loop = loop;
            source.mute = m_SoundMuted;
            return source;
        }
    }
}
