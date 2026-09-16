using System.Globalization;
using System.Text;
using CasualKit.Core;
using TMPro;
using UnityEngine;

namespace MatchRacers
{
    public sealed class RaceDebugPanel : MonoBehaviour
    {
        private const float MessageHoldSeconds = 1.5f;
        private const float NoticeHoldSeconds = 6f;

        [Header("Layout")]
        [SerializeField] private GameObject m_Content;
        [SerializeField] private TMP_Text m_Header;
        [SerializeField] private TMP_Text m_Table;
        [SerializeField] private TMP_Text m_Footer;

        [Header("Behaviour")]
        [SerializeField] private bool m_VisibleOnStart;
        [SerializeField, Min(0f)] private float m_RefreshInterval = 0.1f;
        [SerializeField, Range(0.4f, 1f)] private float m_MonospaceEm = 0.6f;

        private readonly StringBuilder m_Builder = new StringBuilder(2048);

        private RaceSimulation m_Simulation;
        private RaceRecorder m_Recorder;
        private bool m_Subscribed;
        private bool m_Visible;
        private string m_LastInput = "-";
        private float m_LastInputTime = float.NegativeInfinity;
        private string m_Notice = string.Empty;
        private float m_NoticeTime = float.NegativeInfinity;
        private float m_RefreshTimer;

        public bool IsVisible => m_Visible;

        public void Bind(RaceSimulation simulation, RaceRecorder recorder)
        {
            Unbind();

            m_Simulation = simulation;
            m_Recorder = recorder;

            EB.Gameplay.Add<BuffAccepted>(OnBuffAccepted);
            EB.Gameplay.Add<BuffRejected>(OnBuffRejected);
            m_Subscribed = true;

            m_Visible = m_VisibleOnStart;
            m_RefreshTimer = 0f;
            ApplyVisibility();
            gameObject.SetActive(true);
        }

        public void Unbind()
        {
            if (m_Subscribed)
            {
                EB.Gameplay.Remove<BuffAccepted>(OnBuffAccepted);
                EB.Gameplay.Remove<BuffRejected>(OnBuffRejected);
                m_Subscribed = false;
            }

            m_Simulation = null;
            m_Recorder = null;
        }

        public void SetRecorder(RaceRecorder recorder)
        {
            m_Recorder = recorder;
        }

        public void Toggle()
        {
            m_Visible = !m_Visible;
            m_RefreshTimer = 0f;
            ApplyVisibility();
        }

