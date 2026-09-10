namespace CasualKit.Core
{
    public static class EB
    {
        public static readonly EBBase Gameplay = new();
        public static readonly EBBase Presentation = new();
        public static readonly EBBase Analytics = new();

        public static void ClearAll()
        {
            Gameplay.Clear();
            Presentation.Clear();
            Analytics.Clear();
        }
    }
}
