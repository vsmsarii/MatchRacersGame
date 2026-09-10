using NUnit.Framework;

namespace MatchRacers.Tests
{
    public sealed class LifecycleTests
    {
        private RaceConfigSO m_Config;
        private RaceScenarioSO m_Scenario;

        [SetUp]
        public void SetUp()
        {
            m_Config = RaceTestKit.LoadConfig();
            m_Scenario = RaceTestKit.LoadBalancedScenario();
        }

        [Test]
        public void FinishOrderFollowsFinishTimeNotCarIndex()
        {
            RaceSimulation race = RaceTestKit.CreateFullRace(m_Config, m_Scenario);
            RaceTestKit.RunToFinish(race, 90210u, 0f);

            for (int a = 0; a < race.CarCount; a++)
            {
                for (int b = 0; b < race.CarCount; b++)
                {
                    if (a == b)
                        continue;

                    CarState carA = race.GetCar(a);
                    CarState carB = race.GetCar(b);

                    if (carA.FinishTime < carB.FinishTime)
                        Assert.Less(carA.FinishOrder, carB.FinishOrder,
                            $"car {a} finished earlier than car {b} but was ordered behind it");
                }
            }
        }

        [Test]
        public void EveryCarFinishesExactlyOnce()
        {
            RaceSimulation race = RaceTestKit.CreateFullRace(m_Config, m_Scenario);
            RaceTestKit.RunToFinish(race, 1337u, 0f);

            bool[] seen = new bool[race.CarCount + 1];
            for (int i = 0; i < race.CarCount; i++)
            {
                CarState car = race.GetCar(i);
                Assert.IsTrue(car.Finished, "car " + i + " never finished");
                Assert.IsFalse(seen[car.FinishOrder], "duplicate finish order " + car.FinishOrder);
                seen[car.FinishOrder] = true;
            }
        }

        [Test]
        public void BuffActiveAtFinishLineIsCleared()
        {
            RaceSimulation race = RaceTestKit.CreateIsolatedRace(m_Config, out TestAgent player);
            race.Reset(1337u);
            RaceTestKit.StepUntilRacing(race);

            CarState car = race.GetCar(race.PlayerCarIndex);
            int guard = 0;

            while (!car.Finished && guard++ < 60000)
            {
                if (!car.HasActiveBuff && car.CooldownStepsRemaining == 0 &&
                    car.Distance > m_Config.RaceLengthMeters - m_Config.BaseSpeed * 2f)
                {
                    player.NextKey = 5;
                }

                race.Step();
            }

            Assert.IsTrue(car.Finished, "car never crossed the line");
            Assert.AreEqual(0, car.ActiveBuffKey, "buff must be cleared on finish");
            Assert.AreEqual(0, car.BuffStepsRemaining);
            Assert.GreaterOrEqual(car.FinishTime, 0f);
        }

        [Test]
        public void ModeSwitchDoesNotLeakTargetState()
        {
            RaceSimulation race = RaceTestKit.CreateFullRace(m_Config, m_Scenario);

            race.Configure(ERaceMode.TargetOrder, 7);
            RaceTestKit.RunToFinish(race, 1337u, 0f);
            Assert.AreEqual(7, race.GetCar(race.PlayerCarIndex).FinishOrder);

            race.Configure(ERaceMode.Free, 1);
            float[] afterSwitch = RaceTestKit.RunToFinish(race, 1337u, 0f);

            RaceSimulation clean = RaceTestKit.CreateFullRace(m_Config, m_Scenario);
            clean.Configure(ERaceMode.Free, 1);
            float[] reference = RaceTestKit.RunToFinish(clean, 1337u, 0f);

            for (int i = 0; i < reference.Length; i++)
                Assert.AreEqual(reference[i], afterSwitch[i], 1e-6f,
                    "car " + i + " differed after switching back to Free mode");

            Assert.IsFalse(race.TargetMatched, "target result must not survive a mode switch");
        }

        [Test]
        public void TargetChangeDoesNotLeakBetweenRaces()
        {
            RaceSimulation race = RaceTestKit.CreateFullRace(m_Config, m_Scenario);

            race.Configure(ERaceMode.TargetOrder, 2);
            RaceTestKit.RunToFinish(race, 4242u, 0f);
            Assert.AreEqual(2, race.GetCar(race.PlayerCarIndex).FinishOrder);

            race.Configure(ERaceMode.TargetOrder, 8);
            RaceTestKit.RunToFinish(race, 4242u, 0f);
            Assert.AreEqual(8, race.GetCar(race.PlayerCarIndex).FinishOrder,
                "previous target leaked into the new race");

            race.Configure(ERaceMode.TargetOrder, 1);
            RaceTestKit.RunToFinish(race, 4242u, 0f);
            Assert.AreEqual(1, race.GetCar(race.PlayerCarIndex).FinishOrder);
        }

        [Test]
        public void RepeatedResetsRestoreCleanState()
        {
            RaceSimulation race = RaceTestKit.CreateFullRace(m_Config, m_Scenario);

            float[] first = RaceTestKit.RunToFinish(race, 1337u, 0f);
            RaceTestKit.RunToFinish(race, 90210u, 0f);
            float[] third = RaceTestKit.RunToFinish(race, 1337u, 0f);

            for (int i = 0; i < first.Length; i++)
                Assert.AreEqual(first[i], third[i], 1e-6f,
                    "car " + i + " differed after the simulation was reused");

            race.Reset(1337u);
            for (int i = 0; i < race.CarCount; i++)
            {
                CarState car = race.GetCar(i);
                Assert.AreEqual(0f, car.Distance, 1e-6f);
                Assert.IsFalse(car.Finished);
                Assert.AreEqual(0, car.ActiveBuffKey);
                Assert.AreEqual(0, car.AcceptedBuffCount);
                Assert.AreEqual(0, car.RejectedBuffCount);
                Assert.AreEqual(1f, car.BalanceMultiplier, 1e-6f);
            }
        }
    }
}
