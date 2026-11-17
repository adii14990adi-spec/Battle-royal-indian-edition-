using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using BattleHorizons.Characters;
using UnityEditor;
using UnityEngine;

namespace BattleHorizons.EditorTools
{
    /// <summary>
    /// Editor-side access to the saved <see cref="CharacterAnimationConfig"/>:
    /// find-or-create the asset under Assets/Generated/Resources, register
    /// imported sources (content hashing for duplicate prevention) and write
    /// import reports. All paths are code-generated because this repository
    /// commits no .meta files — assets are wired by path, never by GUID.
    /// </summary>
    public static class CharacterAnimationConfigLibrary
    {
        public const string GeneratedRoot = "Assets/Generated";
        public const string ResourcesFolder = GeneratedRoot + "/Resources";
        public const string ConfigAssetPath = ResourcesFolder + "/CharacterAnimationConfig.asset";
        public const string ReportPath = GeneratedRoot + "/LastImportReport.md";
        public const string GenericRigListPath = GeneratedRoot + "/GenericRigs.txt";
        public const string ControllersFolder = "Assets/AnimationControllers";
        public const string AvatarMasksFolder = "Assets/AvatarMasks";
        public const string CharactersRoot = "Assets/Characters";
        public const string AnimationsRoot = "Assets/Animations";

        public static CharacterAnimationConfig LoadOrCreate()
        {
            var existing = AssetDatabase.LoadAssetAtPath<CharacterAnimationConfig>(ConfigAssetPath);
            if (existing != null)
            {
                return existing;
            }

            EnsureFolder(ResourcesFolder);
            var config = ScriptableObject.CreateInstance<CharacterAnimationConfig>();
            config.selectedCharacterId = "male";
            AssetDatabase.CreateAsset(config, ConfigAssetPath);
            AssetDatabase.SaveAssets();
            return config;
        }

        public static CharacterAnimationConfig LoadIfExists()
        {
            return AssetDatabase.LoadAssetAtPath<CharacterAnimationConfig>(ConfigAssetPath);
        }

        public static void Save(CharacterAnimationConfig config)
        {
            if (config == null)
            {
                return;
            }

            config.generatedAtUtc = DateTime.UtcNow.ToString("o");
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        /// <summary>SHA-256 of a file's bytes (duplicate-import identity).</summary>
        public static string Sha256OfFile(string absolutePath)
        {
            using (var stream = File.OpenRead(absolutePath))
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(stream);
                var builder = new StringBuilder(hash.Length * 2);
                for (var i = 0; i < hash.Length; i++)
                {
                    builder.Append(hash[i].ToString("x2"));
                }

                return builder.ToString();
            }
        }

