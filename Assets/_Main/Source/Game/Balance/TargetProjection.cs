namespace MatchRacers
{
    public static class TargetProjection
    {
        private const int MaxEvents = 400;
        private const int BisectionIterations = 50;
        private const double ReadyEpsilon = 1e-9;

        private static readonly double[] s_KeyCooldown = new double[BuffTableSO.MaxKey + 1];

        public static double PaceIntegral(double pace0, double target, double rate, double tau)
        {
            if (tau <= 0.0)
                return 0.0;

            if (System.Math.Abs(target - pace0) < 1e-12 || rate <= 0.0)
                return pace0 * tau;

            double sign = target > pace0 ? 1.0 : -1.0;
            double rampTime = System.Math.Abs(target - pace0) / rate;

            if (tau <= rampTime)
                return pace0 * tau + sign * rate * tau * tau * 0.5;

            return pace0 * rampTime + sign * rate * rampTime * rampTime * 0.5 + target * (tau - rampTime);
        }

        public static double BestFinishTime(CarState car, BuffTableSO table, float baseSpeed, int stepHz,
            double pace0, double paceTarget, double rate, double raceLength, double now)
        {
            double invHz = 1.0 / stepHz;
            double distance = car.Distance;
            double energy = car.Energy;
            double globalCooldown = car.CooldownStepsRemaining * invHz;
            int activeKey = car.ActiveBuffKey;
            double window = car.BuffStepsRemaining * invHz;

            for (int key = BuffTableSO.MinKey; key <= BuffTableSO.MaxKey; key++)
                s_KeyCooldown[key] = car.KeyCooldownSteps[key] * invHz;

            double scale = baseSpeed * car.BaseSpeedMultiplier;
            double energyMax = table.EnergyMax;
            double regen = table.EnergyRegenPerSecond;
            double windowSeconds = table.GetWindowSteps(stepHz) * invHz;
            double gcdSeconds = System.Math.Round(table.GlobalCooldownSeconds * stepHz) * invHz;
            double t = 0.0;

            for (int guard = 0; guard < MaxEvents && distance < raceLength; guard++)
            {
                if (activeKey > 0)
                {
                    double buffSpeed = activeKey * scale;
                    double needed = (raceLength - distance) / buffSpeed;
                    if (needed <= window)
                        return now + t + needed;

                    distance += buffSpeed * window;
                    energy = System.Math.Min(energyMax, energy + regen * window);
                    DecayCooldowns(window);
                    t += window;
                    activeKey = 0;
                    window = 0.0;
                    globalCooldown = gcdSeconds;
                    continue;
                }

                double wait = double.MaxValue;
                for (int key = 2; key <= BuffTableSO.MaxKey; key++)
                {
                    if (!table.TryGetEnergyCost(key, out float cost) || cost > energyMax)
                        continue;

                    double energyWait = energy >= cost ? 0.0 : (regen > 0.0 ? (cost - energy) / regen : double.MaxValue);
                    double ready = System.Math.Max(globalCooldown, System.Math.Max(s_KeyCooldown[key], energyWait));
                    if (ready < wait)
                        wait = ready;
                }

                double horizon = wait < double.MaxValue ? wait : 1e6;
                if (TrySolve(pace0, paceTarget, rate, t, raceLength - distance, scale, t + horizon, out double finish))
                    return now + finish;

                distance += scale * (PaceIntegral(pace0, paceTarget, rate, t + horizon) - PaceIntegral(pace0, paceTarget, rate, t));
                energy = System.Math.Min(energyMax, energy + regen * horizon);
                globalCooldown = System.Math.Max(0.0, globalCooldown - horizon);
                DecayCooldowns(horizon);
                t += horizon;

                int pick = PickGreedyKey(table, energy, globalCooldown);
                if (pick == 0)
                {
                    double nudge = invHz;
                    distance += scale * (PaceIntegral(pace0, paceTarget, rate, t + nudge) - PaceIntegral(pace0, paceTarget, rate, t));
                    energy = System.Math.Min(energyMax, energy + regen * nudge);
                    globalCooldown = System.Math.Max(0.0, globalCooldown - nudge);
                    DecayCooldowns(nudge);
                    t += nudge;
                    continue;
                }

                table.TryGetEnergyCost(pick, out float pickCost);
                energy -= pickCost;
                s_KeyCooldown[pick] = table.GetKeyCooldownSteps(pick, stepHz) * invHz;
                activeKey = pick;
                window = windowSeconds;
            }

            return now + t;
        }

        public static double SlowestFinishTime(double distance, int activeKey, double windowRemaining,
            double speedScale, double pace0, double paceTarget, double rate, double raceLength, double now)
        {
            double t = 0.0;

            if (activeKey > 0 && windowRemaining > 0.0)
            {
                double buffSpeed = activeKey * speedScale;
                double needed = (raceLength - distance) / buffSpeed;
                if (needed <= windowRemaining)
                    return now + needed;

                distance += buffSpeed * windowRemaining;
                t = windowRemaining;
            }

            double slowestPace = System.Math.Max(1e-6, System.Math.Min(pace0, paceTarget));
            double bound = t + (raceLength - distance) / (speedScale * slowestPace)
                           + System.Math.Abs(pace0 - paceTarget) / System.Math.Max(1e-6, rate) + 1.0;

            if (TrySolve(pace0, paceTarget, rate, t, raceLength - distance, speedScale, bound, out double finish))
                return now + finish;

            return now + bound;
        }

        public static int BestReadyKey(CarState car, BuffTableSO table)
        {
            int best = 0;
            double bestRatio = double.MinValue;

            for (int key = 2; key <= BuffTableSO.MaxKey; key++)
            {
                if (car.KeyCooldownSteps[key] > 0)
                    continue;

                if (!table.TryGetEnergyCost(key, out float cost) || cost <= 0f || car.Energy < cost)
                    continue;

                double ratio = (key - 1) / (double)cost;
                if (ratio > bestRatio)
                {
                    bestRatio = ratio;
                    best = key;
                }
            }

            return best;
        }

        private static bool TrySolve(double pace0, double target, double rate, double tStart, double needed,
            double scale, double tEnd, out double finish)
        {
            double baseIntegral = PaceIntegral(pace0, target, rate, tStart);
            finish = tEnd;

            if (scale * (PaceIntegral(pace0, target, rate, tEnd) - baseIntegral) < needed)
                return false;

            double lo = tStart;
            double hi = tEnd;
            for (int i = 0; i < BisectionIterations; i++)
            {
                double mid = (lo + hi) * 0.5;
                if (scale * (PaceIntegral(pace0, target, rate, mid) - baseIntegral) >= needed)
                    hi = mid;
                else
                    lo = mid;
            }

            finish = hi;
            return true;
        }

        private static int PickGreedyKey(BuffTableSO table, double energy, double globalCooldown)
        {
            if (globalCooldown > ReadyEpsilon)
                return 0;

            int best = 0;
            double bestRatio = double.MinValue;

            for (int key = 2; key <= BuffTableSO.MaxKey; key++)
            {
                if (s_KeyCooldown[key] > ReadyEpsilon)
                    continue;

                if (!table.TryGetEnergyCost(key, out float cost) || cost <= 0f || energy < cost - ReadyEpsilon)
                    continue;

                double ratio = (key - 1) / (double)cost;
                if (ratio > bestRatio)
                {
                    bestRatio = ratio;
                    best = key;
                }
            }

            return best;
        }

        private static void DecayCooldowns(double seconds)
        {
            for (int key = BuffTableSO.MinKey; key <= BuffTableSO.MaxKey; key++)
                s_KeyCooldown[key] = System.Math.Max(0.0, s_KeyCooldown[key] - seconds);
        }
    }
}
