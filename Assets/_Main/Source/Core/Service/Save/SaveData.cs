using System;

namespace CasualKit.Core
{
    [Serializable]
    public sealed class SaveData
    {
        public int Version;
        public CoreProgressData Progress;
        public string GameJson;
    }

    [Serializable]
    public sealed class CoreProgressData
    {
        public int CurrentLevelIndex;
        public bool FirstLevelCompleted;
        public int TotalScore;
        public int TotalAttempts;
        public int TotalCompletionSeconds;
        public LevelRecordData[] LevelScores;
    }

    [Serializable]
    public sealed class LevelRecordData
    {
        public int LevelIndex;
        public int Score;
        public int Attempts;
        public int CompletionSeconds;
    }

    [Serializable]
    public sealed class LegacySaveData
    {
        public int Version;
        public int CurrentLevelIndex;
        public bool FirstLevelCompleted;
        public int TotalScore;
        public int TotalAttempts;
        public int TotalCompletionSeconds;
        public LevelRecordData[] LevelScores;
        public CoreProgressData Progress;
        public string GameJson;
    }
}
