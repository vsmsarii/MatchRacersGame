namespace CasualKit.Core
{
    public interface ISaveService : IService
    {
        int CurrentLevelIndex { get; }
        int CurrentLevelNumber { get; }
        bool HasCompletedFirstLevel { get; }
        string GameJson { get; }

        int GetLevelScore(int levelIndex);
        int GetTotalScore();
        int GetTotalAttempts();
        int GetTotalCompletionSeconds();
        int GetLevelAttempts(int levelIndex);
        void CompleteLevel(int levelIndex, int score, int completionSeconds);
        int IncrementLevelAttempts(int levelIndex);

        void SetGameJson(string json);
        void FlushPending();
    }
}