        public static string AbsolutePathOf(string projectRelativePath)
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                ?? throw new InvalidOperationException("cannot locate project root");
            return Path.Combine(projectRoot, projectRelativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        /// <summary>
        /// Create nested folders under Assets/ (AssetDatabase.CreateFolder
        /// only handles one level at a time).
        /// </summary>
        public static void EnsureFolder(string projectRelativeFolder)
        {
            if (string.IsNullOrEmpty(projectRelativeFolder) || AssetDatabase.IsValidFolder(projectRelativeFolder))
            {
                return;
            }

            var parts = projectRelativeFolder.Split('/');
            var current = parts[0]; // "Assets"
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        /// <summary>Find a character by id, creating an empty entry when absent.</summary>
        public static CharacterDefinition GetOrCreateCharacter(
            CharacterAnimationConfig config, string id, string displayName)
        {
            var existing = config.FindCharacter(id);
            if (existing != null)
            {
                return existing;
            }

            var entry = new CharacterDefinition
            {
                id = id,
                displayName = displayName,
            };
            config.characters.Add(entry);
            return entry;
        }

        /// <summary>
        /// Register a source file's content hash so a second import of the
        /// same bytes is skipped. Returns true when this content was seen
        /// before (duplicate).
        /// </summary>
        public static bool RegisterSource(
            CharacterAnimationConfig config, string projectRelativePath, string sha256, long bytes)
        {
            if (config.IsDuplicateSource(sha256))
            {
                return true;
            }

            config.AddOrUpdateSource(new ImportedSourceRecord
            {
                sourcePath = projectRelativePath,
                sha256 = sha256,
                bytes = bytes,
                importedAtUtc = DateTime.UtcNow.ToString("o"),
            });
            return false;
        }

        /// <summary>Write the generated import report next to the config.</summary>
        public static void WriteReport(ImportReport report)
        {
            EnsureFolder(GeneratedRoot);
            File.WriteAllText(ReportPath, report.ToMarkdown(), Encoding.UTF8);
            AssetDatabase.Refresh();
        }

        // ------------------------------------------------------------------
        // Explicit-Generic rig registry (manual escape hatch). Unity's default
        // for a new FBX is also Generic, so an explicit choice must be
        // recorded out-of-band — the importer rule only honours a recorded
        // choice, never an accidental default.
        // ------------------------------------------------------------------

        public static bool IsExplicitGeneric(string assetPath)
        {
            var absolute = AbsolutePathOf(GenericRigListPath);
            if (!File.Exists(absolute) || string.IsNullOrEmpty(assetPath))
            {
                return false;
            }

            foreach (var line in File.ReadAllLines(absolute))
            {
                if (string.Equals(line.Trim(), assetPath, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public static void MarkExplicitGeneric(string assetPath)
        {
            if (IsExplicitGeneric(assetPath) || string.IsNullOrEmpty(assetPath))
            {
                return;
            }

            EnsureFolder(GeneratedRoot);
            var absolute = AbsolutePathOf(GenericRigListPath);
            File.AppendAllText(absolute, assetPath + "\n", Encoding.UTF8);
        }

        public static void ClearExplicitGeneric(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return;
            }

            var absolute = AbsolutePathOf(GenericRigListPath);
            if (!File.Exists(absolute))
            {
                return;
            }

            var lines = File.ReadAllLines(absolute);
            var kept = new List<string>();
            for (var i = 0; i < lines.Length; i++)
            {
                if (!string.Equals(lines[i].Trim(), assetPath, StringComparison.OrdinalIgnoreCase))
                {
                    kept.Add(lines[i]);
                }
            }

            if (kept.Count != lines.Length)
            {
                File.WriteAllLines(absolute, kept.ToArray(), Encoding.UTF8);
            }
        }
    }

    /// <summary>
    /// Collects success / warning / failed lines for an import run and renders
    /// them for the manager window and for the saved Markdown report (§ import
    /// reports with successful, warning and failed items).
    /// </summary>
    public sealed class ImportReport
    {
        public enum Severity
        {
            Success,
            Warning,
            Failed,
        }

        public sealed class Line
        {
            public Severity severity;
            public string stage;
            public string message;
        }

        public readonly List<Line> Lines = new List<Line>();
        public string Title = "Import report";

        public int SuccessCount
        {
            get
            {
                var count = 0;
                for (var i = 0; i < Lines.Count; i++)
                {
                    if (Lines[i].severity == Severity.Success) count++;
                }

                return count;
            }
        }

        public int WarningCount
        {
            get
            {
                var count = 0;
                for (var i = 0; i < Lines.Count; i++)
                {
                    if (Lines[i].severity == Severity.Warning) count++;
                }

                return count;
            }
        }

        public int FailedCount
        {
            get
            {
                var count = 0;
                for (var i = 0; i < Lines.Count; i++)
                {
                    if (Lines[i].severity == Severity.Failed) count++;
                }

                return count;
            }
        }

        public void Success(string stage, string message) => Add(Severity.Success, stage, message);

        public void Warn(string stage, string message) => Add(Severity.Warning, stage, message);

        public void Fail(string stage, string message) => Add(Severity.Failed, stage, message);

        private void Add(Severity severity, string stage, string message)
        {
            Lines.Add(new Line { severity = severity, stage = stage, message = message });
            var prefix = severity == Severity.Success ? "OK  "
                : severity == Severity.Warning ? "WARN"
                : "FAIL";
            Debug.Log("[bh][import] " + prefix + " [" + stage + "] " + message);
        }

        public string ToMarkdown()
        {
            var builder = new StringBuilder();
            builder.Append("# ").Append(Title).Append("\n\n");
            builder.Append("Generated: ").Append(DateTime.UtcNow.ToString("o")).Append("\n\n");
            builder.Append("**").Append(SuccessCount).Append(" succeeded · ")
                .Append(WarningCount).Append(" warnings · ")
                .Append(FailedCount).Append(" failed**\n\n");
            builder.Append("| Result | Stage | Detail |\n| --- | --- | --- |\n");
            for (var i = 0; i < Lines.Count; i++)
            {
                var line = Lines[i];
                var badge = line.severity == Severity.Success ? "✅ success"
                    : line.severity == Severity.Warning ? "⚠ warning"
                    : "❌ failed";
                builder.Append("| ").Append(badge)
                    .Append(" | ").Append(line.stage)
                    .Append(" | ").Append(line.message.Replace("|", "\\|"))
                    .Append(" |\n");
            }

            return builder.ToString();
        }
    }
}
