namespace MatchRacers
{
    public sealed class NullAgent : IRaceAgent
    {
        private readonly int m_CarIndex;

        public int CarIndex => m_CarIndex;

        public NullAgent(int carIndex)
        {
            m_CarIndex = carIndex;
        }

        public void Reset(uint seed)
        {
        }

        public int PollBuffKey(IRaceView view, float stepTime, float stepDelta)
        {
            return 0;
        }
    }
}
