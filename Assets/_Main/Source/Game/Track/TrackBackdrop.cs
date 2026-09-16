using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Random = System.Random;

namespace MatchRacers
{
    public sealed class TrackBackdrop
    {
        private const float ClearanceShare = 0.75f;
        private const float SinkDepth = 25f;
        private const float HillStep = 4f;
        private const float HillCrestShare = 0.45f;
        private const float HillBackHeight = 0.35f;
        private const float HillBackShade = 0.6f;
        private const int HillTaperSamples = 8;
        private const float OpenEndExtension = 220f;
        private const float FootprintMargin = 0.5f;

        private readonly RacePath m_Path;
        private readonly PathClearance m_Clearance;
        private readonly List<Footprint> m_Footprints = new List<Footprint>();
        private readonly float m_RoadHalfWidth;
        private readonly float m_FarSign;
        private readonly float m_GroundOffset;
        private readonly float m_From;
        private readonly float m_To;
        private readonly bool m_Closed;

        private TrackBackdrop(RacePath path, float roadHalfWidth, float farSign, float roadFrom, float roadTo, float groundOffset)
        {
            m_Path = path;
            m_Clearance = new PathClearance(path, roadFrom, roadTo);
            m_RoadHalfWidth = roadHalfWidth;
            m_FarSign = farSign;
            m_GroundOffset = groundOffset;
            m_Closed = path.IsClosed;
            m_From = m_Closed ? 0f : roadFrom - OpenEndExtension;
            m_To = m_Closed ? path.TotalLength : roadTo + OpenEndExtension;
        }

        public static List<Mesh> Build(RacePath path, RaceEnvironmentSO environment, float roadHalfWidth, float farSign,
            float roadFrom, float roadTo, float groundOffset)
        {
            List<Mesh> meshes = new List<Mesh>();
            if (path == null || environment == null || environment.BackdropStyle == EBackdropStyle.None)
                return meshes;

            TrackBackdrop backdrop = new TrackBackdrop(path, roadHalfWidth, farSign, roadFrom, roadTo, groundOffset);

            for (int i = 0; i < environment.BackdropLayerCount; i++)
            {
                BackdropLayer layer = environment.GetBackdropLayer(i);
                if (layer == null)
                    continue;

                Random random = new Random(environment.BackdropSeed * 7919 + i * 104729);
                BackdropMesh mesh = new BackdropMesh();

                if (environment.BackdropStyle == EBackdropStyle.City)
                    backdrop.BuildCity(layer, random, mesh);
                else
                    backdrop.BuildHills(layer, random, mesh);

                if (!mesh.IsEmpty)
                    meshes.Add(mesh.ToMesh("Backdrop_" + i));
            }

            return meshes;
        }

        private void BuildCity(BackdropLayer layer, Random random, BackdropMesh mesh)
        {
            float required = m_RoadHalfWidth + layer.Offset * ClearanceShare;
            float cursor = m_From;

            while (cursor < m_To)
            {
                float width = layer.Spacing * Range(random, 0.6f, 1.4f);
                float gap = layer.Spacing * Range(random, 0.08f, 0.4f);
                float depth = layer.Depth * Range(random, 0.55f, 1f);
                float inset = (layer.Depth - depth) * Next(random);
                float height = Mathf.Lerp(layer.MinHeight, layer.MaxHeight, Mathf.Pow(Next(random), 1.6f));
                float shade = Range(random, 0.85f, 1.15f);
                float seed = Next(random);
                float center = cursor + width * 0.5f;
                cursor += width + gap;

                m_Path.Evaluate(center, out Vector3 position, out Vector3 forward);
                Vector3 along = RacePath.Flatten(forward);
                Vector3 outward = RacePath.RightOf(forward) * m_FarSign;
                Vector3 front = position + outward * (m_RoadHalfWidth + layer.Offset + inset);
                Vector3 halfAlong = along * (width * 0.5f);
                Vector3 back = outward * depth;

                if (!m_Clearance.IsClear(front - halfAlong, required) || !m_Clearance.IsClear(front + halfAlong, required) ||
                    !m_Clearance.IsClear(front - halfAlong + back, required) || !m_Clearance.IsClear(front + halfAlong + back, required))
                    continue;

                Footprint footprint = new Footprint(front + back * 0.5f, along, outward, width * 0.5f, depth * 0.5f);
                if (Overlaps(footprint))
                    continue;

                m_Footprints.Add(footprint);
                Color color = layer.Color * shade;
                mesh.AddBuilding(front, along, outward, width, depth, position.y + m_GroundOffset, height, color, layer.Windows, seed);
            }
        }

