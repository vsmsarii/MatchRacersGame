namespace MatchRacers
{
    public sealed class PlayerAgent : IRaceAgent
    {
        private readonly int m_CarIndex;
        private int m_PendingKey;

        public int CarIndex => m_CarIndex;

        public PlayerAgent(int carIndex)
        {
            m_CarIndex = carIndex;
        }

        public void Reset(uint seed)
        {
            m_PendingKey = 0;
        }

        public void Request(int key)
        {
            if (!BuffTableSO.IsValidKey(key))
                return;

            if (m_PendingKey == 0)
                m_PendingKey = key;
        }

        public int PollBuffKey(IRaceView view, float stepTime, float stepDelta)
        {
            int key = m_PendingKey;
            m_PendingKey = 0;
            return key;
        }
    }
}
