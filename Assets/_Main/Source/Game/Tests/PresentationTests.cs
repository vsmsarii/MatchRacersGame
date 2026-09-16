using System.Collections.Generic;
using CasualKit.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MatchRacers.Tests
{
    public sealed class PresentationTests
    {
        private RaceConfigSO m_Config;
        private RaceScenarioSO m_Passive;

        [SetUp]
        public void SetUp()
        {
            m_Config = RaceTestKit.LoadConfig();
            m_Passive = RaceTestKit.LoadScenario("Scenario_NoBuff");
            Assert.IsNotNull(m_Passive);
        }

        [Test]
        public void RenderDistanceSitsBetweenTheLastTwoSteps()
        {
            RaceSimulation race = RaceTestKit.CreateTargetRace(m_Config, m_Passive, 1);
            race.Reset(1337u);

            int guard = 0;
            while (race.State != ERaceState.Racing && guard++ < 10000)
                race.Step();

            for (int i = 0; i < 30; i++)
                race.Step();

            race.Advance(m_Config.FixedDeltaTime * 0.5f);

            CarState player = race.GetCar(race.PlayerCarIndex);
            float alpha = race.InterpolationAlpha;
            float midpoint = (player.PreviousDistance + player.Distance) * 0.5f;

            Assert.AreEqual(0.5f, alpha, 1e-3f);
            Assert.Less(player.PreviousDistance, player.Distance);
            Assert.AreEqual(midpoint, player.GetRenderDistance(alpha), 1e-3f);
        }

        [Test]
        public void FinishedCarsStopAndHoldTheirRenderedPosition()
        {
            RaceSimulation race = RaceTestKit.CreateTargetRace(m_Config, m_Passive, 1);
            RaceTestKit.RunToFinish(race, 1337u, 0f);

            Assert.AreEqual(ERaceState.Finished, race.State);
            Assert.AreEqual(1f, race.InterpolationAlpha);

            for (int i = 0; i < race.CarCount; i++)
            {
                CarState car = race.GetCar(i);
                Assert.AreEqual(0f, car.Speed, "car " + i);
                Assert.AreEqual(car.Distance, car.GetRenderDistance(race.InterpolationAlpha), 1e-4f, "car " + i);
            }
        }

        [Test]
        public void EngineTierFollowsThePlayersSpeedState()
        {
            CarState car = new CarState();
            car.Reset(100f);

            Assert.AreEqual(0, RaceFeedback.GetEngineTier(car, ERaceState.Countdown, false));
            Assert.AreEqual(0, RaceFeedback.GetEngineTier(car, ERaceState.Racing, true));
            Assert.AreEqual(1, RaceFeedback.GetEngineTier(car, ERaceState.Racing, false));

            for (int key = 2; key <= BuffTableSO.MaxKey; key++)
            {
                car.ActiveBuffKey = key;
                Assert.AreEqual(key, RaceFeedback.GetEngineTier(car, ERaceState.Racing, false), "key " + key);
            }

            car.ActiveBuffKey = 0;
            car.Finished = true;
            Assert.AreEqual(0, RaceFeedback.GetEngineTier(car, ERaceState.Racing, false));
        }

        [Test]
        public void MissingTierClipFallsBackToCruiseThenIdle()
        {
            Assert.AreEqual(EAudioName.EngineNitro5,
                RaceFeedback.ResolveEngineSound(EAudioName.EngineNitro5, name => true));
            Assert.AreEqual(EAudioName.EngineCruise,
                RaceFeedback.ResolveEngineSound(EAudioName.EngineNitro5, name => name == EAudioName.EngineCruise));
            Assert.AreEqual(EAudioName.EngineIdle,
                RaceFeedback.ResolveEngineSound(EAudioName.EngineNitro3, name => name == EAudioName.EngineIdle));
            Assert.AreEqual(EAudioName.None,
                RaceFeedback.ResolveEngineSound(EAudioName.EngineNitro3, name => false));
        }

        [Test]
        public void CameraShakeHardensWithNitroLevel()
        {
            NitroLevelTableSO table = ScriptableObject.CreateInstance<NitroLevelTableSO>();

            try
            {
                Assert.AreEqual(0f, table.Get(BuffTableSO.MinKey).CameraShake);

                float previous = 0f;
                for (int key = BuffTableSO.MinKey + 1; key <= BuffTableSO.MaxKey; key++)
                {
                    float shake = table.Get(key).CameraShake;
                    Assert.Greater(shake, previous, "key " + key);
                    previous = shake;
                }

                Assert.AreEqual(0f, RaceCameraRig.GetShakeAmplitude(0.26f, 0f, false, 1f, 0.45f));
                Assert.Greater(RaceCameraRig.GetShakeAmplitude(0.26f, 0f, true, 1f, 0.45f),
                    RaceCameraRig.GetShakeAmplitude(0.10f, 0f, true, 1f, 0.45f));
                Assert.Greater(RaceCameraRig.GetShakeAmplitude(0.26f, 0.5f, true, 1f, 0.45f),
                    RaceCameraRig.GetShakeAmplitude(0.26f, 0f, true, 1f, 0.45f));
            }
            finally
            {
                Object.DestroyImmediate(table);
            }
        }

        [Test]
        public void CarVfxFollowsTheCarsMotionState()
        {
            Assert.IsTrue(CarVfxCatalogSO.IsWanted(ECarVfx.IdleSmoke, false, false));
            Assert.IsFalse(CarVfxCatalogSO.IsWanted(ECarVfx.IdleSmoke, true, false));
            Assert.IsTrue(CarVfxCatalogSO.IsWanted(ECarVfx.CruiseFlame, true, false));
            Assert.IsFalse(CarVfxCatalogSO.IsWanted(ECarVfx.CruiseFlame, true, true));
            Assert.IsFalse(CarVfxCatalogSO.IsWanted(ECarVfx.CruiseFlame, false, false));
            Assert.IsTrue(CarVfxCatalogSO.IsWanted(ECarVfx.NitroFlame, true, true));
            Assert.IsFalse(CarVfxCatalogSO.IsWanted(ECarVfx.NitroFlame, true, false));
        }

        [Test]
        public void NitroFlameGrowsTenPercentPerLevel()
        {
            CarVfxCatalogSO catalog = ScriptableObject.CreateInstance<CarVfxCatalogSO>();

            try
            {
                Assert.AreEqual(1.22f, catalog.GetTargetScale(ECarVfx.IdleSmoke, 0), 1e-5f);
                Assert.AreEqual(0.2f, catalog.GetTargetScale(ECarVfx.CruiseFlame, 0), 1e-5f);
                Assert.IsFalse(catalog.IsNitro(1));
                Assert.AreEqual(0f, catalog.GetTargetScale(ECarVfx.NitroFlame, 1), 1e-5f);
                Assert.AreEqual(1.0f, catalog.GetTargetScale(ECarVfx.NitroFlame, 2), 1e-5f);
                Assert.AreEqual(1.1f, catalog.GetTargetScale(ECarVfx.NitroFlame, 3), 1e-5f);
                Assert.AreEqual(1.2f, catalog.GetTargetScale(ECarVfx.NitroFlame, 4), 1e-5f);
                Assert.AreEqual(1.3f, catalog.GetTargetScale(ECarVfx.NitroFlame, 5), 1e-5f);
            }
            finally
            {
                Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void VfxMotionIgnoresStandstillAndRestartJumps()
        {
            Assert.IsFalse(RaceVfxSystem.IsMoving(10f, 10f, 0.016f, 0.5f));
            Assert.IsTrue(RaceVfxSystem.IsMoving(10f, 10.5f, 0.016f, 0.5f));
            Assert.IsFalse(RaceVfxSystem.IsMoving(1700f, 0f, 0.016f, 0.5f));
            Assert.IsFalse(RaceVfxSystem.IsMoving(10f, 10.5f, 0f, 0.5f));
        }

        [Test]
        public void EngineTiersRiseInPitch()
        {
            EngineAudioTableSO table = ScriptableObject.CreateInstance<EngineAudioTableSO>();

            try
            {
                float previous = 0f;
                for (int tier = 0; tier <= BuffTableSO.MaxKey; tier++)
                {
                    Assert.IsTrue(table.TryGetLayer(tier, out EngineAudioLayer layer), "tier " + tier);
                    Assert.Greater(layer.Pitch, previous, "tier " + tier);
                    previous = layer.Pitch;
                }
            }
            finally
            {
                Object.DestroyImmediate(table);
            }
        }

        [Test]
        public void FinishCoastLeavesTheLineAtFinishSpeedAndSettles()
        {
            const float speed = 24f;
            const float stop = 20f;
            const float step = 1e-3f;

            Assert.AreEqual(-speed * 0.1f, FinishCoast.Evaluate(speed, stop, -0.1f), 1e-4f);
            Assert.AreEqual(speed, FinishCoast.Evaluate(speed, stop, step) / step, 0.05f);

            float previous = 0f;
            for (float t = 0f; t < 5f; t += 0.01f)
            {
                float travelled = FinishCoast.Evaluate(speed, stop, t);
                Assert.GreaterOrEqual(travelled, previous - 1e-5f, "t " + t);
                previous = travelled;
            }

            Assert.AreEqual(stop, FinishCoast.Evaluate(speed, stop, 10f), 1e-4f);
        }

        [Test]
        public void FinishedCarsStopAtDifferentPointsInsideTheRunout()
        {
            RaceSimulation race = RaceTestKit.CreateTargetRace(m_Config, m_Passive, 4);
            RaceTestKit.RunToFinish(race, 1337u, 0f);

            float limit = m_Config.RunoutMeters - 1f;
            HashSet<int> distinct = new HashSet<int>();

            for (int i = 0; i < race.CarCount; i++)
            {
                CarState car = race.GetCar(i);
                float stop = FinishCoast.GetStopDistance(m_Config, car.FinishSpeed, i, car.FinishTime, false);

                Assert.Greater(car.FinishSpeed, 0f, "car " + i);
                Assert.Greater(stop, 0f, "car " + i);
                Assert.LessOrEqual(stop, limit + 1e-4f, "car " + i);
                distinct.Add(Mathf.RoundToInt(stop * 10f));
            }

            Assert.GreaterOrEqual(distinct.Count, 5);
        }

        [Test]
        public void CameraHoldsThePlayerSteadyAtUnevenFrameRates()
        {
            RaceSimulation race = RaceTestKit.CreateIsolatedRace(m_Config, out _);
            race.Reset(1337u);
            RaceTestKit.StepUntilRacing(race);

            GameObject host = new GameObject("TestCamera");
            try
            {
                Camera camera = host.AddComponent<Camera>();
                RacePath path = RacePath.CreateStraight(m_Config.RaceLengthMeters, m_Config.RunoutMeters);
                RaceCameraRig rig = new RaceCameraRig(camera, race, path, m_Config, null);
                rig.Sync(1f);

                float[] frames = { 1f / 90f, 1f / 75f, 1f / 110f, 1f / 60f };
                float previousOffset = 0f;
                float worstJump = 0f;

                for (int frame = 0; frame < 600; frame++)
                {
                    float deltaTime = frames[frame % frames.Length];
                    race.Advance(deltaTime);
                    rig.Sync(deltaTime);

                    CarState player = race.GetCar(race.PlayerCarIndex);
                    float offset = rig.FocusDistance - player.GetRenderDistance(race.InterpolationAlpha);

                    if (frame > 240)
                        worstJump = Mathf.Max(worstJump, Mathf.Abs(offset - previousOffset));

                    previousOffset = offset;
                }

                Assert.Less(worstJump, 0.01f, $"camera offset jumped {worstJump:0.0000} m between frames");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void NitroFlameOvershootsThenSettles()
        {
            CarVfxEntry entry = new CarVfxEntry(ECarVfx.NitroFlame, 1f, 0.12f, 0.25f, 8, true, 0.1f, 0.15f);
            float scale = 0f;
            float lastTarget = 0f;
            bool overshooting = false;
            float peak = 0f;

            for (int i = 0; i < 120; i++)
            {
                scale = CarVfxRig.StepScale(entry, scale, 1f, 1f / 120f, ref lastTarget, ref overshooting);
                peak = Mathf.Max(peak, scale);
            }

            Assert.AreEqual(1.1f, peak, 1e-3f);
            Assert.AreEqual(1f, scale, 1e-3f);

            CarVfxEntry plain = new CarVfxEntry(ECarVfx.NitroFlame, 1f, 0.12f, 0.25f, 8, true);
            scale = 0f;
            lastTarget = 0f;
            overshooting = false;
            peak = 0f;

            for (int i = 0; i < 120; i++)
            {
                scale = CarVfxRig.StepScale(plain, scale, 1f, 1f / 120f, ref lastTarget, ref overshooting);
                peak = Mathf.Max(peak, scale);
            }

            Assert.AreEqual(1f, peak, 1e-4f);
        }

        [Test]
        public void EachCarGetsItsOwnAudioChannels()
        {
            HashSet<int> channels = new HashSet<int>();

            for (int car = 0; car < RaceConfigSO.CarCount; car++)
            {
                for (int slot = 0; slot < 3; slot++)
                {
                    int channel = RaceFeedback.GetCarChannel(car, slot);
                    Assert.IsTrue(channels.Add(channel), $"car {car} slot {slot} reuses channel {channel}");
                }
            }
        }
    }
}
