using System.Collections.Generic;
using UnityEngine;

namespace MatchRacers
{
    public static class TrackLoopSolver
    {
        private const float AngleEpsilon = 0.01f;
        private const float LengthEpsilon = 0.01f;

        public static bool TrySolve(Vector3 from, float fromHeading, Vector3 to, float toHeading, float radius,
            List<TrackSegment> result)
        {
            result.Clear();

            Vector3 gap = Flat(to - from);
            if (gap.magnitude < 0.05f && Mathf.Abs(Mathf.DeltaAngle(fromHeading, toHeading)) < 0.5f)
                return true;

            float bestCost = float.MaxValue;
            float bestFirst = 0f;
            float bestStraight = 0f;
            float bestSecond = 0f;
            bool found = false;

            for (int first = -1; first <= 1; first += 2)
            {
                for (int second = -1; second <= 1; second += 2)
                {
                    if (!TryCsc(from, fromHeading, to, toHeading, radius, first, second,
                            out float firstAngle, out float straight, out float secondAngle))
                        continue;

                    float cost = (Mathf.Abs(firstAngle) + Mathf.Abs(secondAngle)) * Mathf.Deg2Rad * radius + straight;
                    if (cost >= bestCost)
                        continue;

                    bestCost = cost;
                    bestFirst = firstAngle;
                    bestStraight = straight;
                    bestSecond = secondAngle;
                    found = true;
                }
            }

            if (!found)
                return false;

            float firstLength = Mathf.Abs(bestFirst) * Mathf.Deg2Rad * radius;
            float secondLength = Mathf.Abs(bestSecond) * Mathf.Deg2Rad * radius;
            float total = Mathf.Max(1e-3f, firstLength + bestStraight + secondLength);
            float rise = to.y - from.y;

            if (Mathf.Abs(bestFirst) > AngleEpsilon)
                result.Add(TrackSegment.Turn(bestFirst, radius, rise * firstLength / total));
            if (bestStraight > LengthEpsilon)
                result.Add(TrackSegment.Straight(bestStraight, rise * bestStraight / total));
            if (Mathf.Abs(bestSecond) > AngleEpsilon)
                result.Add(TrackSegment.Turn(bestSecond, radius, rise * secondLength / total));

            if (result.Count == 0)
                result.Add(TrackSegment.Straight(Mathf.Max(TrackSegment.MinLength, gap.magnitude), rise));

            return true;
        }

        private static bool TryCsc(Vector3 from, float fromHeading, Vector3 to, float toHeading, float radius,
            int firstSign, int secondSign, out float firstAngle, out float straight, out float secondAngle)
        {
            Vector3 firstCenter = Flat(from) + RightNormal(fromHeading) * (radius * firstSign);
            Vector3 secondCenter = Flat(to) + RightNormal(toHeading) * (radius * secondSign);
            Vector3 between = secondCenter - firstCenter;
            float distance = between.magnitude;
            float heading = Mathf.Atan2(between.x, between.z) * Mathf.Rad2Deg;

            firstAngle = 0f;
            secondAngle = 0f;
            straight = 0f;

            float tangentHeading;
            if (firstSign == secondSign)
            {
                if (distance < 1e-4f)
                    return false;

                straight = distance;
                tangentHeading = heading;
            }
            else
            {
                float diameter = 2f * radius;
                if (distance < diameter)
                    return false;

                straight = Mathf.Sqrt(distance * distance - diameter * diameter);
                float skew = Mathf.Atan2(diameter, straight) * Mathf.Rad2Deg;
                tangentHeading = firstSign > 0 ? heading + skew : heading - skew;
            }

            firstAngle = Sweep(fromHeading, tangentHeading, firstSign);
            secondAngle = Sweep(tangentHeading, toHeading, secondSign);
            return true;
        }

        private static float Sweep(float fromHeading, float toHeading, int sign)
        {
            float sweep = sign > 0
                ? Mathf.Repeat(toHeading - fromHeading, 360f)
                : Mathf.Repeat(fromHeading - toHeading, 360f);

            if (sweep > 360f - 1e-3f)
                sweep = 0f;

            return sweep * sign;
        }

        private static Vector3 RightNormal(float heading)
        {
            return TrackLayoutSO.HeadingToDirection(heading + 90f);
        }

        private static Vector3 Flat(Vector3 value)
        {
            value.y = 0f;
            return value;
        }
    }
}