        private void BuildHills(BackdropLayer layer, Random random, BackdropMesh mesh)
        {
            float required = m_RoadHalfWidth + layer.Offset * ClearanceShare;
            float span = m_To - m_From;
            int segments = Mathf.Max(2, Mathf.CeilToInt(span / HillStep));
            float step = span / segments;
            int count = m_Closed ? segments : segments + 1;
            float phase = Next(random) * 1000f;

            Vector3[] fronts = new Vector3[count];
            Vector3[] crests = new Vector3[count];
            Vector3[] backs = new Vector3[count];
            float[] grounds = new float[count];
            float[] heights = new float[count];
            bool[] valid = new bool[count];

            for (int i = 0; i < count; i++)
            {
                float distance = m_From + step * i;
                m_Path.Evaluate(distance, out Vector3 position, out Vector3 forward);
                Vector3 outward = RacePath.RightOf(forward) * m_FarSign;

                fronts[i] = position + outward * (m_RoadHalfWidth + layer.Offset);
                crests[i] = fronts[i] + outward * (layer.Depth * HillCrestShare);
                backs[i] = fronts[i] + outward * layer.Depth;
                grounds[i] = position.y + m_GroundOffset;
                heights[i] = Mathf.Lerp(layer.MinHeight, layer.MaxHeight, Ridge(distance / layer.Spacing + phase));
                valid[i] = m_Clearance.IsClear(fronts[i], required) && m_Clearance.IsClear(crests[i], required) &&
                           m_Clearance.IsClear(backs[i], required);
            }

            Taper(valid, heights, m_Closed);

            int[] columns = new int[count];
            for (int i = 0; i < count; i++)
            {
                columns[i] = valid[i]
                    ? mesh.AddHillColumn(fronts[i], crests[i], backs[i], grounds[i], heights[i], m_From + step * i, layer.Color)
                    : -1;
            }

            int links = m_Closed ? count : count - 1;
            for (int i = 0; i < links; i++)
            {
                int next = (i + 1) % count;
                if (columns[i] >= 0 && columns[next] >= 0)
                    mesh.LinkHillColumns(columns[i], columns[next]);
            }
        }

        private bool Overlaps(Footprint footprint)
        {
            for (int i = 0; i < m_Footprints.Count; i++)
            {
                if (Footprint.Intersects(footprint, m_Footprints[i]))
                    return true;
            }

            return false;
        }

        private static void Taper(bool[] valid, float[] heights, bool closed)
        {
            List<int> blocked = new List<int>();
            for (int i = 0; i < valid.Length; i++)
            {
                if (!valid[i])
                    blocked.Add(i);
            }

            if (blocked.Count == 0)
                return;

            int count = valid.Length;
            for (int i = 0; i < count; i++)
            {
                if (!valid[i])
                    continue;

                int nearest = int.MaxValue;
                for (int b = 0; b < blocked.Count; b++)
                {
                    int gap = Mathf.Abs(i - blocked[b]);
                    if (closed)
                        gap = Mathf.Min(gap, count - gap);

                    nearest = Mathf.Min(nearest, gap);
                }

                if (nearest < HillTaperSamples)
                    heights[i] *= Mathf.SmoothStep(0f, 1f, nearest / (float)HillTaperSamples);
            }
        }

        private static float Ridge(float x)
        {
            float value = Noise(x) * 0.6f + Noise(x * 2.3f + 17f) * 0.28f + Noise(x * 5.1f + 41f) * 0.12f;
            return Mathf.SmoothStep(0f, 1f, value);
        }

        private static float Noise(float x)
        {
            int index = Mathf.FloorToInt(x);
            float t = x - index;
            t = t * t * (3f - 2f * t);
            return Mathf.Lerp(Hash(index), Hash(index + 1), t);
        }

