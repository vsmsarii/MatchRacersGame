namespace CasualKit.Core
{
    public interface ISaveService : IService
    {
        int CurrentLevelIndex { get; }
        int CurrentLevelNumber { get; }
        bool HasCompletedFirstLevel { get; }
        int Hearts { get; }
        int MaxHearts { get; }
        long SecondsUntilNextHeart { get; }
        int SoftCurrency { get; }
        string GameJson { get; }

        int GetLevelScore(int levelIndex);
        int GetTotalScore();
        int GetTotalAttempts();
        int GetTotalCompletionSeconds();
        int GetLevelAttempts(int levelIndex);
        void CompleteLevel(int levelIndex, int score, int completionSeconds);
        int IncrementLevelAttempts(int levelIndex);

        void AddSoftCurrency(int amount);
        bool TrySpendSoftCurrency(int amount);
        void SetGameJson(string json);

        void ConfigureHearts(int maxHeartCount, float refillMinutes);
        bool TrySpendHeart();
        void GrantHearts(int amount);
        void RefreshHearts();
        void FlushPending();
    }
}
