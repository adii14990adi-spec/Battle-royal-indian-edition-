using System;
using System.Collections.Generic;
using System.IO;
using BattleHorizons.Characters;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace BattleHorizons.EditorTools
{
    /// <summary>
    /// Character &amp; Animation Manager — the tool window that implements the
    /// full authoring loop (§ character manager, § animation manager,
    /// § assignment, § preview):
    ///
    ///  * Character tab: upload an FBX (Male / Female / custom), pick the
    ///    external texture folder (PNG/JPG/TGA), see rig + texture + material
    ///    validation warnings, explicit Generic escape hatch,
    ///  * Animations tab: multi-select animation FBX files, inspect duration /
    ///    frame rate / loop / retargetability, change the category before
    ///    import, import (duplicates are skipped),
    ///  * Assign tab: character → category → clip with compatibility checks,
    ///    Save persists the config AND rebuilds the Animator Controllers,
    ///  * Preview tab: real-time 3D preview (rotate / zoom / play-pause /
    ///    clip switch / skeleton &amp; material status),
    ///  * Report tab: the success / warning / failed report of the last run.
    /// </summary>
    public sealed class CharacterAnimationManagerWindow : EditorWindow
    {
        private const string ScratchControllerPath =
            CharacterAnimationConfigLibrary.ControllersFolder + "/_EditorPreview.controller";

        private static readonly CharacterAnimCategory[] Categories =
            (CharacterAnimCategory[])Enum.GetValues(typeof(CharacterAnimCategory));

        private int _tab;
        private CharacterAnimationConfig _config;
        private ImportReport _report;

        // Character tab
        private CharacterSlot _slot = CharacterSlot.Male;
        private string _customName = "CustomCharacter";
        private string _externalTextureFolder = string.Empty;
        private bool _forceGenericRig;
        private bool _confirmedOverwrite;
        private string _lastCharacterMessage = string.Empty;

        // Animations tab
        private readonly List<string> _pendingSources = new List<string>();
        private readonly Dictionary<string, CharacterAnimCategory> _pendingOverrides =
            new Dictionary<string, CharacterAnimCategory>(StringComparer.OrdinalIgnoreCase);
        private Vector2 _pendingScroll;
        private Vector2 _animationScroll;
        private string _lastAnimationMessage = string.Empty;

        // Assign tab
        private string _selectedCharacterId;
        private readonly Dictionary<string, AnimationClip> _draftAssignments =
            new Dictionary<string, AnimationClip>();
        private Vector2 _assignScroll;
        private string _lastAssignMessage = string.Empty;

        // Preview tab
        private CharacterPreviewController _preview;
        private bool _previewInitialized;

        [MenuItem("Battle Horizons/Asset Management/Character & Animation Manager")]
        public static void Open()
        {
            var window = GetWindow<CharacterAnimationManagerWindow>();
            window.titleContent = new GUIContent("Char & Anim Manager");
            window.minSize = new Vector2(980f, 640f);
            window.Show();
        }

        private void OnEnable()
        {
            ReloadConfig();
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            TeardownPreview();
        }

        private void ReloadConfig()
        {
            _config = CharacterAnimationConfigLibrary.LoadIfExists();
            if (_config != null && !string.IsNullOrEmpty(_config.selectedCharacterId))
            {
                _selectedCharacterId = _config.selectedCharacterId;
            }

            LoadDraftAssignments();
        }

        private void OnGUI()
        {
            if (_config == null)
            {
                EditorGUILayout.HelpBox(
                    "No CharacterAnimationConfig found yet. Import a character or animation to create "
                    + CharacterAnimationConfigLibrary.ConfigAssetPath + ".",
                    MessageType.Info);
                if (GUILayout.Button("Create empty configuration", GUILayout.Width(260f)))
                {
                    ReloadConfig();
                }
            }

            var tabs = new[] { "Character", "Animations", "Assign", "Preview", "Report" };
            _tab = GUILayout.Toolbar(_tab, tabs);
            EditorGUILayout.Space(6f);

            switch (_tab)
            {
                case 0: DrawCharacterTab(); break;
                case 1: DrawAnimationsTab(); break;
                case 2: DrawAssignTab(); break;
                case 3: DrawPreviewTab(); break;
                default: DrawReportTab(); break;
            }
        }

        // ==================================================================
        // Character tab
        // ==================================================================

        private void DrawCharacterTab()
        {
            EditorGUILayout.LabelField("Upload character FBX", EditorStyles.boldLabel);
            _slot = (CharacterSlot)EditorGUILayout.EnumPopup(
                new GUIContent("Character slot", "Files are filed under Assets/Characters/<slot>/"),
                _slot);
            if (_slot == CharacterSlot.Custom)
            {
                _customName = EditorGUILayout.TextField("Custom name", _customName);
            }

            _forceGenericRig = EditorGUILayout.ToggleLeft(
                new GUIContent("Import as Generic rig (no humanoid retargeting) — manual option"),
                _forceGenericRig);
            _externalTextureFolder = EditorGUILayout.TextField(
                new GUIContent("External textures folder",
                    "Optional folder with PNG/JPG/TGA textures to copy next to the model"),
                _externalTextureFolder);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Browse textures…", GUILayout.Width(160f)))
                {
                    var chosen = EditorUtility.OpenFolderPanel("Select texture folder", "", "");
                    if (!string.IsNullOrEmpty(chosen))
                    {
                        _externalTextureFolder = chosen;
                    }
                }

                _confirmedOverwrite = EditorGUILayout.ToggleLeft(
                    "I confirm replacing an existing character file", _confirmedOverwrite);
            }

            EditorGUILayout.Space(8f);
            if (GUILayout.Button("Choose character FBX…", GUILayout.Height(34f)))
            {
                var path = EditorUtility.OpenFilePanel("Select character FBX", "", "fbx");
                if (!string.IsNullOrEmpty(path))
                {
                    RunCharacterImport(path);
                }
            }

            if (!string.IsNullOrEmpty(_lastCharacterMessage))
            {
                EditorGUILayout.HelpBox(_lastCharacterMessage, MessageType.None);
            }

            EditorGUILayout.Space(12f);
            EditorGUILayout.LabelField("Imported characters", EditorStyles.boldLabel);
            if (_config != null)
            {
                for (var i = 0; i < _config.characters.Count; i++)
                {
                    var character = _config.characters[i];
                    if (character == null)
                    {
                        continue;
                    }

                    using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                    {
                        EditorGUILayout.LabelField(character.displayName + "  (" + character.id + ")",
                            GUILayout.Width(300f));
                        EditorGUILayout.LabelField(
                            character.humanoidRigValid ? "humanoid ✓" : "rig needs attention",
                            GUILayout.Width(130f));
                        EditorGUILayout.LabelField(
                            character.controller != null ? "controller ✓" : "controller —",
                            GUILayout.Width(110f));
                        GUILayout.Label(
                            character.model != null ? character.model.name : "NO MODEL",
                            EditorStyles.miniLabel, GUILayout.Width(220f));
                    }

                    if (!string.IsNullOrEmpty(character.rigReport))
                    {
                        EditorGUILayout.HelpBox(character.rigReport, MessageType.None);
                    }

                    if (!string.IsNullOrEmpty(character.materialReport))
                    {
                        EditorGUILayout.HelpBox(character.materialReport, MessageType.None);
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Revalidate rig", GUILayout.Width(140f)))
                        {
                            RevalidateRig(character);
                        }

                        if (character.model != null
                            && GUILayout.Button("Locate model", GUILayout.Width(130f)))
                        {
                            Selection.activeObject = character.model;
                            EditorGUIUtility.PingObject(character.model);
                        }
                    }
                }
            }
        }

        private void RunCharacterImport(string path)
        {
            var options = new CharacterImportService.Options
            {
                slot = _slot,
                customName = _customName,
                externalTextureFolder = string.IsNullOrWhiteSpace(_externalTextureFolder)
                    ? null
                    : _externalTextureFolder,
                forceGenericRig = _forceGenericRig,
                confirmOverwrite = (target, source) =>
                {
                    if (_confirmedOverwrite)
                    {
                        return true;
                    }

                    var choice = EditorUtility.DisplayDialogComplex(
                        "Replace existing character?",
                        target + " already exists.\nReplace it with " + Path.GetFileName(source) + "?",
                        "Replace", "Cancel", "Overwrite after checking this every time");
                    if (choice == 2)
                    {
                        _confirmedOverwrite = true;
                        return true;
                    }

                    return choice == 0;
                },
            };

            var result = CharacterImportService.ImportCharacter(path, options);
            _report = result.report;
            _lastCharacterMessage = (result.accepted ? "Import finished: " : "Import finished WITH FAILURES: ")
                + result.report.SuccessCount + " ok · " + result.report.WarningCount + " warnings · "
                + result.report.FailedCount + " failed";
            _tab = 4; // show the report
            ReloadConfig();
            Repaint();
        }

        private void RevalidateRig(CharacterDefinition character)
        {
            if (character == null || character.model == null)
            {
                return;
            }

            var path = AssetDatabase.GetAssetPath(character.model);
            var report = RigDetectionUtility.Inspect(path);
            character.humanoidRigValid = report.kind == RigKind.Humanoid && report.success;
            character.rigReport = report.Combined;
            CharacterAnimationConfigLibrary.Save(_config);
            _lastCharacterMessage = report.summary;
            Repaint();
        }

        // ==================================================================
        // Animations tab
        // ==================================================================

        private void DrawAnimationsTab()
        {
            EditorGUILayout.LabelField("Upload animation FBX files (multi-select)", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Choose animation FBX files…", GUILayout.Height(32f), GUILayout.Width(320f)))
                {
                    var paths = EditorUtility.OpenFilePanelMultiple("Select animation FBX files", "", "fbx");
                    foreach (var path in paths)
                    {
                        if (string.IsNullOrEmpty(path))
                        {
                            continue;
                        }

                        _pendingSources.Add(path);
                        var fileName = Path.GetFileName(path);
                        if (!_pendingOverrides.ContainsKey(fileName))
                        {
                            _pendingOverrides[fileName] =
                                CharacterAnimCategories.InferFromFileName(fileName);
                        }
                    }
                }

                if (GUILayout.Button("Clear list", GUILayout.Width(110f)))
                {
                    _pendingSources.Clear();
                    _pendingOverrides.Clear();
                }

                if (GUILayout.Button("Scan existing Assets/Animations", GUILayout.Width(250f)))
                {
                    var scanReport = new ImportReport { Title = "Animation scan" };
                    var added = AnimationImportService.RegisterExistingAnimationAssets(scanReport);
                    _report = scanReport;
                    _lastAnimationMessage = "scan registered " + added + " clip(s)";
                    _tab = 4;
                }
            }

            _pendingScroll = EditorGUILayout.BeginScrollView(_pendingScroll, GUILayout.Height(220f));
            if (_pendingSources.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No files queued. Categories are inferred from file names "
                    + "(Idle, Walk, Run, Sprint, Jump, Fall, Crouch, Aim, Shoot, Reload, Death, …) "
                    + "and can be changed per file below before importing.",
                    MessageType.Info);
            }

            var removeSource = null as string;
            foreach (var source in _pendingSources)
            {
                var fileName = Path.GetFileName(source);
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    var inspection = AnimationImportService.InspectSources(new[] { source });
                    var clips = inspection.Count > 0 ? inspection[0] : null;
                    var detail = clips != null && clips.clips.Count > 0
                        ? AnimationImportService.DescribeClips(clips.clips)
                        : (clips != null && clips.failures.Count > 0
                            ? string.Join("; ", clips.failures)
                            : "no clips");
                    EditorGUILayout.LabelField(fileName, GUILayout.Width(260f));
                    EditorGUILayout.LabelField(detail, EditorStyles.miniLabel, GUILayout.Width(430f));
                    if (_pendingOverrides.TryGetValue(fileName, out var category))
                    {
                        var newCategory = (CharacterAnimCategory)EditorGUILayout.EnumPopup(
                            category, GUILayout.Width(140f));
                        if (newCategory != category)
                        {
                            _pendingOverrides[fileName] = newCategory;
                        }
                    }

                    if (GUILayout.Button("✕", GUILayout.Width(28f)))
                    {
                        removeSource = source;
                    }
                }
            }

            if (removeSource != null)
            {
                _pendingSources.Remove(removeSource);
                _pendingOverrides.Remove(Path.GetFileName(removeSource));
            }

            EditorGUILayout.EndScrollView();

            if (GUILayout.Button(
                    "Import " + _pendingSources.Count + " animation file(s) into Assets/Animations/",
                    GUILayout.Height(34f)))
            {
                RunAnimationImport();
            }

            if (!string.IsNullOrEmpty(_lastAnimationMessage))
            {
                EditorGUILayout.HelpBox(_lastAnimationMessage, MessageType.None);
            }

            EditorGUILayout.Space(12f);
            EditorGUILayout.LabelField("Registered clips (" + (_config != null ? _config.clips.Count : 0) + ")",
                EditorStyles.boldLabel);
            _animationScroll = EditorGUILayout.BeginScrollView(_animationScroll);
            if (_config != null)
            {
                foreach (var clip in _config.clips)
                {
                    if (clip == null)
                    {
                        continue;
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label(clip.name, GUILayout.Width(320f));
                        GUILayout.Label(
                            string.Format("{0:0.00}s · {1:0} fps · {2} · {3}",
                                clip.length, clip.frameRate,
                                clip.isLooping ? "loop" : "one-shot",
                                clip.humanMotion ? "humanoid" : "generic"),
                            EditorStyles.miniLabel);
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void RunAnimationImport()
        {
            var options = new AnimationImportService.Options();
            foreach (var pair in _pendingOverrides)
            {
                options.CategoryOverrides[pair.Key] = pair.Value;
            }

            var result = AnimationImportService.ImportAnimations(_pendingSources.ToArray(), options);
            _report = result.report;
            _lastAnimationMessage = "imported " + result.Clips.Count + " clip(s) · "
                + result.report.WarningCount + " warnings · " + result.report.FailedCount + " failed";
            if (result.Clips.Count > 0)
            {
                _pendingSources.Clear();
            }

            ReloadConfig();
            _tab = 4;
            Repaint();
        }

        // ==================================================================
        // Assign tab
        // ==================================================================

        private void DrawAssignTab()
        {
            if (_config == null || _config.characters.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "Import a character first — assignments are per character.", MessageType.Info);
                return;
            }

            var characterIds = new string[_config.characters.Count];
            for (var i = 0; i < _config.characters.Count; i++)
            {
                characterIds[i] = _config.characters[i]?.id ?? ("empty" + i);
            }

            var currentIndex = Mathf.Max(0, Array.IndexOf(characterIds, _selectedCharacterId));
            var newIndex = EditorGUILayout.Popup("Character", currentIndex, characterIds);
            _selectedCharacterId = characterIds[newIndex];
            var character = _config.FindCharacter(_selectedCharacterId);
            if (character == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(_selectedCharacterId)
                || !ReferenceEquals(GetDraftOwner(), character))
            {
                SelectDraftCharacter(character);
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.HelpBox(
                "Select a clip per category, then Save. Assignments are stored per character — "
                + "different characters keep different clips. Incompatible clips (generic source or "
                + "invalid avatar) are refused with an explanation.",
                MessageType.Info);

            _assignScroll = EditorGUILayout.BeginScrollView(_assignScroll);
            foreach (var category in Categories)
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(CharacterAnimCategories.DisplayName(category),
                        GUILayout.Width(170f));
                    _draftAssignments.TryGetValue(category.ToString(), out var current);
                    var updated = (AnimationClip)EditorGUILayout.ObjectField(
                        current, typeof(AnimationClip), false, GUILayout.Width(340f));
                    if (updated != current)
                    {
                        if (updated != null && !IsCompatible(character, updated, out var reason))
                        {
                            EditorUtility.DisplayDialog(
                                "Incompatible animation",
                                "'" + updated.name + "' cannot be retargeted onto "
                                + character.displayName + ":\n\n" + reason,
                                "OK");
                        }
                        else
                        {
                            _draftAssignments[category.ToString()] = updated;
                        }
                    }

                    if (updated != null)
                    {
                        GUILayout.Label(
                            string.Format("{0:0.00}s · {1} · {2}",
                                updated.length, updated.isLooping ? "loop" : "once",
                                updated.humanMotion ? "humanoid" : "generic"),
                            EditorStyles.miniLabel, GUILayout.Width(190f));
                    }
                }
            }

            EditorGUILayout.EndScrollView();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Load current config", GUILayout.Width(180f)))
                {
                    LoadDraftAssignments();
                }

                if (GUILayout.Button("Validate & Save (config + controllers)",
                        GUILayout.Height(36f), GUILayout.Width(360f)))
                {
                    SaveAssignments(character);
                }
            }

            if (!string.IsNullOrEmpty(_lastAssignMessage))
            {
                EditorGUILayout.HelpBox(_lastAssignMessage, MessageType.None);
            }
        }

        private CharacterDefinition _draftOwner;

        private CharacterDefinition GetDraftOwner() => _draftOwner;

        private void SelectDraftCharacter(CharacterDefinition character)
        {
            _draftOwner = character;
            _draftAssignments.Clear();
            for (var i = 0; i < _config.assignments.Count; i++)
            {
                var assignment = _config.assignments[i];
                if (assignment == null || assignment.clip == null)
                {
                    continue;
                }

                var isDefault = assignment.characterId == null;
                var isOurs = string.Equals(assignment.characterId, character.id, StringComparison.Ordinal);
                if (isDefault || isOurs)
                {
                    _draftAssignments[assignment.category.ToString()] = assignment.clip;
                }
            }
        }

        private void LoadDraftAssignments()
        {
            if (_config == null || string.IsNullOrEmpty(_selectedCharacterId))
            {
                return;
            }

            var character = _config.FindCharacter(_selectedCharacterId);
            if (character != null)
            {
                SelectDraftCharacter(character);
            }
        }

        private static bool IsCompatible(CharacterDefinition character, AnimationClip clip, out string reason)
        {
            var avatar = character.avatar;
            if (avatar == null && character.model != null)
            {
                avatar = RigDetectionUtility.FindAvatar(AssetDatabase.GetAssetPath(character.model));
            }

            return RigDetectionUtility.IsRetargetCompatible(clip, avatar, out reason);
        }

        private void SaveAssignments(CharacterDefinition character)
        {
            if (_config == null || character == null)
            {
                return;
            }

            // Drop stale drafts for this character, then write the draft back.
            for (var i = _config.assignments.Count - 1; i >= 0; i--)
            {
                var assignment = _config.assignments[i];
                if (assignment != null
                    && string.Equals(assignment.characterId, character.id, StringComparison.Ordinal))
                {
                    _config.assignments.RemoveAt(i);
                }
            }

            foreach (var category in Categories)
            {
                _draftAssignments.TryGetValue(category.ToString(), out var clip);
                if (clip == null)
                {
                    continue;
                }

                _config.AddOrUpdateAssignment(new CharacterAnimAssignment
                {
                    characterId = character.id,
                    category = category,
                    clip = clip,
                    sourcePath = string.Empty,
                }, character.id);
            }

            _config.selectedCharacterId = character.id;
            CharacterAnimationConfigLibrary.Save(_config);

            var build = AnimatorControllerBuilder.BuildController(_config, character);
            CharacterAnimationConfigLibrary.Save(_config);

            _lastAssignMessage = build.success
                ? "Saved. " + build.summary
                : "Saved assignments, controller NOT rebuilt: " + build.summary;
            if (build.warnings.Count > 0)
            {
                _lastAssignMessage += "\n" + string.Join("\n", build.warnings);
            }

            _report = new ImportReport { Title = "Controller build — " + character.displayName };
            _report.Lines.Add(new ImportReport.Line
            {
                severity = build.success ? ImportReport.Severity.Success : ImportReport.Severity.Failed,
                stage = "controller",
                message = build.summary,
            });
            for (var i = 0; i < build.warnings.Count; i++)
            {
                _report.Warn("controller", build.warnings[i]);
            }

            TeardownPreview();
            Repaint();
        }

        // ==================================================================
        // Preview tab
        // ==================================================================

        private void DrawPreviewTab()
        {
            EditorGUILayout.LabelField("Real-time 3D preview", EditorStyles.boldLabel);
            EnsurePreview();

            var rect = GUILayoutUtility.GetRect(560f, 640f, GUILayout.ExpandWidth(true));
            var texture = _preview != null ? _preview.OutputTexture : null;
            if (texture != null)
            {
                GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, false);
            }
            else
            {
                EditorGUI.DrawRect(rect, new Color(0.05f, 0.07f, 0.1f, 1f));
                GUI.Label(rect, "preview unavailable", CenteredGrey);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("◀ character"))
                {
                    _preview?.PrevCharacter();
                }

                if (GUILayout.Button("character ▶"))
                {
                    _preview?.NextCharacter();
                }

                if (GUILayout.Button("◀ clip"))
                {
                    _preview?.PrevAnimation();
                }

                if (GUILayout.Button("clip ▶"))
                {
                    _preview?.NextAnimation();
                }

                if (GUILayout.Button(_preview != null && _preview.IsPlaying ? "Pause" : "Play"))
                {
                    _preview?.TogglePlayPause();
                }

                if (GUILayout.Button("Reset view"))
                {
                    if (_preview != null)
                    {
                        _preview.Rotate(-_preview.Yaw, -_preview.Pitch);
                        _preview.ZoomBy(_preview.Distance - 3.2f);
                    }
                }
            }

            // Mouse orbit / zoom inside the preview rect.
            var currentEvent = Event.current;
            if (currentEvent != null && rect.Contains(currentEvent.mousePosition))
            {
                if (currentEvent.type == EventType.MouseDrag && currentEvent.button == 0 && _preview != null)
                {
                    _preview.Rotate(currentEvent.delta.x * 0.4f, -currentEvent.delta.y * 0.3f);
                    Repaint();
                }
                else if (currentEvent.type == EventType.ScrollWheel && _preview != null)
                {
                    _preview.ZoomBy(currentEvent.delta.y * 0.4f);
                    Repaint();
                }
            }

            if (_preview != null)
            {
                EditorGUILayout.LabelField("Character: " + _preview.CurrentCharacterName
                    + "   Animation: " + _preview.CurrentAnimationName);
                EditorGUILayout.LabelField(_preview.CurrentAnimationInfo);
                EditorGUILayout.HelpBox(_preview.RigStatusText, MessageType.None);
                EditorGUILayout.HelpBox(_preview.MaterialStatusText, MessageType.None);
                EditorGUILayout.HelpBox(_preview.LastMessage, MessageType.None);
            }
        }

        private static GUIStyle _centeredGrey;

        private static GUIStyle CenteredGrey
        {
            get
            {
                if (_centeredGrey == null)
                {
                    _centeredGrey = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                    {
                        fontSize = 22,
                        alignment = TextAnchor.MiddleCenter,
                    };
                }

                return _centeredGrey;
            }
        }

        private CharacterAnimationConfig _previewConfig;

        private void EnsurePreview()
        {
            var config = _config ?? CharacterAnimationConfigLibrary.LoadOrCreate();
            if (_preview != null && _previewInitialized && ReferenceEquals(_previewConfig, config))
            {
                return;
            }

            TeardownPreview();
            var previewObject = new GameObject("CharacterManagerPreview")
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            _preview = previewObject.AddComponent<CharacterPreviewController>();
            _preview.Initialize(config);
            _preview.SetManualRendering(true);
            _previewConfig = config;
            _previewInitialized = true;
        }

        private void TeardownPreview()
        {
            if (_preview != null)
            {
                DestroyImmediate(_preview.gameObject);
                _preview = null;
            }

            _previewInitialized = false;
        }

        private void OnEditorUpdate()
        {
            if (_tab != 3 || _preview == null || _preview.OutputTexture == null)
            {
                return;
            }

            var deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
            {
                deltaTime = 1f / 60f;
            }

            _preview.Tick(deltaTime);
            _preview.RenderOnce();
            Repaint();
        }

        private bool hasPreviewTexture() => _preview.OutputTexture != null;

        // ==================================================================
        // Report tab
        // ==================================================================

        private void DrawReportTab()
        {
            if (_report == null)
            {
                EditorGUILayout.HelpBox(
                    "No import has run in this session yet. The report of the last import "
                    + "is also saved to " + CharacterAnimationConfigLibrary.ReportPath + ".",
                    MessageType.Info);
                if (GUILayout.Button("Load saved report", GUILayout.Width(200f)))
                {
                    var path = CharacterAnimationConfigLibrary.AbsolutePathOf(
                        CharacterAnimationConfigLibrary.ReportPath);
                    if (File.Exists(path))
                    {
                        _report = new ImportReport { Title = "Saved report (loaded)" };
                        _report.Success("report", File.ReadAllText(path));
                    }
                }

                return;
            }

            EditorGUILayout.LabelField(_report.Title, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                _report.SuccessCount + " succeeded · " + _report.WarningCount
                + " warnings · " + _report.FailedCount + " failed");
            EditorGUILayout.Space(4f);

            foreach (var line in _report.Lines)
            {
                var messageType = line.severity == ImportReport.Severity.Success ? MessageType.Info
                    : line.severity == ImportReport.Severity.Warning ? MessageType.Warning
                    : MessageType.Error;
                EditorGUILayout.HelpBox("[" + line.stage + "] " + line.message, messageType);
            }

            if (GUILayout.Button("Save report to " + CharacterAnimationConfigLibrary.ReportPath,
                    GUILayout.Width(420f)))
            {
                CharacterAnimationConfigLibrary.WriteReport(_report);
                ShowNotification(new GUIContent("Report saved"));
            }
        }
    }
}
