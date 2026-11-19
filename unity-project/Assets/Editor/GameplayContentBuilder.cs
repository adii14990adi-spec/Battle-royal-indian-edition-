using System.IO;
using BattleHorizons.Gameplay.Loot;
using BattleHorizons.Gameplay.Match;
using UnityEditor;
using UnityEngine;

namespace BattleHorizons.EditorTools
{
    /// <summary>
    /// Persists the code-authored gameplay defaults (DefaultLootContent,
    /// SafeZoneSettings.CreateDefault) as editable ScriptableObject assets, so
    /// designers can tune tiers, weights and the zone schedule without touching
    /// C#. Nothing here runs in a player build; the runtime falls back to the
    /// code defaults when no asset is assigned.
    /// </summary>
    public static class GameplayContentBuilder
    {
        private const string LootFolder = "Assets/Resources/Config/Loot";
        private const string MatchFolder = "Assets/Resources/Config/Match";

        [MenuItem("Battle Horizons/Gameplay/Create Default Loot Assets")]
        public static void CreateDefaultLootAssets()
        {
            EnsureFolder(LootFolder);

            var items = DefaultLootContent.CreateItems();
            foreach (var pair in items)
            {
                pair.Value.itemId = pair.Key;
                CreateOrReplace(pair.Value, $"{LootFolder}/Items/{pair.Value.name}.asset");
            }

            var tables = DefaultLootContent.CreateTables(items);
            foreach (var pair in tables)
            {
                CreateOrReplace(pair.Value, $"{LootFolder}/{pair.Value.name}.asset");
            }

            var config = DefaultLootContent.BuildConfig(items, tables);
            CreateOrReplace(config, $"{LootFolder}/{config.name}.asset");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[bh][editor] default loot assets written under {LootFolder} " +
                      $"({items.Count} items, {tables.Count} tables, 1 config)");
        }

        [MenuItem("Battle Horizons/Gameplay/Create Default Safe Zone Settings")]
        public static void CreateDefaultSafeZoneSettings()
        {
            EnsureFolder(MatchFolder);
            var settings = SafeZoneSettings.CreateDefault();
            CreateOrReplace(settings, $"{MatchFolder}/{settings.name}.asset");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[bh][editor] safe zone schedule written to {MatchFolder}/{settings.name}.asset");
        }

        private static void CreateOrReplace(Object asset, string path)
        {
            var directory = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(directory))
            {
                EnsureFolder(directory);
            }

            var existing = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(asset, existing);
                Object.DestroyImmediate(asset);
                return;
            }

            AssetDatabase.CreateAsset(asset, path);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var leaf = Path.GetFileName(path);
            if (string.IsNullOrEmpty(parent))
            {
                return;
            }

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
