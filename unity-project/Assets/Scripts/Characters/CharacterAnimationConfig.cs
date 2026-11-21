using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleHorizons.Characters
{
    /// <summary>Provenance record used to prevent duplicate imports.</summary>
    [Serializable]
    public sealed class ImportedSourceRecord
    {
        public string sourcePath;
        public string sha256;
        public long bytes;
        public string importedAtUtc;
    }

    /// <summary>An imported (or custom) base character model.</summary>
    [Serializable]
    public sealed class CharacterDefinition
    {
        public string id;
        public string displayName;
        public GameObject model;
        public bool humanoidRigValid;
        public string rigReport;
        public string materialReport;
    }

    /// <summary>
    /// Saved character roster, written by the editor-time
    /// <c>CharacterRosterIntegration.BuildFromCommandLine</c> and read by the
    /// runtime preview, the player visual, and the in-game Character Bay.
    /// Lives under <c>Assets/Generated/Resources/</c> so builds can load it
    /// without hard-coded GUID references.
    ///
    /// History: this asset used to carry the Mixamo animation-clip library,
    /// category assignments and generated Animator Controllers. All animation
    /// clips were removed from the project on 2026-11-20 by owner decision
    /// ("Remove all animations" — running/crouch/jump FBX clips), so only the
    /// character roster remains. The class keeps its historical name so the
    /// protected-asset lineage and existing references stay traceable.
    /// </summary>
    public sealed class CharacterAnimationConfig : ScriptableObject
    {
        /// <summary>Resources.Load path (file lives in any Resources folder).</summary>
        public const string ResourcePath = "CharacterAnimationConfig";

        public List<CharacterDefinition> characters = new List<CharacterDefinition>();
        public List<ImportedSourceRecord> importedSources = new List<ImportedSourceRecord>();
        public string selectedCharacterId = "male";
        public string generatedAtUtc;

        /// <summary>True when these exact bytes were imported before.</summary>
        public bool IsDuplicateSource(string sha256)
        {
            if (importedSources == null || string.IsNullOrEmpty(sha256))
            {
                return false;
            }

            for (var i = 0; i < importedSources.Count; i++)
            {
                var record = importedSources[i];
                if (record != null
                    && string.Equals(record.sha256, sha256, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Records a source file, replacing an entry with the same hash.</summary>
        public void AddOrUpdateSource(ImportedSourceRecord record)
        {
            if (record == null || string.IsNullOrEmpty(record.sha256))
            {
                return;
            }

            if (importedSources == null)
            {
                importedSources = new List<ImportedSourceRecord>();
            }

            for (var i = 0; i < importedSources.Count; i++)
            {
                var existing = importedSources[i];
                if (existing != null
                    && string.Equals(existing.sha256, record.sha256, StringComparison.OrdinalIgnoreCase))
                {
                    importedSources[i] = record;
                    return;
                }
            }

            importedSources.Add(record);
        }

        public static CharacterAnimationConfig Load()
        {
            return Resources.Load<CharacterAnimationConfig>(ResourcePath);
        }

        public CharacterDefinition FindCharacter(string id)
        {
            if (characters == null || string.IsNullOrEmpty(id))
            {
                return null;
            }

            for (var i = 0; i < characters.Count; i++)
            {
                if (characters[i] != null && string.Equals(characters[i].id, id, StringComparison.OrdinalIgnoreCase))
                {
                    return characters[i];
                }
            }

            return null;
        }

        public CharacterDefinition SelectedCharacter()
        {
            var found = FindCharacter(selectedCharacterId);
            if (found != null)
            {
                return found;
            }

            if (characters != null && characters.Count > 0)
            {
                for (var i = 0; i < characters.Count; i++)
                {
                    if (characters[i] != null && characters[i].model != null)
                    {
                        return characters[i];
                    }
                }
            }

            return null;
        }
    }
}
