using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;

namespace MatchRacers.Editor
{
    public static class RaceAddressablesEnsure
    {
        private const string RaceConfigPath = "Assets/_Main/SO/Race/RaceConfig.asset";
        private const string BuffTablePath = "Assets/_Main/SO/Race/BuffTable.asset";

        [MenuItem("MatchRacers/Addressables/Ensure Settings")]
        public static void EnsureFromMenu()
        {
            Ensure();
            AssetDatabase.SaveAssets();
        }

        public static void Ensure()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            if (settings == null)
                return;

            EnsureEntryByPath(settings, RaceConfigPath, RaceAddressableKeys.RaceConfig);
            EnsureEntryByPath(settings, BuffTablePath, RaceAddressableKeys.BuffTable);
        }

        private static void EnsureEntryByPath(AddressableAssetSettings settings, string assetPath, string address)
        {
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid))
            {
                UnityEngine.Debug.LogWarning("Addressable path missing: " + assetPath);
                return;
            }

            AddressableAssetEntry entry = settings.FindAssetEntry(guid);
            if (entry == null)
                entry = settings.CreateOrMoveEntry(guid, settings.DefaultGroup, false, false);

            if (entry != null && entry.address != address)
                entry.SetAddress(address, false);
        }
    }
}
