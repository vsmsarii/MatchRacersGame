using CasualKit.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MatchRacers
{
    public sealed class RaceDebugOverlay : MonoBehaviour
    {
        private const float PanelWidth = 780f;
        private const float MessageHoldSeconds = 1.5f;

        private RaceSimulation m_Simulation;
        private RaceSession m_Session;
        private RaceRecorder m_Recorder;

        private GUIStyle m_RowStyle;
        private GUIStyle m_HeaderStyle;
        private string m_LastBuffMessage = "-";
        private float m_LastBuffMessageTime;
        private string m_Notice = string.Empty;
        private float m_NoticeTime;
        private bool m_Visible = true;

        public static RaceDebugOverlay Create(RaceSimulation simulation, RaceSession session, RaceRecorder recorder)
        {
            GameObject host = new GameObject("RaceDebugOverlay");
            RaceDebugOverlay overlay = host.AddComponent<RaceDebugOverlay>();
            overlay.m_Simulation = simulation;
            overlay.m_Session = session;
            overlay.m_Recorder = recorder;
            return overlay;
        }

        public void Dispose()
        {
            if (this != null)
                Destroy(gameObject);
        }

        private void OnEnable()
        {
            EB.Gameplay.Add<BuffAccepted>(OnBuffAccepted);
            EB.Gameplay.Add<BuffRejected>(OnBuffRejected);
        }

        private void OnDisable()
        {
            EB.Gameplay.Remove<BuffAccepted>(OnBuffAccepted);
            EB.Gameplay.Remove<BuffRejected>(OnBuffRejected);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.f1Key.wasPressedThisFrame)
                m_Visible = !m_Visible;

            if (keyboard.f2Key.wasPressedThisFrame && m_Session != null)
                m_Session.RestartWithSeed(m_Simulation.Seed);

            if (keyboard.f3Key.wasPressedThisFrame && m_Session != null)
                m_Session.RestartWithSeed(0u);

            if (keyboard.f4Key.wasPressedThisFrame && m_Recorder != null)
                ShowNotice("telemetry -> " + m_Recorder.Flush());
        }

        private void OnBuffAccepted(BuffAccepted evt)
        {
            if (evt.CarIndex != m_Simulation.PlayerCarIndex)
                return;

            m_LastBuffMessage = "ACCEPTED  key " + evt.Key;
            m_LastBuffMessageTime = Time.time;
        }

        private void OnBuffRejected(BuffRejected evt)
        {
            if (evt.CarIndex != m_Simulation.PlayerCarIndex)
                return;

            m_LastBuffMessage = "REJECTED  key " + evt.Key + "  (" + evt.Reason + ")";
            m_LastBuffMessageTime = Time.time;
        }

        private void ShowNotice(string text)
        {
            m_Notice = text;
            m_NoticeTime = Time.time;
        }

        private void OnGUI()
        {
            if (!m_Visible || m_Simulation == null)
                return;

            EnsureStyles();

            float dt = m_Simulation.Config.FixedDeltaTime;
            CarState player = m_Simulation.GetCar(m_Simulation.PlayerCarIndex);

            GUILayout.BeginArea(new Rect(12f, 12f, PanelWidth, 460f), GUI.skin.box);

            GUILayout.Label(
                $"mode {m_Simulation.Mode}" +
                (m_Simulation.Mode == ERaceMode.TargetOrder ? $"  target P{m_Simulation.TargetPosition}" : string.Empty) +
                $"    seed {m_Simulation.Seed}    t {m_Simulation.Time:0.00}s    {m_Simulation.State}" +
                (m_Simulation.State == ERaceState.Countdown ? $"  ({m_Simulation.CountdownRemaining:0.0})" : string.Empty),
                m_HeaderStyle);

            GUILayout.Label("pos car     dist      spd   dPlayer  energy  buff        bal    ai", m_HeaderStyle);

            for (int position = 1; position <= m_Simulation.CarCount; position++)
            {
                int carIndex = m_Simulation.GetCarAtPosition(position);
                CarState car = m_Simulation.GetCar(carIndex);
                if (car == null)
                    continue;

                if (car.Finished)
                {
                    GUILayout.Label(
                        $"{car.FinishOrder,3} {Name(car, carIndex),-6} FINISHED {car.FinishTime,7:0.00}s   " +
                        $"acc {car.AcceptedBuffCount,2}  rej {car.RejectedBuffCount,3}",
                        m_RowStyle);
                    continue;
                }

                string buff = car.HasActiveBuff
                    ? $"x{car.ActiveBuffKey} {m_Simulation.Buffs.GetRemainingWindowSeconds(car, dt):0.00}"
                    : (car.CooldownStepsRemaining > 0
                        ? $"cd {m_Simulation.Buffs.GetRemainingCooldownSeconds(car, dt):0.00}"
                        : "-");

                string ai = m_Simulation.GetAgent(carIndex) is AiAgent agent
                    ? agent.State.ToString()
                    : (car.IsPlayer ? "player" : "-");

                if (m_Simulation.Mode == ERaceMode.TargetOrder && !car.IsPlayer)
                {
                    ai = (m_Simulation.Director.IsAheadSide(carIndex) ? "AHEAD " : "BEHIND ")
                         + m_Simulation.Director.GetTargetGap(carIndex).ToString("+0;-0") + "m";
                }

                float delta = car.Distance - player.Distance;

                GUILayout.Label(
                    $"{position,3} {Name(car, carIndex),-6} {car.Distance,8:0.0} {car.Speed,6:0.0} {delta,8:+0.0;-0.0;0.0} " +
                    $"{car.Energy,6:0}  {buff,-11} {car.BalanceMultiplier:0.000} {ai}",
                    m_RowStyle);
            }

            GUILayout.Space(6f);
            bool fresh = Time.time - m_LastBuffMessageTime < MessageHoldSeconds;
            GUILayout.Label("last input: " + (fresh ? m_LastBuffMessage : "-"), m_HeaderStyle);

            if (m_Recorder != null)
                GUILayout.Label($"recorder: {m_Recorder.SampleCount} samples, {m_Recorder.TotalOvertakes} overtakes", m_RowStyle);

            GUILayout.Label("1-5 buff   F1 overlay   F2 same seed   F3 new seed   F4 write telemetry", m_RowStyle);

            if (!string.IsNullOrEmpty(m_Notice) && Time.time - m_NoticeTime < 6f)
                GUILayout.Label(m_Notice, m_HeaderStyle);

            GUILayout.EndArea();
        }

        private static string Name(CarState car, int carIndex)
        {
            return car.IsPlayer ? "YOU" : "AI" + carIndex;
        }

        private void EnsureStyles()
        {
            if (m_RowStyle != null)
                return;

            Font mono = Font.CreateDynamicFontFromOSFont("Courier New", 13);
            m_RowStyle = new GUIStyle(GUI.skin.label) { font = mono, fontSize = 13, richText = false };
            m_HeaderStyle = new GUIStyle(m_RowStyle) { fontStyle = FontStyle.Bold };
        }
    }
}
