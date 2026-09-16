using System.Text;
using CasualKit.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MatchRacers
{
    public sealed class RaceHudView : MonoBehaviour
    {
        [Header("Top Left")]
        [SerializeField] private TMP_Text m_Mode;
        [SerializeField] private TMP_Text m_Position;
        [SerializeField] private TMP_Text m_Remaining;
        [SerializeField] private Image m_ProgressFill;

        [Header("Top Right")]
        [SerializeField] private TMP_Text m_Speed;
        [SerializeField] private TMP_Text m_BuffState;
        [SerializeField] private Image m_BuffFill;

        [Header("Energy And Keys")]
        [SerializeField] private TMP_Text m_EnergyLabel;
        [SerializeField] private Image m_EnergyFill;
        [SerializeField] private TMP_Text[] m_KeyLabels = new TMP_Text[BuffTableSO.MaxKey];
        [SerializeField] private Image[] m_KeyBacks = new Image[BuffTableSO.MaxKey];
        [SerializeField] private Button[] m_KeyButtons = new Button[BuffTableSO.MaxKey];

        [Header("Center")]
        [SerializeField] private TMP_Text m_Countdown;

        [Header("Results")]
        [SerializeField] private GameObject m_ResultsPanel;
        [SerializeField] private TMP_Text m_Results;
        [SerializeField] private Button m_FinishButton;

        [Header("Colors")]
        [SerializeField] private Color m_Ink = new Color(0.95f, 0.96f, 0.94f);
        [SerializeField] private Color m_Muted = new Color(0.68f, 0.71f, 0.69f);
        [SerializeField] private Color m_Accept = new Color(0.35f, 0.85f, 0.55f);
        [SerializeField] private Color m_Reject = new Color(0.95f, 0.38f, 0.28f);
        [SerializeField] private Color m_KeyDisabledText = new Color(0.45f, 0.47f, 0.46f);
        [SerializeField] private Color m_KeyReadyBack = new Color(0.16f, 0.35f, 0.30f, 0.85f);
        [SerializeField] private Color m_KeyIdleBack = new Color(0.10f, 0.11f, 0.12f, 0.7f);
        [SerializeField] private Color m_KeyCooldownBack = new Color(0.30f, 0.13f, 0.11f, 0.8f);

        private readonly StringBuilder m_Builder = new StringBuilder(256);

        private RaceSimulation m_Simulation;
        private RaceConfigSO m_Config;
        private bool m_KeyButtonsWired;
        private bool m_FinishButtonWired;

        public void Bind(RaceSimulation simulation, RaceConfigSO config)
        {
            Unbind();

            m_Simulation = simulation;
            m_Config = config;

            WireKeyButtons();
            WireFinishButton();
            ResetVisuals();
            gameObject.SetActive(true);
        }

        private void WireFinishButton()
        {
            if (m_FinishButtonWired || m_FinishButton == null)
                return;

            m_FinishButtonWired = true;
            m_FinishButton.onClick.AddListener(() => EB.Gameplay.Invoke(new RaceRestartRequested()));
        }

        private void WireKeyButtons()
        {
            if (m_KeyButtonsWired)
                return;

            m_KeyButtonsWired = true;

            for (int slot = 0; slot < BuffTableSO.MaxKey; slot++)
            {
                Button button = ResolveKeyButton(slot);
                if (button == null)
                    continue;

                Image back = slot < m_KeyBacks.Length ? m_KeyBacks[slot] : null;
                if (button.targetGraphic == null && back != null)
                    button.targetGraphic = back;

                if (button.targetGraphic != null)
                    button.targetGraphic.raycastTarget = true;

                int key = slot + BuffTableSO.MinKey;
                button.onClick.AddListener(() => EB.Gameplay.Invoke(new PlayerBuffRequested(key)));
            }
        }

        private Button ResolveKeyButton(int slot)
        {
            if (m_KeyButtons != null && slot < m_KeyButtons.Length && m_KeyButtons[slot] != null)
                return m_KeyButtons[slot];

            if (m_KeyBacks != null && slot < m_KeyBacks.Length && m_KeyBacks[slot] != null)
                return m_KeyBacks[slot].GetComponentInParent<Button>(true);

            return null;
        }

        public void Unbind()
        {
            m_Simulation = null;
            m_Config = null;
        }

        private void OnDestroy()
        {
            Unbind();
        }

        public void Tick(float deltaTime)
        {
            if (m_Simulation == null)
                return;

            CarState player = m_Simulation.GetCar(m_Simulation.PlayerCarIndex);
            if (player == null)
                return;

            UpdateProgress(player);
            UpdateSpeedAndBuff(player);
            UpdateEnergy(player);
            UpdateCountdown();
        }

        public void ShowResults()
        {
            if (m_Simulation == null || m_Results == null)
                return;

            m_Builder.Clear();

            if (m_Simulation.Mode == ERaceMode.TargetOrder)
            {
                m_Builder.Append("HEDEF ").Append(m_Simulation.TargetPosition)
                    .Append("  ·  ").AppendLine(m_Simulation.TargetMatched ? "TUTTU" : "TUTMADI");
            }
            else
            {
                m_Builder.AppendLine("SERBEST MOD");
            }

            m_Builder.AppendLine("YARIŞ SONUCU");

            for (int position = 1; position <= m_Simulation.CarCount; position++)
            {
                int carIndex = m_Simulation.GetCarAtPosition(position);
                CarState car = m_Simulation.GetCar(carIndex);
                if (car == null)
                    continue;

                m_Builder.Append(position).Append(".  ")
                    .Append(car.IsPlayer ? "SEN" : "AI " + carIndex)
                    .Append("   ").Append(car.FinishTime.ToString("0.00")).Append(" sn");

                if (position > 1)
                {
                    CarState leader = m_Simulation.GetCar(m_Simulation.GetCarAtPosition(1));
                    if (leader != null)
                        m_Builder.Append("   +").Append((car.FinishTime - leader.FinishTime).ToString("0.00"));
                }

                m_Builder.AppendLine();
            }

            m_Results.text = m_Builder.ToString();

            if (m_ResultsPanel != null)
                m_ResultsPanel.SetActive(true);
        }

        private void ResetVisuals()
        {
            if (m_Countdown != null)
                m_Countdown.gameObject.SetActive(false);

            if (m_ResultsPanel != null)
                m_ResultsPanel.SetActive(false);

            if (m_BuffFill != null)
                m_BuffFill.fillAmount = 0f;
        }

        private void UpdateProgress(CarState player)
        {
            int position = m_Simulation.GetPosition(m_Simulation.PlayerCarIndex);
            m_Position.text = position + " / " + m_Simulation.CarCount;

            m_Mode.text = m_Simulation.Mode == ERaceMode.TargetOrder
                ? "HEDEF SIRA  ·  " + m_Simulation.TargetPosition + "."
                : "SERBEST MOD";

            float length = m_Config.RaceLengthMeters;
            float remaining = Mathf.Max(0f, length - player.Distance);
            m_Remaining.text = remaining.ToString("0") + " metre";
            m_ProgressFill.fillAmount = Mathf.Clamp01(player.Distance / length);
        }

        private void UpdateSpeedAndBuff(CarState player)
        {
            m_Speed.text = player.Speed.ToString("0.0") + " m/sn";

            float dt = m_Config.FixedDeltaTime;
            if (player.HasActiveBuff)
            {
                float remaining = m_Simulation.Buffs.GetRemainingWindowSeconds(player, dt);
                m_BuffState.text = "BUFF  ×" + player.ActiveBuffKey + "   " + remaining.ToString("0.00") + " sn";
                m_BuffState.color = m_Accept;
                m_BuffFill.fillAmount = Mathf.Clamp01(remaining / m_Config.BuffTable.WindowSeconds);
                return;
            }

            if (player.CooldownStepsRemaining > 0)
            {
                float cooldown = m_Simulation.Buffs.GetRemainingCooldownSeconds(player, dt);
                m_BuffState.text = "BEKLEME  " + cooldown.ToString("0.00") + " sn";
                m_BuffState.color = m_Reject;
                m_BuffFill.fillAmount = Mathf.Clamp01(cooldown / Mathf.Max(0.01f, m_Config.BuffTable.GlobalCooldownSeconds));
                return;
            }

            m_BuffState.text = "BUFF  hazır";
            m_BuffState.color = m_Muted;
            m_BuffFill.fillAmount = 0f;
        }

        private void UpdateEnergy(CarState player)
        {
            float max = m_Config.BuffTable.EnergyMax;
            m_EnergyFill.fillAmount = max > 0f ? Mathf.Clamp01(player.Energy / max) : 0f;
            m_EnergyLabel.text = "ENERJİ  " + player.Energy.ToString("0") + " / " + max.ToString("0");

            bool blocked = player.HasActiveBuff || player.CooldownStepsRemaining > 0;
            float dt = m_Config.FixedDeltaTime;

            for (int key = BuffTableSO.MinKey; key <= BuffTableSO.MaxKey; key++)
            {
                int slot = key - BuffTableSO.MinKey;
                if (slot >= m_KeyLabels.Length || slot >= m_KeyBacks.Length || m_KeyLabels[slot] == null)
                    continue;

                m_Config.BuffTable.TryGetEnergyCost(key, out float cost);

                float keyCooldown = m_Simulation.Buffs.GetRemainingKeyCooldownSeconds(player, key, dt);
                bool onCooldown = keyCooldown > 0f;
                bool ready = !blocked && !onCooldown && player.Energy >= cost;

                m_KeyLabels[slot].text = onCooldown
                    ? key + "\n<size=13>" + keyCooldown.ToString("0.0") + "</size>"
                    : key + "\n<size=13>" + cost.ToString("0") + "</size>";
                m_KeyLabels[slot].color = ready ? m_Ink : m_KeyDisabledText;

                if (m_KeyBacks[slot] != null)
                    m_KeyBacks[slot].color = onCooldown ? m_KeyCooldownBack : ready ? m_KeyReadyBack : m_KeyIdleBack;
            }
        }

        private void UpdateCountdown()
        {
            if (m_Simulation.State == ERaceState.Countdown)
            {
                m_Countdown.text = Mathf.CeilToInt(m_Simulation.CountdownRemaining).ToString();
                m_Countdown.gameObject.SetActive(true);
                return;
            }

            if (m_Simulation.State == ERaceState.Racing && m_Simulation.Time < m_Config.CountdownSeconds + 0.8f)
            {
                m_Countdown.text = "BAŞLA";
                m_Countdown.gameObject.SetActive(true);
                return;
            }

            m_Countdown.gameObject.SetActive(false);
        }
    }
}
