using UnityEditor;
using UnityEngine;

namespace MatchRacers.Tests
{
    public sealed class TestAgent : IRaceAgent
    {
        private readonly int m_CarIndex;

        public int NextKey;
        public int CarIndex => m_CarIndex;

        public TestAgent(int carIndex)
        {
            m_CarIndex = carIndex;
        }

        public void Reset(uint seed)
        {
            NextKey = 0;
        }

        public int PollBuffKey(IRaceView view, float stepTime, float stepDelta)
        {
            int key = NextKey;
            NextKey = 0;
            return key;
        }
    }

    public static class RaceTestKit
    {
        public const string ConfigPath = "Assets/_Main/SO/Race/RaceConfig.asset";
        public const string BalancedScenarioPath = "Assets/_Main/SO/Race/Scenarios/Scenario_BalancedActive.asset";

        public static RaceConfigSO LoadConfig()
        {
            RaceConfigSO config = AssetDatabase.LoadAssetAtPath<RaceConfigSO>(ConfigPath);
            Assert(config != null, "RaceConfig asset missing at " + ConfigPath);
            Assert(config.BuffTable != null, "RaceConfig has no BuffTable");
            return config;
        }

        public static RaceScenarioSO LoadBalancedScenario()
        {
            return AssetDatabase.LoadAssetAtPath<RaceScenarioSO>(BalancedScenarioPath);
        }

        public static RaceSimulation CreateIsolatedRace(RaceConfigSO config, out TestAgent playerAgent)
        {
            playerAgent = new TestAgent(0);

            IRaceAgent[] agents = new IRaceAgent[RaceConfigSO.CarCount];
            agents[0] = playerAgent;
            for (int i = 1; i < agents.Length; i++)
                agents[i] = new NullAgent(i);

            RaceSimulation simulation = new RaceSimulation(config, agents) { EmitEvents = false };
            return simulation;
        }

        public static RaceSimulation CreateTargetRace(RaceConfigSO config, RaceScenarioSO scenario, int target)
        {
            RaceSimulation race = CreateFullRace(config, scenario);
            race.Configure(ERaceMode.TargetOrder, target);
            return race;
        }

        public static RaceConfigSO WithRaceLength(RaceConfigSO source, float meters)
        {
            RaceConfigSO copy = Object.Instantiate(source);
            SerializedObject serialized = new SerializedObject(copy);
            serialized.FindProperty("m_RaceLengthMeters").floatValue = meters;
            serialized.FindProperty("m_TrackLayout").objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return copy;
        }

        public static RaceScenarioSO CreateScenario(EScenarioInputMode mode, string displayName)
        {
            RaceScenarioSO scenario = ScriptableObject.CreateInstance<RaceScenarioSO>();
            SerializedObject serialized = new SerializedObject(scenario);
            serialized.FindProperty("m_InputMode").intValue = (int)mode;
            serialized.FindProperty("m_DisplayName").stringValue = displayName;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return scenario;
        }

        public static RaceScenarioSO LoadScenario(string assetName)
        {
            return AssetDatabase.LoadAssetAtPath<RaceScenarioSO>(
                "Assets/_Main/SO/Race/Scenarios/" + assetName + ".asset");
        }

        public static RaceSimulation CreateFullRace(RaceConfigSO config, RaceScenarioSO scenario)
        {
            IRaceAgent[] agents = new IRaceAgent[RaceConfigSO.CarCount];
            agents[0] = new ScriptedAgent(0, scenario);

            for (int i = 1; i < agents.Length; i++)
            {
                agents[i] = config.TryGetRivalProfile(i - 1, out AiProfileSO profile)
                    ? new AiAgent(i, profile, config.BuffTable)
                    : (IRaceAgent)new NullAgent(i);
            }

            return new RaceSimulation(config, agents) { EmitEvents = false };
        }

        public static void StepUntilRacing(RaceSimulation simulation)
        {
            int guard = 0;
            while (simulation.State == ERaceState.Countdown && guard++ < 10000)
                simulation.Step();
        }

        public static void StepTimes(RaceSimulation simulation, int steps)
        {
            for (int i = 0; i < steps; i++)
                simulation.Step();
        }

        public static float[] RunToFinish(RaceSimulation simulation, uint seed, float frameDelta, int maxSteps = 60000)
        {
            simulation.Reset(seed);

            int guard = 0;
            while (simulation.State != ERaceState.Finished && guard++ < maxSteps)
            {
                if (frameDelta > 0f)
                    simulation.Advance(frameDelta);
                else
                    simulation.Step();
            }

            float[] times = new float[simulation.CarCount];
            for (int i = 0; i < times.Length; i++)
                times[i] = simulation.GetCar(i).FinishTime;

            return times;
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new System.InvalidOperationException(message);
        }
    }
}
