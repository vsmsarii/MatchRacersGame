namespace MatchRacers
{
    public interface IRaceView
    {
        int CarCount { get; }
        int PlayerCarIndex { get; }
        float RaceLengthMeters { get; }
        float Time { get; }
        ERaceState State { get; }
        float RaceProgress { get; }
        float InterpolationAlpha { get; }
        BuffTableSO BuffTable { get; }

        CarState GetCar(int carIndex);
        int GetPosition(int carIndex);
        int GetCarAtPosition(int position);
        bool TryGetCarAhead(int carIndex, out int aheadCarIndex, out float gapMeters);
        bool TryGetCarBehind(int carIndex, out int behindCarIndex, out float gapMeters);
    }
}
