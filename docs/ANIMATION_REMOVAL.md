# Animation removal (2026-11-20)

**Owner instruction:** *"Remove all animations"* — clarified as the Mixamo FBX
animation clips (running, crouch, jump, death, weapon …) under
`unity-project/Assets/Animations/`. The Male/Female character model FBX files
were explicitly **out of scope** and remain in the project.

This document records exactly what was deleted, what was rewritten, what the
game does now, and which gates keep the removal honest.

## 1. What the game does now

* `ThirdPersonMotor` still owns movement, sprint, crouch state, jumping and
  aiming — gameplay, input and camera are untouched.
* `PlayerCharacterVisual` still spawns the configured Male/Female model under
  the player and hides the capsule. It no longer creates an `Animator`, no
  longer assigns a generated Animator Controller and no longer pushes
  `Speed` / `IsCrouching` / `IsAiming` parameters.
* The character therefore renders in its **static imported pose** while the
  transform moves. There is no walk cycle, no crouch pose, no jump, no shoot or
  reload one-shot and no death animation.
* `SetAiming`, `TriggerShoot`, `TriggerReload`, `TriggerDeath` and
  `ResetToDefaultState` remain on `PlayerCharacterVisual` as **honest no-ops**
  so `WeaponController` and the PlayMode hook-safety tests keep compiling and
  keep proving the call sites are safe.
* The Character Bay and the Home Screen showcase still render the real model
  live into a RenderTexture with orbit/zoom; the CLIP ◀/▶ buttons, PLAY/PAUSE
  and the idle-clip auto-select are gone.

## 2. Deleted assets (153 files under `Assets/`)

`unity-project/Assets/Animations/{Combat,Crouch,Death,Movement,Weapon}/` —
74 `.fbx` clips + their 74 `.fbx.meta` importer settings + 5 `.gitkeep` files.
Clip counts per folder: Movement 45, Crouch 13, Death 7, Combat 5, Weapon 4.

Also removed because they only existed to serve clips:

* `Assets/AnimationControllers/.gitkeep`, `Assets/AvatarMasks/.gitkeep`
  (generated controller / avatar-mask folders).

A GUID sweep over every committed `*.unity`, `*.prefab`, `*.asset`, `*.mat`,
`*.controller` and `*.playable` confirmed **zero serialized references** to the
74 clip `.meta` GUIDs before deletion, so no scene or prefab broke.

## 3. Deleted C# (with their `.meta` where committed)

| File | Why |
| --- | --- |
| `Assets/Editor/AnimationImportService.cs` | imported clips from FBX |
| `Assets/Editor/AnimatorControllerBuilder.cs` | built the generated controllers + UpperBody mask |
| `Assets/Editor/CharacterAnimationManagerWindow.cs` | clip/assignment authoring window |
| `Assets/Editor/MixamoAnimationImportRules.cs` | `MixamoImportSettings` / `MixamoAnimationImportService` audit |
| `Assets/Editor/FBXProductionVerifier.cs` | licensed-Unity clip/controller/deformation verification |
| `Assets/Editor/FBXProductionCapture.cs` | software-GL clip deformation screenshots |
| `Assets/Scripts/Characters/FootIK.cs` | foot planting on the walk cycle |
| `Assets/Scripts/Characters/HeadLookIK.cs` | head aim blending |
| `Assets/Scripts/Characters/WeaponHandIK.cs` | foregrip hand IK |
| `Assets/Scripts/Characters/ProceduralLean.cs` | spine lean over the locomotion blend |
| `Assets/Scripts/Characters/CharacterMotionRig.cs` | orchestrator for the four IK components |
| `Assets/Scripts/Characters/PlayerLocomotion.cs` | drove the `Speed` blend parameter |
| `Assets/Tests/EditMode/CharacterAnimationIntegrationTests.cs` | clip registration / controller build tests |
| `Assets/Tests/EditMode/CharacterRepairRegressionTests.cs` | clip-assignment regression tests |
| `Assets/Tests/PlayMode/FBXProductionPlayModeTests.cs` | clip playback / deformation PlayMode tests |

## 4. Rewritten C#

* `Assets/Scripts/Characters/CharacterAnimationConfig.cs` — now only the
  character roster (`CharacterDefinition`, selection helpers) plus the
  imported-source SHA registry used for duplicate prevention. `clips`,
  `assignments`, `CharacterAnimCategory`, `CharacterAnimAssignment`,
  `CharacterAnimParameters`, `CharacterAnimCategories`, `FindClip`,
  `FindAssignment`, `ValidateReferences` and the controller reference are gone.
  The class keeps its historical name so the asset path and the protected-asset
  lineage stay traceable.
* `Assets/Editor/CharacterAnimationIntegration.cs` — same class name and same
  CI entry points (`Build`, `BuildFromMenu`, `BuildFromCommandLine`), but it now
  builds the roster only and **fails** if an FBX reappears under
  `Assets/Animations/`.
