using System.Collections.Generic;
using UnityEngine;

namespace MatchRacers
{
    public sealed class RacePath
    {
        private const int DefaultSamplesPerSegment = 24;

        private readonly Vector3[] m_Samples;
        private readonly float[] m_Cumulative;

        public float TotalLength => m_Cumulative[m_Cumulative.Length - 1];
        public int SampleCount => m_Samples.Length;

        public RacePath(IReadOnlyList<Vector3> waypoints, int samplesPerSegment = DefaultSamplesPerSegment)
        {
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

        public static RacePath CreateStraight(float length, float runout)
        {
            if (length < 1f)
                length = 1f;
            if (runout < 0f)
                runout = 0f;

            return new RacePath(new[]
            {
                new Vector3(-runout, 0f, 0f),
                new Vector3(length * 0.5f, 0f, 0f),
                new Vector3(length + runout, 0f, 0f)
            }, 2);
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
            forward = SegmentDirection(index);
        }

        private Vector3 SegmentDirection(int index)
        {
            index = Mathf.Clamp(index, 0, m_Samples.Length - 2);
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