        private static float Hash(int value)
        {
            unchecked
            {
                uint hash = (uint)value * 2654435761u;
                hash ^= hash >> 15;
                hash *= 2246822519u;
                hash ^= hash >> 13;
                return (hash & 0xFFFFFF) / 16777215f;
            }
        }

        private static float Next(Random random)
        {
            return (float)random.NextDouble();
        }

        private static float Range(Random random, float min, float max)
        {
            return Mathf.Lerp(min, max, Next(random));
        }

        private readonly struct Footprint
        {
            private readonly Vector2 m_Center;
            private readonly Vector2 m_Along;
            private readonly Vector2 m_Outward;
            private readonly float m_HalfWidth;
            private readonly float m_HalfDepth;

            public Footprint(Vector3 center, Vector3 along, Vector3 outward, float halfWidth, float halfDepth)
            {
                m_Center = new Vector2(center.x, center.z);
                m_Along = new Vector2(along.x, along.z).normalized;
                m_Outward = new Vector2(outward.x, outward.z).normalized;
                m_HalfWidth = Mathf.Max(0f, halfWidth - FootprintMargin);
                m_HalfDepth = Mathf.Max(0f, halfDepth - FootprintMargin);
            }

            public static bool Intersects(Footprint a, Footprint b)
            {
                return !Separated(a, b, a.m_Along) && !Separated(a, b, a.m_Outward) &&
                       !Separated(a, b, b.m_Along) && !Separated(a, b, b.m_Outward);
            }

            private static bool Separated(Footprint a, Footprint b, Vector2 axis)
            {
                float distance = Mathf.Abs(Vector2.Dot(b.m_Center - a.m_Center, axis));
                return distance >= a.Extent(axis) + b.Extent(axis);
            }

            private float Extent(Vector2 axis)
            {
                return m_HalfWidth * Mathf.Abs(Vector2.Dot(m_Along, axis)) + m_HalfDepth * Mathf.Abs(Vector2.Dot(m_Outward, axis));
            }
        }

        private sealed class PathClearance
        {
            private const float CellSize = 32f;
            private const float SampleStep = 2f;

            private readonly Dictionary<long, List<Vector2>> m_Cells = new Dictionary<long, List<Vector2>>();

            public PathClearance(RacePath path, float from, float to)
            {
                int steps = Mathf.Max(1, Mathf.CeilToInt((to - from) / SampleStep));
                for (int i = 0; i <= steps; i++)
                {
                    Vector3 point = path.GetPosition(Mathf.Lerp(from, to, i / (float)steps));
                    long key = Key(Cell(point.x), Cell(point.z));
                    if (!m_Cells.TryGetValue(key, out List<Vector2> samples))
                    {
                        samples = new List<Vector2>();
                        m_Cells.Add(key, samples);
                    }

                    samples.Add(new Vector2(point.x, point.z));
                }
            }

            public bool IsClear(Vector3 point, float radius)
            {
                int cellX = Cell(point.x);
                int cellZ = Cell(point.z);
                int reach = Mathf.CeilToInt(radius / CellSize);
                float limit = radius * radius;
                Vector2 flat = new Vector2(point.x, point.z);

                for (int dx = -reach; dx <= reach; dx++)
                {
                    for (int dz = -reach; dz <= reach; dz++)
                    {
                        if (!m_Cells.TryGetValue(Key(cellX + dx, cellZ + dz), out List<Vector2> samples))
                            continue;

                        for (int i = 0; i < samples.Count; i++)
                        {
                            if ((samples[i] - flat).sqrMagnitude < limit)
                                return false;
                        }
                    }
                }

                return true;
            }

            private static int Cell(float value)
            {
                return Mathf.FloorToInt(value / CellSize);
            }

            private static long Key(int x, int z)
            {
                return ((long)x << 32) ^ (uint)z;
            }
        }

        private sealed class BackdropMesh
        {
            private readonly List<Vector3> m_Vertices = new List<Vector3>();
            private readonly List<Color> m_Colors = new List<Color>();
            private readonly List<Vector2> m_Uvs = new List<Vector2>();
            private readonly List<Vector2> m_Data = new List<Vector2>();
            private readonly List<int> m_Triangles = new List<int>();

            public bool IsEmpty => m_Triangles.Count == 0;

