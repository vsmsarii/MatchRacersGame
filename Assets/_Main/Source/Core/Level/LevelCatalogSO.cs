using UnityEngine;

namespace CasualKit.Core
{
    [CreateAssetMenu(fileName = "LevelCatalog", menuName = "CasualKit/Level Catalog")]
    public sealed class LevelCatalogSO : ScriptableObject, ILevelCatalog
    {
        [SerializeField, Min(1)] private int m_LevelCount = 1;
        [SerializeField] private string[] m_DefinitionKeys;
        [SerializeField] private string m_DefaultLayoutKey;

        public int LevelCount => m_LevelCount < 1 ? 1 : m_LevelCount;
        public int LastLevelIndex => LevelCount - 1;

        public bool TryGetDefinitionKey(int index, out string key)
        {
            key = null;
            if (m_DefinitionKeys == null || index < 0 || index >= m_DefinitionKeys.Length)
                return false;

            key = m_DefinitionKeys[index];
            return !string.IsNullOrEmpty(key);
        }

        public bool TryGetDefaultLayoutKey(out string key)
        {
            key = m_DefaultLayoutKey;
            return !string.IsNullOrEmpty(key);
        }
    }
}
