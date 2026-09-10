using CasualKit.Core;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;

namespace CasualKit.Editor
{
    public static class AddressablesEnsureSettings
    {
        [MenuItem("CasualKit/Addressables/Ensure Settings")]
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

            EnsureEntryByPath(settings, "Assets/_Main/Input/PillFrenzyInput.inputactions", AddressableKeys.InputActions);
            EnsureEntryByPath(settings, "Assets/_Main/SO/Settings/IAPCatalog.asset", AddressableKeys.IapCatalog);
            EnsureEntryByPath(settings, "Assets/_Main/SO/Settings/GlobalSettings.asset", AddressableKeys.GlobalSettings);
            EnsureEntryByPath(settings, "Assets/_Main/SO/Settings/LevelCatalog.asset", AddressableKeys.LevelManifest);
            EnsureEntryByPath(settings, "Assets/_Main/SO/Settings/AudioCatalog.asset", AddressableKeys.AudioCatalog);
            EnsureEntryByPath(settings, "Assets/_Main/SO/Settings/UIPanelCatalog.asset", AddressableKeys.UiPanelCatalog);
            EnsureEntryByPath(settings, "Assets/_Main/Prefab/UI/MainMenuCanvas.prefab", AddressableKeys.UiMainMenu);
            EnsureEntryByPath(settings, "Assets/_Main/Prefab/UI/GameplayCanvas.prefab", AddressableKeys.UiGameplay);
            EnsureEntryByPath(settings, "Assets/_Main/Prefab/UI/WinCanvas.prefab", AddressableKeys.UiWin);
            EnsureEntryByPath(settings, "Assets/_Main/Prefab/UI/LoseCanvas.prefab", AddressableKeys.UiLose);
            EnsureEntryByPath(settings, "Assets/_Main/Prefab/UI/LoadingCanvas.prefab", AddressableKeys.UiLoading);
            EnsureEntryByPath(settings, "Assets/_Main/Prefab/UI/ShopCanvas.prefab", AddressableKeys.UiShop);
            EnsureEntryByPath(settings, "Assets/_Main/Prefab/UI/SettingsCanvas.prefab", AddressableKeys.UiSettings);
        }

        private static void EnsureEntryByPath(AddressableAssetSettings settings, string assetPath, string address)
        {
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid))
                return;

            AddressableAssetEntry entry = settings.FindAssetEntry(guid);
            if (entry == null)
                entry = settings.CreateOrMoveEntry(guid, settings.DefaultGroup, false, false);

            if (entry != null && entry.address != address)
                entry.SetAddress(address, false);
        }
    }
}
