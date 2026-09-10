using CasualKit.Core;
using UnityEngine;

namespace MatchRacers
{
    public sealed class RaceAudioPlayer
    {
        private readonly RaceAudioCatalogSO m_Catalog;
        private readonly NitroLevelTableSO m_Levels;
        private readonly IAudioService m_KitAudio;

        private readonly GameObject m_Root;
        private readonly AudioSource[] m_OneShots;
        private readonly AudioSource m_Loop;

        private int m_NextVoice;
        private float m_LoopTarget;

        public RaceAudioPlayer(RaceAudioCatalogSO catalog, NitroLevelTableSO levels, IAudioService kitAudio,
            Transform loopAnchor)
        {
            m_Catalog = catalog;
            m_Levels = levels;
            m_KitAudio = kitAudio;

            m_Root = new GameObject("RaceAudio");
            if (loopAnchor != null)
                m_Root.transform.SetParent(loopAnchor, false);

            int voices = catalog != null ? catalog.OneShotVoices : 4;
            m_OneShots = new AudioSource[voices];
            for (int i = 0; i < voices; i++)
                m_OneShots[i] = CreateSource("OneShot" + i, false);

            m_Loop = CreateSource("NitroLoop", true);
        }

        public void Dispose()
        {
            if (m_Root != null)
                Object.Destroy(m_Root);
        }

        public void Play(ERaceSound sound, float pitch = 1f, float volumeScale = 1f)
        {
            if (m_Catalog == null || IsMuted)
                return;

            if (!m_Catalog.TryGet(sound, out AudioClip clip, out float volume))
                return;

            AudioSource source = m_OneShots[m_NextVoice];
            m_NextVoice = (m_NextVoice + 1) % m_OneShots.Length;

            source.pitch = pitch;
            source.volume = volume * volumeScale * m_Catalog.MasterVolume;
            source.PlayOneShot(clip, 1f);
        }

        public void PlayNitroEntry(int key)
        {
            if (m_Levels == null)
            {
                Play(ERaceSound.NitroEntry);
                return;
            }

            NitroLevelEntry level = m_Levels.Get(key);
            Play(ERaceSound.NitroEntry, level.Pitch, level.Volume);
            StartLoop(key);
        }

        public void PlayNitroExit(int key)
        {
            NitroLevelEntry level = m_Levels != null ? m_Levels.Get(key) : default;
            Play(ERaceSound.NitroExit, m_Levels != null ? level.Pitch : 1f, m_Levels != null ? level.Volume : 1f);
            m_LoopTarget = 0f;
        }

        public void Tick(float deltaTime)
        {
            if (m_Loop == null)
                return;

            float fade = m_LoopTarget > m_Loop.volume ? 12f : 4f;
            m_Loop.volume = Mathf.MoveTowards(m_Loop.volume, IsMuted ? 0f : m_LoopTarget, fade * deltaTime);

            if (m_Loop.volume <= 0.001f && m_Loop.isPlaying)
                m_Loop.Stop();
        }

        private void StartLoop(int key)
        {
            if (m_Catalog == null || m_Loop == null || IsMuted)
                return;

            if (!m_Catalog.TryGet(ERaceSound.NitroLoop, out AudioClip clip, out float volume))
                return;

            NitroLevelEntry level = m_Levels != null ? m_Levels.Get(key) : default;

            m_Loop.clip = clip;
            m_Loop.pitch = m_Levels != null ? level.Pitch : 1f;
            m_LoopTarget = volume * (m_Levels != null ? level.Volume : 1f) * m_Catalog.MasterVolume;

            if (!m_Loop.isPlaying)
                m_Loop.Play();
        }

        private bool IsMuted => m_KitAudio != null && m_KitAudio.SoundMuted;

        private AudioSource CreateSource(string sourceName, bool loop)
        {
            GameObject host = new GameObject(sourceName);
            host.transform.SetParent(m_Root.transform, false);

            AudioSource source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.volume = loop ? 0f : 1f;
            return source;
        }
    }
}
