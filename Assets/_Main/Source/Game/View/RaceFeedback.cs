using System;
using CasualKit.Core;
using UnityEngine;

namespace MatchRacers
{
    public sealed class RaceFeedback
    {
        private const int CarChannelBase = 20;
        private const int ChannelsPerCar = 3;
        private const int EngineSlotA = 0;
        private const int EngineSlotB = 1;
        private const int NitroSlot = 2;
        private const float RejectSoundThrottle = 0.18f;
        private const float DefaultRivalVolume = 0.7f;

        private sealed class CarVoice
        {
            public readonly EAudioName[] EngineSounds = { EAudioName.None, EAudioName.None };
            public readonly float[] EngineVolumes = new float[2];
            public readonly float[] EnginePitches = { 1f, 1f };
            public int EngineActive;
            public int EngineTier = -1;
            public float EngineVolumeTarget;
            public float EnginePitchTarget = 1f;
            public float NitroVolume;
            public float NitroTarget;
            public float NitroPitch = 1f;
        }

        private readonly IAudioService m_Audio;
        private readonly RaceCameraRig m_Camera;
        private readonly NitroLevelTableSO m_Levels;
        private readonly EngineAudioTableSO m_Engine;
        private readonly CarView[] m_Views;
        private readonly CarVoice[] m_Voices;
        private readonly int m_PlayerCarIndex;

        private float m_LastRejectSoundTime = float.NegativeInfinity;

        public RaceFeedback(IAudioService audio, RaceCameraRig camera, NitroLevelTableSO levels,
            EngineAudioTableSO engine, int playerCarIndex, CarView[] views)
        {
            m_Audio = audio;
            m_Camera = camera;
            m_Levels = levels;
            m_Engine = engine;
            m_PlayerCarIndex = playerCarIndex;
            m_Views = views;

            m_Voices = new CarVoice[views != null && views.Length > 0 ? views.Length : RaceConfigSO.CarCount];
            for (int i = 0; i < m_Voices.Length; i++)
                m_Voices[i] = new CarVoice();

            EB.Gameplay.Add<BuffAccepted>(OnBuffAccepted);
            EB.Gameplay.Add<BuffRejected>(OnBuffRejected);
            EB.Gameplay.Add<BuffExpired>(OnBuffExpired);
            EB.Gameplay.Add<CarFinished>(OnCarFinished);
        }

        public static int GetCarChannel(int carIndex, int slot)
        {
            return CarChannelBase + carIndex * ChannelsPerCar + slot;
        }

        public static int GetEngineTier(CarState player, ERaceState state, bool awaitingSelection)
        {
            if (awaitingSelection || player == null || player.Finished || state != ERaceState.Racing)
                return 0;

            return player.HasActiveBuff ? Mathf.Clamp(player.ActiveBuffKey, 1, BuffTableSO.MaxKey) : 1;
        }

        public static EAudioName ResolveEngineSound(EAudioName preferred, Func<EAudioName, bool> hasClip)
        {
            if (hasClip(preferred))
                return preferred;

            if (hasClip(EAudioName.EngineCruise))
                return EAudioName.EngineCruise;

            return hasClip(EAudioName.EngineIdle) ? EAudioName.EngineIdle : EAudioName.None;
        }

        public void Tick(float deltaTime, IRaceView race, bool awaitingSelection)
        {
            if (m_Audio == null || race == null)
                return;

            for (int i = 0; i < m_Voices.Length && i < race.CarCount; i++)
            {
                CarVoice voice = m_Voices[i];
                int tier = GetEngineTier(race.GetCar(i), race.State, awaitingSelection);
                Vector3 position = GetPosition(i);

                if (tier != voice.EngineTier)
                    ApplyEngineTier(voice, tier);

                TickNitroLoop(i, voice, deltaTime, position);
                TickEngine(i, voice, deltaTime, position);
            }
        }

        public void Dispose()
        {
            EB.Gameplay.Remove<BuffAccepted>(OnBuffAccepted);
            EB.Gameplay.Remove<BuffRejected>(OnBuffRejected);
            EB.Gameplay.Remove<BuffExpired>(OnBuffExpired);
            EB.Gameplay.Remove<CarFinished>(OnCarFinished);

            if (m_Audio == null)
                return;

            for (int i = 0; i < m_Voices.Length; i++)
            {
                m_Audio.StopLoop(GetCarChannel(i, EngineSlotA));
                m_Audio.StopLoop(GetCarChannel(i, EngineSlotB));
                m_Audio.StopLoop(GetCarChannel(i, NitroSlot));
            }
        }

        private Vector3 GetPosition(int carIndex)
        {
            if (m_Views != null && carIndex < m_Views.Length && m_Views[carIndex] != null && m_Views[carIndex].Root != null)
                return m_Views[carIndex].Root.position;

            return m_Camera != null && m_Camera.Camera != null ? m_Camera.Camera.transform.position : Vector3.zero;
        }

        private float GetVolumeScale(int carIndex)
        {
            if (carIndex == m_PlayerCarIndex)
                return 1f;

            return m_Engine != null ? m_Engine.RivalVolume : DefaultRivalVolume;
        }

