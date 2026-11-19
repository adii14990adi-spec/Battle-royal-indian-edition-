# Unity 6 GLTF Player Import Test Report

**Status:** isolated experiment complete; **recommendation: KEEP EXISTING FBX pending explicit approval.** Both corrected GLTF copies import and render in licensed Unity 6, and both build valid Humanoid Avatars. Existing-animation retargeting, deformation, Android APK output and device runtime are NOT VERIFIED. No production player asset was replaced and no automatic architecture change was made.

**Game repository:** `adii14990adi-spec/Battle-royal-indian-edition-`  
**Character source:** `adii14990adi-spec/Animation-.meta-`, branch `main`, commit `b3872f9276461dc3ecffed9e15731e98ed867d86`  
**Inspected source URL:** https://github.com/adii14990adi-spec/Animation-.meta-/tree/main/Character  
**Engine / target:** Unity `6000.0.85f2`, URP, Android  
**Report date:** 2026-11-19 (final update)

> Verification boundary: static checks, licensed Unity 6 Editor validation, EditMode/PlayMode tests and genuine software-GL Unity camera screenshots are reported below. The Android APK build was attempted but refused by the project validator before producing an APK; no Android device/runtime test was performed. A successful import/compile/Editor render is never treated as proof of device correctness.

## 1. Source repository and exact imported filenames

The live source tree was inspected via the GitHub API and a codeload archive. **23 files** were present under `Character/` at inspection time, including a dotfile named `. gitkeep` in the Female folder. All files were copied byte-for-byte into `Assets/Characters/GLTF_Import_Test/{Male,Female}/Original/`. The complete source inventory and hashes are also in `Assets/Characters/GLTF_Import_Test/Documentation/SOURCE_MANIFEST.md` and `processing/unity/gltf_import_test_source_manifest.json`.

### Male — 11 exact files

| File | Bytes | SHA-256 |
| --- | ---: | --- |
| `License_Standard.txt` | 806 | `0f4beaf0fe360a7732e58bbe3dbf60a2422367fbea60cb9ea4add968f383268e` |
| `Superhero_Male_FullBody.bin` | 720076 | `459003f9745853ae562a85506a2b94dd56515c1f37728f9fa3d2ce1a3e4cd92f` |
| `Superhero_Male_FullBody.gltf` | 30989 | `e7fcea214ecf8855afbf910b50de6f9c7d1decfb71ca28bad8a4481452dafeb4` |
| `T_Eye_Brown.png` | 35958 | `d08e3356a83211bc6ca21fe3a8e39f4b5c1a3b8f85457fc2c0fb57be09935025` |
| `T_Eye_Normal.png` | 15099 | `9ed61f7726a54fe346a78b9e5a18905d8e2b88f86235d97f53cd207a26f3f8c7` |
| `T_Hair_1_BaseColor.png` | 1570376 | `bc7aa863bd22ab0a995cd838cceb4d3a5186ee54ee2fb0108fad85d20c057e6e` |
| `T_Hair_1_BaseColor_png.png` | 1570376 | `bc7aa863bd22ab0a995cd838cceb4d3a5186ee54ee2fb0108fad85d20c057e6e` |
| `T_Hair_1_Normal.png` | 4326126 | `57fd0ad8c96a4d01b38769637e066cecaa285b456f6e48ce00863754a457affb` |
| `T_Superhero_Male_Dark.png` | 1384380 | `df680cbc1967a61e85998bfafb8f7bc0a36dd604bb3266486c4b06b1a184bb45` |
| `T_Superhero_Male_Normal.png` | 4252937 | `9b25cf9200216eb754b79cf4c63b5380c419780788322f3c57cdd2b3e02679a6` |
| `T_Superhero_Male_Roughness.png` | 3152049 | `c70dabf48574cda55644d556beae1ee7cf1bdcf68a50d259d20f2f2358f27991` |

### Female — 12 exact files

