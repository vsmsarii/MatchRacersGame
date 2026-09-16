using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;

namespace MatchRacers.Editor
{
    public static class RaceAddressablesEnsure
    {
        private const string RaceConfigPath = "Assets/_Main/SO/Race/RaceConfig.asset";
        private const string BuffTablePath = "Assets/_Main/SO/Race/BuffTable.asset";
        private const string TrackSelectionPath = "Assets/_Main/Prefab/UI/RaceTrackSelection.prefab";
        private const string TrackSelectionAddress = "ui.race.track";

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
            EnsureEntryByPath(settings, TrackSelectionPath, TrackSelectionAddress);
            EnsureCarVfx(settings);
        }

        private static void EnsureCarVfx(AddressableAssetSettings settings)
        {
            RaceConfigSO config = AssetDatabase.LoadAssetAtPath<RaceConfigSO>(RaceConfigPath);
            if (config == null || config.CarVfx == null)
                return;

            CarVfxCatalogSO catalog = config.CarVfx;
            for (int i = 0; i < catalog.EntryCount; i++)
            {
                CarVfxEntry entry = catalog.GetEntry(i);
                if (entry.Prefab == null)
                    continue;

                EnsureEntryByPath(settings, AssetDatabase.GetAssetPath(entry.Prefab), CarVfxCatalogSO.GetAddress(entry.Effect));
            }
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
