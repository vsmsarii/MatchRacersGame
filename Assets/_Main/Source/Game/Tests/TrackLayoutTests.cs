using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MatchRacers.Tests
{
    public sealed class TrackLayoutTests
    {
        private TrackLayoutSO m_Layout;

        [SetUp]
        public void SetUp()
        {
            m_Layout = ScriptableObject.CreateInstance<TrackLayoutSO>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(m_Layout);
        }

        [Test]
        public void StraightTurnStraightLandsWhereGeometrySays()
        {
            SetSegments(TrackSegment.Straight(100f), TrackSegment.Turn(90f, 50f), TrackSegment.Straight(50f));
            RacePath path = RacePath.FromLayout(m_Layout);

            float expected = 100f + Mathf.PI * 0.5f * 50f + 50f;
            Assert.AreEqual(expected, path.TotalLength, 0.05f);

            path.Evaluate(path.TotalLength, out Vector3 end, out Vector3 forward);
            Assert.AreEqual(150f, end.x, 0.1f);
            Assert.AreEqual(-100f, end.z, 0.1f);
            Assert.Greater(Vector3.Dot(forward, Vector3.back), 0.999f);
        }

        [Test]
        public void FourQuarterTurnsCloseTheLoop()
        {
            SetSegments(
                TrackSegment.Straight(100f), TrackSegment.Turn(-90f, 40f),
                TrackSegment.Straight(100f), TrackSegment.Turn(-90f, 40f),
                TrackSegment.Straight(100f), TrackSegment.Turn(-90f, 40f),
                TrackSegment.Straight(100f), TrackSegment.Turn(-90f, 40f));
            RacePath path = RacePath.FromLayout(m_Layout);

            path.Evaluate(0f, out Vector3 start, out Vector3 startForward);
            path.Evaluate(path.TotalLength, out Vector3 end, out Vector3 endForward);

            Assert.Less(Vector3.Distance(start, end), 0.1f);
            Assert.Greater(Vector3.Dot(startForward, endForward), 0.999f);
        }

        [Test]
        public void ForwardNeverSnapsAlongATightTurn()
        {
            SetSegments(TrackSegment.Straight(20f), TrackSegment.Turn(180f, 15f), TrackSegment.Straight(20f));
            RacePath path = RacePath.FromLayout(m_Layout);

            path.Evaluate(0f, out _, out Vector3 previous);
            float worstStep = 0f;

            for (float d = 0.05f; d <= path.TotalLength; d += 0.05f)
            {
                path.Evaluate(d, out _, out Vector3 forward);
                worstStep = Mathf.Max(worstStep, Vector3.Angle(previous, forward));
                previous = forward;
            }

            Assert.Less(worstStep, 0.5f);
        }

        [Test]
        public void HandleFramesMatchTheSampledRoute()
        {
            SetSegments(TrackSegment.Straight(60f), TrackSegment.Turn(70f, 30f), TrackSegment.Straight(40f),
                TrackSegment.Turn(-120f, 25f), TrackSegment.Straight(15f));
            RacePath path = RacePath.FromLayout(m_Layout);

            for (int i = 0; i <= m_Layout.SegmentCount; i++)
            {
                m_Layout.GetSegmentStart(i, out Vector3 expected, out float heading);
                path.Evaluate(m_Layout.GetSegmentStartDistance(i), out Vector3 sampled, out Vector3 forward);

                Assert.Less(Vector3.Distance(expected, sampled), 0.05f, "segment " + i);
                Assert.Greater(Vector3.Dot(TrackLayoutSO.HeadingToDirection(heading), forward), 0.995f, "segment " + i);
            }
        }

        [Test]
        public void TrackAnchoredLandmarkKeepsItsOffsetFromTheRoad()
        {
            SetSegments(TrackSegment.Straight(80f), TrackSegment.Turn(90f, 40f), TrackSegment.Straight(80f));
            RacePath path = RacePath.FromLayout(m_Layout);

            path.Evaluate(120f, out Vector3 road, out Vector3 forward);
            Vector3 placed = road + RacePath.RightOf(forward) * -18f + Vector3.up * 2f;
            Quaternion facing = Quaternion.LookRotation(RacePath.Flatten(forward), Vector3.up);

            TrackLandmark landmark = TrackLandmark.OnTrack("tower", null, path, placed, facing, Vector3.one);
            landmark.Resolve(path, out Vector3 resolved, out Quaternion rotation);

            Assert.AreEqual(120f, landmark.Distance, 0.1f);
            Assert.AreEqual(-18f, landmark.Lateral, 0.05f);
            Assert.Less(Vector3.Distance(placed, resolved), 0.05f);
            Assert.Less(Quaternion.Angle(facing, rotation), 0.5f);
        }

        [Test]
        public void LoopSolverReturnsExactlyToTheStart()
        {
            TrackSegment[] open =
            {
                TrackSegment.Straight(180f), TrackSegment.Turn(90f, 45f), TrackSegment.Straight(220f, 6f),
                TrackSegment.Turn(60f, 50f), TrackSegment.Straight(90f), TrackSegment.Turn(-40f, 40f)
            };
            SetSegments(open);

            m_Layout.GetSegmentStart(open.Length, out Vector3 end, out float endHeading);
            var bridge = new System.Collections.Generic.List<TrackSegment>();
            Assert.IsTrue(TrackLoopSolver.TrySolve(end, endHeading, m_Layout.StartPosition, m_Layout.StartHeading, 40f, bridge));

            TrackSegment[] closed = new TrackSegment[open.Length + bridge.Count];
            open.CopyTo(closed, 0);
            bridge.CopyTo(closed, open.Length);
            SetSegments(closed);

            Assert.Less(m_Layout.GetLoopGap(), 0.05f);
            Assert.Less(m_Layout.GetLoopHeadingError(), 0.1f);
        }

        [Test]
        public void ClosedRouteWrapsContinuously()
        {
            SetSegments(
                TrackSegment.Straight(100f), TrackSegment.Turn(180f, 40f),
                TrackSegment.Straight(100f), TrackSegment.Turn(180f, 40f));
            SetClosed(true);
            RacePath path = RacePath.FromLayout(m_Layout);

            Assert.IsTrue(path.IsClosed);

            float total = path.TotalLength;
            for (float d = -30f; d < 30f; d += 7.5f)
            {
                path.Evaluate(d, out Vector3 wrapped, out Vector3 wrappedForward);
                path.Evaluate(d + total, out Vector3 lapped, out Vector3 lappedForward);
                Assert.Less(Vector3.Distance(wrapped, lapped), 0.01f, "d " + d);
                Assert.Greater(Vector3.Dot(wrappedForward, lappedForward), 0.9999f, "d " + d);
            }

            path.Evaluate(total - 0.02f, out _, out Vector3 beforeSeam);
            path.Evaluate(0.02f, out _, out Vector3 afterSeam);
            Assert.Less(Vector3.Angle(beforeSeam, afterSeam), 1f);
        }

        private void SetClosed(bool closed)
        {
            SerializedObject serialized = new SerializedObject(m_Layout);
            serialized.FindProperty("m_ClosedLoop").boolValue = closed;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private void SetSegments(params TrackSegment[] segments)
        {
            SerializedObject serialized = new SerializedObject(m_Layout);
            SerializedProperty array = serialized.FindProperty("m_Segments");
            array.arraySize = segments.Length;

            for (int i = 0; i < segments.Length; i++)
            {
                SerializedProperty element = array.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("m_Type").enumValueIndex = (int)segments[i].Type;
                element.FindPropertyRelative("m_Length").floatValue = segments[i].Length;
                element.FindPropertyRelative("m_Angle").floatValue = segments[i].Angle;
                element.FindPropertyRelative("m_Radius").floatValue = segments[i].Radius;
                element.FindPropertyRelative("m_Rise").floatValue = segments[i].Rise;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [Test]
        public void RaceLengthFollowsTheActiveTrack()
        {
            RaceConfigSO config = Object.Instantiate(RaceTestKit.LoadConfig());
            TrackLayoutSO layout = CreateLayoutWithLength(777f);
            TrackLayoutSO fallback = CreateLayoutWithLength(555f);

            SerializedObject configSerialized = new SerializedObject(config);
            configSerialized.FindProperty("m_TrackLayout").objectReferenceValue = fallback;
            configSerialized.ApplyModifiedPropertiesWithoutUndo();

            config.UseTrack(layout);
            Assert.AreEqual(777f, config.RaceLengthMeters, 1e-3f);
            Assert.AreSame(layout, config.TrackLayout);
            Assert.AreSame(fallback, config.DefaultTrackLayout, "selecting a track must not overwrite the default");

            config.UseTrack(null);
            Assert.AreEqual(555f, config.RaceLengthMeters, 1e-3f);

            configSerialized.FindProperty("m_TrackLayout").objectReferenceValue = null;
            configSerialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.AreEqual(config.DefaultRaceLengthMeters, config.RaceLengthMeters, 1e-3f);

            Object.DestroyImmediate(layout);
            Object.DestroyImmediate(fallback);
            Object.DestroyImmediate(config);
        }

        private static TrackLayoutSO CreateLayoutWithLength(float meters)
        {
            TrackLayoutSO layout = ScriptableObject.CreateInstance<TrackLayoutSO>();
            SerializedObject serialized = new SerializedObject(layout);
            serialized.FindProperty("m_RaceLengthMeters").floatValue = meters;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return layout;
        }

        [Test]
        public void BackdropStaysClearOfTheRoadOnEveryTrack()
        {
            RaceConfigSO config = RaceTestKit.LoadConfig();
            string[] layoutPaths =
            {
                "Assets/_Main/SO/Race/Tracks/TrackLayout_Loop.asset",
                "Assets/_Main/SO/Race/Tracks/TrackLayout_Straight.asset"
            };

            foreach (string layoutPath in layoutPaths)
            {
                TrackLayoutSO layout = AssetDatabase.LoadAssetAtPath<TrackLayoutSO>(layoutPath);
                Assert.IsNotNull(layout, layoutPath);

                RaceEnvironmentSO environment = layout.Environment != null ? layout.Environment : config.Environment;
                if (environment == null || environment.BackdropStyle == EBackdropStyle.None)
                    continue;

                RacePath path = RacePath.FromLayout(layout);
                float half = config.RoadWidthMeters * 0.5f;
                float roadFrom = path.IsClosed ? 0f : -config.RunoutMeters;
                float roadTo = path.IsClosed
                    ? path.TotalLength
                    : Mathf.Max(path.TotalLength, layout.RaceLengthMeters + config.RunoutMeters);

                List<Mesh> meshes = TrackBackdrop.Build(path, environment, half, -config.ViewSideSign, roadFrom, roadTo,
                    layout.GroundHeight);
                try
                {
                    Assert.Greater(meshes.Count, 0, layoutPath + " built no backdrop");

                    List<Vector2> road = new List<Vector2>();
                    for (float d = roadFrom; d <= roadTo; d += 2f)
                    {
                        Vector3 point = path.GetPosition(d);
                        road.Add(new Vector2(point.x, point.z));
                    }

                    float nearest = float.MaxValue;
                    foreach (Mesh mesh in meshes)
                    {
                        foreach (Vector3 vertex in mesh.vertices)
                        {
                            Vector2 flat = new Vector2(vertex.x, vertex.z);
                            for (int i = 0; i < road.Count; i++)
                                nearest = Mathf.Min(nearest, Vector2.Distance(road[i], flat));
                        }
                    }

                    Assert.Greater(nearest, half + 5f, layoutPath + " backdrop reaches the road");
                }
                finally
                {
                    foreach (Mesh mesh in meshes)
                        Object.DestroyImmediate(mesh);
                }
            }
        }

        [Test]
        public void TrackMusicSwitchesBetweenLobbyAndRace()
        {
            AudioClip lobby = AudioClip.Create("Lobby", 441, 1, 44100, false);
            AudioClip race = AudioClip.Create("Race", 441, 1, 44100, false);
            try
            {
                Assert.IsFalse(m_Layout.TryGetMusic(false, out _, out _), "an empty track has no lobby music");

                SerializedObject serialized = new SerializedObject(m_Layout);
                serialized.FindProperty("m_LobbyMusic").objectReferenceValue = lobby;
                serialized.FindProperty("m_LobbyMusicVolume").floatValue = 0.4f;
                serialized.FindProperty("m_RaceMusic").objectReferenceValue = race;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.IsTrue(m_Layout.TryGetMusic(false, out AudioClip clip, out float volume));
                Assert.AreSame(lobby, clip);
                Assert.AreEqual(0.4f, volume, 1e-4f);

                Assert.IsTrue(m_Layout.TryGetMusic(true, out clip, out volume));
                Assert.AreSame(race, clip);
                Assert.AreEqual(0.8f, volume, 1e-4f);
            }
            finally
            {
                Object.DestroyImmediate(lobby);
                Object.DestroyImmediate(race);
            }
        }
    }
}