| File | Bytes | SHA-256 |
| --- | ---: | --- |
| `. gitkeep` | 1 | `01ba4719c80b6fe911b091a7c05124b64eeece964e09c058ef8f9805daca546b` |
| `License_Standard(1).txt` | 806 | `0f4beaf0fe360a7732e58bbe3dbf60a2422367fbea60cb9ea4add968f383268e` |
| `Superhero_Female_FullBody.bin` | 990808 | `3a8220a485b33d05d879115a50697728b45a151781106033afb8b8c243fca208` |
| `Superhero_Female_FullBody.gltf` | 31656 | `adedf28000a0716f689b009a70314506fc62f827498f77ba852acb5610f3f3f4` |
| `T_Eye_Brown.png` | 35958 | `d08e3356a83211bc6ca21fe3a8e39f4b5c1a3b8f85457fc2c0fb57be09935025` |
| `T_Eye_Normal.png` | 15099 | `9ed61f7726a54fe346a78b9e5a18905d8e2b88f86235d97f53cd207a26f3f8c7` |
| `T_Hair_2_BaseColor.png` | 1729467 | `f9f4f2fb3eeeefb0b6f0fdef38b0770fd6607640c1c680b8139dd76697e9f9b3` |
| `T_Hair_2_BaseColor_png.png` | 1729467 | `f9f4f2fb3eeeefb0b6f0fdef38b0770fd6607640c1c680b8139dd76697e9f9b3` |
| `T_Hair_2_Normal.png` | 4710013 | `172f230aae0d4366c3cfc0402b2e6d1811b11f8487344bff28605eeacac1558d` |
| `T_Superhero_Female_Dark_BaseColor.png` | 1319410 | `d366843ee7d9e9f37ec87a0e1c98c7f2e45573c08eeb80a51a6d9344e68cf288` |
| `T_Superhero_Female_Normal.png` | 3940537 | `cf922460b43ccd31e983e34db05514c9d451dd2f9cdd01a843978e797719f859` |
| `T_Superhero_Female_Roughness.png` | 3277022 | `d2beabf2bd0313e32c68dcdbb80dca371a6e994fabf96efa97128bf9f14fa57b` |

## 2. Import layout / isolation

- `Male/Original/` and `Female/Original/`: exact source sets, unchanged.
- `Male/UnityTestCopy/` and `Female/UnityTestCopy/`: complete companion sets plus **only** a corrected `.gltf` JSON. No PNG was renamed and no BIN was modified.
- `Materials/` and `Textures/`: explanatory README files; glTFast 6.21.0 generates materials/textures as glTF sub-assets and requires companion PNGs beside the corrected glTF.
- `Documentation/`: licence copies, exact source manifest, URI fixes, Editor validation and screenshot reports.
- `GLTF_Player_Import_Test.unity`: isolated test scene. The committed scene is the valid Unity-generated scene produced by `GLTFImportTestBuilder.BuildSceneFromCommandLine` and committed by the licensed visual-capture run (`41ec45e`); the original placeholder is no longer present.
- `Runtime/` and `Editor/`: isolated loader, environment builder, Editor validator, scene builder and screenshot capture scripts. No gameplay script, prefab, controller, FBX or `.fbx.meta` was modified.

## 3. Male / Female import result (static + licensed Editor)
| Sex | Original direct import | Corrected Unity test copy | Static status | Licensed Unity 6 Editor status |
| --- | --- | --- | --- | --- |
| Male | FAIL for direct use: glTF references `T_Hair_1_Normal_png.png` and `T_Eye_Normal_png.png`, which are absent. Original files preserved. | PASS static URI check: all BIN/PNG URIs resolve; corrected JSON is separate. | PASS (test copy) / known source URI defect documented | PASS: glTFast ScriptedImporter produced a native Unity prefab; 3 renderers / 3 SkinnedMeshRenderers; materials, UVs, skin bones and transform bounds passed. |
| Female | FAIL for direct use: glTF references `T_Eye_Normal_png.png`, which is absent. Original files preserved. | PASS static URI check: all BIN/PNG URIs resolve; corrected JSON is separate. | PASS (test copy) / known source URI defect documented | PASS: glTFast ScriptedImporter produced a native Unity prefab; 3 renderers / 3 SkinnedMeshRenderers; materials, UVs, skin bones and transform bounds passed. |

Editor bounds were Male `size=(1.860, 1.916, 0.441), minY=-0.102` and Female `size=(1.670, 1.854, 0.443), minY=-0.085`; the capture step then aligned both to `minY=0.000` on the test floor. Screenshots show both full bodies upright in T-pose. Embedded glTF animation is absent, so deformation under animation is **NOT VERIFIED**.

## 4. BIN and PNG URI verification

- BIN URIs: `Superhero_Male_FullBody.bin` and `Superhero_Female_FullBody.bin` resolve in both original and test-copy folders.
- Male corrected URIs: `T_Hair_1_Normal_png.png` → `T_Hair_1_Normal.png`; `T_Eye_Normal_png.png` → `T_Eye_Normal.png`.
- Female corrected URIs: `T_Eye_Normal_png.png` → `T_Eye_Normal.png`.
- All other image URIs already resolve case-sensitively.
- Corrections exist only in `UnityTestCopy/*.gltf` and `URI_FIXES.json`; source `.gltf`, `.bin` and `.png` hashes remain unchanged.

