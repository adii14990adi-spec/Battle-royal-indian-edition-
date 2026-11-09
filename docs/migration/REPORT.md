# Migration report — Godot 4.4 → Unity 6 (URP)

**Project:** Battle Horizons: Last Survival
**Repository:** `adii14990adi-spec/Battle-royal-indian-edition-`
**Migration branch:** `arena/f2d42ca9-battle-royal-indian-edition` (from `main` @ `d93598d`)
**Source engine:** Godot 4.4, GL Compatibility, Android
**Target engine:** Unity 6 `6000.0.32f1`, URP 17, IL2CPP/ARM64, Android
**Date of this report:** 2026-11-09

---

## 0. Read this first — what this report can and cannot claim

The migration was executed in an environment with **no Unity editor, no Godot
binary, no Blender and no Android device**. The consequence is very specific and
is not hidden anywhere below:

| Verified here (executable) | Not verified here (needs the tools) |
| --- | --- |
| The converted island is byte-identical in meaning to the Godot bake (11 checks, sample-level) | That the Unity project **opens and compiles** |
| All 29 C# files parse; namespaces, serialized-field references, asmdefs and project settings are consistent (30 static checks) | That the editor tooling produces the scenes/materials/URP asset |
| The Godot project is untouched and still passes its own gates | That the game runs, at what frame rate, on a device |
| Blender pipeline unit tests (13/13) | The asset validator returned SKIP (no output manifest); generators have not run under real Blender |
| The Godot CI workflow is intact; the new Unity workflow is valid YAML | That the Unity CI job builds an APK (needs a licence secret) |

The vocabulary used throughout is the one the brief asked for, and it means
exactly this:

| Status | Meaning |
| --- | --- |
| **Implemented** | Code/config written and reviewed; may still be untested at runtime |
| **Tested** | Executed here (or in CI) and observed to pass, with the command shown |
| **Partially tested** | Some paths executed; others cannot be in this environment |
| **Not tested** | Written, but never executed — no tool available |
| **Blocked** | Cannot proceed without something outside this environment (Unity licence, device, Blender, source content that does not exist) |

No item below is marked Tested because it "should" work.

---

## 1. Deliverable checklist (the ten required items)

