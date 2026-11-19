# Static validation — production FBX characters

Static only: this validator reads committed bytes and source. It does not claim Unity import, Avatar validity, animation playback, rendering, APK output or device behaviour.

| Check | Status | Detail |
| --- | --- | --- |
| verification boundary | PASS | Static validator reads committed bytes and source only; it never claims Unity import, rendering, Avatar validity, animation playback, APK output or device behaviour. |
| all 76 production FBX files immutable | PASS | 76/76 FBX files match processing/unity/fbx-baseline.json byte-for-byte |
| production Unity assets and FBX metadata unchanged | PASS | 246 protected Assets/ProjectSettings files are byte-identical; both character .fbx.meta GUIDs are unchanged |
| GLTF fallback files and test scene preserved | PASS | isolated GLTF validator: 33 PASS / 0 FAIL / 4 NOT VERIFIED; original GLTF/BIN/PNG files and test scene are present |
| Male FBX GUID unchanged | PASS | unity-project/Assets/Characters/Male/Male character.fbx.meta has stable GUID c1867a8de9f4b1f1b76e69e097d73103 |
| Male Unity importer settings | PASS | 8 importer settings match the committed Humanoid policy in unity-project/Assets/Characters/Male/Male character.fbx.meta |
| Male FBX binary structure | PASS | FBX 7400, Z-up, unit scale 100, 3 skinned meshes, 65 bones, 22 canonical humanoid bones mapped, 0 missing required bones, no embedded AnimationStack |
| Male embedded source textures | PASS | 7/7 texture nodes carry embedded PNG bytes (3,939,246 bytes); no unresolved external texture path |
| Male no dangling committed material/texture asset reference | PASS | unity-project/Assets/Characters/Male/Materials and unity-project/Assets/Characters/Male/Textures contain only .gitkeep; FBX is the self-contained production source |
| Female FBX GUID unchanged | PASS | unity-project/Assets/Characters/Female/Female character.fbx.meta has stable GUID 103ec3786913589cd676c3534c5074e7 |
| Female Unity importer settings | PASS | 8 importer settings match the committed Humanoid policy in unity-project/Assets/Characters/Female/Female character.fbx.meta |
| Female FBX binary structure | PASS | FBX 7400, Z-up, unit scale 100, 3 skinned meshes, 65 bones, 22 canonical humanoid bones mapped, 0 missing required bones, no embedded AnimationStack |
| Female embedded source textures | FAIL | 6/7 texture nodes carry embedded PNG bytes (2,717,177 bytes); unresolved external path(s): C:\Dropbox\Work\Blender\_FreeModels\_Unfinished\Universal Base Characters\Base Characters\textures\T_Superhero_Female_Normal.png |
| Female no dangling committed material/texture asset reference | PASS | unity-project/Assets/Characters/Female/Materials and unity-project/Assets/Characters/Female/Textures contain only .gitkeep; FBX is the self-contained production source |
| available clip: Idle | PASS | unity-project/Assets/Animations/Movement/idle.fbx: FBX 7700, one real AnimationStack, 315 curves, 63 frames, 22 canonical humanoid bones, 0 missing required bones |
| Idle clip Humanoid import metadata | PASS | 7 importer settings match the committed Humanoid policy in unity-project/Assets/Animations/Movement/idle.fbx.meta |
| available clip: Walk | PASS | unity-project/Assets/Animations/Movement/walking.fbx: FBX 7700, one real AnimationStack, 315 curves, 41 frames, 22 canonical humanoid bones, 0 missing required bones |
| Walk clip Humanoid import metadata | PASS | 7 importer settings match the committed Humanoid policy in unity-project/Assets/Animations/Movement/walking.fbx.meta |
| available clip: Run | PASS | unity-project/Assets/Animations/Movement/Running.fbx: FBX 7700, one real AnimationStack, 315 curves, 21 frames, 22 canonical humanoid bones, 0 missing required bones |
| Run clip Humanoid import metadata | PASS | 7 importer settings match the committed Humanoid policy in unity-project/Assets/Animations/Movement/Running.fbx.meta |
| available clip: Jump | PASS | unity-project/Assets/Animations/Movement/jump up.fbx: FBX 7700, one real AnimationStack, 315 curves, 16 frames, 22 canonical humanoid bones, 0 missing required bones |
| Jump clip Humanoid import metadata | PASS | 7 importer settings match the committed Humanoid policy in unity-project/Assets/Animations/Movement/jump up.fbx.meta |
| available clip: Aim | PASS | unity-project/Assets/Animations/Combat/idle aiming.fbx: FBX 7700, one real AnimationStack, 315 curves, 63 frames, 22 canonical humanoid bones, 0 missing required bones |
| Aim clip Humanoid import metadata | PASS | 7 importer settings match the committed Humanoid policy in unity-project/Assets/Animations/Combat/idle aiming.fbx.meta |
| available clip: Shoot | PASS | unity-project/Assets/Animations/Weapon/firing rifle.fbx: FBX 7700, one real AnimationStack, 315 curves, 8 frames, 22 canonical humanoid bones, 0 missing required bones |
| Shoot clip Humanoid import metadata | PASS | 7 importer settings match the committed Humanoid policy in unity-project/Assets/Animations/Weapon/firing rifle.fbx.meta |
| available clip: Reload | PASS | unity-project/Assets/Animations/Weapon/reloading.fbx: FBX 7700, one real AnimationStack, 315 curves, 99 frames, 22 canonical humanoid bones, 0 missing required bones |
| Reload clip Humanoid import metadata | PASS | 7 importer settings match the committed Humanoid policy in unity-project/Assets/Animations/Weapon/reloading.fbx.meta |
| runtime FBX spawn path preserved | PASS | PlayerCharacterVisual loads the generated config, instantiates the FBX model, assigns the controller and disables the capsule MeshRenderer when configured |
| Aim Animator parameter is driven by runtime | FAIL | no SetBool(IsAiming) exists under Assets/Scripts; the generated Aim state uses IsAiming, while PlayerCharacterVisual.SetAiming only changes UpperBody layer weight |
| Aim gameplay hook is called | FAIL | PlayerCharacterVisual.SetAiming(bool) has no runtime caller in Assets/Scripts; the Aim state cannot be entered from current gameplay input |
| live shooting gameplay integration | NOT VERIFIED | no live weapon/fire system exists under Assets/Scripts; only PlayerCharacterVisual.TriggerShoot/TriggerReload animation hooks and UI copy were found |
| committed gameplay prefab / controller / config references | NOT VERIFIED | no committed production prefab (0), controller (0) or config asset (0) exists; generated config/controllers and PlayerTest scene are Editor-created and must be checked in licensed Unity |
| Android-compatible rendering configuration | PASS | Linear, GPU skinning, min SDK 26, ARMv7+ARM64, IL2CPP, URP 17 and ASTC source settings are present |
| Android device rendering / shader variants / FPS / memory / thermal | NOT VERIFIED | configuration is static evidence only; no APK was built or installed and no physical Android device measurement was performed by this validator |
| Unity Editor import settings and Humanoid Avatar validity | NOT VERIFIED | requires licensed Unity 6; see EDITOR_VALIDATION.md |
| Animator transitions and missing generated references | NOT VERIFIED | requires the Editor-generated config/controllers; see EDITOR_VALIDATION.md |
| animation deformation and visual mesh quality | NOT VERIFIED | requires AnimationMode sampling and genuine Editor renders; see EDITOR_VALIDATION.md and CAPTURE_RESULT.md |
| visible player mesh in gameplay scene | NOT VERIFIED | requires PlayerTest scene and generated config in licensed Unity; see EDITOR_VALIDATION.md |
| movement / camera / collision / ground alignment | NOT VERIFIED | requires PlayMode tests in licensed Unity; see the PlayMode results in the CI job |
| human front / facial / UV seam inspection | NOT VERIFIED | requires human visual review; rear-view software-GL renders are not sufficient |

## Summary

- PASS: 29
- FAIL: 3
- NOT VERIFIED: 9

Licensed Unity Editor evidence is written to `EDITOR_VALIDATION.md`; software-GL visual evidence, when captured, is written to `CAPTURE_RESULT.md` and the `screenshots/` directory.
