namespace MatchRacers
{
    public interface IPaceController
    {
        void Reset(CarState[] cars, uint seed, int targetPosition);
        void Step(CarState[] cars, int playerCarIndex, float raceTime, float playerLastBuffTime, float deltaTime);
    }
}
