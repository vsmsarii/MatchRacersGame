using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MatchRacers.Editor
{
    public sealed class RaceLabWindow : EditorWindow
    {
        private const int MaxStepsPerRace = 60000;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private RaceConfigSO m_Config;
        private readonly List<RaceScenarioSO> m_Scenarios = new List<RaceScenarioSO>();
        private float m_SampleHz = 10f;
        private Vector2 m_Scroll;
        private string m_Report = string.Empty;
        private string m_OutputDirectory = string.Empty;

        [MenuItem("MatchRacers/Race Lab")]
        public static void Open()
        {
            GetWindow<RaceLabWindow>("Race Lab").minSize = new Vector2(560f, 420f);
        }

        private void OnEnable()
        {
            if (m_Config == null)
                LoadDefaults();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Batch race runner", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Runs scenarios headlessly: no scene, no cars, no camera. Each scenario runs once per seed.\n" +
                "Exit Play Mode before running so gameplay events do not mix into the recordings.",
                MessageType.None);

            m_Config = (RaceConfigSO)EditorGUILayout.ObjectField("Race Config", m_Config, typeof(RaceConfigSO), false);
            m_SampleHz = EditorGUILayout.Slider("Sample Hz", m_SampleHz, 1f, 60f);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Scenarios", EditorStyles.boldLabel);

            for (int i = 0; i < m_Scenarios.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                m_Scenarios[i] = (RaceScenarioSO)EditorGUILayout.ObjectField(m_Scenarios[i], typeof(RaceScenarioSO), false);
                if (GUILayout.Button("x", GUILayout.Width(22f)))
                {
                    m_Scenarios.RemoveAt(i);
                    EditorGUILayout.EndHorizontal();
                    break;
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Slot"))
                m_Scenarios.Add(null);
            if (GUILayout.Button("Load Defaults"))
                LoadDefaults();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8f);

            using (new EditorGUI.DisabledScope(m_Config == null || m_Scenarios.Count == 0 || EditorApplication.isPlaying))
            {
                if (GUILayout.Button("Run All", GUILayout.Height(30f)))
                    RunAll();
            }

            if (EditorApplication.isPlaying)
                EditorGUILayout.HelpBox("Play Mode is active. Stop it before running the lab.", MessageType.Warning);

            if (string.IsNullOrEmpty(m_Report))
                return;

            EditorGUILayout.Space(8f);
            if (!string.IsNullOrEmpty(m_OutputDirectory) && GUILayout.Button("Reveal Output Folder"))
                EditorUtility.RevealInFinder(m_OutputDirectory);

            m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll);
            EditorGUILayout.TextArea(m_Report, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        private void LoadDefaults()
        {
            m_Config = AssetDatabase.LoadAssetAtPath<RaceConfigSO>("Assets/_Main/SO/Race/RaceConfig.asset");

            m_Scenarios.Clear();
            foreach (string guid in AssetDatabase.FindAssets("t:RaceScenarioSO", new[] { "Assets/_Main/SO/Race" }))
            {
                RaceScenarioSO scenario =
                    AssetDatabase.LoadAssetAtPath<RaceScenarioSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (scenario != null)
                    m_Scenarios.Add(scenario);
            }
        }

        private void RunAll()
        {
            StringBuilder summary = new StringBuilder(4096);
            StringBuilder report = new StringBuilder(4096);

            summary.Append("mode,target,targetMatched,scenario,seed,playerOrder,playerTime,gapToWinner,packSpread,")
                .Append("nearestRivalAtFinish,finalMinGap,finalMaxGap,accepted,rejected,spentEnergy,")
                .Append("totalOvertakes,playerOvertakes");
            for (int i = 0; i < RaceConfigSO.CarCount; i++)
                summary.Append(",car").Append(i).Append("_time");
            summary.AppendLine();

            int runIndex = 0;
            int totalRuns = 0;
            foreach (RaceScenarioSO scenario in m_Scenarios)
            {
                if (scenario != null && scenario.Seeds != null)
                    totalRuns += scenario.Seeds.Length;
            }

            totalRuns += RaceConfigSO.CarCount * 3;
            int targetMatches = 0;
            int targetRuns = 0;

            try
            {
                report.AppendLine("FREE MODE");
                foreach (RaceScenarioSO scenario in m_Scenarios)
                {
                    if (scenario == null || scenario.Seeds == null)
                        continue;

                    report.AppendLine("  " + scenario.DisplayName);

                    foreach (int seed in scenario.Seeds)
                    {
                        EditorUtility.DisplayProgressBar("Race Lab",
                            scenario.DisplayName + "  seed " + seed,
                            totalRuns > 0 ? runIndex / (float)totalRuns : 0f);
                        runIndex++;

                        RunOne(ERaceMode.Free, 0, scenario, unchecked((uint)seed), summary, report);
                    }
                }

                report.AppendLine();
                report.AppendLine("TARGET ORDER MODE");

                RaceScenarioSO[] behaviours = PickTargetBehaviours();
                for (int target = 1; target <= RaceConfigSO.CarCount; target++)
                {
                    report.AppendLine("  target " + target);

                    for (int run = 0; run < 3; run++)
                    {
                        RaceScenarioSO scenario = behaviours[run % behaviours.Length];
                        int[] seeds = scenario.Seeds != null && scenario.Seeds.Length > 0
                            ? scenario.Seeds
                            : new[] { 1337 };
                        uint seed = unchecked((uint)seeds[(run + target) % seeds.Length]);

                        EditorUtility.DisplayProgressBar("Race Lab",
                            "target " + target + "  " + scenario.DisplayName,
                            totalRuns > 0 ? runIndex / (float)totalRuns : 0f);
                        runIndex++;
                        targetRuns++;

                        if (RunOne(ERaceMode.TargetOrder, target, scenario, seed, summary, report))
                            targetMatches++;
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            report.AppendLine();
            report.AppendLine("TARGET MATCH: " + targetMatches + " / " + targetRuns);

            m_OutputDirectory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "RaceRuns");
            Directory.CreateDirectory(m_OutputDirectory);
            File.WriteAllText(Path.Combine(m_OutputDirectory, "summary.csv"), summary.ToString());

            report.AppendLine("summary.csv -> " + m_OutputDirectory);
            m_Report = report.ToString();
            Debug.Log("Race Lab finished " + runIndex + " runs -> " + m_OutputDirectory);
        }

        private RaceScenarioSO[] PickTargetBehaviours()
        {
            RaceScenarioSO balanced = null;
            RaceScenarioSO passive = null;
            RaceScenarioSO spam = null;

            foreach (RaceScenarioSO scenario in m_Scenarios)
            {
                if (scenario == null)
                    continue;

                if (scenario.InputMode == EScenarioInputMode.None)
                    passive = scenario;
                else if (scenario.InputMode == EScenarioInputMode.RepeatingKey)
                    spam = scenario;
                else if (balanced == null)
                    balanced = scenario;
            }

            RaceScenarioSO fallback = balanced ?? passive ?? spam;
            return new[] { balanced ?? fallback, passive ?? fallback, spam ?? fallback };
        }

        private bool RunOne(ERaceMode mode, int target, RaceScenarioSO scenario, uint seed,
            StringBuilder summary, StringBuilder report)
        {
            IRaceAgent[] agents = new IRaceAgent[RaceConfigSO.CarCount];
            agents[0] = new ScriptedAgent(0, scenario);

            for (int i = 1; i < agents.Length; i++)
            {
                agents[i] = m_Config.TryGetRivalProfile(i - 1, out AiProfileSO profile)
                    ? new AiAgent(i, profile, m_Config.BuffTable)
                    : (IRaceAgent)new NullAgent(i);
            }

            string stem = mode == ERaceMode.TargetOrder
                ? "target" + target + "_" + Sanitize(scenario.DisplayName)
                : Sanitize(scenario.DisplayName);

            RaceSimulation simulation = new RaceSimulation(m_Config, agents);
            simulation.Configure(mode, target);
            RaceRecorder recorder = new RaceRecorder(simulation, m_Config, stem, m_SampleHz);

            try
            {
                simulation.Reset(seed);

                int steps = 0;
                while (simulation.State != ERaceState.Finished && steps < MaxStepsPerRace)
                {
                    simulation.Step();
                    recorder.Sample();
                    steps++;
                }

                recorder.Flush();
                AppendSummaryRow(scenario, seed, simulation, recorder, summary, report, steps);
                return mode == ERaceMode.TargetOrder && simulation.TargetMatched;
            }
            finally
            {
                recorder.Dispose();
            }
        }

        private void AppendSummaryRow(RaceScenarioSO scenario, uint seed, RaceSimulation simulation,
            RaceRecorder recorder, StringBuilder summary, StringBuilder report, int steps)
        {
            CarState player = simulation.GetCar(simulation.PlayerCarIndex);
            CarState winner = simulation.GetCar(simulation.GetCarAtPosition(1));
            CarState last = simulation.GetCar(simulation.GetCarAtPosition(RaceConfigSO.CarCount));

            float nearest = float.MaxValue;
            for (int i = 0; i < simulation.CarCount; i++)
            {
                if (i == simulation.PlayerCarIndex)
                    continue;

                float delta = Mathf.Abs(simulation.GetCar(i).FinishTime - player.FinishTime);
                if (delta < nearest)
                    nearest = delta;
            }

            float packSpread = last.FinishTime - winner.FinishTime;

            bool isTarget = simulation.Mode == ERaceMode.TargetOrder;
            summary.Append(simulation.Mode).Append(',')
                .Append(isTarget ? simulation.TargetPosition : 0).Append(',')
                .Append(isTarget ? (simulation.TargetMatched ? "1" : "0") : "-").Append(',')
                .Append(Sanitize(scenario.DisplayName)).Append(',').Append(seed).Append(',')
                .Append(player.FinishOrder).Append(',').Append(F(player.FinishTime)).Append(',')
                .Append(F(player.FinishTime - winner.FinishTime)).Append(',').Append(F(packSpread)).Append(',')
                .Append(F(nearest)).Append(',').Append(F(recorder.FinalSegmentMinGap)).Append(',')
                .Append(F(recorder.FinalSegmentMaxGap)).Append(',')
                .Append(player.AcceptedBuffCount).Append(',').Append(player.RejectedBuffCount).Append(',')
                .Append(F(player.SpentEnergy)).Append(',')
                .Append(recorder.TotalOvertakes).Append(',').Append(recorder.PlayerOvertakes);

            for (int i = 0; i < simulation.CarCount; i++)
                summary.Append(',').Append(F(simulation.GetCar(i).FinishTime));

            summary.AppendLine();

            report.Append("    ")
                .Append(isTarget ? (simulation.TargetMatched ? "OK   " : "MISS ") : "     ")
                .Append("seed ").Append(seed.ToString().PadLeft(10))
                .Append("   P").Append(player.FinishOrder).Append("/8")
                .Append(isTarget ? " (hedef " + simulation.TargetPosition + ")" : string.Empty)
                .Append("   ").Append(player.FinishTime.ToString("0.00", Inv)).Append("s")
                .Append("   spread ").Append(packSpread.ToString("0.00", Inv)).Append("s")
                .Append("   nearest ").Append(nearest.ToString("0.00", Inv)).Append("s")
                .Append("   overtakes ").Append(recorder.TotalOvertakes)
                .Append("   acc ").Append(player.AcceptedBuffCount)
                .Append("/rej ").Append(player.RejectedBuffCount);

            if (simulation.State != ERaceState.Finished)
                report.Append("   [INCOMPLETE after ").Append(steps).Append(" steps]");

            report.AppendLine();
        }

        private static string Sanitize(string value)
        {
            return string.IsNullOrEmpty(value) ? "scenario" : value.Replace(' ', '_').Replace(',', '_');
        }

        private static string F(float value)
        {
            return value.ToString("0.###", Inv);
        }
    }
}
