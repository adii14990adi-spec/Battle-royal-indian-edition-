# Unity 6 GLTF Player Import Test Report

**Status:** isolated experiment only. **Recommendation: KEEP EXISTING FBX.** No production player asset was replaced and no automatic architecture change was made.

**Game repository:** `adii14990adi-spec/Battle-royal-indian-edition-`  
**Character source:** `adii14990adi-spec/Animation-.meta-`, branch `main`, commit `b3872f9276461dc3ecffed9e15731e98ed867d86`  
**Inspected source URL:** https://github.com/adii14990adi-spec/Animation-.meta-/tree/main/Character  
**Engine / target:** Unity `6000.0.85f2`, URP, Android  
**Report date:** 2026-11-18

> Verification boundary: this file initially records static-only results and explicitly marks Editor/visual/device checks as NOT VERIFIED. Licensed Unity CI and visual-capture results are appended after the workflow run. A successful APK build is never treated as proof that a character renders correctly on a real Android device.

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
- `GLTF_Player_Import_Test.unity`: isolated test scene. The committed placeholder is replaced by a valid Unity-generated scene in licensed CI via `GLTFImportTestBuilder.BuildSceneFromCommandLine`.
- `Runtime/` and `Editor/`: isolated loader, environment builder, Editor validator, scene builder and screenshot capture scripts. No gameplay script, prefab, controller, FBX or `.fbx.meta` was modified.

## 3. Male / Female import result (static + pending Editor)

| Sex | Original direct import | Corrected Unity test copy | Status |
| --- | --- | --- | --- |
| Male | FAIL for direct use: glTF references `T_Hair_1_Normal_png.png` and `T_Eye_Normal_png.png`, which are absent. Original files preserved. | PASS static URI check: all BIN/PNG URIs resolve; corrected JSON is separate. | PASS (test copy) / known source URI defect documented |
| Female | FAIL for direct use: glTF references `T_Eye_Normal_png.png`, which is absent. Original files preserved. | PASS static URI check: all BIN/PNG URIs resolve; corrected JSON is separate. | PASS (test copy) / known source URI defect documented |

Unity Editor import, mesh rendering, material/shader correctness and Avatar validity are **NOT VERIFIED** in this static section; see §11–§12.

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
- No pink/missing-material claim is made from static JSON; Editor shader/texture validation and screenshots are required.

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

**STATIC COMPATIBILITY: PASS candidate. UNITY AVATAR VALIDITY: NOT VERIFIED.** Both GLTF skeletons contain all 22 canonical humanoid bones checked by the project's rig conventions, with a connected hierarchy. `GLTFImportTestValidator` builds a `HumanDescription` from the imported hierarchy in licensed Unity CI and checks `Avatar.isValid && Avatar.isHuman` independently for Male and Female. No incompatible rig is forced into Humanoid mode.

## 8. Existing FBX animation compatibility

- Existing production character FBXs use the same 65 Unreal-style bone names as the GLTF skeletons.
- Existing production animation FBXs use Mixamo `mixamorig:*` names. All 74 have 65 bones and the canonical required humanoid aliases.
- Similar humanoid anatomy is **not** treated as proof of retargeting. Avatar mapping and retargeted clip sampling are separate Editor checks, and no existing animation FBX, `.fbx.meta`, Animator Controller, Avatar or gameplay prefab is modified.

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

- 2048² PNGs dominate texture memory. If all 7 referenced PNGs were imported uncompressed RGBA with mipmaps, the static estimate is roughly **128 MiB per character** before compression. Existing project texture rules favour 512² ASTC on Android, but glTFast runtime imports may not use `AssetImportRules`; this needs an APK/device check.
- The source also contains unused duplicate `*_BaseColor_png.png` files. They are preserved as part of the exact source set but should not be included in a runtime build.
- 3 meshes / 3 materials per character implies at least 3 renderers/material slots and likely 3+ draw calls per visible character. Exact draw calls are **NOT VERIFIED**.
- 65 bones and 8–15k vertices are modest for one or two test characters but should be profiled on a real device with the normal gameplay scene.
- glTFast runtime materials use URP shader variants; missing variants can produce magenta materials only in player builds. This isolated scene is not added to production build settings.
- No texture quality reduction, mesh simplification, animation removal or runtime allocation change was made.

## 11. CI / test results

**Local static sweep before push (2026-11-18):** `validate_gltf_import_test.py` 33 PASS / 0 FAIL / 4 NOT VERIFIED; `processing/unity/tests` 23/23; `validate_structure.py` 17/0; `validate_csharp.py` 36/0 (tree-sitter, not a Unity compile); existing character/animation validator 20/0 with 7 Editor-only SKIP / 6 INFO; Blender 13/13; metadata sync 26/26. `validate_unity_map.py` remains a separate pre-existing LFS-pointer issue and is not part of this isolated test.

**Licensed Unity 6 GitHub Actions:** pending. Editor import, scene validation, missing-reference checks, Avatar validity, EditMode/PlayMode tests, visual capture and optional Android APK build are reported after the workflow completes. No unlicensed Unity runner is used. Static-only and Editor-only workflows are separate.

## 12. Visual screenshots / explicit NOT VERIFIED status

**NOT VERIFIED at initial static stage.** No Unity Editor screenshot or human visual inspection is claimed. A separate graphics-enabled Xvfb step uses `GLTFImportTestCapture.CaptureFromCommandLine` to render the imported prefabs to PNGs. If that step succeeds and the images are inspected, this section is updated with the exact screenshot paths and observations. If it fails, it remains NOT VERIFIED with the exact error.

## 13. Errors and exact causes

- **Known source URI mismatch — Male:** `T_Hair_1_Normal_png.png` and `T_Eye_Normal_png.png` are referenced but absent. Corrected only in the separate test-copy JSON.
- **Known source URI mismatch — Female:** `T_Eye_Normal_png.png` is referenced but absent. Corrected only in the separate test-copy JSON.
- No other static errors are known at this stage. Any Unity import/compile/scene/capture errors will be appended with exact log causes.

## 14. Recommendation

**KEEP EXISTING FBX.** The corrected GLTF test copies are valid isolated preview candidates, but this task is a trial import. Visual playback, Avatar/retargeting quality, Android runtime memory/FPS and long-term asset-pipeline impact remain to be verified. Wait for explicit approval before considering any production replacement. Do not merge or replace production character assets automatically.

---

**PR:** https://github.com/adii14990adi-spec/Battle-royal-indian-edition-/pull/26 (existing open PR on the fixed Arena branch)  
**Final commit hash:** see the PR head / final answer after all CI and report updates.  
**Merge status:** not merged; explicit approval required.
