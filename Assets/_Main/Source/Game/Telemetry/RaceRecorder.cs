using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using CasualKit.Core;
using UnityEngine;

namespace MatchRacers
{
    public sealed class RaceRecorder
    {
        private const string FolderName = "RaceRuns";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private readonly RaceSimulation m_Simulation;
        private readonly RaceConfigSO m_Config;
        private readonly string m_ScenarioName;
        private readonly float m_SampleInterval;

        private readonly StringBuilder m_Samples = new StringBuilder(1 << 16);
        private readonly StringBuilder m_Events = new StringBuilder(1 << 12);
        private readonly List<float> m_BalanceSum = new List<float>();

        private float m_NextSampleTime;
        private int m_SampleCount;
        private int m_TotalOvertakes;
        private int m_PlayerOvertakes;
        private float m_LastSegmentMinGap = float.MaxValue;
        private float m_LastSegmentMaxGap;

        public string OutputDirectory { get; }
        public RacePerformanceProbe Performance { get; set; }
        public int SampleCount => m_SampleCount;
        public int TotalOvertakes => m_TotalOvertakes;
        public int PlayerOvertakes => m_PlayerOvertakes;
        public float FinalSegmentMinGap => m_LastSegmentMinGap == float.MaxValue ? 0f : m_LastSegmentMinGap;
        public float FinalSegmentMaxGap => m_LastSegmentMaxGap;

        public RaceRecorder(RaceSimulation simulation, RaceConfigSO config, string scenarioName, float sampleHz)
        {
            m_Simulation = simulation;
            m_Config = config;
            m_ScenarioName = string.IsNullOrEmpty(scenarioName) ? "live" : scenarioName;
            m_SampleInterval = sampleHz > 0f ? 1f / sampleHz : 0.1f;

            OutputDirectory = Application.isEditor
                ? Path.Combine(Directory.GetParent(Application.dataPath).FullName, FolderName)
                : Path.Combine(Application.persistentDataPath, FolderName);

            for (int i = 0; i < simulation.CarCount; i++)
                m_BalanceSum.Add(0f);

            WriteSampleHeader();
            m_Events.AppendLine("time,carIndex,event,key,detail");

            EB.Gameplay.Add<BuffAccepted>(OnBuffAccepted);
            EB.Gameplay.Add<BuffRejected>(OnBuffRejected);
            EB.Gameplay.Add<BuffExpired>(OnBuffExpired);
            EB.Gameplay.Add<OvertakeOccurred>(OnOvertake);
            EB.Gameplay.Add<CarFinished>(OnCarFinished);
            EB.Gameplay.Add<RaceStateChanged>(OnStateChanged);
        }

        public void Dispose()
        {
            EB.Gameplay.Remove<BuffAccepted>(OnBuffAccepted);
            EB.Gameplay.Remove<BuffRejected>(OnBuffRejected);
            EB.Gameplay.Remove<BuffExpired>(OnBuffExpired);
            EB.Gameplay.Remove<OvertakeOccurred>(OnOvertake);
            EB.Gameplay.Remove<CarFinished>(OnCarFinished);
            EB.Gameplay.Remove<RaceStateChanged>(OnStateChanged);
        }

        public void Sample()
        {
            if (m_Simulation.State == ERaceState.Idle)
                return;

            while (m_Simulation.Time >= m_NextSampleTime)
            {
                WriteSampleRow();
                m_NextSampleTime += m_SampleInterval;
            }
        }

        private void WriteSampleHeader()
        {
            m_Samples.Append("time,state,packSpread,playerPosition,playerGapAhead,playerGapBehind,playerGapToLeader");
            for (int i = 0; i < m_Simulation.CarCount; i++)
            {
                m_Samples.Append(",car").Append(i).Append("_dist")
                    .Append(",car").Append(i).Append("_speed")
                    .Append(",car").Append(i).Append("_buff")
                    .Append(",car").Append(i).Append("_energy")
                    .Append(",car").Append(i).Append("_balance")
                    .Append(",car").Append(i).Append("_position");
            }

            m_Samples.AppendLine();
        }