## 5. Material and texture verification
- Each glTF declares **3 materials**: hair, eyes and superhero body.
- Each corrected glTF declares **7 image URIs**; all resolve to valid PNG files with IHDR dimensions.
- Source PNG dimensions: eye textures **256×256**; hair/body textures **2048×2048**. Colour types and exact hashes are in the source manifest.
- No source texture was downscaled, renamed, recompressed or converted.
- Licensed Editor validator: **PASS** for Male and Female `materials / shaders / textures` — "no error shader or missing texture property found". It also found **3 renderers / 3 SkinnedMeshRenderers** per character.
- Genuine Unity camera screenshots contain **0 magenta/pink pixels** by pixel scan. The side-by-side comparison render contains cool blue/white specular/normal highlights (about 0.56% blue-dominant pixels) while the individual renders show tan skin, dark hair/eye areas and clothing; these are not magenta missing-material indicators. Front/facial texture detail is **NOT VERIFIED** because the committed cameras view the characters from the rear.
- Static JSON alone was not used to claim visual correctness; the screenshot evidence is in §12.

## 6. Skeleton and bone validation (static)

| | Male | Female |
| --- | ---: | ---: |
| glTF nodes | 69 | 69 |
| skins | 1 | 1 |
| skin joints | 65 | 65 |
| unique joint names | 65 | 65 |
| connected-to-root joints | 65 | 65 |
| canonical required humanoid bones mapped | 22 | 22 |
| meshes | 3 | 3 |
| vertices | 8,483 | 8,844 |
| triangles | 14,318 | 15,060 |
| embedded animations | 0 | 0 |

`JOINTS_0` / `WEIGHTS_0` accessors cover every vertex, joint indices are within the 65-joint skin, and each vertex's four weights sum to approximately 1.0. Bone names are the Unreal-style set (`root`, `pelvis`, `spine_01…03`, `clavicle_l/r`, `upperarm_l/r`, …, `ball_l/r`), identical to the existing Male/Female character FBX skeletons.

## 7. Humanoid Avatar compatibility
**UNITY AVATAR VALIDITY: PASS for both characters in licensed Unity 6.** `GLTFImportTestValidator` built a `HumanDescription` from the glTFast-imported hierarchy using canonical `HumanTrait.BoneName` strings, explicit parent names and hierarchy flags, then called `AvatarBuilder.BuildHumanAvatar`.

| Sex | Editor result | Detail |
| --- | --- | --- |
| Male | PASS | `Avatar.isValid && Avatar.isHuman`; **37 human bones mapped from 69 transforms**. |
| Female | PASS | `Avatar.isValid && Avatar.isHuman`; **37 human bones mapped from 69 transforms**. |

An earlier experimental run produced `isValid=False, isHuman=False` because the custom `HumanDescription` used spaced human names and left internal hierarchy/parent fields unset. That was a validator construction defect, not a forced production rig change; it was corrected before the final licensed run. Production character FBX Avatars remain separate and were re-checked as `isValid=True, isHuman=True` with 52/55 bones mapped in the project validator.

## 8. Existing FBX animation compatibility
- Existing production character FBXs use the same 65 Unreal-style bone names as the GLTF skeletons.
- Existing production animation FBXs use Mixamo `mixamorig:*` names. All 74 have 65 bones and the canonical required humanoid aliases.
- Similar humanoid anatomy is **not** treated as proof of retargeting.
- Licensed Editor validator sampled `Assets/Animations/Movement/idle.fbx` through each valid test Avatar with `AnimationMode.SampleAnimationClip`. Result for both Male and Female: **NOT VERIFIED** — "clip sampled but spine rotation did not change; retargeting result needs manual visual confirmation". The idle clip may contain no measurable spine motion, or the custom Avatar muscle/rest-pose limits may need tuning. No retargeting compatibility claim is made.
- No existing animation FBX, `.fbx.meta`, Animator Controller, Avatar, gameplay prefab or production reference was modified.

## 9. Licence findings

Both licence files are byte-identical and state **CC0 1.0 Universal / Public Domain Dedication**, crediting `@Quaternius`.