            public void AddBuilding(Vector3 front, Vector3 along, Vector3 outward, float width, float depth, float ground,
                float height, Color color, float windows, float seed)
            {
                float bottom = ground - SinkDepth;
                float top = ground + height;
                Vector3 halfAlong = along * (width * 0.5f);
                Vector3 back = outward * depth;
                Vector3 frontLeft = front - halfAlong;
                Vector3 frontRight = front + halfAlong;
                Vector3 backLeft = frontLeft + back;
                Vector3 backRight = frontRight + back;

                Color facade = new Color(color.r, color.g, color.b, windows);
                Color roof = new Color(color.r, color.g, color.b, 0f);

                AddWall(frontLeft, frontRight, bottom, top, ground, height, 0f, facade, seed);
                AddWall(backLeft, frontLeft, bottom, top, ground, height, width, facade, seed);
                AddWall(frontRight, backRight, bottom, top, ground, height, width + depth, facade, seed);

                int start = m_Vertices.Count;
                AddVertex(At(frontLeft, top), new Vector2(0f, height), new Vector2(seed, 1f), roof);
                AddVertex(At(backLeft, top), new Vector2(0f, height), new Vector2(seed, 1f), roof);
                AddVertex(At(backRight, top), new Vector2(0f, height), new Vector2(seed, 1f), roof);
                AddVertex(At(frontRight, top), new Vector2(0f, height), new Vector2(seed, 1f), roof);
                AddQuad(start, start + 1, start + 2, start + 3);
            }

            public int AddHillColumn(Vector3 front, Vector3 crest, Vector3 back, float ground, float height, float distance, Color color)
            {
                Color tint = new Color(color.r, color.g, color.b, 0f);
                float backHeight = height * HillBackHeight;

                int start = m_Vertices.Count;
                AddVertex(At(front, ground - SinkDepth), new Vector2(distance, -SinkDepth), Vector2.zero, tint);
                AddVertex(At(crest, ground + height), new Vector2(distance, height), new Vector2(0f, 1f), tint);
                AddVertex(At(back, ground + backHeight), new Vector2(distance, backHeight), new Vector2(0f, HillBackShade), tint);
                return start;
            }

            public void LinkHillColumns(int a, int b)
            {
                AddQuad(a, a + 1, b + 1, b);
                AddQuad(a + 1, a + 2, b + 2, b + 1);
            }

            public Mesh ToMesh(string meshName)
            {
                Mesh mesh = new Mesh { name = meshName };
                if (m_Vertices.Count > 65000)
                    mesh.indexFormat = IndexFormat.UInt32;

                mesh.SetVertices(m_Vertices);
                mesh.SetColors(m_Colors);
                mesh.SetUVs(0, m_Uvs);
                mesh.SetUVs(1, m_Data);
                mesh.SetTriangles(m_Triangles, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }

            private void AddWall(Vector3 start, Vector3 end, float bottom, float top, float ground, float height, float offset,
                Color color, float seed)
            {
                float length = Vector2.Distance(new Vector2(start.x, start.z), new Vector2(end.x, end.z));
                float low = bottom - ground;
                float lowShade = height > 0f ? low / height : 0f;

                int first = m_Vertices.Count;
                AddVertex(At(start, bottom), new Vector2(offset, low), new Vector2(seed, lowShade), color);
                AddVertex(At(start, top), new Vector2(offset, height), new Vector2(seed, 1f), color);
                AddVertex(At(end, top), new Vector2(offset + length, height), new Vector2(seed, 1f), color);
                AddVertex(At(end, bottom), new Vector2(offset + length, low), new Vector2(seed, lowShade), color);
                AddQuad(first, first + 1, first + 2, first + 3);
            }

            private void AddVertex(Vector3 position, Vector2 uv, Vector2 data, Color color)
            {
                m_Vertices.Add(position);
                m_Uvs.Add(uv);
                m_Data.Add(data);
                m_Colors.Add(color);
            }

            private void AddQuad(int a, int b, int c, int d)
            {
                m_Triangles.Add(a);
                m_Triangles.Add(b);
                m_Triangles.Add(c);
                m_Triangles.Add(a);
                m_Triangles.Add(c);
                m_Triangles.Add(d);
            }

            private static Vector3 At(Vector3 point, float y)
            {
                return new Vector3(point.x, y, point.z);
            }
        }
    }
}
