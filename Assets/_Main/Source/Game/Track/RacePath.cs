using System.Collections.Generic;
using UnityEngine;

namespace MatchRacers
{
    public sealed class RacePath
    {
        private const int DefaultSamplesPerSegment = 24;

        private readonly Vector3[] m_Samples;
        private readonly float[] m_Cumulative;
        private readonly bool m_Closed;

        public float TotalLength => m_Cumulative[m_Cumulative.Length - 1];
        public int SampleCount => m_Samples.Length;
        public bool IsClosed => m_Closed;

        public RacePath(IReadOnlyList<Vector3> waypoints, int samplesPerSegment = DefaultSamplesPerSegment,
            bool closed = false)
        {
            m_Closed = closed && waypoints != null && waypoints.Count >= 3;

            if (waypoints == null || waypoints.Count < 2)
            {
                m_Samples = new[] { Vector3.zero, Vector3.forward };
                m_Cumulative = new[] { 0f, 1f };
                return;
            }

            if (samplesPerSegment < 1)
                samplesPerSegment = 1;

            int segmentCount = waypoints.Count - 1;
            int sampleCount = segmentCount * samplesPerSegment + 1;
            m_Samples = new Vector3[sampleCount];
            m_Cumulative = new float[sampleCount];

            int write = 0;
            for (int segment = 0; segment < segmentCount; segment++)
            {
                Vector3 p0 = waypoints[Mathf.Max(segment - 1, 0)];
                Vector3 p1 = waypoints[segment];
                Vector3 p2 = waypoints[segment + 1];
                Vector3 p3 = waypoints[Mathf.Min(segment + 2, waypoints.Count - 1)];

                for (int i = 0; i < samplesPerSegment; i++)
                {
                    float t = i / (float)samplesPerSegment;
                    m_Samples[write++] = CatmullRom(p0, p1, p2, p3, t);
                }
            }

            m_Samples[write] = waypoints[waypoints.Count - 1];

            m_Cumulative[0] = 0f;
            for (int i = 1; i < m_Samples.Length; i++)
                m_Cumulative[i] = m_Cumulative[i - 1] + Vector3.Distance(m_Samples[i - 1], m_Samples[i]);
        }

        public static RacePath FromConfig(RaceConfigSO config)
        {
            if (config.HasTrackLayout)
                return FromLayout(config.TrackLayout);

            return CreateStraight(config.RaceLengthMeters, config.RunoutMeters);
        }

        public static RacePath FromLayout(TrackLayoutSO layout)
        {
            List<Vector3> points = new List<Vector3>();
            layout.BuildPolyline(points);
            return new RacePath(points, 1, layout.ClosedLoop);
        }

        public static RacePath CreateStraight(float length, float runout)
        {
            if (length < 1f)
                length = 1f;
            if (runout < 0f)
                runout = 0f;

            return new RacePath(new[]
            {
                Vector3.zero,
                new Vector3(length + runout, 0f, 0f)
            }, 1);
        }

        public Vector3 GetPosition(float distance)
        {
            Evaluate(distance, out Vector3 position, out _);
            return position;
        }

        public void Evaluate(float distance, out Vector3 position, out Vector3 forward)
        {
            int last = m_Samples.Length - 1;
            float total = TotalLength;

            if (m_Closed && total > 1e-3f)
                distance = Mathf.Repeat(distance, total);

            if (distance <= 0f)
            {
                forward = SegmentDirection(0);
                position = m_Samples[0] + forward * distance;
                return;
            }

            if (distance >= total)
            {
                forward = SegmentDirection(last - 1);
                position = m_Samples[last] + forward * (distance - total);
                return;
            }

            int index = FindSegment(distance);
            float segmentLength = m_Cumulative[index + 1] - m_Cumulative[index];
            float t = segmentLength > 0f ? (distance - m_Cumulative[index]) / segmentLength : 0f;

            position = Vector3.Lerp(m_Samples[index], m_Samples[index + 1], t);
            forward = t < 0.5f
                ? Blend(SegmentDirection(index - 1), SegmentDirection(index), t + 0.5f)
                : Blend(SegmentDirection(index), SegmentDirection(index + 1), t - 0.5f);
        }