| Question | Finding |
| --- | --- |
| Commercial game usage | Permitted by CC0 |
| Modification and conversion | Permitted |
| Redistribution inside a game | Permitted |
| Android APK distribution | Permitted |
| Required attribution | **No legal attribution requirement**; the licence suggests supporting `@Quaternius` on Patreon and lists `https://quaternius.com` and a Discord link |
| Other restrictions | No copyright/related-right conditions; no warranty; patent/trademark rights are not waived. The source repository has no top-level `LICENSE` file, so this finding applies only to the two licensed Character folders. This is not legal advice. |

## 10. Android optimization observations (no source changes)
- 2048² PNGs dominate texture memory. If all 7 referenced PNGs were imported uncompressed RGBA with mipmaps, the static estimate is roughly **107–128 MiB per character** before compression, depending on whether duplicate source PNGs are counted. Existing project texture rules favour 512² ASTC on Android, but glTFast ScriptedImporter/runtime material behaviour was not measured in an APK.
- The source also contains unused duplicate `*_BaseColor_png.png` files. They are preserved as part of the exact source set but should not be included in a runtime build.
- Static geometry: Male **8,483 vertices / 14,318 triangles**; Female **8,844 vertices / 15,060 triangles**; **65 skin joints**; **3 meshes / 3 materials** each. Licensed Editor confirmed **3 renderers / 3 SkinnedMeshRenderers** per character, implying at least 3 material/renderer submissions; exact draw calls and GPU memory are **NOT VERIFIED**.
- Editor bounds: Male `(1.860, 1.916, 0.441)` m, Female `(1.670, 1.854, 0.443)` m; both stand near y=0 after floor alignment. Pivot/floor alignment is PASS for the isolated scene.
- glTFast runtime materials use URP shader variants; missing variants can produce magenta materials only in player builds. No APK was produced, so shader-variant behaviour in an Android player is **NOT VERIFIED**.
- No texture quality reduction, mesh simplification, animation removal or runtime allocation change was made. No real-device FPS, thermal, memory or startup measurement was performed.

## 11. CI / test results
**Local static sweep:** `validate_gltf_import_test.py` 33 PASS / 0 FAIL / 4 NOT VERIFIED; `processing/unity/tests` 24/24; `validate_structure.py` 17/0; `validate_csharp.py` 36/0 (tree-sitter, not a Unity compile); existing character/animation validator 20/0 with 7 Editor-only SKIP / 6 INFO; Blender 13/13; metadata sync 26/26. `docs/gltf_import_test/STATIC_VALIDATION.md` and `static_validation.json` are committed.

**Main-branch note:** `main` deleted four `Assets/UserAssets` placeholder/demo files after this branch was created. The PR merge inherits those deletions; they are not caused by the GLTF experiment and are excluded from the protected-asset hash comparison. `validate_structure.py` now treats only those recorded main deletions as allowed missing directories.

**Licensed Unity 6 GitHub Actions:** no unlicensed Unity runner was used.

| Run / head | Result |
| --- | --- |
| Push static `44464393619` and PR static `44464399653` at `2e6bc0d` | PASS. Exact source, URI, GUID, skeleton, licence and protected-asset checks passed. |
| Push Unity `44464393544` at `2e6bc0d`, job `135358732703` | Licensed compile, project configure, character/animation integration, isolated scene builder, **GLTF Editor validation PASS**, EditMode 68 passed / 1 skipped, PlayMode 14 passed. ProjectValidator then failed on 3 pre-existing errors; APK step skipped. |
| PR Unity `44464399662` at `2e6bc0d`, job `135358686670` | GLTF Editor validation, EditMode and PlayMode all succeeded; ProjectValidator failed on the same 3 pre-existing errors and APK/capture steps were skipped. Independent data job failed on the LFS pointer. GLTF-specific Editor validation was not the cause. |
| `workflow_dispatch` `44465569128` at `a6ef753`, job `135362606225` | Licensed GLTF validation PASS (0 FAIL, 2 NOT VERIFIED), EditMode 68/1 skipped, PlayMode 14/0, **visual capture PASS**, screenshots/scene committed as `41ec45e`. APK build FAILED before output. |

### Final PASS / FAIL / NOT VERIFIED matrix

