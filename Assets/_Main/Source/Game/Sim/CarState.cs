namespace MatchRacers
{
    public sealed class CarState
    {
        public int CarIndex;
        public int LaneIndex;
        public bool IsPlayer;

        public float Distance;
        public float Speed;
        public float BaseSpeedMultiplier = 1f;
        public float BalanceMultiplier = 1f;
        public float PaceBias;

        public int ActiveBuffKey;
        public int BuffStepsRemaining;
        public int CooldownStepsRemaining;
        public float Energy;

        public bool Finished;
        public float FinishTime;
        public int FinishOrder;

        public int AcceptedBuffCount;
        public int RejectedBuffCount;
        public float SpentEnergy;

        public bool HasActiveBuff => ActiveBuffKey > 0;

        public void Reset(float energy)
        {
            Distance = 0f;
            Speed = 0f;
            BalanceMultiplier = 1f;
            PaceBias = 0f;
            ActiveBuffKey = 0;
            BuffStepsRemaining = 0;
            CooldownStepsRemaining = 0;
            Energy = energy;
            Finished = false;
            FinishTime = 0f;
            FinishOrder = 0;
            AcceptedBuffCount = 0;
            RejectedBuffCount = 0;
            SpentEnergy = 0f;
        }
    }
}