        private void WriteSampleRow()
        {
            int playerIndex = m_Simulation.PlayerCarIndex;
            CarState player = m_Simulation.GetCar(playerIndex);

            float leaderDistance = float.MinValue;
            float lastDistance = float.MaxValue;
            for (int i = 0; i < m_Simulation.CarCount; i++)
            {
                float d = m_Simulation.GetCar(i).Distance;
                if (d > leaderDistance) leaderDistance = d;
                if (d < lastDistance) lastDistance = d;
            }

            m_Simulation.TryGetCarAhead(playerIndex, out _, out float gapAhead);
            m_Simulation.TryGetCarBehind(playerIndex, out _, out float gapBehind);

            if (player.Distance >= m_Config.RaceLengthMeters * 0.8f && !player.Finished)
            {
                float nearest = Mathf.Min(gapAhead > 0f ? gapAhead : float.MaxValue,
                                          gapBehind > 0f ? gapBehind : float.MaxValue);
                if (nearest < m_LastSegmentMinGap) m_LastSegmentMinGap = nearest;
                if (nearest > m_LastSegmentMaxGap && nearest < float.MaxValue) m_LastSegmentMaxGap = nearest;
            }

            m_Samples.Append(F(m_Simulation.Time)).Append(',')
                .Append(m_Simulation.State).Append(',')
                .Append(F(leaderDistance - lastDistance)).Append(',')
                .Append(m_Simulation.GetPosition(playerIndex)).Append(',')
                .Append(F(gapAhead)).Append(',')
                .Append(F(gapBehind)).Append(',')
                .Append(F(leaderDistance - player.Distance));

            for (int i = 0; i < m_Simulation.CarCount; i++)
            {
                CarState car = m_Simulation.GetCar(i);
                m_BalanceSum[i] += car.BalanceMultiplier;

                m_Samples.Append(',').Append(F(car.Distance))
                    .Append(',').Append(F(car.Speed))
                    .Append(',').Append(car.ActiveBuffKey)
                    .Append(',').Append(F(car.Energy))
                    .Append(',').Append(car.BalanceMultiplier.ToString("0.0000", Inv))
                    .Append(',').Append(m_Simulation.GetPosition(i));
            }

            m_Samples.AppendLine();
            m_SampleCount++;
        }

        private void OnBuffAccepted(BuffAccepted evt) => LogEvent(evt.Time, evt.CarIndex, "accepted", evt.Key, string.Empty);
        private void OnBuffExpired(BuffExpired evt) => LogEvent(evt.Time, evt.CarIndex, "expired", evt.Key, string.Empty);
        private void OnBuffRejected(BuffRejected evt) => LogEvent(evt.Time, evt.CarIndex, "rejected", evt.Key, evt.Reason.ToString());

        private void OnCarFinished(CarFinished evt) =>
            LogEvent(evt.FinishTime, evt.CarIndex, "finished", 0, "order=" + evt.FinishOrder);

        private void OnStateChanged(RaceStateChanged evt) =>
            LogEvent(evt.Time, -1, "state", 0, evt.State.ToString());

        private void OnOvertake(OvertakeOccurred evt)
        {
            m_TotalOvertakes++;
            if (evt.PassingCarIndex == m_Simulation.PlayerCarIndex || evt.PassedCarIndex == m_Simulation.PlayerCarIndex)
                m_PlayerOvertakes++;

            LogEvent(evt.Time, evt.PassingCarIndex, "overtake", 0, "passed=" + evt.PassedCarIndex);
        }

        private void LogEvent(float time, int carIndex, string type, int key, string detail)
        {
            m_Events.Append(F(time)).Append(',').Append(carIndex).Append(',')
                .Append(type).Append(',').Append(key).Append(',').Append(detail).AppendLine();
        }

        public string Flush()
        {
            Directory.CreateDirectory(OutputDirectory);

            string stem = $"run_{m_Simulation.Seed}_{m_ScenarioName}";
            File.WriteAllText(Path.Combine(OutputDirectory, stem + "_samples.csv"), m_Samples.ToString());
            File.WriteAllText(Path.Combine(OutputDirectory, stem + "_events.csv"), m_Events.ToString());
            File.WriteAllText(Path.Combine(OutputDirectory, stem + "_summary.json"), BuildSummaryJson());

            if (Performance != null && Performance.SampleCount > 0)
                File.WriteAllText(Path.Combine(OutputDirectory, stem + "_performance.csv"), Performance.BuildCsv());

            EditorLog.Info("Race telemetry written to " + OutputDirectory + " (" + stem + ")");
            return OutputDirectory;
        }

