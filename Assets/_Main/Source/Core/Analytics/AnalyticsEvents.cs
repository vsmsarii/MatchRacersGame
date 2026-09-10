namespace CasualKit.Core
{
    public readonly struct MatchStartAnalytics
    {
        public readonly int LevelIndex;

        public MatchStartAnalytics(int levelIndex)
        {
            LevelIndex = levelIndex;
        }
    }

    public readonly struct MatchWinAnalytics
    {
        public readonly int LevelIndex;
        public readonly float Time;

        public MatchWinAnalytics(int levelIndex, float time)
        {
            LevelIndex = levelIndex;
            Time = time;
        }
    }

    public readonly struct MatchLoseAnalytics
    {
        public readonly int LevelIndex;
        public readonly float ResultTime;
        public readonly int AttemptCount;

        public MatchLoseAnalytics(int levelIndex, float resultTime, int attemptCount)
        {
            LevelIndex = levelIndex;
            ResultTime = resultTime;
            AttemptCount = attemptCount;
        }
    }
}
