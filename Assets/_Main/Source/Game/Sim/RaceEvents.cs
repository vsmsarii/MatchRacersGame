namespace MatchRacers
{
    public readonly struct PlayerBuffRequested
    {
        public readonly int Key;

        public PlayerBuffRequested(int key)
        {
            Key = key;
        }
    }

    public readonly struct RaceRestartRequested
    {
    }

    public readonly struct BuffAccepted
    {
        public readonly int CarIndex;
        public readonly int Key;
        public readonly float Time;

        public BuffAccepted(int carIndex, int key, float time)
        {
            CarIndex = carIndex;
            Key = key;
            Time = time;
        }
    }

    public readonly struct BuffRejected
    {
        public readonly int CarIndex;
        public readonly int Key;
        public readonly EBuffRejectReason Reason;
        public readonly float Time;

        public BuffRejected(int carIndex, int key, EBuffRejectReason reason, float time)
        {
            CarIndex = carIndex;
            Key = key;
            Reason = reason;
            Time = time;
        }
    }

    public readonly struct BuffExpired
    {
        public readonly int CarIndex;
        public readonly int Key;
        public readonly float Time;

        public BuffExpired(int carIndex, int key, float time)
        {
            CarIndex = carIndex;
            Key = key;
            Time = time;
        }
    }

    public readonly struct RaceStateChanged
    {
        public readonly ERaceState State;
        public readonly float Time;

        public RaceStateChanged(ERaceState state, float time)
        {
            State = state;
            Time = time;
        }
    }

    public readonly struct CarFinished
    {
        public readonly int CarIndex;
        public readonly int FinishOrder;
        public readonly float FinishTime;

        public CarFinished(int carIndex, int finishOrder, float finishTime)
        {
            CarIndex = carIndex;
            FinishOrder = finishOrder;
            FinishTime = finishTime;
        }
    }

    public readonly struct OvertakeOccurred
    {
        public readonly int PassingCarIndex;
        public readonly int PassedCarIndex;
        public readonly float Time;

        public OvertakeOccurred(int passingCarIndex, int passedCarIndex, float time)
        {
            PassingCarIndex = passingCarIndex;
            PassedCarIndex = passedCarIndex;
            Time = time;
        }
    }
}
