namespace MatchRacers
{
    public struct Xorshift
    {
        private uint m_State;

        public Xorshift(uint seed)
        {
            m_State = seed == 0u ? 2463534242u : seed;
        }

        public uint NextUInt()
        {
            m_State ^= m_State << 13;
            m_State ^= m_State >> 17;
            m_State ^= m_State << 5;
            return m_State;
        }

        public float NextFloat()
        {
            return (NextUInt() >> 8) * (1f / 16777216f);
        }

        public float Range(float minInclusive, float maxExclusive)
        {
            return minInclusive + (maxExclusive - minInclusive) * NextFloat();
        }

        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
                return minInclusive;

            return minInclusive + (int)(NextUInt() % (uint)(maxExclusive - minInclusive));
        }
    }
}
