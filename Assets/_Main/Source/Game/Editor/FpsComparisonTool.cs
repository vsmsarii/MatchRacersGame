using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MatchRacers.Editor
{
    public static class FpsComparisonTool
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly int[] FrameRates = { 30, 60, 120 };
        private const int MaxSteps = 60000;

        [MenuItem("MatchRacers/Verification/FPS Comparison")]
        public static void Run()
        {
            RaceConfigSO config = AssetDatabase.LoadAssetAtPath<RaceConfigSO>("Assets/_Main/SO/Race/RaceConfig.asset");
            RaceScenarioSO scenario = AssetDatabase.LoadAssetAtPath<RaceScenarioSO>(
                "Assets/_Main/SO/Race/Scenarios/Scenario_BalancedActive.asset");

            if (config == null || scenario == null || scenario.Seeds == null)
            {
                Debug.LogError("FPS comparison needs RaceConfig and Scenario_BalancedActive.");
                return;
            }

            StringBuilder csv = new StringBuilder(4096);
            csv.Append("seed,fps,playerFinishTime,deviationSeconds,deviationPercent,playerOrder");
            for (int i = 0; i < RaceConfigSO.CarCount; i++)
                csv.Append(",car").Append(i).Append("_time");
            csv.AppendLine();

            StringBuilder log = new StringBuilder(1024);

            foreach (int seed in scenario.Seeds)
            {
                uint unsignedSeed = unchecked((uint)seed);
                float[] reference = RunRace(config, scenario, unsignedSeed, 0f, out int referenceOrder);

                foreach (int fps in FrameRates)
                {
                    float[] times = RunRace(config, scenario, unsignedSeed, 1f / fps, out int order);
                    float deviation = times[0] - reference[0];
                    float percent = reference[0] > 0f ? deviation / reference[0] * 100f : 0f;

                    csv.Append(seed).Append(',').Append(fps).Append(',')
                        .Append(F(times[0])).Append(',').Append(deviation.ToString("0.######", Inv)).Append(',')
                        .Append(percent.ToString("0.######", Inv)).Append(',').Append(order);

                    for (int i = 0; i < times.Length; i++)
                        csv.Append(',').Append(F(times[i]));

                    csv.AppendLine();

                    log.Append("seed ").Append(seed).Append("  ").Append(fps).Append(" FPS  player ")
                        .Append(times[0].ToString("0.0000", Inv)).Append("s  deviation ")
                        .Append(deviation.ToString("0.000000", Inv)).Append("s (")
                        .Append(percent.ToString("0.0000", Inv)).Append("%)  order P")
                        .Append(order).Append(" vs P").Append(referenceOrder).AppendLine();
                }
            }

            string directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "RaceRuns");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "fps_comparison.csv");
            File.WriteAllText(path, csv.ToString());

            Debug.Log("FPS comparison written to " + path + "\n" + log);
            EditorUtility.RevealInFinder(path);
        }

        private static float[] RunRace(RaceConfigSO config, RaceScenarioSO scenario, uint seed,
            float frameDelta, out int playerOrder)
        {
            IRaceAgent[] agents = new IRaceAgent[RaceConfigSO.CarCount];
            agents[0] = new ScriptedAgent(0, scenario);

            for (int i = 1; i < agents.Length; i++)
            {
                agents[i] = config.TryGetRivalProfile(i - 1, out AiProfileSO profile)
                    ? new AiAgent(i, profile, config.BuffTable)
                    : (IRaceAgent)new NullAgent(i);
            }

            RaceSimulation race = new RaceSimulation(config, agents) { EmitEvents = false };
            race.Reset(seed);

            int guard = 0;
            while (race.State != ERaceState.Finished && guard++ < MaxSteps)
            {
                if (frameDelta > 0f)
                    race.Advance(frameDelta);
                else
                    race.Step();
            }

            float[] times = new float[race.CarCount];
            for (int i = 0; i < times.Length; i++)
                times[i] = race.GetCar(i).FinishTime;

            playerOrder = race.GetCar(race.PlayerCarIndex).FinishOrder;
            return times;
        }

        private static string F(float value)
        {
            return value.ToString("0.####", Inv);
        }
    }
}
