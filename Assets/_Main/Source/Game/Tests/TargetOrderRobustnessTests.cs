using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace MatchRacers.Tests
{
    public sealed class TargetOrderRobustnessTests
    {
        private const float ReferenceBaseSpeed = 12f;
        private static readonly uint[] Seeds = { 1337u, 4242u, 90210u };

        private readonly List<Object> m_Created = new List<Object>();

        private RaceConfigSO m_Config;
        private RaceScenarioSO m_Balanced;
        private RaceScenarioSO m_Passive;
        private RaceScenarioSO m_Spam;
        private RaceScenarioSO m_Greedy;

        [SetUp]
        public void SetUp()
        {
            m_Config = RaceTestKit.LoadConfig();
            m_Balanced = RaceTestKit.LoadScenario("Scenario_BalancedActive");
            m_Passive = RaceTestKit.LoadScenario("Scenario_NoBuff");
            m_Spam = RaceTestKit.LoadScenario("Scenario_Spam5");
            m_Greedy = Track(RaceTestKit.CreateScenario(EScenarioInputMode.Greedy, "Greedy"));

            Assert.IsNotNull(m_Balanced);
            Assert.IsNotNull(m_Passive);
            Assert.IsNotNull(m_Spam);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < m_Created.Count; i++)
            {
                if (m_Created[i] != null)
                    Object.DestroyImmediate(m_Created[i]);
            }

            m_Created.Clear();
        }

        [TestCase(200f)]
        [TestCase(500f)]
        [TestCase(1000f)]
        public void EveryTargetHoldsForEveryBehaviourAtLength(float raceLength)
        {
            RaceConfigSO config = Track(RaceTestKit.WithRaceLength(m_Config, Scaled(raceLength)));
            RaceScenarioSO[] behaviours = { m_Greedy, m_Balanced, m_Passive, m_Spam };
            List<string> misses = new List<string>();

            for (int target = 1; target <= RaceConfigSO.CarCount; target++)
            {
                for (int b = 0; b < behaviours.Length; b++)
                {
                    for (int s = 0; s < Seeds.Length; s++)
                    {
                        RaceSimulation race = RaceTestKit.CreateTargetRace(config, behaviours[b], target);
                        RaceTestKit.RunToFinish(race, Seeds[s], 0f);

                        int order = race.GetCar(race.PlayerCarIndex).FinishOrder;
                        if (order != target)
                            misses.Add($"{config.RaceLengthMeters:0} m ({raceLength} m @ {ReferenceBaseSpeed} m/sn), " +
                                       $"target {target}, {behaviours[b].DisplayName}, seed {Seeds[s]}: P{order}");
                    }
                }
            }

            Assert.IsEmpty(misses, string.Join("\n", misses));
        }

        [Test]
        public void ShortRaceSkilledPlayerFinishesOnTarget()
        {
            RaceConfigSO config = Track(RaceTestKit.WithRaceLength(m_Config, Scaled(200f)));
            uint[] seeds = { 1337u, 4242u, 90210u, 7u, 555u };

            for (int i = 0; i < seeds.Length; i++)
            {
                RaceSimulation race = RaceTestKit.CreateTargetRace(config, m_Greedy, 4);
                RaceTestKit.RunToFinish(race, seeds[i], 0f);
                Assert.AreEqual(4, race.GetCar(race.PlayerCarIndex).FinishOrder, "seed " + seeds[i]);
            }
        }

        [Test]
        public void ShortRaceSpamPlayerSeed555FinishesOnTarget()
        {
            RaceConfigSO config = Track(RaceTestKit.WithRaceLength(m_Config, Scaled(200f)));
            RaceSimulation race = RaceTestKit.CreateTargetRace(config, m_Spam, 4);
            RaceTestKit.RunToFinish(race, 555u, 0f);
            Assert.AreEqual(4, race.GetCar(race.PlayerCarIndex).FinishOrder);
        }

        [Test]
        public void EnvelopeNeverTouchesThePlayer()
        {
            RaceConfigSO config = Track(RaceTestKit.WithRaceLength(m_Config, Scaled(200f)));
            RaceSimulation race = RaceTestKit.CreateTargetRace(config, m_Greedy, 1);
            race.Reset(1337u);

            int guard = 0;
            while (race.State != ERaceState.Finished && guard++ < 60000)
            {
                race.Step();

                CarState player = race.GetCar(race.PlayerCarIndex);
                Assert.AreEqual(1f, player.BalanceMultiplier, 1e-6f);
                Assert.AreEqual(0f, player.PaceBias, 1e-6f);
                Assert.AreEqual(0, player.CommandedBuffKey);
                Assert.IsFalse(player.HoldBuffs);
            }
        }

        [Test]
        public void SideBySideBaseSpeedsMatchUnlessCritical()
        {
            float tolerance = m_Config.TargetNearPaceBand + 0.005f;
            float sideBySide = m_Config.TargetNearInnerMeters * 0.8f;
            RaceScenarioSO[] behaviours = { m_Greedy, m_Balanced, m_Passive, m_Spam };
            int[] targets = { 2, 4, 6 };
            List<string> misses = new List<string>();
            int samples = 0;

            for (int t = 0; t < targets.Length; t++)
            {
                for (int b = 0; b < behaviours.Length; b++)
                {
                    RaceSimulation race = RaceTestKit.CreateTargetRace(m_Config, behaviours[b], targets[t]);
                    race.Reset(1337u);
                    bool[] wasBuffed = new bool[race.CarCount];

                    int guard = 0;
                    while (race.State != ERaceState.Finished && guard++ < 200000)
                    {
                        race.Step();

                        if (race.State != ERaceState.Racing)
                            continue;

                        CarState player = race.GetCar(race.PlayerCarIndex);
                        bool launching = race.Director.IsLaunching;

                        for (int i = 0; i < race.CarCount; i++)
                        {
                            CarState car = race.GetCar(i);
                            bool previouslyBuffed = wasBuffed[i];
                            wasBuffed[i] = car.HasActiveBuff;

                            if (i == race.PlayerCarIndex || car.Finished || player.Finished)
                                continue;

                            if (car.HasActiveBuff || previouslyBuffed || player.HasActiveBuff)
                                continue;

                            if (Mathf.Abs(car.PaceIntervention) > 1)
                                continue;

                            ETargetChallengePhase phase = race.Director.GetChallengePhase(i);
                            if (launching || race.Director.IsSettling(i) || phase == ETargetChallengePhase.Fallback ||
                                phase == ETargetChallengePhase.Return)
                                continue;

                            if (Mathf.Abs(car.Distance - player.Distance) >= sideBySide)
                                continue;

                            samples++;
                            float ratio = car.BaseSpeedMultiplier * car.BalanceMultiplier / player.BaseSpeedMultiplier;
                            if (Mathf.Abs(ratio - 1f) > tolerance && misses.Count < 10)
                                misses.Add($"target {targets[t]}, {behaviours[b].DisplayName}, car {i}, t {race.Time:0.00}: {ratio:0.000}");
                        }
                    }
                }
            }

            Assert.Greater(samples, 0);
            Assert.IsEmpty(misses, string.Join("\n", misses));
        }

        [Test]
        public void BehindRivalsChallengeThePlayerAndFallBack()
        {
            RaceScenarioSO[] behaviours = { m_Balanced, m_Passive };
            int[] targets = { 1, 4 };
            List<string> misses = new List<string>();

            for (int t = 0; t < targets.Length; t++)
            {
                for (int b = 0; b < behaviours.Length; b++)
                {
                    for (int s = 0; s < Seeds.Length; s++)
                    {
                        RaceSimulation race = RaceTestKit.CreateTargetRace(m_Config, behaviours[b], targets[t]);
                        race.Reset(Seeds[s]);
                        string label = $"target {targets[t]}, {behaviours[b].DisplayName}, seed {Seeds[s]}";
                        int ledSteps = 0;

                        int guard = 0;
                        while (race.State != ERaceState.Finished && guard++ < 200000)
                        {
                            race.Step();

                            CarState player = race.GetCar(race.PlayerCarIndex);
                            if (race.State != ERaceState.Racing || player.Finished)
                                continue;

                            float progress = player.Distance / m_Config.RaceLengthMeters;

                            for (int i = 0; i < race.CarCount; i++)
                            {
                                if (i == race.PlayerCarIndex || race.Director.IsAheadSide(i))
                                    continue;

                                ETargetChallengePhase phase = race.Director.GetChallengePhase(i);
                                if (phase == ETargetChallengePhase.Lead && race.GetCar(i).Distance > player.Distance)
                                    ledSteps++;

                                bool attacking = phase == ETargetChallengePhase.Attack || phase == ETargetChallengePhase.Lead;
                                if (attacking && progress >= m_Config.TargetAttackLockProgress && misses.Count < 10)
                                    misses.Add($"{label}: car {i} still attacking at {progress:0.00}");
                            }
                        }

                        if (ledSteps == 0)
                            misses.Add($"{label}: no behind rival ever led the player");

                        int order = race.GetCar(race.PlayerCarIndex).FinishOrder;
                        if (order != targets[t])
                            misses.Add($"{label}: P{order}");
                    }
                }
            }

            Assert.IsEmpty(misses, string.Join("\n", misses));
        }

        [Test]
        public void AheadRivalsComeBackAndReattack()
        {
            RaceScenarioSO[] behaviours = { m_Balanced, m_Passive };
            int[] targets = { 4, 8 };
            List<string> misses = new List<string>();

            for (int t = 0; t < targets.Length; t++)
            {
                for (int b = 0; b < behaviours.Length; b++)
                {
                    for (int s = 0; s < Seeds.Length; s++)
                    {
                        RaceSimulation race = RaceTestKit.CreateTargetRace(m_Config, behaviours[b], targets[t]);
                        race.Reset(Seeds[s]);
                        string label = $"target {targets[t]}, {behaviours[b].DisplayName}, seed {Seeds[s]}";
                        bool reattackedFromBehind = false;

                        int guard = 0;
                        while (race.State != ERaceState.Finished && guard++ < 200000)
                        {
                            race.Step();

                            CarState player = race.GetCar(race.PlayerCarIndex);
                            if (race.State != ERaceState.Racing || player.Finished)
                                continue;

                            for (int i = 0; i < race.CarCount; i++)
                            {
                                if (i == race.PlayerCarIndex || !race.Director.IsAheadSide(i))
                                    continue;

                                if (race.Director.GetChallengePhase(i) == ETargetChallengePhase.Attack &&
                                    race.GetCar(i).Distance < player.Distance)
                                    reattackedFromBehind = true;
                            }
                        }

                        if (!reattackedFromBehind)
                            misses.Add($"{label}: no ahead rival came back and re-attacked");

                        int order = race.GetCar(race.PlayerCarIndex).FinishOrder;
                        if (order != targets[t])
                            misses.Add($"{label}: P{order}");
                    }
                }
            }

            Assert.IsEmpty(misses, string.Join("\n", misses));
        }

        [Test]
        public void RivalsDoNotRideSideBySideForLong()
        {
            const float PairMeters = 3f;
            const float VisibleMeters = 40f;
            const float MaxSeconds = 12f;

            RaceScenarioSO[] behaviours = { m_Greedy, m_Balanced, m_Passive, m_Spam };
            int[] targets = { 2, 4, 6 };
            List<string> misses = new List<string>();

            for (int t = 0; t < targets.Length; t++)
            {
                for (int b = 0; b < behaviours.Length; b++)
                {
                    RaceSimulation race = RaceTestKit.CreateTargetRace(m_Config, behaviours[b], targets[t]);
                    race.Reset(1337u);
                    float[,] together = new float[race.CarCount, race.CarCount];
                    float longest = 0f;

                    int guard = 0;
                    while (race.State != ERaceState.Finished && guard++ < 200000)
                    {
                        race.Step();

                        CarState player = race.GetCar(race.PlayerCarIndex);
                        if (race.State != ERaceState.Racing || player.Finished)
                            continue;

                        if (player.Distance < m_Config.RaceLengthMeters * 0.05f)
                            continue;

                        for (int i = 0; i < race.CarCount; i++)
                        {
                            if (i == race.PlayerCarIndex)
                                continue;

                            for (int j = i + 1; j < race.CarCount; j++)
                            {
                                if (j == race.PlayerCarIndex)
                                    continue;

                                CarState first = race.GetCar(i);
                                CarState second = race.GetCar(j);
                                bool visible = !first.Finished && !second.Finished &&
                                               Mathf.Abs(first.Distance - second.Distance) < PairMeters &&
                                               Mathf.Abs((first.Distance + second.Distance) * 0.5f - player.Distance) < VisibleMeters;

                                together[i, j] = visible ? together[i, j] + m_Config.FixedDeltaTime : 0f;
                                longest = Mathf.Max(longest, together[i, j]);
                            }
                        }
                    }

                    if (longest > MaxSeconds)
                        misses.Add($"target {targets[t]}, {behaviours[b].DisplayName}: rivals side by side for {longest:0.0} s");
                }
            }

            Assert.IsEmpty(misses, string.Join("\n", misses));
        }

        private float Scaled(float referenceMeters)
        {
            return referenceMeters * m_Config.BaseSpeed / ReferenceBaseSpeed;
        }

        private T Track<T>(T created) where T : Object
        {
            m_Created.Add(created);
            return created;
        }
    }
}
