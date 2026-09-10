using CasualKit.Core;
using UnityEngine;

namespace MatchRacers
{
    public sealed class RaceFeedback
    {
        private readonly IAudioService m_Audio;
        private readonly RaceCameraRig m_Camera;
        private readonly RaceAudioPlayer m_Race;
        private readonly int m_PlayerCarIndex;

        private const float RejectSoundThrottle = 0.18f;
        private float m_LastRejectSoundTime = float.NegativeInfinity;

        public RaceFeedback(IAudioService audio, RaceCameraRig camera, RaceAudioPlayer raceAudio, int playerCarIndex)
        {
            m_Audio = audio;
            m_Camera = camera;
            m_Race = raceAudio;
            m_PlayerCarIndex = playerCarIndex;

            EB.Gameplay.Add<BuffAccepted>(OnBuffAccepted);
            EB.Gameplay.Add<BuffRejected>(OnBuffRejected);
            EB.Gameplay.Add<BuffExpired>(OnBuffExpired);
            EB.Gameplay.Add<OvertakeOccurred>(OnOvertake);
            EB.Gameplay.Add<CarFinished>(OnCarFinished);
        }

        public void Tick(float deltaTime)
        {
            if (m_Race != null)
                m_Race.Tick(deltaTime);
        }

        private void OnBuffExpired(BuffExpired evt)
        {
            if (evt.CarIndex != m_PlayerCarIndex || m_Race == null)
                return;

            m_Race.PlayNitroExit(evt.Key);
        }

        private void OnOvertake(OvertakeOccurred evt)
        {
            if (m_Race == null)
                return;

            if (evt.PassingCarIndex == m_PlayerCarIndex || evt.PassedCarIndex == m_PlayerCarIndex)
                m_Race.Play(ERaceSound.Overtake);
        }

        public void Dispose()
        {
            EB.Gameplay.Remove<BuffAccepted>(OnBuffAccepted);
            EB.Gameplay.Remove<BuffRejected>(OnBuffRejected);
            EB.Gameplay.Remove<BuffExpired>(OnBuffExpired);
            EB.Gameplay.Remove<OvertakeOccurred>(OnOvertake);
            EB.Gameplay.Remove<CarFinished>(OnCarFinished);
        }

        private void OnBuffAccepted(BuffAccepted evt)
        {
            if (evt.CarIndex != m_PlayerCarIndex)
                return;

            if (m_Race != null)
                m_Race.PlayNitroEntry(evt.Key);
            else
                m_Audio.Play(EAudioName.SfxUiClick);

            if (m_Camera != null)
                m_Camera.Punch(evt.Key);
        }

        private void OnBuffRejected(BuffRejected evt)
        {
            if (evt.CarIndex != m_PlayerCarIndex || evt.Reason == EBuffRejectReason.NotRacing)
                return;

            if (Time.time - m_LastRejectSoundTime < RejectSoundThrottle)
                return;

            m_LastRejectSoundTime = Time.time;

            if (m_Race != null)
                m_Race.Play(ERaceSound.BuffRejected);
            else
                m_Audio.Play(EAudioName.SfxFail);
        }

        private void OnCarFinished(CarFinished evt)
        {
            if (evt.CarIndex != m_PlayerCarIndex)
                return;

            if (m_Race != null)
                m_Race.Play(ERaceSound.RaceFinish);
            else
                m_Audio.Play(EAudioName.SfxComplete);
        }
    }
}
