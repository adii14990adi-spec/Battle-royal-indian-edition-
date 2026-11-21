# Static validation — production FBX characters

Static only: this validator reads committed bytes and source. It does not claim Unity import, Avatar validity, animation playback, rendering, APK output or device behaviour.

| Check | Status | Detail |
| --- | --- | --- |
| verification boundary | PASS | Static validator reads committed bytes and source only; it never claims Unity import, rendering, Avatar validity, animation playback, APK output or device behaviour. |
| all 2 production FBX files immutable | PASS | 2/2 committed production FBX files (the Male and Female character models) match processing/unity/fbx-baseline.json byte-for-byte |
| all other production Unity assets and FBX metadata unchanged | PASS | 86 non-allowlisted protected Assets/ProjectSettings files are byte-identical; FBX binaries and both character GUIDs are unchanged |
| protected manifest allowlist is minimal | PASS | 2 allowlisted path(s): only the two test asmdefs are exempt from the byte-identity gate |
| animation FBX clips removed | PASS | no FBX animation clip remains under unity-project/Assets/Animations |
| GLTF fallback files and test scene preserved | PASS | isolated GLTF validator: 33 PASS / 0 FAIL / 3 NOT VERIFIED; original GLTF/BIN/PNG files and test scene are present |
| Git LFS object identity and checkout smudge rules | PASS | 5/5 committed PNG blobs match their LFS oid/size and declare filter/diff/merge=lfs; CI checkout lfs:true smudges the exact objects (water_normal_256.png=pointer, battle_horizons_loading_bar.png=pointer, battle_horizons_loading_spinner.png=pointer, battle_horizons_logo.png=pointer, battle_horizons_icon.png=pointer) |
| 14 exact production character textures and importer settings | PASS | 7 Male + 7 Female PNGs are byte-identical to FBX Video Content or, for the one unresolved Female normal, the unchanged existing GLTF fallback; all normal maps are TextureImporterType.NormalMap with linear/no-convert settings |
| 6 deterministic URP/Lit material remaps | PASS | 3 Male + 3 Female MI_* FBX material names remap by exact GUID to committed URP/Lit materials with exact base/normal texture GUIDs |
| Male FBX GUID unchanged | PASS | unity-project/Assets/Characters/Male/Male character.fbx.meta has stable GUID c1867a8de9f4b1f1b76e69e097d73103 |
| Male Unity importer settings | PASS | 11 importer settings match the committed Humanoid policy in unity-project/Assets/Characters/Male/Male character.fbx.meta |
| Male Unity material import mode | PASS | materialImportMode=1 (ImportStandard); exact committed URP/Lit external material remaps are active |
| Male FBX binary structure | PASS | FBX 7400, Z-up, unit scale 100, 3 skinned meshes, 65 bones, 22 canonical humanoid bones mapped, 0 missing required bones, no embedded AnimationStack |
| Male embedded source textures | PASS | 7/7 embedded PNGs (3,939,246 bytes); no unresolved external path |
| Male exact targeted material/texture asset scope | PASS | 7 PNGs + 7 PNG metas + 3 .mat + 3 .mat metas, no unexpected files, both .gitkeep markers retained |
| Female FBX GUID unchanged | PASS | unity-project/Assets/Characters/Female/Female character.fbx.meta has stable GUID 103ec3786913589cd676c3534c5074e7 |
| Female Unity importer settings | PASS | 11 importer settings match the committed Humanoid policy in unity-project/Assets/Characters/Female/Female character.fbx.meta |
| Female Unity material import mode | PASS | materialImportMode=1 (ImportStandard); exact committed URP/Lit external material remaps are active |
| Female FBX binary structure | PASS | FBX 7400, Z-up, unit scale 100, 3 skinned meshes, 65 bones, 22 canonical humanoid bones mapped, 0 missing required bones, no embedded AnimationStack |
| Female embedded source textures | PASS | 6/7 embedded PNGs (2,717,177 bytes); the immutable FBX's one external T_Superhero_Female_Normal.png is resolved at import by the byte-identical existing GLTF fallback texture |
| Female exact targeted material/texture asset scope | PASS | 7 PNGs + 7 PNG metas + 3 .mat + 3 .mat metas, no unexpected files, both .gitkeep markers retained |
| runtime FBX spawn path preserved | PASS | PlayerCharacterVisual loads the generated roster config, instantiates the FBX model and disables the capsule MeshRenderer when configured |
| player visual carries no Animator wiring | PASS | PlayerCharacterVisual.cs holds no Animator / AnimatorController / AnimationClip reference after the animation removal |
| config asset is a character roster without clips | PASS | CharacterAnimationConfig keeps the character roster and selection helpers and no longer stores AnimationClip assignments or generated controllers |
| actual runtime aim state and touch input exist | PASS | ThirdPersonMotor.AimInput/IsAiming and PlayerInputRouter/MobileControlsBuilder AIM hold-button wiring are present |
| no runtime script plays animation clips | PASS | no file under Assets/Scripts references an Animator, runtimeAnimatorController or AnimationClip (comments excluded) |
| live shooting gameplay integration | NOT VERIFIED | aiming state exists and the visual hooks are no-ops, but this validator cannot exercise a live match; only PlayMode hook-safety tests cover the call sites |
| committed gameplay prefab / controller / config references | NOT VERIFIED | no committed production prefab (0), controller (0) or config asset (0) exists; the generated roster config and PlayerTest scene are Editor-created and must be checked in licensed Unity |
| Android-compatible rendering configuration | PASS | Linear, GPU skinning, min SDK 26, ARMv7+ARM64, IL2CPP, URP 17 and ASTC source settings are present |
| Android device rendering / shader variants / FPS / memory / thermal | NOT VERIFIED | configuration is static evidence only; no APK was built or installed and no physical Android device measurement was performed by this validator |
| Unity Editor import settings and Humanoid Avatar validity | NOT VERIFIED | requires licensed Unity 6 |
| visible player mesh in gameplay scene | NOT VERIFIED | requires the PlayerTest scene and generated roster config in licensed Unity |
| movement / camera / collision / ground alignment | NOT VERIFIED | requires PlayMode tests in licensed Unity; see the PlayMode results in the CI job |
| human front / facial / UV seam inspection | NOT VERIFIED | requires human visual review; rear-view software-GL renders are not sufficient |
| correct textured Male/Female Editor camera captures | NOT VERIFIED | requires licensed Unity 6 software-GL Camera.Render evidence |

## Summary

- PASS: 27
- FAIL: 0
- NOT VERIFIED: 8

Licensed Unity Editor evidence is written to `EDITOR_VALIDATION.md`; software-GL visual evidence, when captured, is written to `CAPTURE_RESULT.md` and the `screenshots/` directory.
