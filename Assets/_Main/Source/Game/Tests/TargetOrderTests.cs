using NUnit.Framework;
using UnityEngine;

namespace MatchRacers.Tests
{
    public sealed class TargetOrderTests
    {
        private static readonly uint[] Seeds = { 1337u, 4242u, 90210u };

        private RaceConfigSO m_Config;
        private RaceScenarioSO m_Balanced;
        private RaceScenarioSO m_Passive;
        private RaceScenarioSO m_Spam;

        [SetUp]
        public void SetUp()
        {
            m_Config = RaceTestKit.LoadConfig();
            m_Balanced = RaceTestKit.LoadScenario("Scenario_BalancedActive");
            m_Passive = RaceTestKit.LoadScenario("Scenario_NoBuff");
            m_Spam = RaceTestKit.LoadScenario("Scenario_Spam5");

            Assert.IsNotNull(m_Balanced);
            Assert.IsNotNull(m_Passive);
            Assert.IsNotNull(m_Spam);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        [TestCase(8)]
        public void TargetIsReachedForEveryBehaviour(int target)
        {
            RaceScenarioSO[] behaviours = { m_Balanced, m_Passive, m_Spam };

            for (int i = 0; i < behaviours.Length; i++)
            {
                RaceSimulation race = RaceTestKit.CreateTargetRace(m_Config, behaviours[i], target);
                RaceTestKit.RunToFinish(race, Seeds[i], 0f);

                CarState player = race.GetCar(race.PlayerCarIndex);
                Assert.AreEqual(target, player.FinishOrder,
                    $"target {target}, behaviour {behaviours[i].DisplayName}: player finished P{player.FinishOrder}");
                Assert.IsTrue(race.TargetMatched);
            }
        }

        [Test]
        public void PlayerPaceIsNeverTouchedInTargetMode()
        {
            RaceSimulation race = RaceTestKit.CreateTargetRace(m_Config, m_Balanced, 5);
            race.Reset(1337u);

            int guard = 0;
            while (race.State != ERaceState.Finished && guard++ < 60000)
            {
                race.Step();

                CarState player = race.GetCar(race.PlayerCarIndex);
                Assert.AreEqual(1f, player.BalanceMultiplier, 1e-6f, "player pace was modified at step " + guard);
                Assert.AreEqual(0f, player.PaceBias, 1e-6f, "player attack bias was modified at step " + guard);
            }
        }

        [Test]
        public void PlayerBuffDistanceIsUnchangedByTargetMode()
        {
            float free = MeasureBuffWindow(ERaceMode.Free, 0);
            float targeted = MeasureBuffWindow(ERaceMode.TargetOrder, 6);
            float expected = 4 * m_Config.BaseSpeed * m_Config.BuffTable.WindowSeconds;

            Assert.AreEqual(expected, free, expected * 0.001f, "free mode buff distance");
            Assert.AreEqual(expected, targeted, expected * 0.001f, "target mode buff distance");
        }

        [Test]
        public void RivalPaceStaysInsideDeclaredBounds()
        {
            RaceSimulation race = RaceTestKit.CreateTargetRace(m_Config, m_Spam, 4);
            race.Reset(90210u);

            int guard = 0;
            while (race.State != ERaceState.Finished && guard++ < 60000)
            {
                race.Step();

                for (int i = 1; i < race.CarCount; i++)
                {
                    float pace = race.GetCar(i).BalanceMultiplier;
                    Assert.GreaterOrEqual(pace, m_Config.TargetPaceMin - 1e-4f, "car " + i + " below pace floor");
                    Assert.LessOrEqual(pace, m_Config.TargetPaceMax + 1e-4f, "car " + i + " above pace ceiling");
                }
            }
        }

        [Test]
        public void RivalsNeverMoveBackwards()
        {
            RaceSimulation race = RaceTestKit.CreateTargetRace(m_Config, m_Passive, 1);
            race.Reset(4242u);

            float[] previous = new float[race.CarCount];
            int guard = 0;

            while (race.State != ERaceState.Finished && guard++ < 60000)
            {
                race.Step();

                for (int i = 0; i < race.CarCount; i++)
                {
                    CarState car = race.GetCar(i);
                    Assert.GreaterOrEqual(car.Distance, previous[i] - 1e-4f, "car " + i + " moved backwards");
                    previous[i] = car.Distance;
                }
            }
        }

        private float MeasureBuffWindow(ERaceMode mode, int target)
        {
            RaceSimulation race = RaceTestKit.CreateIsolatedRace(m_Config, out TestAgent player);
            race.Configure(mode, target <= 0 ? 1 : target);
            race.Reset(1337u);
            RaceTestKit.StepUntilRacing(race);
            RaceTestKit.StepTimes(race, 10);

            CarState car = race.GetCar(race.PlayerCarIndex);
            float before = car.Distance;

            player.NextKey = 4;
            RaceTestKit.StepTimes(race, m_Config.BuffTable.GetWindowSteps(m_Config.FixedStepHz));
            return car.Distance - before;
        }
    }
}
