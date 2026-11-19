# Static validation — isolated GLTF import test

Static only: no Unity Editor, no glTF import, no rendering, no APK and no device measurement was performed by this validator.

| Check | Status | Detail |
| --- | --- | --- |
| verification boundary | PASS | This Python validator is static-only and never claims Unity import, rendering, Avatar validity, APK build or device runtime. |
| source inventory manifest | PASS | 23 exact source files recorded from live main @ b3872f9276461dc3ecffed9e15731e98ed867d86 |
| original source files preserved byte-for-byte | PASS | 23/23 original files, names, sizes and SHA-256 values match the live source repository |
| Male test-copy companions byte-identical | PASS | all BIN/PNG/licence/.gitkeep companions copied with original names |
| Male original URI mismatches (documented) | INFO | 2 known unresolved PNG URI(s): T_Eye_Normal_png.png, T_Hair_1_Normal_png.png. Original glTF is preserved unchanged. |
| Male corrected test-copy URIs resolve case-sensitively | PASS | 8 buffer/image URIs resolve in UnityTestCopy; known mismatches corrected only in the test copy |
| Male URI fix manifest matches code | PASS | 2 documented URI corrections |
| Male original glTF not modified | PASS | original glTF SHA-256 matches live source |
| Female test-copy companions byte-identical | PASS | all BIN/PNG/licence/.gitkeep companions copied with original names |
| Female original URI mismatches (documented) | INFO | 1 known unresolved PNG URI(s): T_Eye_Normal_png.png. Original glTF is preserved unchanged. |
| Female corrected test-copy URIs resolve case-sensitively | PASS | 8 buffer/image URIs resolve in UnityTestCopy; known mismatches corrected only in the test copy |
| Female URI fix manifest matches code | PASS | 1 documented URI corrections |
| Female original glTF not modified | PASS | original glTF SHA-256 matches live source |
| licence files | PASS | both licence files are byte-identical CC0 1.0 Universal texts crediting @Quaternius |
| licence findings | INFO | CC0 permits commercial use, modification/conversion, redistribution inside a game and Android APK distribution. Attribution is suggested, not required. No warranty; patent/trademark rights are not waived. Source repo has no top-level LICENSE file. |
| Male glTF structure | PASS | glTF 2.0, 69 nodes, 3 meshes, 3 materials, 1 skin, no embedded animations |
| Male skeleton / required humanoid bones | PASS | 65 unique skin joints, all connected to the root, 22 canonical humanoid bones mapped |
| Male UV / skin weights | PASS | JOINTS_0/WEIGHTS_0 exist for every vertex; all weights sum to ~1 and joint indices are in range |
| Male mesh counts | PASS | 8483 vertices, 14318 triangles (3 meshes) |
| Male PNG dimensions / formats | PASS | 7 PNG image URIs resolve; eye textures 256², hair/body textures 2048²; no texture downscaled |
| Male Android texture memory (static estimate) | INFO | 7 source PNGs; uncompressed RGBA + mipmaps ≈ 107.3 MiB if all originals are imported at native 2048². ASTC/512 recommendations are in the final report; no source optimization was applied. |
| Female glTF structure | PASS | glTF 2.0, 69 nodes, 3 meshes, 3 materials, 1 skin, no embedded animations |
| Female skeleton / required humanoid bones | PASS | 65 unique skin joints, all connected to the root, 22 canonical humanoid bones mapped |
| Female UV / skin weights | PASS | JOINTS_0/WEIGHTS_0 exist for every vertex; all weights sum to ~1 and joint indices are in range |
| Female mesh counts | PASS | 8844 vertices, 15060 triangles (3 meshes) |
| Female PNG dimensions / formats | PASS | 7 PNG image URIs resolve; eye textures 256², hair/body textures 2048²; no texture downscaled |
| Female Android texture memory (static estimate) | INFO | 7 source PNGs; uncompressed RGBA + mipmaps ≈ 107.3 MiB if all originals are imported at native 2048². ASTC/512 recommendations are in the final report; no source optimization was applied. |
| production assets unchanged | PASS | 250 protected Unity Assets/ProjectSettings files are byte-identical; only the two allowlisted test asmdefs were updated to reference new test assemblies |
| no new or moved FBX files | PASS | all 76 FBX files are the original protected set |
| committed GUID integrity | PASS | 91 unique, well-formed GUIDs across 91 committed .meta files |
| no production controllers/prefabs/scenes added | PASS | no committed production controller, prefab or scene was added or replaced |
| Unity 6-compatible glTF importer pinned | PASS | com.unity.cloud.gltfast 6.21.0 was already pinned; package.json declares unity 6000.0 |
| PNG runtime modules enabled | PASS | UnityWebRequest, UnityWebRequestTexture and ImageConversion built-in modules are enabled for glTFast PNG loading |
| isolated scene exists | PASS | GLTF_Player_Import_Test.unity and its stable .meta GUID are committed; licensed CI replaces the placeholder with Unity-generated YAML |
| scene has no gameplay system references | PASS | scene contains no production player/movement/network/shooting type names |
| GLTF skeleton matches production character FBX bone names | PASS | both GLTF characters and both production character FBXs use the same 65 Unreal-style bone names |
| existing animation FBX skeleton conventions | PASS | 74/74 existing Mixamo animation FBXs have 65 bones and all required canonical humanoid aliases |
| existing animation FBX compatibility | INFO | Production animations use mixamorig:* names, while GLTF and character FBX use Unreal-style names. Unity Humanoid Avatar mapping must bridge the two namespaces; static name equality is NOT claimed. Avatar and retargeting are tested separately in licensed Unity Editor CI. |
| Unity Editor glTF import / Avatar validity | NOT VERIFIED | requires licensed Unity Editor; run GLTFImportTestValidator.ValidateFromCommandLine in unity.yml |
| visual appearance / material / UV screenshots | NOT VERIFIED | requires genuine Unity Editor camera renders or human visual inspection |
| animation retargeting playback | NOT VERIFIED | requires a valid Humanoid Avatar and an Editor/PlayMode sample, not just matching bone names |
| Android build / device runtime | NOT VERIFIED | APK build and FPS/PSS/thermal/touch measurements require a licensed Editor build and a physical device |
| licence caveat | INFO | CC0 finding applies to the two Character/Male and Character/Female folders. The source repository has no top-level LICENSE file; do not assume unrelated files are CC0. This is a technical report, not legal advice. |
| final report present | PASS | docs/GLTF_PLAYER_IMPORT_TEST_REPORT.md exists |

## Summary

- PASS: 33
- FAIL: 0
- NOT VERIFIED: 4

Editor import, Avatar validity, retargeting playback, visual screenshots and Android runtime behaviour are checked separately in licensed Unity CI and/or marked NOT VERIFIED.
