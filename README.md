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

**The map is baked in CI, not on your laptop** — the workflow is the only place
the 64 terrain chunks are produced:

1. `gates` — `tools/verify_phase1.sh` and `tools/verify_phase3.sh` on a fresh
   clone (no Godot needed; the bake gate skips until something is baked).
2. `build` — JDK 17 + Android SDK (`build-tools;34.0.0`) + Godot 4.4.1 and its
   export templates (cached) → `--import` twice (a cold pass may print ERROR
   lines, a warm pass may not) → signing material → `bash
   tools/bake_siran_island.sh` (import → bake → self-check `--require-bake`) →
   `tools/verify_phase3.sh` again, now with 64 chunks on disk → `--export-release`
   → `--export-debug` → `tools/verify_apk.sh` on both APKs.
3. `publish` — tag pushes only.

Grab the APK from the run's **Artifacts**; the `siran-island-bake-<sha>`
artifact holds the generated island preview, the bake summary and the bake log.
No retries on import/bake/export; every failure is a hard stop with a
`::error::` annotation.

### Every APK must pass four gates

`tools/verify_apk.sh <apk>` enforces them, so a green build is evidence rather
than a hope:

| Gate | Rule | Why |
| --- | --- | --- |
| A | **no** `android.permission.INTERNET` | the game is fully offline |
| B | `android.permission.WAKE_LOCK` **present** | an empty permission list means manifest generation silently failed |
| C | size ≤ **120 MB** | install budget on the target device |
| D | `arm64-v8a` present (`armeabi-v7a` warn-only) | Unisoc T606 is 64-bit |

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
| Terrain mesh + `HeightMapShape3D` per chunk (never a trimesh) | `scripts/world/bake/terrain_mesh_builder.gd` |
| One-command bake (import → bake → self-check) | `bash tools/bake_siran_island.sh` |
| 500 m chunk streaming, threaded, radius driven | `scripts/world/chunk_manager.gd` |
| World scene (ocean, lake, river, sun, streamer, camera, HUD) | `scenes/world/siran_island.tscn` |
| Unshaded terrain/road/water shaders | `assets/shaders/` |
| Machine-checkable Phase 3 gates | `bash tools/verify_phase3.sh` |
| Bake + gates + APK, run automatically on every push | `.github/workflows/build.yml` |
| In-engine self-check (collision vs mesh, flatness, radius maths) | `godot --headless --path . scenes/tests/phase3_selfcheck.tscn` |

### Bake the island

```bash
bash tools/bake_siran_island.sh        # needs Godot 4.4+; writes ~11 MB of chunks
# then run the world (or tap "ENTER SIRAN ISLAND" on the boot screen)
godot --path . scenes/world/siran_island.tscn
```

The baked chunks are **not** committed (64 scenes + 128 resources of
generated data): `tools/bake_siran_island.sh` writes them, and CI runs it on
every push, so `battle-horizons.apk` always ships a freshly baked island. A
fresh clone without a bake still opens the world — the HUD just tells you to
bake.

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
├── scenes/      core (boot), world (island scene + baked chunks), tests,
│                gameplay, characters, weapons, loot, ui
├── scripts/     autoload, world (map config, chunk streamer), world/bake,
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
bash tools/bake_siran_island.sh      # import -> bake -> self-check (needs Godot)
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