* `Assets/Editor/CharacterAnimationConfigLibrary.cs` — clip/controller folders
  dropped; `AnimationsRoot` kept so validators can prove the folder is empty.
* `Assets/Editor/ProjectValidator.cs` — `CheckMixamoAnimationImportSettings`
  removed, `CheckCharacterAnimationAssets` replaced by `CheckCharacterAssets`
  (character models, Humanoid avatars, roster, and a new
  "no animation clips remain" gate).
* `Assets/Editor/AssetImportRules.cs` — `IsHumanoidAsset` no longer treats
  `Assets/Animations/` as a Humanoid clip folder.
* `Assets/Editor/ProjectBootstrap.cs` — no longer creates `Assets/Animations`.
* `Assets/Scripts/Characters/{PlayerCharacterVisual,CharacterPreviewController,CharacterBayMenu}.cs`,
  `Assets/Scripts/UI/LobbyCharacterShowcase.cs` — animation driving removed,
  preview/orbit/zoom kept.
* `Assets/Characters/GLTF_Import_Test/Editor/GLTFImportTestValidator.cs` —
  `AnimationMode.SampleAnimationClip` retargeting probe retired; it now reports
  `NOT VERIFIED — no AnimationClip remains` instead of measuring motion.
* Tests: `CharacterPipelineTests.cs` and `CharacterPreviewRuntimeTests.cs`
  rewritten around the roster/preview behaviour, plus a new
  `AnimationLibrary_IsRemovedAndStaysRemoved` regression test and an
  integration-builder test that proves the roster builds without clips.

## 5. Deleted pipeline / CI

* `processing/unity/validate_character_animation.py` + its unit tests.
* `processing/animation-metadata/` (`.fbx.meta` sync tool, manifest, tests).
* Workflows: `validate-animations.yml`, `validate-character-animation.yml`,
  `sync-animation-metadata.yml`.
* `processing/unity/fbx_production_repair_manifest.json` and
  `fbx_production_repair_baseline.json` — the historical targeted-repair chain
  referenced deleted editor files, so the chain was retired rather than
  re-derived; `fbx-baseline.json` now hash-locks exactly the two character FBX.
* `unity.yml`: animation-metadata test step, `validate_character_animation.py`
  step, licensed `FBXProductionVerifier` step, software-GL FBX capture step,
  its artifact upload, its evidence-commit step, the
  `capture_fbx_screenshots` dispatch input and the FBX part of the final gate.
  The character integration step and the APK gate in `build.yml` were renamed to
  "character roster integration".

## 6. Deleted / updated docs

Deleted: `CHARACTER_ANIMATION_PIPELINE.md`, `gameplay/CHARACTER_MOTION.md`,
`asset-pipeline/ANIMATION_METADATA_SYNC.md`,
`verification/UNITY_CHARACTER_ANIMATION_VERIFICATION.md`,
`FBX_PRODUCTION_VERIFICATION_REPORT.md`,
`fbx_production_verification/{EDITOR_VALIDATION,CAPTURE_RESULT}.{md,json}` and
the deformation screenshots.

Updated: `README.md`, `CONTRIBUTING.md`, `ASSET_PIPELINE.md`,
`gameplay/SHOOTING_MECHANICS.md`, `GLTF_PLAYER_IMPORT_TEST_REPORT.md`,
`unity-project/Assets/UserAssets/README.md`, and the two committed static
validation reports (`docs/gltf_import_test/`, `docs/fbx_production_verification/`).

## 7. New invariants (enforced, not documented-only)

1. `Assets/Animations/` must contain **no** `.fbx`
   (`validate_fbx_production.py` → "animation FBX clips removed",
   `validate_gltf_import_test.py` → same, `ProjectValidator` → "no animation
   clips remain", `CharacterAnimationIntegration.Build` → hard failure,
   `CharacterPipelineTests.AnimationLibrary_IsRemovedAndStaysRemoved`).
2. No file under `Assets/Scripts` may reference `Animator`,
   `runtimeAnimatorController` or `AnimationClip` in code (comments excluded) —
   `validate_fbx_production.py`.
3. `PlayerCharacterVisual.cs` must keep the capsule-fallback spawn path and must
   carry no Animator wiring.
4. `CharacterAnimationConfig` must stay a clip-free roster.
5. `fbx-baseline.json` must hash-lock exactly two FBX files (Male + Female).

## 8. Local validation after the change

```
validate_structure.py        18 passed, 0 failed
validate_csharp.py           36 passed, 0 failed   (tree-sitter, not a Unity compile)
validate_unity_map.py        26 passed, 0 failed
validate_gltf_import_test.py 33 passed, 0 failed, 3 not verified
validate_fbx_production.py   27 passed, 0 failed, 8 not verified
processing/unity/tests       23 tests OK
processing/blender/tests     13 tests OK
```

Unity EditMode/PlayMode compilation and the licensed-Editor gates can only be
proven by CI; `NOT VERIFIED` rows are never counted as passes.
