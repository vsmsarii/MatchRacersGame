using NUnit.Framework;
using UnityEngine;

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
            int launchSteps = m_Config.BuffTable.GetLaunchSteps(m_Config.FixedStepHz);
            RaceTestKit.StepTimes(race, launchSteps + windowSteps);

            float travelled = car.Distance - before;
            float dt = m_Config.FixedDeltaTime;
            float expected = launchSteps * dt * m_Config.BaseSpeed * m_Config.BuffTable.LaunchDipMultiplier
                             + key * m_Config.BaseSpeed * windowSteps * dt;

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
            int launchSteps = m_Config.BuffTable.GetLaunchSteps(m_Config.FixedStepHz);
            RaceTestKit.StepTimes(race, launchSteps + windowSteps);

            float dt = m_Config.FixedDeltaTime;
            float baseline = launchSteps * dt * m_Config.BaseSpeed * m_Config.BuffTable.LaunchDipMultiplier
                             + m_Config.BaseSpeed * windowSteps * dt;
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

            while (car.IsLaunching)
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
        public void SameKeyIsBlockedByItsOwnCooldown()
        {
            RaceSimulation race = RaceTestKit.CreateIsolatedRace(m_Config, out TestAgent player);
            race.Reset(1337u);
            RaceTestKit.StepUntilRacing(race);

            CarState car = race.GetCar(race.PlayerCarIndex);
            const int key = 2;

            player.NextKey = key;
            int windowSteps = m_Config.BuffTable.GetWindowSteps(m_Config.FixedStepHz);
            RaceTestKit.StepTimes(race, windowSteps);

            int keyCooldown = m_Config.BuffTable.GetKeyCooldownSteps(key, m_Config.FixedStepHz);
            Assert.Greater(keyCooldown, 0, "test needs a key with a real cooldown");

            int globalCooldown = m_Simulation_GlobalCooldownSteps();
            RaceTestKit.StepTimes(race, globalCooldown + 1);

            car.Energy = m_Config.BuffTable.EnergyMax;
            int rejectedBefore = car.RejectedBuffCount;

            player.NextKey = key;
            race.Step();

            Assert.AreEqual(0, car.ActiveBuffKey, "key must stay on its own cooldown");
            Assert.AreEqual(rejectedBefore + 1, car.RejectedBuffCount);
        }

        [Test]
        public void AnotherKeyStaysUsableWhileOneIsCoolingDown()
        {
            RaceSimulation race = RaceTestKit.CreateIsolatedRace(m_Config, out TestAgent player);
            race.Reset(1337u);
            RaceTestKit.StepUntilRacing(race);

            CarState car = race.GetCar(race.PlayerCarIndex);

            player.NextKey = 2;
            int windowSteps = m_Config.BuffTable.GetWindowSteps(m_Config.FixedStepHz);
            int launchSteps = m_Config.BuffTable.GetLaunchSteps(m_Config.FixedStepHz);
            RaceTestKit.StepTimes(race, launchSteps + windowSteps);
            RaceTestKit.StepTimes(race, m_Simulation_GlobalCooldownSteps() + 1);

            car.Energy = m_Config.BuffTable.EnergyMax;

            player.NextKey = 3;
            race.Step();

            Assert.AreEqual(3, car.ActiveBuffKey, "an independent key must remain available");
        }

        private int m_Simulation_GlobalCooldownSteps()
        {
            return Mathf.RoundToInt(m_Config.BuffTable.GlobalCooldownSeconds * m_Config.FixedStepHz);
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
            int launchSteps = m_Config.BuffTable.GetLaunchSteps(m_Config.FixedStepHz);
            RaceTestKit.StepTimes(race, launchSteps + windowSteps);

            Assert.AreEqual(0, car.ActiveBuffKey, "window should have expired");
            Assert.Greater(car.CooldownStepsRemaining, 0, "cooldown should have started");

            int rejectedBefore = car.RejectedBuffCount;
            player.NextKey = 2;
            race.Step();

            Assert.AreEqual(0, car.ActiveBuffKey, "cooldown must block reuse");
            Assert.AreEqual(rejectedBefore + 1, car.RejectedBuffCount);
        }

        [Test]
        public void NitroHoldsBackBeforeItLaunches()
        {
            int launchSteps = m_Config.BuffTable.GetLaunchSteps(m_Config.FixedStepHz);
            Assert.Greater(launchSteps, 0, "this test needs a launch delay in the BuffTable");

            RaceSimulation race = RaceTestKit.CreateIsolatedRace(m_Config, out TestAgent player);
            race.Reset(1337u);
            RaceTestKit.StepUntilRacing(race);
            RaceTestKit.StepTimes(race, 10);

            CarState car = race.GetCar(race.PlayerCarIndex);
            int windowSteps = m_Config.BuffTable.GetWindowSteps(m_Config.FixedStepHz);

            player.NextKey = 5;
            RaceTestKit.StepTimes(race, launchSteps);

            float dipped = m_Config.BaseSpeed * m_Config.BuffTable.LaunchDipMultiplier;
            Assert.AreEqual(5, car.ActiveBuffKey, "the buff is active while the car still holds back");
            Assert.AreEqual(dipped, car.Speed, dipped * 0.001f, "the car must dip below base speed first");
            Assert.AreEqual(windowSteps, car.BuffStepsRemaining, "the delay must not eat into the window");

            RaceTestKit.StepTimes(race, 1);

            float boosted = 5f * m_Config.BaseSpeed;
            Assert.AreEqual(boosted, car.Speed, boosted * 0.001f, "the boost must land right after the delay");
            Assert.AreEqual(windowSteps - 1, car.BuffStepsRemaining);
        }
    }
}
