namespace CasualKit.Core
{
    public readonly struct RunHudChanged
    {
        public readonly int Score;
        public readonly int Combo;
        public readonly int Health;

        public RunHudChanged(int score, int combo, int health)
        {
            Score = score;
            Combo = combo;
            Health = health;
        }
    }

    public readonly struct RunEnded
    {
        public readonly bool IsComplete;
        public readonly int Score;
        public readonly int BestCombo;

        public RunEnded(bool isComplete, int score, int bestCombo)
        {
            IsComplete = isComplete;
            Score = score;
            BestCombo = bestCombo;
        }
    }
}
