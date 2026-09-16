namespace MatchRacers
{
    public sealed class CarState
    {
        public int CarIndex;
        public int LaneIndex;
        public bool IsPlayer;

        public float Distance;
        public float PreviousDistance;
        public float Speed;
        public float BaseSpeedMultiplier = 1f;
        public float BalanceMultiplier = 1f;
        public float PaceBias;
        public int CommandedBuffKey;
        public bool HoldBuffs;
        public int MaxBuffKey = BuffTableSO.MaxKey;
        public int PaceIntervention;
        public float NearGateWeight;

        public int ActiveBuffKey;
        public int BuffStepsRemaining;
        public int LaunchStepsRemaining;
        public int CooldownStepsRemaining;
        public readonly int[] KeyCooldownSteps = new int[BuffTableSO.MaxKey + 1];
        public float Energy;

        public bool Finished;
        public float FinishTime;
        public int FinishOrder;
        public float FinishSpeed;

        public int AcceptedBuffCount;
        public int RejectedBuffCount;
        public float SpentEnergy;

        public bool HasActiveBuff => ActiveBuffKey > 0;
        public bool IsLaunching => LaunchStepsRemaining > 0;

        public float GetRenderDistance(float alpha)
        {
            return PreviousDistance + (Distance - PreviousDistance) * alpha;
        }

        public void Reset(float energy)
        {
            Distance = 0f;
            PreviousDistance = 0f;
            Speed = 0f;
            BalanceMultiplier = 1f;
            PaceBias = 0f;
            CommandedBuffKey = 0;
            HoldBuffs = false;
            MaxBuffKey = BuffTableSO.MaxKey;
            PaceIntervention = 0;
            NearGateWeight = 0f;
            ActiveBuffKey = 0;
            BuffStepsRemaining = 0;
            LaunchStepsRemaining = 0;
            CooldownStepsRemaining = 0;

            for (int i = 0; i < KeyCooldownSteps.Length; i++)
                KeyCooldownSteps[i] = 0;

            Energy = energy;
            Finished = false;
            FinishTime = 0f;
            FinishOrder = 0;
            FinishSpeed = 0f;
            AcceptedBuffCount = 0;
            RejectedBuffCount = 0;
            SpentEnergy = 0f;
        }
    }
}