| Area | Status | Evidence / exact limitation |
| --- | --- | --- |
| Exact 23 source files, hashes and licences | PASS | §1, source manifest, static validator |
| URI defects corrected only in separate test copies | PASS | §4, static validator |
| Unity 6-compatible importer pinned, no unnecessary packages | PASS | `com.unity.cloud.gltfast 6.21.0`; only built-in UnityWebRequest modules added |
| Isolated scene, no gameplay link, no missing scripts | PASS | Editor validator: cameras=3, lights=2, loaders=2, floors=1; missing scripts=0 |
| GLTF load and visible mesh/body parts | PASS | licensed Editor prefab import + 3 renderers each + screenshots |
| Materials / textures / no pink / missing texture properties | PASS | Editor shader/texture check PASS; 0 magenta pixels in PNGs |
| UVs, skeleton hierarchy, skin weights, bone validity | PASS | static glTF analysis + Editor UV/skin-bone checks; 65 joints, 37 mapped human bones |
| Scale / orientation / pivot / floor | PASS | Editor bounds and capture `minY=0.000`; screenshots upright on floor |
| Humanoid Avatar `isValid && isHuman` | PASS | Male/Female 37 human bones from 69 transforms |
| Existing animation FBX compatibility by Avatar mapping + retargeting | **NOT VERIFIED** | idle clip sampling produced no measurable spine rotation; needs visual confirmation |
| Deformation under animation | **NOT VERIFIED** | no embedded animation; retargeting not proven |
| Genuine visual screenshots | PASS | three Unity `Camera.Render` PNGs under Xvfb/llvmpipe; rear views only |
| Licence commercial/modification/redistribution/attribution | PASS with caveats | CC0 1.0; no top-level source LICENSE; not legal advice |
| Android/performance metrics | PARTIAL / **NOT VERIFIED** | static dimensions, formats, verts/tris, bones, materials, renderers reported; no APK/device measurement |
| Android APK build | **FAIL** | `AndroidBuild.ConfigureValidateAndBuildApk` refused to build because `ProjectValidator.ValidateAll` returned 3 failures |
| Android device runtime / FPS / memory | **NOT VERIFIED** | no APK, no physical device |
| Existing production FBX/Animator/controller/gameplay references preserved | PASS | protected-asset hash comparison and project validator FBX checks |
| Static CI, C# compile, EditMode, PlayMode | PASS | runs above; no unlicensed Editor runner |
| Independent Unity island data integrity job | FAIL | pre-existing `water_normal_256.png` Git LFS pointer; not caused by GLTF test |

**Exact ProjectValidator failures (pre-existing, not GLTF):**
1. `water normal map present — missing Assets/Resources/Art/Textures/water_normal_256.png`
2. `game logo texture present — missing Assets/Resources/UI/battle_horizons_logo.png`
3. `application icon configured — no default application icon assigned`

The two PNG paths are Git LFS pointer files in this checkout, so Unity cannot import them as `Texture2D`. `AndroidBuild.ConfigureValidateAndBuildApk` calls `ProjectValidator.ValidateAll` and exits with `[bh][build] refusing to build: 3 project validation failure(s)` before `BuildPipeline.BuildPlayer`; therefore no APK was produced and no device claim is made.

## 12. Visual screenshots / explicit NOT VERIFIED status
**PASS — genuine Unity Editor camera renders, not import-success messages.** `workflow_dispatch` run `44465569128` executed `GLTFImportTestCapture.CaptureFromCommandLine` under Xvfb with `-force-opengl` and software GL. The graphics device was `OpenGLCore / llvmpipe (LLVM 20.1.2, 256 bits)`.

| File | Observation |
| --- | --- |
| `docs/gltf_import_test/screenshots/male_female_side_by_side.png` | Both complete Male/Female bodies visible in T-pose, upright, feet on the neutral floor. Cooler blue/white specular/normal highlights are visible in this wider comparison view; no magenta/pink. |
| `docs/gltf_import_test/screenshots/male_character.png` | Full male rear view: head, torso, both arms/hands, legs/feet and dark shorts visible; tan skin, no obvious missing body part, no magenta. A cropped arm from the other character appears at the right edge because the individual camera shares the comparison environment. |
| `docs/gltf_import_test/screenshots/female_character.png` | Full female rear view: head, torso, both arms/hands, legs/feet and dark clothing visible; tan skin, no obvious missing body part, no magenta. A cropped arm from the other character appears at the left edge. |

`CAPTURE_REPORT.md` records Male bounds `(1.860, 1.916, 0.441)` with `minY=0.000` and Female bounds `(1.670, 1.854, 0.443)` with `minY=0.000`. A pixel scan found **0 magenta pixels** in all three PNGs.

**Explicitly NOT VERIFIED:** front/facial appearance, close-up UV seam quality, animation deformation, shader variants in an Android player, and any real-device visual result. The screenshots are Editor-only rear views under software GL; they do not prove Android runtime correctness.

