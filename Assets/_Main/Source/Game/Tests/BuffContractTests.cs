using NUnit.Framework;

namespace MatchRacers.Tests
{
    public sealed class BuffContractTests
    {
        private RaceConfigSO m_Config;

        [SetUp]
        public void SetUp()
        {
            m_Config = RaceTestKit.LoadConfig();
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void WindowDistanceMatchesContract(int key)
        {
            RaceSimulation race = RaceTestKit.CreateIsolatedRace(m_Config, out TestAgent player);
            race.Reset(1337u);
            RaceTestKit.StepUntilRacing(race);
            RaceTestKit.StepTimes(race, 10);

            CarState car = race.GetCar(race.PlayerCarIndex);
            float before = car.Distance;

            player.NextKey = key;
            int windowSteps = m_Config.BuffTable.GetWindowSteps(m_Config.FixedStepHz);
            RaceTestKit.StepTimes(race, windowSteps);

            float travelled = car.Distance - before;
            float expected = key * m_Config.BaseSpeed * m_Config.BuffTable.WindowSeconds;

            Assert.AreEqual(expected, travelled, expected * 0.001f,
                $"key {key}: expected {expected} m over the window, travelled {travelled} m");
        }

        [Test]
        public void KeyOneGivesNoAdvantage()
        {
            RaceSimulation race = RaceTestKit.CreateIsolatedRace(m_Config, out TestAgent player);
            race.Reset(1337u);
            RaceTestKit.StepUntilRacing(race);

            CarState car = race.GetCar(race.PlayerCarIndex);
            float before = car.Distance;
            float energyBefore = car.Energy;

            player.NextKey = 1;
            int windowSteps = m_Config.BuffTable.GetWindowSteps(m_Config.FixedStepHz);
            RaceTestKit.StepTimes(race, windowSteps);

            float baseline = m_Config.BaseSpeed * m_Config.BuffTable.WindowSeconds;
            Assert.AreEqual(baseline, car.Distance - before, baseline * 0.001f);
            Assert.GreaterOrEqual(car.Energy, energyBefore, "key 1 must not cost energy");
        }

        [Test]
        public void RequestDuringActiveWindowIsIgnored()
        {
            RaceSimulation race = RaceTestKit.CreateIsolatedRace(m_Config, out TestAgent player);
            race.Reset(1337u);
            RaceTestKit.StepUntilRacing(race);

            CarState car = race.GetCar(race.PlayerCarIndex);
            player.NextKey = 3;
            race.Step();

            int stepsRemaining = car.BuffStepsRemaining;
            float energyAfterAccept = car.Energy;

            player.NextKey = 5;
            race.Step();

            Assert.AreEqual(3, car.ActiveBuffKey, "active key must not change");
            Assert.AreEqual(stepsRemaining - 1, car.BuffStepsRemaining, "window must not be extended");
            Assert.LessOrEqual(car.Energy, energyAfterAccept + m_Config.BuffTable.EnergyRegenPerSecond,
                "rejected request must not spend energy");
        }

        [Test]
        public void InsufficientEnergyRejectsWithoutCost()
        {
            RaceSimulation race = RaceTestKit.CreateIsolatedRace(m_Config, out TestAgent player);
            race.Reset(1337u);
            RaceTestKit.StepUntilRacing(race);

            CarState car = race.GetCar(race.PlayerCarIndex);
            car.Energy = 0f;

            player.NextKey = 5;
            race.Step();

            Assert.AreEqual(0, car.ActiveBuffKey, "buff must not activate without energy");
            Assert.AreEqual(1, car.RejectedBuffCount);
            Assert.LessOrEqual(car.SpentEnergy, 0f, "rejected request must not consume energy");
        }

        [Test]
        public void RequestDuringCountdownIsRejected()
        {
            RaceSimulation race = RaceTestKit.CreateIsolatedRace(m_Config, out TestAgent player);
            race.Reset(1337u);

            Assert.AreEqual(ERaceState.Countdown, race.State);

            CarState car = race.GetCar(race.PlayerCarIndex);
            player.NextKey = 5;
            race.Step();

            Assert.AreEqual(0, car.ActiveBuffKey);
            Assert.AreEqual(1, car.RejectedBuffCount);
            Assert.AreEqual(0f, car.Distance, "no movement during countdown");
        }

        [Test]
        public void HeldKeyProducesOnlyOneRequest()
        {
            PlayerAgent agent = new PlayerAgent(0);
            agent.Reset(0u);

            agent.Request(3);
            agent.Request(3);
            agent.Request(5);

            Assert.AreEqual(3, agent.PollBuffKey(null, 0f, 0f), "first press wins");
            Assert.AreEqual(0, agent.PollBuffKey(null, 0f, 0f), "holding must not repeat");
        }

        [Test]
        public void CooldownBlocksImmediateReuse()
        {
            RaceSimulation race = RaceTestKit.CreateIsolatedRace(m_Config, out TestAgent player);
            race.Reset(1337u);
            RaceTestKit.StepUntilRacing(race);

            CarState car = race.GetCar(race.PlayerCarIndex);
            player.NextKey = 2;
            int windowSteps = m_Config.BuffTable.GetWindowSteps(m_Config.FixedStepHz);
            RaceTestKit.StepTimes(race, windowSteps);

            Assert.AreEqual(0, car.ActiveBuffKey, "window should have expired");
            Assert.Greater(car.CooldownStepsRemaining, 0, "cooldown should have started");

            int rejectedBefore = car.RejectedBuffCount;
            player.NextKey = 2;
            race.Step();

            Assert.AreEqual(0, car.ActiveBuffKey, "cooldown must block reuse");
            Assert.AreEqual(rejectedBefore + 1, car.RejectedBuffCount);
        }
    }
}
