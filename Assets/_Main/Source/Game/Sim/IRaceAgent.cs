namespace MatchRacers
{
    public interface IRaceAgent
    {
        int CarIndex { get; }

        void Reset(uint seed);
        int PollBuffKey(IRaceView view, float stepTime, float stepDelta);
    }
}