        public static Vector3 RightOf(Vector3 forward)
        {
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            return right.sqrMagnitude > 1e-8f ? right.normalized : Vector3.right;
        }

        public static Vector3 Flatten(Vector3 forward)
        {
            forward.y = 0f;
            return forward.sqrMagnitude > 1e-8f ? forward.normalized : Vector3.forward;
        }

        public Vector3 GetSample(int index)
        {
            return m_Samples[Mathf.Clamp(index, 0, m_Samples.Length - 1)];
        }

        public float GetSampleDistance(int index)
        {
            return m_Cumulative[Mathf.Clamp(index, 0, m_Cumulative.Length - 1)];
        }

        public float FindClosestDistance(Vector3 point)
        {
            float bestDistance = 0f;
            float bestSqr = float.MaxValue;
            int bestIndex = 0;

            for (int i = 0; i < m_Samples.Length - 1; i++)
            {
                Vector3 a = m_Samples[i];
                Vector3 ab = m_Samples[i + 1] - a;
                float lengthSqr = ab.sqrMagnitude;
                float t = lengthSqr > 1e-8f ? Mathf.Clamp01(Vector3.Dot(point - a, ab) / lengthSqr) : 0f;
                float sqr = (a + ab * t - point).sqrMagnitude;

                if (sqr >= bestSqr)
                    continue;

                bestSqr = sqr;
                bestIndex = i;
                bestDistance = Mathf.Lerp(m_Cumulative[i], m_Cumulative[i + 1], t);
            }

            return RefineClosestDistance(point, bestDistance, bestIndex);
        }

        private float RefineClosestDistance(Vector3 point, float coarse, int index)
        {
            float span = 1.5f * Mathf.Max(
                GetSegmentLength(index - 1),
                Mathf.Max(GetSegmentLength(index), GetSegmentLength(index + 1)));
            float total = TotalLength;
            float low = coarse - span;
            float high = coarse + span;

            if (!m_Closed)
            {
                low = Mathf.Max(0f, low);
                high = Mathf.Min(total, high);
            }

            if (AlongTrackOffset(point, low) < 0f || AlongTrackOffset(point, high) > 0f)
                return coarse;

            for (int i = 0; i < 32; i++)
            {
                float middle = (low + high) * 0.5f;
                if (AlongTrackOffset(point, middle) > 0f)
                    low = middle;
                else
                    high = middle;
            }

            float refined = (low + high) * 0.5f;
            return m_Closed && total > 1e-3f ? Mathf.Repeat(refined, total) : refined;
        }

        private float AlongTrackOffset(Vector3 point, float distance)
        {
            Evaluate(distance, out Vector3 position, out Vector3 forward);
            return Vector3.Dot(point - position, Flatten(forward));
        }

        private float GetSegmentLength(int index)
        {
            int segments = m_Samples.Length - 1;
            index = m_Closed ? ((index % segments) + segments) % segments : Mathf.Clamp(index, 0, segments - 1);
            return m_Cumulative[index + 1] - m_Cumulative[index];
        }

        private static Vector3 Blend(Vector3 from, Vector3 to, float t)
        {
            Vector3 blended = Vector3.Lerp(from, to, t);
            return blended.sqrMagnitude > 1e-8f ? blended.normalized : to;
        }

        private Vector3 SegmentDirection(int index)
        {
            int segments = m_Samples.Length - 1;
            index = m_Closed ? ((index % segments) + segments) % segments : Mathf.Clamp(index, 0, segments - 1);
            Vector3 delta = m_Samples[index + 1] - m_Samples[index];
            return delta.sqrMagnitude > 1e-8f ? delta.normalized : Vector3.forward;
        }

        private int FindSegment(float distance)
        {
            int low = 0;
            int high = m_Cumulative.Length - 1;

            while (low < high - 1)
            {
                int mid = (low + high) >> 1;
                if (m_Cumulative[mid] <= distance)
                    low = mid;
                else
                    high = mid;
            }

            return low;
        }

        private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;

            return 0.5f * (
                2f * p1 +
                (p2 - p0) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (3f * p1 - p0 - 3f * p2 + p3) * t3);
        }
    }
}