## 13. Errors and exact causes
- **Known source URI mismatch — Male:** `T_Hair_1_Normal_png.png` and `T_Eye_Normal_png.png` are referenced but absent. Corrected only in the separate test-copy JSON.
- **Known source URI mismatch — Female:** `T_Eye_Normal_png.png` is referenced but absent. Corrected only in the separate test-copy JSON.
- **Earlier AvatarBuilder false negative (fixed):** at `290105b`, both test Avatars were `isValid=False, isHuman=False` with 37 mapped bones. Cause: the experimental `HumanDescription` used spaced human names (`Left Upper Arm`) instead of canonical `HumanTrait.BoneName` values (`LeftUpperArm`) and left internal `parentName` / `m_SkeletonHasParents` / `m_GlobalScale` / `m_RootMotionBoneName` fields unset. Fixed in `b364ec7`/`2e6bc0d`; final licensed run reports valid Humanoid Avatars for both.
- **Retargeting NOT VERIFIED:** valid Avatar plus `idle.fbx` sampling did not change the measured spine rotation. This is a measurement limitation / possible rest-pose or muscle-limit mismatch, not a proven incompatibility and not a proven success.
- **ProjectValidator FAIL (pre-existing):** `water_normal_256.png`, `battle_horizons_logo.png` and the default application icon. The PNGs are LFS pointers in the CI checkout; the icon is not assigned in ProjectSettings. These failures are inherited from the production project/main branch, not introduced by the isolated GLTF test.
- **APK build FAIL:** `AndroidBuild.ConfigureValidateAndBuildApk` refuses to call `BuildPipeline.BuildPlayer` while `ProjectValidator.ValidateAll` reports failures. It exited before producing `build/battle-horizons.apk`. No Gradle/Android packaging error was reached.
- **Unity island data integrity FAIL (independent):** `validate_unity_map.py` reports the `water_normal_256.png` LFS pointer and manifest hash mismatch. It is a separate pre-existing data job and does not invalidate the licensed GLTF Editor results.
- **Generated scene GUID false positive (fixed):** the licensed scene builder uses `GameObject.CreatePrimitive` for the neutral floor, which serializes URP's built-in default material GUID `31321ba15b8f8eb4c954353edc038b1d`. The committed-reference validator initially treated it as a missing project asset and skipped the final-head licensed Unity job. `validate_character_animation.py` now recognises that GUID as a Unity built-in; no material asset was created or committed.
- **Static structure check adjustment:** PR merge inherits four `Assets/UserAssets` deletions from `main`. `validate_structure.py` now allows only those recorded deleted directories to be absent; no deleted file was re-added.
- No production FBX, `.fbx.meta`, GUID, Avatar, Animator Controller, gameplay prefab, scene reference or gameplay script was modified by this experiment.

## 14. Recommendation
**KEEP EXISTING FBX and wait for explicit approval.**

The corrected GLTF test copies now **PASS** isolated Unity 6 import, mesh/body-part visibility, material/texture property checks, UV/skin-bone checks, bounds/floor alignment, genuine Editor screenshots and Humanoid Avatar validity for both Male and Female. However, existing-animation retargeting and animation deformation are **NOT VERIFIED**, the Android APK build is **FAIL** because of pre-existing production ProjectValidator errors, and no Android device/runtime/performance test exists. Licence terms are permissive but the source repository has no top-level LICENSE file.

Suggested fixes before any production consideration:
1. Restore/commit real `water_normal_256.png` and `battle_horizons_logo.png` assets instead of LFS pointers and assign a default application icon, then re-run the licensed Android build.
2. Re-test retargeting with a clip known to move the spine, verify the Humanoid rest pose/T-pose and muscle limits, and capture animated front/rear screenshots.
3. Build an APK containing the isolated test scene, inspect shader variants for magenta, and profile texture memory, draw calls, FPS and thermals on a real Android device.
4. Only after those checks and explicit approval should any production character replacement be discussed.

Do not merge PR #26, do not replace production player assets, and do not change the gameplay architecture automatically.

---

**PR:** https://github.com/adii14990adi-spec/Battle-royal-indian-edition-/pull/26 (existing open PR on the fixed Arena branch)  
**Final commit hash:** see PR #26 head after this report commit; evidence commits include `2e6bc0d`, `a6ef753` and `41ec45e`.  
**Merge status:** not merged; explicit approval required.
