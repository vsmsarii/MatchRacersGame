namespace MatchRacers
{
    public sealed class RankingSystem
    {
        private readonly int[] m_Order;
        private readonly int[] m_Position;
        private readonly int[] m_PreviousPosition;

        public RankingSystem(int carCount)
        {
            m_Order = new int[carCount];
            m_Position = new int[carCount];
            m_PreviousPosition = new int[carCount];

            for (int i = 0; i < carCount; i++)
            {
                m_Order[i] = i;
                m_Position[i] = i + 1;
                m_PreviousPosition[i] = i + 1;
            }
        }

        public int GetPosition(int carIndex)
        {
            return carIndex >= 0 && carIndex < m_Position.Length ? m_Position[carIndex] : 0;
        }

        public int GetPreviousPosition(int carIndex)
        {
            return carIndex >= 0 && carIndex < m_PreviousPosition.Length ? m_PreviousPosition[carIndex] : 0;
        }

        public int GetCarAtPosition(int position)
        {
            int rank = position - 1;
            return rank >= 0 && rank < m_Order.Length ? m_Order[rank] : -1;
        }

        public void Rebuild(CarState[] cars)
        {
            for (int i = 0; i < m_Position.Length; i++)
                m_PreviousPosition[i] = m_Position[i];

            for (int i = 1; i < m_Order.Length; i++)
            {
                int carIndex = m_Order[i];
                int j = i - 1;
                while (j >= 0 && ComesBefore(cars[carIndex], cars[m_Order[j]]))
                {
                    m_Order[j + 1] = m_Order[j];
                    j--;
                }

                m_Order[j + 1] = carIndex;
            }

            for (int rank = 0; rank < m_Order.Length; rank++)
                m_Position[m_Order[rank]] = rank + 1;
        }

        private static bool ComesBefore(CarState a, CarState b)
        {
            if (a.Finished && b.Finished)
                return a.FinishOrder < b.FinishOrder;

            if (a.Finished != b.Finished)
                return a.Finished;

            return a.Distance > b.Distance;
        }
    }
}
