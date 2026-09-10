using System.Text;
using CasualKit.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MatchRacers
{
    public sealed class RaceHudView
    {
        private const float ToastDuration = 1.4f;
        private const float FlashDuration = 0.35f;

        private static readonly Color ColorInk = new Color(0.95f, 0.96f, 0.94f);
        private static readonly Color ColorMuted = new Color(0.68f, 0.71f, 0.69f);
        private static readonly Color ColorAccept = new Color(0.35f, 0.85f, 0.55f);
        private static readonly Color ColorReject = new Color(0.95f, 0.38f, 0.28f);
        private static readonly Color ColorEnergy = new Color(0.35f, 0.78f, 0.92f);
        private static readonly Color ColorProgress = new Color(0.95f, 0.62f, 0.25f);
        private static readonly Color ColorPanel = new Color(0.05f, 0.06f, 0.07f, 0.62f);

        private readonly RaceSimulation m_Simulation;
        private readonly RaceConfigSO m_Config;
        private readonly GameObject m_Root;
        private readonly StringBuilder m_Builder = new StringBuilder(256);

        private TextMeshProUGUI m_Mode;
        private TextMeshProUGUI m_Position;
        private TextMeshProUGUI m_Remaining;
        private Image m_ProgressFill;
        private TextMeshProUGUI m_Speed;
        private TextMeshProUGUI m_BuffState;
        private Image m_BuffFill;
        private Image m_EnergyFill;
        private TextMeshProUGUI m_EnergyLabel;
        private TextMeshProUGUI m_Countdown;
        private TextMeshProUGUI m_Toast;
        private Image m_Flash;
        private TextMeshProUGUI m_Results;

        private readonly TextMeshProUGUI[] m_KeyChips = new TextMeshProUGUI[BuffTableSO.MaxKey];
        private readonly Image[] m_KeyChipBacks = new Image[BuffTableSO.MaxKey];

        private float m_ToastRemaining;
        private float m_FlashRemaining;
        private Color m_FlashColor;

        public RaceHudView(Transform parent, RaceSimulation simulation, RaceConfigSO config)
        {
            m_Simulation = simulation;
            m_Config = config;

            RectTransform root = HudFactory.CreateRect("RaceHud", parent,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            m_Root = root.gameObject;

            BuildFlash(root);
            BuildTopLeft(root);
            BuildTopRight(root);
            BuildBottom(root);
            BuildCenter(root);
            BuildResults(root);

            EB.Gameplay.Add<BuffAccepted>(OnBuffAccepted);
            EB.Gameplay.Add<BuffRejected>(OnBuffRejected);
            EB.Gameplay.Add<BuffExpired>(OnBuffExpired);
        }

        public void Dispose()
        {
            EB.Gameplay.Remove<BuffAccepted>(OnBuffAccepted);
            EB.Gameplay.Remove<BuffRejected>(OnBuffRejected);
            EB.Gameplay.Remove<BuffExpired>(OnBuffExpired);

            if (m_Root != null)
                Object.Destroy(m_Root);
        }

        public void Tick(float deltaTime)
        {
            CarState player = m_Simulation.GetCar(m_Simulation.PlayerCarIndex);
            if (player == null)
                return;

            UpdateProgress(player);
            UpdateSpeedAndBuff(player);
            UpdateEnergy(player);
            UpdateCountdown();
            UpdateFeedback(deltaTime);
        }

        public void ShowResults()
        {
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
            m_Results.transform.parent.gameObject.SetActive(true);
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
            m_Remaining.text = remaining.ToString("0") + " m kaldı";
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
                m_BuffState.color = ColorAccept;
                m_BuffFill.fillAmount = Mathf.Clamp01(remaining / m_Config.BuffTable.WindowSeconds);
                return;
            }

            if (player.CooldownStepsRemaining > 0)
            {
                float cooldown = m_Simulation.Buffs.GetRemainingCooldownSeconds(player, dt);
                m_BuffState.text = "BEKLEME  " + cooldown.ToString("0.00") + " sn";
                m_BuffState.color = ColorReject;
                m_BuffFill.fillAmount = Mathf.Clamp01(cooldown / Mathf.Max(0.01f, m_Config.BuffTable.GlobalCooldownSeconds));
                return;
            }

            m_BuffState.text = "BUFF  hazır";
            m_BuffState.color = ColorMuted;
            m_BuffFill.fillAmount = 0f;
        }

        private void UpdateEnergy(CarState player)
        {
            float max = m_Config.BuffTable.EnergyMax;
            m_EnergyFill.fillAmount = max > 0f ? Mathf.Clamp01(player.Energy / max) : 0f;
            m_EnergyLabel.text = "ENERJİ  " + player.Energy.ToString("0") + " / " + max.ToString("0");

            bool blocked = player.HasActiveBuff || player.CooldownStepsRemaining > 0;
            for (int key = BuffTableSO.MinKey; key <= BuffTableSO.MaxKey; key++)
            {
                int slot = key - BuffTableSO.MinKey;
                m_Config.BuffTable.TryGetEnergyCost(key, out float cost);
                bool affordable = !blocked && player.Energy >= cost;

                m_KeyChips[slot].color = affordable ? ColorInk : new Color(0.45f, 0.47f, 0.46f);
                m_KeyChipBacks[slot].color = affordable
                    ? new Color(0.16f, 0.35f, 0.30f, 0.85f)
                    : new Color(0.10f, 0.11f, 0.12f, 0.7f);
            }
        }

        private void UpdateCountdown()
        {
            if (m_Simulation.State == ERaceState.Countdown)
            {
                float remaining = m_Simulation.CountdownRemaining;
                m_Countdown.text = Mathf.CeilToInt(remaining).ToString();
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

        private void UpdateFeedback(float deltaTime)
        {
            if (m_ToastRemaining > 0f)
            {
                m_ToastRemaining -= deltaTime;
                if (m_ToastRemaining <= 0f)
                    m_Toast.text = string.Empty;
            }

            if (m_FlashRemaining <= 0f)
            {
                m_Flash.color = new Color(0f, 0f, 0f, 0f);
                return;
            }

            m_FlashRemaining -= deltaTime;
            float alpha = Mathf.Clamp01(m_FlashRemaining / FlashDuration) * 0.28f;
            m_Flash.color = new Color(m_FlashColor.r, m_FlashColor.g, m_FlashColor.b, alpha);
        }

        private void OnBuffAccepted(BuffAccepted evt)
        {
            if (evt.CarIndex != m_Simulation.PlayerCarIndex)
                return;

            ShowToast("KABUL  ×" + evt.Key, ColorAccept);
        }

        private void OnBuffRejected(BuffRejected evt)
        {
            if (evt.CarIndex != m_Simulation.PlayerCarIndex)
                return;

            ShowToast("RET  ×" + evt.Key + "  —  " + DescribeReason(evt.Reason), ColorReject);
        }

        private void OnBuffExpired(BuffExpired evt)
        {
            if (evt.CarIndex != m_Simulation.PlayerCarIndex)
                return;

            ShowToast("BİTTİ  ×" + evt.Key, ColorMuted);
        }

        private static string DescribeReason(EBuffRejectReason reason)
        {
            switch (reason)
            {
                case EBuffRejectReason.InsufficientEnergy: return "enerji yetersiz";
                case EBuffRejectReason.BuffActive: return "buff zaten aktif";
                case EBuffRejectReason.Cooldown: return "bekleme süresi";
                case EBuffRejectReason.NotRacing: return "yarış başlamadı";
                case EBuffRejectReason.AlreadyFinished: return "yarış bitti";
                default: return "geçersiz";
            }
        }

        private void ShowToast(string message, Color color)
        {
            m_Toast.text = message;
            m_Toast.color = color;
            m_ToastRemaining = ToastDuration;
            m_FlashColor = color;
            m_FlashRemaining = FlashDuration;
        }

        private void BuildFlash(Transform root)
        {
            m_Flash = HudFactory.CreateImage("Flash", root, new Color(0f, 0f, 0f, 0f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }

        private void BuildTopLeft(Transform root)
        {
            RectTransform panel = HudFactory.CreateRect("TopLeft", root,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -132f), new Vector2(344f, -24f));
            HudFactory.CreateImage("Bg", panel, ColorPanel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            m_Mode = HudFactory.CreateText("Mode", panel, "SERBEST", 17f, ColorProgress,
                TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(16f, -28f), new Vector2(-16f, -6f));

            m_Position = HudFactory.CreateText("Position", panel, "1 / 8", 42f, ColorInk,
                TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(16f, -78f), new Vector2(-16f, -30f));

            m_ProgressFill = HudFactory.CreateBar("Progress", panel, ColorProgress,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(16f, 34f), new Vector2(-16f, 48f), out _);

            m_Remaining = HudFactory.CreateText("Remaining", panel, "1000 m kaldı", 20f, ColorMuted,
                TextAlignmentOptions.Left, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(16f, 8f), new Vector2(-16f, 30f));
        }

        private void BuildTopRight(Transform root)
        {
            RectTransform panel = HudFactory.CreateRect("TopRight", root,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-324f, -132f), new Vector2(-24f, -24f));
            HudFactory.CreateImage("Bg", panel, ColorPanel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            m_Speed = HudFactory.CreateText("Speed", panel, "12.0 m/sn", 38f, ColorInk,
                TextAlignmentOptions.Right, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(16f, -58f), new Vector2(-16f, -8f));

            m_BuffState = HudFactory.CreateText("BuffState", panel, "BUFF  hazır", 20f, ColorMuted,
                TextAlignmentOptions.Right, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(16f, 30f), new Vector2(-16f, 52f));

            m_BuffFill = HudFactory.CreateBar("BuffWindow", panel, ColorAccept,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(16f, 12f), new Vector2(-16f, 24f), out _);
            m_BuffFill.fillAmount = 0f;
        }

        private void BuildBottom(Transform root)
        {
            RectTransform panel = HudFactory.CreateRect("Bottom", root,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-290f, 24f), new Vector2(290f, 128f));
            HudFactory.CreateImage("Bg", panel, ColorPanel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            m_EnergyLabel = HudFactory.CreateText("EnergyLabel", panel, "ENERJİ  120 / 120", 18f, ColorMuted,
                TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(16f, -30f), new Vector2(-16f, -8f));

            m_EnergyFill = HudFactory.CreateBar("Energy", panel, ColorEnergy,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -50f), new Vector2(-16f, -32f), out _);

            for (int key = BuffTableSO.MinKey; key <= BuffTableSO.MaxKey; key++)
            {
                int slot = key - BuffTableSO.MinKey;
                float width = 104f;
                float x = 16f + slot * width;

                RectTransform chip = HudFactory.CreateRect("Key" + key, panel,
                    new Vector2(0f, 0f), new Vector2(0f, 0f),
                    new Vector2(x, 12f), new Vector2(x + width - 8f, 58f));

                m_KeyChipBacks[slot] = HudFactory.CreateImage("Bg", chip, new Color(0.10f, 0.11f, 0.12f, 0.7f),
                    Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

                m_Config.BuffTable.TryGetEnergyCost(key, out float cost);
                m_KeyChips[slot] = HudFactory.CreateText("Label", chip,
                    key + "\n<size=13>" + cost.ToString("0") + "</size>", 22f, ColorInk,
                    TextAlignmentOptions.Center, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                m_KeyChips[slot].richText = true;
            }
        }

        private void BuildCenter(Transform root)
        {
            m_Countdown = HudFactory.CreateText("Countdown", root, string.Empty, 120f, ColorInk,
                TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-200f, 20f), new Vector2(200f, 180f));
            m_Countdown.gameObject.SetActive(false);

            m_Toast = HudFactory.CreateText("Toast", root, string.Empty, 30f, ColorInk,
                TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-320f, -110f), new Vector2(320f, -50f));
        }

        private void BuildResults(Transform root)
        {
            RectTransform panel = HudFactory.CreateRect("Results", root,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-230f, -190f), new Vector2(230f, 190f));
            HudFactory.CreateImage("Bg", panel, new Color(0.04f, 0.05f, 0.06f, 0.9f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            m_Results = HudFactory.CreateText("Table", panel, string.Empty, 21f, ColorInk,
                TextAlignmentOptions.TopLeft, Vector2.zero, Vector2.one,
                new Vector2(22f, 18f), new Vector2(-22f, -18f));

            panel.gameObject.SetActive(false);
        }
    }
}