        private string BuildSummaryJson()
        {
            CarState player = m_Simulation.GetCar(m_Simulation.PlayerCarIndex);
            CarState winner = m_Simulation.GetCar(m_Simulation.GetCarAtPosition(1));
            float divisor = m_SampleCount > 0 ? m_SampleCount : 1f;

            StringBuilder json = new StringBuilder(2048);
            json.AppendLine("{");
            json.Append("  \"seed\": ").Append(m_Simulation.Seed).AppendLine(",");
            json.Append("  \"scenario\": \"").Append(m_ScenarioName).AppendLine("\",");
            json.Append("  \"raceLengthMeters\": ").Append(F(m_Config.RaceLengthMeters)).AppendLine(",");
            json.Append("  \"baseSpeed\": ").Append(F(m_Config.BaseSpeed)).AppendLine(",");
            json.Append("  \"fixedStepHz\": ").Append(m_Config.FixedStepHz).AppendLine(",");
            json.Append("  \"balanceEnabled\": ").Append(m_Config.BalanceEnabled ? "true" : "false").AppendLine(",");
            json.Append("  \"mode\": \"").Append(m_Simulation.Mode).AppendLine("\",");
            json.Append("  \"targetPosition\": ")
                .Append(m_Simulation.Mode == ERaceMode.TargetOrder ? m_Simulation.TargetPosition : 0).AppendLine(",");
            json.Append("  \"targetMatched\": ")
                .Append(m_Simulation.Mode == ERaceMode.TargetOrder
                    ? (m_Simulation.TargetMatched ? "true" : "false")
                    : "null").AppendLine(",");
            json.Append("  \"sampleCount\": ").Append(m_SampleCount).AppendLine(",");
            json.Append("  \"totalOvertakes\": ").Append(m_TotalOvertakes).AppendLine(",");
            json.Append("  \"playerOvertakes\": ").Append(m_PlayerOvertakes).AppendLine(",");
            json.Append("  \"playerFinishOrder\": ").Append(player.FinishOrder).AppendLine(",");
            json.Append("  \"playerFinishTime\": ").Append(F(player.FinishTime)).AppendLine(",");
            json.Append("  \"playerAccepted\": ").Append(player.AcceptedBuffCount).AppendLine(",");
            json.Append("  \"playerRejected\": ").Append(player.RejectedBuffCount).AppendLine(",");
            json.Append("  \"playerSpentEnergy\": ").Append(F(player.SpentEnergy)).AppendLine(",");
            json.Append("  \"finalTwentyPercentMinGap\": ")
                .Append(F(m_LastSegmentMinGap == float.MaxValue ? 0f : m_LastSegmentMinGap)).AppendLine(",");
            json.Append("  \"finalTwentyPercentMaxGap\": ").Append(F(m_LastSegmentMaxGap)).AppendLine(",");
            json.AppendLine("  \"cars\": [");

            for (int i = 0; i < m_Simulation.CarCount; i++)
            {
                CarState car = m_Simulation.GetCar(i);
                string profile = m_Simulation.GetAgent(i) is AiAgent agent ? agent.Profile.DisplayName : "player";

                json.Append("    { \"index\": ").Append(i)
                    .Append(", \"profile\": \"").Append(profile).Append('"')
                    .Append(", \"finishOrder\": ").Append(car.FinishOrder)
                    .Append(", \"finishTime\": ").Append(F(car.FinishTime))
                    .Append(", \"gapToWinner\": ").Append(F(car.FinishTime - winner.FinishTime))
                    .Append(", \"accepted\": ").Append(car.AcceptedBuffCount)
                    .Append(", \"rejected\": ").Append(car.RejectedBuffCount)
                    .Append(", \"spentEnergy\": ").Append(F(car.SpentEnergy))
                    .Append(", \"baseSpeedMultiplier\": ").Append(car.BaseSpeedMultiplier.ToString("0.0000", Inv))
                    .Append(", \"averageBalance\": ").Append((m_BalanceSum[i] / divisor).ToString("0.0000", Inv))
                    .Append(" }").AppendLine(i == m_Simulation.CarCount - 1 ? string.Empty : ",");
            }

            json.AppendLine("  ]");
            json.AppendLine("}");
            return json.ToString();
        }

        private static string F(float value)
        {
            return value.ToString("0.###", Inv);
        }
    }
}