        public void ShowNotice(string text)
        {
            m_Notice = text;
            m_NoticeTime = Time.unscaledTime;
            m_RefreshTimer = 0f;
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (!m_Visible || m_Simulation == null)
                return;

            m_RefreshTimer -= unscaledDeltaTime;
            if (m_RefreshTimer > 0f)
                return;

            m_RefreshTimer = m_RefreshInterval;
            Refresh();
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private void ApplyVisibility()
        {
            if (m_Content != null)
                m_Content.SetActive(m_Visible);
        }

        private void OnBuffAccepted(BuffAccepted evt)
        {
            if (m_Simulation == null || evt.CarIndex != m_Simulation.PlayerCarIndex)
                return;

            m_LastInput = "ACCEPTED  key " + evt.Key;
            m_LastInputTime = Time.unscaledTime;
        }

        private void OnBuffRejected(BuffRejected evt)
        {
            if (m_Simulation == null || evt.CarIndex != m_Simulation.PlayerCarIndex)
                return;

            m_LastInput = "REJECTED  key " + evt.Key + "  (" + evt.Reason + ")";
            m_LastInputTime = Time.unscaledTime;
        }

        private void Refresh()
        {
            if (m_Header != null)
                m_Header.text = BuildHeader();

            if (m_Table != null)
                m_Table.text = BuildTable();

            if (m_Footer != null)
                m_Footer.text = BuildFooter();
        }

        private string BuildHeader()
        {
            m_Builder.Clear();
            m_Builder.Append("mode ").Append(m_Simulation.Mode);

            if (m_Simulation.Mode == ERaceMode.TargetOrder)
                m_Builder.Append("  target P").Append(m_Simulation.TargetPosition);

            m_Builder.Append("    seed ").Append(m_Simulation.Seed)
                .Append("    t ").Append(m_Simulation.Time.ToString("0.00", CultureInfo.InvariantCulture)).Append("s    ")
                .Append(m_Simulation.State);

            if (m_Simulation.State == ERaceState.Countdown)
                m_Builder.Append("  (").Append(m_Simulation.CountdownRemaining.ToString("0.0", CultureInfo.InvariantCulture)).Append(')');

            return m_Builder.ToString();
        }

        private string BuildTable()
        {
            float dt = m_Simulation.Config.FixedDeltaTime;
            CarState player = m_Simulation.GetCar(m_Simulation.PlayerCarIndex);

            m_Builder.Clear();
            m_Builder.Append("<mspace=").Append(m_MonospaceEm.ToString("0.00", CultureInfo.InvariantCulture)).Append("em>");
            m_Builder.AppendLine("pos car     dist      spd   dPlayer  energy  buff        bal    ai");

            for (int position = 1; position <= m_Simulation.CarCount; position++)
            {
                int carIndex = m_Simulation.GetCarAtPosition(position);
                CarState car = m_Simulation.GetCar(carIndex);
                if (car == null)
                    continue;

                if (car.Finished)
                {
                    m_Builder.AppendLine(
                        $"{car.FinishOrder,3} {Name(car, carIndex),-6} FINISHED {car.FinishTime,7:0.00}s   " +
                        $"acc {car.AcceptedBuffCount,2}  rej {car.RejectedBuffCount,3}");
                    continue;
                }

                string buff = car.HasActiveBuff
                    ? $"x{car.ActiveBuffKey} {m_Simulation.Buffs.GetRemainingWindowSeconds(car, dt):0.00}"
                    : (car.CooldownStepsRemaining > 0
                        ? $"cd {m_Simulation.Buffs.GetRemainingCooldownSeconds(car, dt):0.00}"
                        : "-");

                float delta = car.Distance - player.Distance;

                m_Builder.AppendLine(
                    $"{position,3} {Name(car, carIndex),-6} {car.Distance,8:0.0} {car.Speed,6:0.0} {delta,8:+0.0;-0.0;0.0} " +
                    $"{car.Energy,6:0}  {buff,-11} {car.BalanceMultiplier:0.000} {DescribeAgent(car, carIndex)}");
            }

            m_Builder.Append("</mspace>");
            return m_Builder.ToString();
        }

        private string DescribeAgent(CarState car, int carIndex)
        {
            string ai = m_Simulation.GetAgent(carIndex) is AiAgent agent
                ? agent.State.ToString()
                : (car.IsPlayer ? "player" : "-");

            if (m_Simulation.Mode != ERaceMode.TargetOrder || car.IsPlayer)
                return ai;

            ai = (m_Simulation.Director.IsAheadSide(carIndex) ? "AHEAD " : "BEHIND ")
                 + m_Simulation.Director.GetTargetGap(carIndex).ToString("+0;-0") + "m";

            if (car.PaceIntervention > 0)
                ai += car.PaceIntervention > 1 ? " PUSH!" : " PUSH";
            else if (car.PaceIntervention < 0)
                ai += car.PaceIntervention < -1 ? " HOLD!" : " HOLD";
            else if (car.MaxBuffKey < BuffTableSO.MaxKey)
                ai += " cap" + car.MaxBuffKey;

            ETargetChallengePhase phase = m_Simulation.Director.GetChallengePhase(carIndex);
            if (phase == ETargetChallengePhase.Attack)
                ai += " ATK";
            else if (phase == ETargetChallengePhase.Lead)
                ai += " LEAD";
            else if (phase == ETargetChallengePhase.Fallback)
                ai += " FALL";
            else if (phase == ETargetChallengePhase.Return)
                ai += " BACK";

            if (car.NearGateWeight > 0.5f)
                ai += " NEAR";

            return ai;
        }

        private string BuildFooter()
        {
            bool fresh = Time.unscaledTime - m_LastInputTime < MessageHoldSeconds;

            m_Builder.Clear();
            m_Builder.Append("last input: ").AppendLine(fresh ? m_LastInput : "-");

            if (m_Recorder != null)
                m_Builder.Append("recorder: ").Append(m_Recorder.SampleCount).Append(" samples, ")
                    .Append(m_Recorder.TotalOvertakes).AppendLine(" overtakes");

            m_Builder.AppendLine("1-5 buff   F1 overlay   F2 same seed   F3 new seed   F4 write telemetry");

            if (!string.IsNullOrEmpty(m_Notice) && Time.unscaledTime - m_NoticeTime < NoticeHoldSeconds)
                m_Builder.Append(m_Notice);

            return m_Builder.ToString();
        }

        private static string Name(CarState car, int carIndex)
        {
            return car.IsPlayer ? "YOU" : "AI" + carIndex;
        }
    }
}
