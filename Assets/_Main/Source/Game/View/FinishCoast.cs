using System;
using UnityEngine;

namespace MatchRacers
{
    public static class FinishCoast
    {
        private const float MinSpeed = 0.1f;
        private const float RunoutMargin = 1f;

        public static float GetStopDistance(RaceConfigSO config, float finishSpeed, int carIndex, float finishTime,
            bool closedRoute)
        {
            if (finishSpeed <= MinSpeed)
                return 0f;

            float natural = finishSpeed * finishSpeed / (2f * config.FinishCoastDeceleration);
            float stop = Mathf.Clamp(natural, config.FinishCoastMinMeters, config.FinishCoastMaxMeters);

            uint seed = (uint)(carIndex + 1) * 2654435761u ^ (uint)BitConverter.SingleToInt32Bits(finishTime);
            Xorshift rng = new Xorshift(seed);
            stop += rng.Range(0f, config.FinishCoastSpreadMeters);

            if (!closedRoute)
                stop = Mathf.Min(stop, Mathf.Max(0f, config.RunoutMeters - RunoutMargin));

            return stop;
        }

        public static float Evaluate(float finishSpeed, float stopDistance, float elapsed)
        {
            if (elapsed <= 0f)
                return finishSpeed * elapsed;

            if (stopDistance <= 0f || finishSpeed <= MinSpeed)
                return 0f;

            float duration = 2f * stopDistance / finishSpeed;
            if (elapsed >= duration)
                return stopDistance;

            float deceleration = finishSpeed / duration;
            return finishSpeed * elapsed - 0.5f * deceleration * elapsed * elapsed;
        }
    }
}