        private void ApplyEngineTier(CarVoice voice, int tier)
        {
            voice.EngineTier = tier;

            if (m_Engine == null || !m_Engine.TryGetLayer(tier, out EngineAudioLayer layer))
            {
                voice.EngineVolumeTarget = 0f;
                return;
            }

            EAudioName sound = ResolveEngineSound(layer.Sound, m_Audio.HasClip);
            if (sound == EAudioName.None)
            {
                voice.EngineVolumeTarget = 0f;
                return;
            }

            voice.EnginePitchTarget = layer.Pitch;
            voice.EngineVolumeTarget = layer.Volume;

            if (voice.EngineSounds[voice.EngineActive] == sound)
                return;

            float startPitch = voice.EngineSounds[voice.EngineActive] != EAudioName.None
                ? voice.EnginePitches[voice.EngineActive]
                : layer.Pitch;

            voice.EngineActive = 1 - voice.EngineActive;
            voice.EngineSounds[voice.EngineActive] = sound;
            voice.EnginePitches[voice.EngineActive] = startPitch;
            voice.EngineVolumes[voice.EngineActive] = 0f;
        }

        private void TickEngine(int carIndex, CarVoice voice, float deltaTime, Vector3 position)
        {
            if (m_Engine == null)
                return;

            float fadeStep = deltaTime / m_Engine.CrossfadeSeconds;
            float scale = GetVolumeScale(carIndex);

            for (int i = 0; i < voice.EngineSounds.Length; i++)
            {
                bool isActive = i == voice.EngineActive;
                float target = isActive ? voice.EngineVolumeTarget : 0f;

                voice.EngineVolumes[i] = Mathf.MoveTowards(voice.EngineVolumes[i], target, fadeStep);
                if (isActive)
                {
                    voice.EnginePitches[i] = Mathf.MoveTowards(voice.EnginePitches[i], voice.EnginePitchTarget,
                        m_Engine.PitchRate * deltaTime);
                }

                if (voice.EngineSounds[i] == EAudioName.None)
                    continue;

                int channel = GetCarChannel(carIndex, i == 0 ? EngineSlotA : EngineSlotB);

                if (voice.EngineVolumes[i] <= 0.001f && target <= 0f)
                {
                    m_Audio.StopLoop(channel);
                    if (!isActive)
                        voice.EngineSounds[i] = EAudioName.None;

                    continue;
                }

                m_Audio.SetLoopAt(channel, voice.EngineSounds[i], voice.EngineVolumes[i] * scale,
                    voice.EnginePitches[i], position);
            }
        }

        private void TickNitroLoop(int carIndex, CarVoice voice, float deltaTime, Vector3 position)
        {
            float rate = voice.NitroTarget > voice.NitroVolume ? 12f : 4f;
            voice.NitroVolume = Mathf.MoveTowards(voice.NitroVolume, voice.NitroTarget, rate * deltaTime);

            int channel = GetCarChannel(carIndex, NitroSlot);
            if (voice.NitroVolume <= 0.001f)
            {
                m_Audio.StopLoop(channel);
                return;
            }

            m_Audio.SetLoopAt(channel, EAudioName.NitroLoop, voice.NitroVolume * GetVolumeScale(carIndex),
                voice.NitroPitch, position);
        }

        private CarVoice GetVoice(int carIndex)
        {
            return carIndex >= 0 && carIndex < m_Voices.Length ? m_Voices[carIndex] : null;
        }

        private void OnBuffAccepted(BuffAccepted evt)
        {
            CarVoice voice = GetVoice(evt.CarIndex);
            NitroLevelEntry level = GetLevel(evt.Key);

            if (voice != null && m_Audio != null)
            {
                voice.NitroPitch = level.Pitch;
                voice.NitroTarget = level.Volume;
                m_Audio.PlayAt(EAudioName.NitroEntry, GetPosition(evt.CarIndex),
                    level.Volume * GetVolumeScale(evt.CarIndex), level.Pitch);
            }

            if (evt.CarIndex == m_PlayerCarIndex && m_Camera != null)
                m_Camera.Punch(evt.Key);
        }

        private void OnBuffExpired(BuffExpired evt)
        {
            CarVoice voice = GetVoice(evt.CarIndex);
            if (voice == null || m_Audio == null)
                return;

            NitroLevelEntry level = GetLevel(evt.Key);
            m_Audio.PlayAt(EAudioName.NitroExit, GetPosition(evt.CarIndex),
                level.Volume * GetVolumeScale(evt.CarIndex), level.Pitch);
            voice.NitroTarget = 0f;
        }

        private void OnBuffRejected(BuffRejected evt)
        {
            if (evt.CarIndex != m_PlayerCarIndex || evt.Reason == EBuffRejectReason.NotRacing || m_Audio == null)
                return;

            if (Time.time - m_LastRejectSoundTime < RejectSoundThrottle)
                return;

            m_LastRejectSoundTime = Time.time;
            m_Audio.Play(EAudioName.SfxFail);
        }

        private void OnCarFinished(CarFinished evt)
        {
            CarVoice voice = GetVoice(evt.CarIndex);
            if (voice != null)
                voice.NitroTarget = 0f;

            if (evt.CarIndex != m_PlayerCarIndex || m_Audio == null)
                return;

            m_Audio.Play(EAudioName.SfxComplete);
        }

        private NitroLevelEntry GetLevel(int key)
        {
            return m_Levels != null
                ? m_Levels.Get(key)
                : new NitroLevelEntry(key, Color.white, 1f, 1f, 0.2f, 1f, 1f, 0f);
        }
    }
}
