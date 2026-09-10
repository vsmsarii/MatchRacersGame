namespace CasualKit.Core
{
    public interface ILevelCatalog
    {
        int LevelCount { get; }
        int LastLevelIndex { get; }
        bool TryGetDefinitionKey(int index, out string key);
        bool TryGetDefaultLayoutKey(out string key);
    }
}
