# Battle Horizons: Last Survival

[![Build APK](https://github.com/adii14990adi-spec/Battle-royal-indian-edition-/actions/workflows/build.yml/badge.svg?branch=develop)](https://github.com/adii14990adi-spec/Battle-royal-indian-edition-/actions/workflows/build.yml)

Fully offline 3D battle royale, Indian theme (Free Fire India-inspired).
Target: Unisoc T606 / 4 GB RAM Android, 30 FPS floor.

## Constraints

- No network calls. Android manifest has NO INTERNET permission.
- GLES3 Compatibility renderer only. No Vulkan, no Forward+.
- Budget: ≤800 MB PSS, ≤180 MB VRAM, ≤60 draw calls, 25 bots per match.

## Setup

1. Godot 4.4+ (with Android export templates installed).
2. `git lfs install` — required before clone of assets.
3. Open `project.godot`, verify renderer = gl_compatibility.
4. Export preset: `Android_APK_Unisoc`.

## Build

CI lives in a single workflow, [`.github/workflows/build.yml`](.github/workflows/build.yml),
with the Godot/Android plumbing factored into a composite action,
[`.github/actions/setup-godot-android`](.github/actions/setup-godot-android/action.yml).

| Trigger | Jobs | Output |
| --- | --- | --- |
| PR to `main`/`develop`, push to `main`/`develop`/`arena/**`, manual run | `gates` → `build` | `battle-horizons.apk` + `battle-horizons-debug.apk`, artifact `battle-horizons-apk-<sha>` (14 days) |
| tag `v*` | `build` → `publish` | the same APK attached to the GitHub Release |

**CI builds the APK; it does not generate the map.** Siran Island is generated
once by `TerrainGenerator` into a single committed scene,
`scenes/world/siran_island_baked.tscn`:

1. `gates` — `tools/verify_phase1.sh` and `tools/verify_phase3.sh` on a fresh
   clone (no Godot needed; gate 7 inspects the committed scene).
2. `build` — JDK 17 + Android SDK (`build-tools;34.0.0`) + Godot 4.4.1 and its
   export templates (cached) → `--import` twice (a cold pass may print ERROR
   lines, a warm pass may not) → config validation → signing material → the
   Phase 3 self-check with `--require-bake` against the committed scene (it
   counts 64 chunk nodes, a mesh surface and a `HeightMapShape3D` per chunk, and
   raycasts the collision against the drawn vertices) → `tools/verify_phase3.sh`
   → `--export-release` → `--export-debug` → `tools/verify_apk.sh` on both APKs.
3. `publish` — tag pushes only.

Grab the APK from the run's **Artifacts**; `siran-island-selfcheck-<sha>` holds
the self-check log if the map itself is what broke. No retries on
import/export; every failure is a hard stop with a `::error::` annotation.

> **Why the map is not baked in CI any more.** It was, and it produced APKs that
> installed, booted, drew the UI — and reported `MISSING BAKE` when you tapped
> the button. The chunk scenes were written *after* the editor had scanned the
> filesystem, so `export_filter="all_resources"` could leave them out of the
> pack entirely. A committed scene cannot be created too late: either it is in
> Git (and therefore in the pack) or gate 7 fails the build before it starts.

### Every APK must pass five gates

`tools/verify_apk.sh <apk>` enforces them, so a green build is evidence rather
than a hope:

| Gate | Rule | Why |
| --- | --- | --- |
| A | **no** `android.permission.INTERNET` | the game is fully offline |
| B | `android.permission.WAKE_LOCK` **present** | an empty permission list means manifest generation silently failed |
| C | size ≤ **120 MB** | install budget on the target device |
| D | `arm64-v8a` present (`armeabi-v7a` warn-only) | Unisoc T606 is 64-bit |
| E | the baked map scene (`siran_island_baked.tscn` or its `.remap`) and the river mesh **inside the APK** | an APK with a working UI and no world looks exactly like a working APK |

Run them locally against a built APK:

```bash
APK_SIZE_LIMIT_MB=120 bash tools/verify_apk.sh build/battle-horizons.apk
```

### Signing

Release builds need three GitHub secrets — see
[`docs/KEYSTORE_SETUP.md`](docs/KEYSTORE_SETUP.md). `tools/patch_keystore.sh`
injects them at build time and fails loudly if any is missing or decodes to an
empty file; the committed `export_presets.cfg` always keeps the keystore fields
blank. Nothing signing-related is ever committed (`.gitignore` blocks
`*.keystore` / `*.jks`).

Acceptance evidence, the mandatory negative tests, and known entry-criterion
failures are recorded in [`docs/PHASE2_ACCEPTANCE.md`](docs/PHASE2_ACCEPTANCE.md).

## Versioning

SemVer, tagged per phase: `v0.0.1-setup`, `v0.2.0-ci`, ...

---

## Phase 3 status — World & map generation (Siran Island greybox)

Terrain, water, roads and coloured zone markers only: **no buildings, props or
vegetation yet**. Full design notes: `docs/PHASE3_WORLD.md`. Gates:
`docs/PHASE3_ACCEPTANCE.md`.

| Item | Where |
| --- | --- |
| 4 km × 4 km island layout (zones, lake, river, moat, 5 roads) as auditable data | `scripts/world/siran_map_config.gd` |
| FastNoiseLite heightfield: island mask, hills, ridged east, lake, river, zone pads | `scripts/world/bake/island_heightmap.gd` |
| Generates the map with FastNoiseLite + SurfaceTool into one committed scene | `scripts/world/bake/terrain_generator.gd` |
| Terrain `HeightMapShape3D` per chunk (never a trimesh) | `scripts/world/bake/terrain_mesh_builder.gd` |
| One-command generation (import → generate → self-check) | `bash tools/generate_siran_island.sh` |
| 500 m chunk streaming, threaded, radius driven | `scripts/world/chunk_manager.gd` |
| World scene (ocean, lake, river, sun, streamer, camera, HUD) | `scenes/world/siran_island.tscn` |
| Unshaded terrain/road/water shaders | `assets/shaders/` |
| Machine-checkable Phase 3 gates | `bash tools/verify_phase3.sh` |
| Gates + APK, run automatically on every push (no bake) | `.github/workflows/build.yml` |
| In-engine self-check (collision vs mesh, flatness, radius maths) | `godot --headless --path . scenes/tests/phase3_selfcheck.tscn` |

### Generate the island

The map is generated **once** with a `@tool` script and committed. In the
editor: open `tools/bake/terrain_generator_editor.gd` and press
**File > Run** (Ctrl+Shift+X). From a terminal:

```bash
bash tools/generate_siran_island.sh    # needs Godot 4.4+; writes the scene + river + summary
# then run the world (or tap "ENTER SIRAN ISLAND" on the boot screen)
godot --path . scenes/world/siran_island.tscn
```

It writes four things, all of which belong in the same commit:

| File | What it is |
| --- | --- |
| `scenes/world/siran_island_baked.tscn` | the whole 4 km map: 64 `Chunk_<x>_<z>` nodes, each with a `SurfaceTool` mesh and a `HeightMapShape3D` collider |
| `resources/world/baked/siran_river_surface.res` | river water ribbon |
| `resources/world/baked/siran_island_summary.tres` | generator version, noise seed, per-zone integrity numbers |
| `docs/images/siran_island_preview.png` | top-down layout, for reviewing "did the island move?" |

Nothing generates terrain at runtime: `ChunkManager` loads that one scene on a
worker thread and streams its chunk children in and out by radius.

### "Nothing happens when I tap ENTER SIRAN ISLAND"

That was a real shipped defect. What the measurements actually say:

| Measurement | Value |
| --- | --- |
| APK built from `main` (`1976adb`) | 112,403,727 bytes |
| APK after the fixes below | 112,415,464 bytes (+11.5 KB) |
| Baked world (64 chunk scenes + 128 resources) | ≈ 11 MB |

The 11.5 KB delta is roughly the extra script text, so the pack **already
contained** the baked island — the export filters were not the cause. What the
build did get wrong was judging itself: at export time Godot converts text
resources to binary and writes `<path>.remap` beside them, so inside an APK
`chunk_0_0.tscn` is not a file at all. `FileAccess.file_exists()` is blind to
that remap (only `ResourceLoader.exists()` follows it), so the button's own
"is the island baked?" test answered **no** in a fully packed build.

Four things now make that class of failure impossible to hide:

| Layer | What it does |
| --- | --- |
| `ResourceLoader.exists()` in `boot_world_link.gd` | existence checks see through `.remap`, so a packed build reports itself as packed |
| status line under the button | prints `Ready — world scene packed, baked map packed` in green *before* you tap, or the exact problem in amber |
| failure text on screen | a failed `change_scene_to_file` is printed on the boot screen, not just to logcat |
| tap fallback in `boot_screen.gd` | a tap that reaches no control still enters the world, so a covered button cannot strand a tester |

In CI, `tools/verify_apk.sh` gate E lists the APK (file **or** its `.remap`) and
fails the build when the payload is short; `verify_phase3.sh` gate 11 pins the
remap-aware check, and gate 12 loads both config files with Godot's own parser.
If the tap still fails on a device, the boot screen now says why — that is the
next thing to read, and `adb logcat -s godot` carries the same lines.

### Streaming, in one line

500 m chunks, `ResourceLoader.load_threaded_request()` at 5 Hz, two loads in
flight, a 50 m radius per spec plus a `keep_ring = 1` floor so the player is
never standing on the edge of the world. Raise `stream_radius_m` once Phase 4
props exist to hide behind.

---

## Phase 1 status

Phase 1 (project setup & version control) is committed and tagged `v0.0.1-setup`.
What is in the repo today:

| Item | Where |
| --- | --- |
| Godot 4.4 project, GLES3 Compatibility, 30 FPS cap | `project.godot` |
| Android APK export preset (no INTERNET permission) | `export_presets.cfg` (`Android_APK_Unisoc`) |
| Git hygiene (cache/export artefacts never committed) | `.gitignore`, `.gitattributes` |
| Folder skeleton for the whole game | `assets/`, `scenes/`, `scripts/`, `resources/`, `addons/` |
| "Hello Unisoc" GPU baseline scene | `scenes/core/boot.tscn` |
| Perf/memory logger autoload | `scripts/autoload/perf_manager.gd` |
| Machine-checkable acceptance gates | `tools/verify_phase1.sh` |
| Acceptance evidence + device runbook | `docs/PHASE1_ACCEPTANCE.md` |
| Performance budget rules | `docs/UNISOC_BUDGET.md` |
| Asset import + LFS rules | `docs/ASSET_PIPELINE.md` |

## Project layout

```
res://
├── assets/      models (per biome), textures, audio (music/sfx/voice), shaders, fonts
├── scenes/      core (boot), world (island scene + baked map scene), tests,
│                gameplay, characters, weapons, loot, ui
├── scripts/     autoload, world (map config, chunk streamer), world/bake
│                (heightfield + TerrainGenerator),
│                systems, player, ui, tests
├── resources/   world (materials, baked terrain), loot_tables, themes, config
├── addons/      third-party plugins (none yet)
└── export_presets/  frozen v0.0.1-setup snapshot (see its README)
```

Root-level extras: `docs/`, `tools/`, `export_presets.cfg`, `project.godot`.

## Verify the baseline

```bash
bash tools/verify_phase1.sh          # Phase 1: budget, preset, project identity
bash tools/verify_phase3.sh          # Phase 3: streaming, shaders, baked output
bash tools/generate_siran_island.sh  # import -> generate -> self-check (needs Godot)
```

Device acceptance (physical Unisoc T606, airplane mode) is documented step by
step in `docs/PHASE1_ACCEPTANCE.md` — it needs a phone, so it is the one gate a
CI machine cannot close for you.

```bash
# Export the APK (Godot 4.4+, Android export templates installed)
godot --headless --path . --export-release "Android_APK_Unisoc" \
      build/battle-horizons.apk

# Install + soak test
adb install -r build/battle-horizons.apk
adb logcat -c && adb shell am start -n com.aurastudios.battlehorizons/com.godot.game.GodotApp
adb logcat | grep -iE "gl_error|vulkan|crash|battlehorizons|perf"
```