| # | Deliverable | Status |
| --- | --- | --- |
| 1 | Separate Unity 6 URP project (`unity-project/`) | **Implemented**, **Partially tested** (settings verified statically, editor never opened) |
| 2 | Godot project preserved unchanged | **Tested** — `validate_structure.py` asserts 23 Godot files intact + its CI workflow unchanged; no Godot file is modified by any commit on this branch |
| 3 | Reusable Blender automation pipeline | **Implemented**, **Partially tested** (13 unit tests pass; the asset validator reports **SKIP** because no manifest exists; generators never ran under Blender → **Blocked** on a Blender install) |
| 4 | Imported and validated character assets | **Blocked** — no character exists in the source project; a rig *harness* generator + validator exist, but no real character was imported |
| 5 | Siran Island migration foundation | **Implemented**, **Tested** at the data level (11/11 checks against the real bake); runtime build **Not tested** |
| 6 | Android configuration | **Implemented**, statically **Tested**; APK **Not tested** |
| 7 | GitHub Actions Unity workflow | **Implemented**; YAML valid, logic reviewed; **Not tested** (cannot run Actions from here) — the Unity job correctly **Blocked** on a licence secret |
| 8 | Automated tests | **Implemented**; Python validation **Tested** (13 unit tests + 11 map checks + 30 C# checks + 27 structure checks); Unity EditMode/PlayMode suites **Not tested** |
| 9 | Technical documentation | **Implemented** (8 documents, listed in §11) |
| 10 | Migration report | This document |

---

## 2. Files created

### 2.1 Unity project — `unity-project/` (57 files, 2.0 MB)

**Runtime C# (22 files, 4,092 lines)**

| File | Lines | Purpose |
| --- | --- | --- |
| `Assets/Scripts/Core/{BattleHorizons.Runtime.asmdef, GameBootstrap, QualityProfile, GameLog, PerformanceMonitor, ObjectPool}.cs` | 630 | boot, quality detection/application, logging, frame-time + set-pass monitoring, pooling |
| `Assets/Scripts/World/{IslandShapeMath, SiranIslandData, SiranIslandTerrain, SiranIslandWater, SiranChunkStreamer, SiranWorldBuilder, SiranMapConfig}.cs` | 2,062 | shape maths, blob loading, Terrain + heightmap collider, river, pooled chunk ring, world assembly, generated layout constants |
| `Assets/Scripts/Player/{ThirdPersonMotor, PlayerInputRouter, VirtualJoystick}.cs` | 582 | movement, unified touch/pad/keyboard input, joystick widget |
| `Assets/Scripts/Camera/ThirdPersonCameraController.cs` | 141 | orbit camera with collision pull-in |
| `Assets/Scripts/UI/{MainMenuController, MobileControlsBuilder, HoldButton, TouchControlsHost, TouchLookArea, WorldDebugHud}.cs` | 677 | menu, runtime-built touch UI (two sticks, jump, held crouch/sprint), look area, F3 HUD |

**Editor C# (4 files, 1,467 lines)**

| File | Lines | Purpose |
| --- | --- | --- |
| `Assets/Editor/ProjectBootstrap.cs` | 551 | one-command setup: URP asset, Android player settings, 4 scenes, 4 materials, build settings; CLI entry points |
| `Assets/Editor/ProjectValidator.cs` | 586 | PASS/FAIL/SKIP validation report across 10 areas |
| `Assets/Editor/AssetImportRules.cs` | 122 | `AssetPostprocessor`: texture/ASTC/mip, audio, model scale, humanoid, 4 bone influences |
| `Assets/Editor/AndroidBuild.cs` | 208 | batch-mode APK/AAB with honest result reporting and env-only signing |

**Tests (3 files, 565 lines)** — `Assets/Tests/EditMode/{SiranIslandDataTests, QualityAndShapeTests}.cs`, `Assets/Tests/PlayMode/WorldAndPlayerPlayModeTests.cs`, plus 3 asmdefs.

**Project configuration** — `Packages/manifest.json` (URP 17.0.3, Input System 1.11.2, uGUI, Test Framework, Android Logcat); `ProjectSettings/{ProjectVersion, ProjectSettings, QualitySettings, GraphicsSettings, TagManager, TimeManager, PhysicsManager, AudioManager, EditorSettings, EditorBuildSettings}.asset`; `README.md`.

**Map data** — `Assets/Resources/Config/`: `siran_island_heights.bytes` (643,204 B), `siran_island_colors.bytes` (643,204 B), `siran_island_roads.bytes` (115,932 B), `siran_island_river.bytes` (8,392 B), `siran_island_layout.json` (8,193 B), `siran_island_terrain.json` (5,074 B), `siran_island_bake_manifest.json`, four `.bytes.meta`; `Assets/Resources/Art/Textures/water_normal_256.png` (copied verbatim, hash-identical to the Godot original).

### 2.2 Conversion and validation tooling — `processing/` (11 Python files, 3,982 lines)

| File | Purpose |
| --- | --- |
| `unity/godot_bake_reader.py` | parses the baked `.tscn`: `ArrayMesh` wire format, 20-byte interleaved stride, packed normals, `HeightMapShape3D`, chunk transforms. `verify_scene()` → **0 issues** |
| `unity/port_siran_config.py` | GDScript layout → `SiranMapConfig.cs` + `siran_island_layout.json`; `--check` drift gate |
| `unity/convert_godot_island.py` | bake → heights/colours/roads/river blobs + hash manifest; `--check` drift gate |
| `unity/validate_unity_map.py` | proves the Unity data equals the bake, sample for sample (11 checks) |
| `unity/validate_csharp.py` | no-Unity static validation of the Unity project (30 checks) |
| `unity/validate_structure.py` | Godot preserved + Unity layout + docs + no secrets (27 checks) |
| `blender/scripts/bh_blender_common.py` | shared Blender helpers; imports cleanly without `bpy` |
| `blender/generators/generate_environment_kit.py` | 9 modular pieces + PBR + box collision + manifest |
| `blender/generators/generate_characters.py` | 2 × 20-bone humanoid rig harness, FBX + sidecar |
| `blender/validators/validate_assets.py` | GLB/FBX manifest validation without Blender |
| `blender/tests/test_pipeline_tools.py` | 13 unittest cases |

### 2.3 CI and docs

* `.github/workflows/unity.yml` — 4 jobs: `structure`, `data`, `assets`, `unity` (licence-gated).
* `docs/migration/ASSET_AUDIT.md`, `docs/migration/BAKE_FORMAT.md`, `docs/migration/REPORT.md`
* `docs/architecture/UNITY_ARCHITECTURE.md`
* `docs/performance/ANDROID_PROFILES.md`
* `docs/asset-pipeline/BLENDER_PIPELINE.md`

---

## 3. Files modified

| File | Change | Risk |
| --- | --- | --- |
| `.gitignore` | appended Unity (`Library/`, `Temp/`, `Logs/`, `Builds/`, `.csproj`, keystores, APKs), Blender `output/`, `__pycache__` | none — additive, no existing rule removed |

**That is the complete list.** No Godot source file, scene, script, shader, resource, export preset, tool or existing CI file was modified. This is asserted by
`processing/unity/validate_structure.py` (23 required Godot paths, project.godot
values, 64 chunk nodes in the baked scene, and the Godot workflow's export/publish
steps) and by `git diff --stat d93598d..HEAD` touching only `unity-project/`,
`processing/`, `docs/`, `.github/workflows/unity.yml` and `.gitignore`.

---

## 4. Assets reused vs assets needing conversion

### Reused as-is (no conversion needed)

| Asset | How |
| --- | --- |
| **Siran Island terrain** (64 chunks, 320 k tris, 166,464 verts) | heights + colours extracted from the bake → the same surface in Unity (8 µm worst error) |
| **Island zone design** (5 zones, lake, moat, river, 5 roads) | constants ported; verified element by element against the brief |
| **18 road ribbons** (4,212 tris) | indexed verbatim into `BHROAD01` |
| **River surface** | regenerated from the same control points (the baked `.res` is binary) |
| **Greybox vertex colours** | RGBA8 per sample, used as URP vertex colours |
| `assets/textures/water_normal_256.png` | copied verbatim (same SHA-256) |
| **30 Hz physics, Low 30 fps / High 60 fps caps, landscape, ETC2/ASTC, no post; Low ≤ 2 / High ≤ 4 lights** | ported as Unity settings/runtime profile overrides |

### Needs conversion (and what was done)

| Asset | Issue | Resolution |
| --- | --- | --- |
| Baked terrain **normals** | garbage in the source | rebuilt from the height field |
| `siran_river_surface.res` | binary `RSRC`, not readable as text | ribbon regenerated from config |
| `ArrayMesh` binary layout | 20 B interleaved, packed normals | decoded by `godot_bake_reader.py`, written as flat float32 |
| 64 mesh chunks | duplicated border vertices, one collider per chunk | de-duplicated into one 401² grid; one Unity Terrain collider |
| `terrain_greybox.gdshader` (unshaded) | no Unity equivalent | URP Lit + vertex-colour keyword; unlit-teal water |
| 64 `HeightMapShape3D` | Godot-specific | one Unity `TerrainCollider` (same shape, cheaper broadphase) |
| `PerfManager` autoload | engine-specific | `PerformanceMonitor` + `GameBootstrap` |
| `chunk_manager.gd` | node-lifecycle coupled | `SiranChunkStreamer` with a pre-allocated pool |
| 5 `island_*.gd` data classes | GDScript | `SiranMapConfig` + `IslandShapeMath` (machine-ported, unit-tested) |

### Missing from the source project — not converted, not invented

The brief asks for character integration, seven animation clips, weapons, vehicles,
loot, bots, buildings and "the modern mansion at 56.1 % X / 31.1 % Y". **None of
these exist in the Godot project.** The audit (`docs/migration/ASSET_AUDIT.md` §3)
documents each one: `.gitkeep`-only folders, no code, no mesh, no coordinate for the
mansion anywhere in the repository.

What was done instead of fabricating art:

* a **character pipeline** (generator + validator + import rules) that is ready to
  consume a real character, plus a documented **rig harness** asset explicitly
  labelled `role: rig_test_harness` (not shippable art),
* a **modular environment kit** (9 pieces) rather than an invented city,
* the mansion recorded as a **gap with its coordinate preserved**, so level design
  can place it later without re-deriving the design intent.

---

## 5. Systems migrated (with status)

| Godot system | Unity equivalent | Status |
| --- | --- | --- |
| `boot.tscn` + `boot_screen.gd` + `boot_world_link.gd` | `Boot.unity` + `GameBootstrap` + `MainMenuController` | Implemented, Not tested (editor) |
| `island_heightmap.gd` (7-pass bake) | **not ported** — the bake output is consumed; re-baking stays in Godot | Not ported (by design) |
| `terrain_mesh_builder.gd` / `surface_mesh_builder.gd` | `SiranIslandTerrain` + `SiranChunkStreamer` | Implemented, data Tested, runtime Not tested |
| `chunk_manager.gd` (radius/keep-ring/throttle) | `SiranChunkStreamer` (pooled ring, amortised) | Implemented, Not tested |
| `island_zone/water_body/river/channel/road/shape_math.gd` | `SiranMapConfig`, `IslandShapeMath`, `SiranIslandWater`, roads mesh | Implemented, shape maths Tested (EditMode suite written; Python cross-check Tested) |
| `PerfManager` autoload | `PerformanceMonitor` + `QualityProfiles` | Implemented, Not tested |
| `save_manager.gd` | not ported (nothing to save yet) | Not ported (documented) |
| `audio_manager.gd` | import rules + folders reserved | Partially (no clips exist) |
| `debug_spectator.gd` + `world_debug_hud.gd` | `WorldDebugHud` (F3 / 3-finger) | Implemented, Not tested |
| — (no player existed) | `ThirdPersonMotor`, `PlayerInputRouter`, `ThirdPersonCameraController`, touch UI | Implemented (new code), Not tested |
| Godot export preset + `build.yml` | `AndroidBuild.cs` + `.github/workflows/unity.yml` | Implemented, Not tested (no licence) |

---

## 6. Tests — run, passed, failed

### Executed here (all passing)

| Suite | Command | Result |
| --- | --- | --- |
| Blender pipeline unit tests | `python3 -m unittest discover -s processing/blender/tests -v` | **13 / 13 pass** |
| Map data vs bake | `python3 processing/unity/validate_unity_map.py` | **11 / 11 pass** (worst height delta 8 µm; all 64 colliders match; all colours match; 18 ribbons / 4,212 tris; river 9.0 → −0.5 m) |
| C# static validation | `python3 processing/unity/validate_csharp.py` | **30 / 30 pass** (29 files parse, 8 namespaces, 55 types / 39 classes with fields, asmdefs resolve, no missing serialized fields, project settings match the configured defaults; Low/High URP override assignments are present) |
| Repository structure | `python3 processing/unity/validate_structure.py` | **27 / 27 pass** (Godot intact, Unity layout, docs, no secrets) |
| Bake reader on the real scene | `python3 processing/unity/godot_bake_reader.py --verify` | **0 issues** across 82 surfaces |
| Conversion drift gates | `port_siran_config.py --check`, `convert_godot_island.py --check` | both in sync |
| Godot's own gates | `tools/verify_phase1.sh`, `tools/verify_phase3.sh` | **56 pass / 0 fail / 4 skip** and **60 / 0 / 4**, respectively; rerun locally on 2026-11-09 |

`python3 processing/blender/validators/validate_assets.py --output processing/blender/output`
was also invoked and returned **SKIP**, not PASS: there is no generated manifest or
asset output yet. That is expected on this clean checkout and is not counted as a
passing asset-validation run.

**Failed tests: none.** Several checks failed during development and were fixed;
each is recorded below rather than quietly deleted:

| Failure found | Fix |
| --- | --- |
| `FindProperty("m_SupportsHDR")` flagged as a missing C# field | Unity's own serialized property names (`m_*`) are now excluded — the check was wrong, not the code |
| Scene names not found in `ProjectBootstrap` | the check now understands `ScenesFolder + "/Gameplay/X.unity"` |
| `.gitignore` checks failed on the new bracket globs | the check now looks for the glob form |
| ProjectValidator reported missing scene scripts twice | de-duplicated via `CountMissingScripts(Scene)` |
| Editor asmdef could not resolve `CameraRig`/`Player`/`UI` | explicit `using` added per namespace (editor asmdefs do not inherit runtime references) |
| QualitySettings had `pixelLightCount: 1`, `shadowCascades: 1` on Low and `antiAliasing: 0` on High, contradicting the documented profiles | corrected to 2 lights / 2 cascades, 4 cascades, MSAA 2×, and `vSyncCount: 0` on both |
| `validate_unity_map.py` was initially placed under `blender/validators` | moved to `processing/unity/` where it belongs |
| `ThirdPersonMotor` applied the ground stick (-2 m/s) *after* the jump impulse, cancelling the launch for one frame | jump now runs first, adds the previous frame's penetration depth, and returns before the stick can be applied again |
| `TouchLookArea` called `FindFirstObjectByType` on every drag event | the rig is cached in `Start`/`Configure`; also scaled by screen height so 720p and 1440p phones feel the same |
| `MobileControlsBuilder` built the two joysticks but nothing in the project called it, and it had no crouch/sprint control | added `TouchControlsHost` (wired into the PlayerTest scene by `ProjectBootstrap`), `HoldButton` (held state, not click) and moved the look surface behind the controls so drags do not land on it |
| `MobileControlsBuilder` placed the move stick at the screen centre | anchored bottom-left, matching the README's control description |
| `QualityProfiles.RenderScale()` described a target but did not apply it, while the shared URP asset stayed at 1× MSAA / 2 cascades / 60 m | `QualityProfiles.Apply()` now updates render scale, cascades, shadow distance/map size, MSAA and additional-light cap on the active URP asset; static code validation cannot compile or execute the Unity API |
| `WorldDebugHud` printed `PeakReservedMb` under the label "draws" | HUD now shows avg / 1 %-low frame time, set-pass calls, managed-heap use and Unity reserved memory, cached at 4 Hz; PSS still requires `dumpsys meminfo` |
| No recent percentile timing existed, so profile frame-time budgets could not be compared from the HUD | `PerformanceMonitor` gained a 240-frame ring buffer, `AverageFrameMs`, `OnePercentLowFrameMs`, `SetPassCalls`, a 4 Hz cached HUD sample, and a log warning only when the one-second mean exceeds 110% of target; set-pass and memory targets are displayed, not auto-warned |

### Written but never executed

| Suite | Files | Why not run | Status |
| --- | --- | --- | --- |
| Unity EditMode | `SiranIslandDataTests.cs` (192 lines), `QualityAndShapeTests.cs` (157) | no Unity editor | **Not tested** |
| Unity PlayMode | `WorldAndPlayerPlayModeTests.cs` (216) | no Unity editor | **Not tested** |
| Unity `ProjectValidator` | 10 check groups | no Unity editor | **Not tested** |
| Blender generators | 2 scripts | no Blender | **Not tested** |
| On-device performance | — | no device | **Blocked** |

---

## 7. Build status

| Target | Status | Evidence |
| --- | --- | --- |
| **Godot APK** (existing pipeline) | **Unchanged and available** | `build.yml` + export preset untouched; the Godot project still passes its phase gates. The APK itself is only built in GitHub Actions; it was not built in this environment |
| **Unity Android APK** | **Not built** | no Unity editor/licence here. `AndroidBuild.cs` and the `unity` CI job are ready; the first real build has not happened. **No successful Unity build is claimed** |
| **Unity project opens/compiles** | **Not verified** | the strongest available evidence is `validate_csharp.py` (29 files parse, references and settings consistent) — necessary, not sufficient |

---

## 8. Remaining blockers

| Blocker | Impact | Unblock by |
| --- | --- | --- |
| **No Unity editor/licence** | project never opened, compiled, configured, tested or built | open `unity-project/` in Unity 6 `6000.0.32f1`; or add `UNITY_LICENSE` (or `UNITY_EMAIL`+`UNITY_PASSWORD`) as a repo secret so the `unity` CI job runs |
| **First Unity compile is unverified** | 29 C# files (6,124 lines) were written without a compiler; only syntax, references, serialized-field names and project settings were checked statically (30/30 checks, including the profile override assignment gate) | open in Unity 6, run `Configure` + `Validate`, then fix any compiler/API issues before treating the project as runnable |
| **No Android device / emulator** | frame rate, PSS, draw calls, touch feel all unmeasured | run the APK on the Snapdragon 6s Gen 3 baseline and a T606 device; `ANDROID_PROFILES.md` §6 has the commands |
| **No Blender install** | generators never executed; no GLB/FBX exists yet | `blender --background --factory-startup --python processing/blender/generators/generate_environment_kit.py`, then the validator |
| **No character/animation assets in the source project** | Phase 4 (humanoid integration, 7 clips) cannot be completed as a *port* | supply a rigged character (or a Mixamo export); the pipeline, import rules and validator are ready |
| **No weapons/vehicles/loot/bots/buildings in the source** | Phases 6 (gameplay foundation) can only supply the foundation | source content must exist before it can be migrated |
| **Mansion at 56.1 % / 31.1 %** | design requirement with no asset or coordinate in the repo | author the model, place at the ratio-derived position (see §9) |
| **Godot re-bake requires Godot** | the island cannot be regenerated in this environment | run the bake on a machine with Godot 4.4 |

---

## 9. Design fidelity: the Siran Island brief, element by element

| Brief requirement | In the converted data | Verified |
| --- | --- | --- |
| Industrial factory, north-west | `factory_island` INDUSTRIAL, centre (−1250, −1300), 760×640, 26 m flat | yes — NW quadrant, negative X and Z |
| Modern city centre | `central_state` URBAN, centre (40, −30), 1150×1150 rounded rect | yes |
| Foggy graveyard, east | `graveyard_center` GRAVEYARD, centre (1350, −80), 820×900 | yes — +X (east) |
| Military runway, south | `aircraft_industry` AIRFIELD, centre (120, 1480), 1020×740 flat 12 m | yes — +Z (south in Unity's left-handed Z-forward convention used here) |
| Coastal dock, south-west | `konkan_docks` DOCK, centre (−1470, 130), 780×540, sea level 4.5 m | yes — −X |
| River through the middle | 9-point river (250,−790) → (1200,2060), 70→110 m wide, 9.0 → −0.5 m | yes — crosses the central band |
| **Modern mansion at ~56.1 % X / 31.1 % Y** | **absent from both projects** | **GAP** — no mesh, no node, no constant. Preserved as a coordinate for future authoring |

Note on the mansion coordinates: the design gives a *ratio*, not a world position.
On the 4,000 m map spanning −2000…+2000, 56.1 % across X and 31.1 % across Y
(Z) resolves to approximately **X = +244 m, Z = −756 m** — inside `central_state`
on the north side of the centre, near the river's upstream reach. That derivation
is recorded here so whoever authors the model does not have to re-derive it, but
**no geometry was fabricated to fill the slot**, because the brief forbids
inventing assets that do not exist.

Fog for the graveyard is also absent in both projects (the Godot build had no fog
setting); it is a rendering feature to add with the graveyard's own art pass, not
something this migration could port.

---

## 10. Two graphics profiles

| | **Low** | **High** |
| --- | --- | --- |
| Render scale | 0.85 (runtime URP override) | 1.0 (runtime URP override) |
| Shadows | 2 cascades, 60 m, 512 px, hard | 4 cascades, 120 m, 1024 px, hard |
| MSAA | 1× | 2× |
| Post | none | none; selective post is not implemented |
| Lights/object | 2 | 4 (runtime URP override) |
| Textures | ≤ 512², ASTC 6×6, 96 MB streaming mip budget | ≤ 512², ASTC 6×6, 192 MB streaming mip budget |
| LOD bias | 0.6, allow 1 level down | 1.0, no reduction |
| Vegetation | 50 % density, 40 m detail, 250 m tree distance, 60 max trees | full density, 80 m detail, 500 m tree distance, 200 max trees |
| Camera far / stream radius | 900 / 1100 m | 1600 / 1600 m |
| Frame-rate target | 30 | 60 |
| Auto-select | default; 4 GB devices | ≥ 5.5 GB RAM and ≥ 8 cores |

These are source settings, not verified runtime results: the Unity editor has not
opened this project. Soft shadows and a 1024² High texture cap are not implemented;
`AssetImportRules` holds both profiles to 512². The single URP asset is adjusted
at runtime by `QualityProfiles.Apply()`; its High overrides remain uncompiled and
untested here.

Detection is a pure function with an EditMode test (written, not executed here); Android's default quality level is 0 and the generated URP base asset uses Low-safe values before `GameBootstrap` applies a detected profile. **No FPS is claimed for either profile**
— see `docs/performance/ANDROID_PROFILES.md` §7.

---

## 11. Documentation index

| Document | Contents |
| --- | --- |
| `docs/migration/ASSET_AUDIT.md` | Phase 1 audit: project facts, inventory, what exists vs what the brief assumes, bake measurements, budgets, CI, code-quality findings |
| `docs/migration/BAKE_FORMAT.md` | `.tscn` wire format, the three defects in the bake, converter output formats byte by byte, the proof that the two maps agree |
| `docs/migration/REPORT.md` | this document |
| `docs/architecture/UNITY_ARCHITECTURE.md` | URP rationale, assemblies, object graph, each system, data flow, Android config, conventions, deliberate omissions |
| `docs/performance/ANDROID_PROFILES.md` | both profiles knob by knob, selection logic, memory rules, device verification commands, what is unverified |
| `docs/asset-pipeline/BLENDER_PIPELINE.md` | pipeline layout, the asset contract, every helper, the kit, the rig harness, validation, tests, promotion into Unity |
| `unity-project/README.md` | how to open, configure, play, build the APK (editor + CLI + CI), layout, where the data comes from |
| `docs/ASSET_PIPELINE.md`, `UNISOC_BUDGET.md`, `PHASE{1,2,3}_ACCEPTANCE.md`, `KEYSTORE_SETUP.md` | pre-existing Godot documents, untouched |

---

## 12. Exact steps to open the Unity project

```text
1. Install Unity Hub, then Unity 6 LTS 6000.0.32f1 with the Android Build Support
   module (Android SDK & NDK Tools + OpenJDK).
2. Unity Hub > Add > Add project from disk > select  <repo>/unity-project
   (not the repository root — the Godot project is the root).
3. Open. First import takes 3-10 minutes: packages resolve, 29 scripts compile,
   Library/ is generated. Console should show no compile errors.
4. Menu: Battle Horizons > Project Setup > Configure
   (creates Assets/Settings/BattleHorizonsURP.asset, 4 materials, 4 scenes,
   writes Android player settings and build scenes)
5. Menu: Battle Horizons > Project Setup > Validate
   (expect: a PASS list and "0 failures"; SKIP lines name anything that could not
   be checked — read them, do not assume they passed)
6. Window > General > Test Runner > EditMode > Run All, then PlayMode > Run All.
7. Open Assets/Scenes/Test/PlayerTest.unity and press Play.
   WASD + mouse, Space to jump, C to crouch, F3 for the HUD.
```

## 13. Exact steps to produce an Android build

```text
Editor route
 1. Complete §12 steps 1-5 (Configure must have run).
 2. File > Build Profiles (Unity 6) or Build Settings > Android > Switch Platform.
 3. Player Settings > Publishing Settings: tick "Custom Keystore", set the keystore
    path, alias and passwords. NEVER commit the .keystore (git ignores *.keystore).
 4. Build > Build (APK) or Build App Bundle (AAB) > output Builds/.
 5. adb install -r Builds/battle-horizons.apk

CLI route
 Unity -batchmode -quit -projectPath unity-project -buildTarget Android \
       -executeMethod BattleHorizons.EditorTools.AndroidBuild.BuildApk
 # result: unity-project/Builds/battle-horizons.apk ; non-zero exit on failure;
 # an APK smaller than 1 MB is treated as a failed build, never as success.

CI route
 1. Repository settings > Secrets: add UNITY_LICENSE (or UNITY_EMAIL +
    UNITY_PASSWORD). For signed release builds also add ANDROID_KEYSTORE_BASE64,
    ANDROID_KEYSTORE_PASS, ANDROID_KEYALIAS_NAME, ANDROID_KEYALIAS_PASS.
 2. Actions > "Unity (URP) validation" > Run workflow, with build_apk = true
    (or push a v* tag).
 3. The APK is uploaded as the artifact battle-horizons-unity-apk-<sha>.
 4. Without the licence secret the job SKIPs with an explicit notice — that is a
    skip, not a pass.
```

---

## 14. Recommended next steps, in order

1. **Open the project and run `Configure` + `Validate`.** The 29 C# files were
   written without a Unity compiler, so API/serialization issues may remain in
   runtime or editor code. Nothing else can be trusted until this passes.
2. **Run the EditMode/PlayMode suites.** They encode the island invariants; a
   failure there means the conversion broke.
3. **Build the APK and install it on the baseline device** (Snapdragon 6s Gen 3,
   8 GB) and on a T606 if available. Measure frame time, PSS and set-pass calls
   with the F3 HUD; record them in `ANDROID_PROFILES.md` §7 to move items out of
   "not tested".
4. **Run the Blender generators** on a machine with Blender 4.2+, then the
   validator, then import a piece into Unity to confirm scale/orientation/UV.
5. **Obtain a rigged character** (own art or Mixamo) and walk it through the
   Phase 4 checklist: humanoid avatar, ≤ 4 influences, idle/walk/run/jump/crouch/
   aim/reload. The pipeline and import rules are already enforcing the constraints.
6. **Source or author the missing content** (buildings, vegetation, weapons,
   vehicles, the mansion) before claiming Phase 6 gameplay is complete.
