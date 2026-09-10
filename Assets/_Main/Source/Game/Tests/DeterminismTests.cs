using NUnit.Framework;
using UnityEngine;

namespace MatchRacers.Tests
{
    public sealed class DeterminismTests
    {
        private const uint Seed = 4242u;

        private RaceConfigSO m_Config;
        private RaceScenarioSO m_Scenario;

        [SetUp]
        public void SetUp()
        {
            m_Config = RaceTestKit.LoadConfig();
            m_Scenario = RaceTestKit.LoadBalancedScenario();
            Assert.IsNotNull(m_Scenario, "Balanced scenario asset missing");
        }

        [Test]
        public void SameSeedProducesIdenticalRace()
        {
            float[] first = RaceTestKit.RunToFinish(RaceTestKit.CreateFullRace(m_Config, m_Scenario), Seed, 0f);
            float[] second = RaceTestKit.RunToFinish(RaceTestKit.CreateFullRace(m_Config, m_Scenario), Seed, 0f);

            for (int i = 0; i < first.Length; i++)
                Assert.AreEqual(first[i], second[i], 1e-6f, "car " + i + " diverged between identical runs");
        }

        [Test]
        public void DifferentSeedsProduceDifferentRaces()
        {
            float[] a = RaceTestKit.RunToFinish(RaceTestKit.CreateFullRace(m_Config, m_Scenario), 1337u, 0f);
            float[] b = RaceTestKit.RunToFinish(RaceTestKit.CreateFullRace(m_Config, m_Scenario), 90210u, 0f);

            bool anyDifference = false;
            for (int i = 1; i < a.Length; i++)
            {
                if (Mathf.Abs(a[i] - b[i]) > 1e-4f)
                    anyDifference = true;
            }

            Assert.IsTrue(anyDifference, "different seeds should not produce identical rival behaviour");
        }

        [TestCase(30)]
        [TestCase(60)]
        [TestCase(120)]
        public void FrameRateDoesNotChangeOutcome(int framesPerSecond)
        {
            float[] reference = RaceTestKit.RunToFinish(RaceTestKit.CreateFullRace(m_Config, m_Scenario), Seed, 0f);
            float[] framed = RaceTestKit.RunToFinish(
                RaceTestKit.CreateFullRace(m_Config, m_Scenario), Seed, 1f / framesPerSecond);

            for (int i = 0; i < reference.Length; i++)
            {
                float tolerance = Mathf.Max(reference[i] * 0.01f, 1e-4f);
                Assert.AreEqual(reference[i], framed[i], tolerance,
                    $"car {i} finish time drifted at {framesPerSecond} FPS");
            }
        }

        [TestCase(30)]
        [TestCase(120)]
        public void BuffDistanceIsFrameRateIndependent(int framesPerSecond)
        {
            RaceSimulation race = RaceTestKit.CreateIsolatedRace(m_Config, out TestAgent player);
            race.Reset(Seed);

            float frameDelta = 1f / framesPerSecond;
            int guard = 0;
            while (race.State == ERaceState.Countdown && guard++ < 10000)
                race.Advance(frameDelta);

            CarState car = race.GetCar(race.PlayerCarIndex);
            player.NextKey = 4;

            float before = car.Distance;
            float windowSeconds = m_Config.BuffTable.WindowSeconds;
            float elapsed = 0f;

            while (elapsed < windowSeconds + frameDelta)
            {
                race.Advance(frameDelta);
                elapsed += frameDelta;

                if (!car.HasActiveBuff && elapsed > frameDelta)
                    break;
            }

            float expected = 4 * m_Config.BaseSpeed * windowSeconds;
            float travelled = car.Distance - before;

            Assert.AreEqual(expected, travelled, expected * 0.01f,
                $"buff distance at {framesPerSecond} FPS was {travelled} m, expected {expected} m");
        }
    }
}
