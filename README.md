# Desi Royale

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

GitHub Actions: `.github/workflows/build.yml` (Phase 2).

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
| In-engine self-check (collision vs mesh, flatness, radius maths) | `godot --headless --path . scenes/tests/phase3_selfcheck.tscn` |

### Bake the island

```bash
bash tools/bake_siran_island.sh        # needs Godot 4.4+; writes ~11 MB of chunks
# then run the world (or tap "ENTER SIRAN ISLAND" on the boot screen)
godot --path . scenes/world/siran_island.tscn
```

The baked chunks are committed, so a fresh clone runs without baking. If they are
missing, the world still opens — the HUD just tells you to bake.

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
└── export_presets/  archived preset copies used by CI
```

Root-level extras: `docs/`, `tools/`, `export_presets.cfg`, `project.godot`.

## Verify the baseline

```bash
bash tools/verify_phase1.sh          # all machine-checkable Phase 1 gates
```

Device acceptance (physical Unisoc T606, airplane mode) is documented step by
step in `docs/PHASE1_ACCEPTANCE.md` — it needs a phone, so it is the one gate a
CI machine cannot close for you.

```bash
# Export the APK (Godot 4.4+, Android export templates installed)
godot --headless --path . --export-release "Android_APK_Unisoc" \
      build/desi-royale-0.0.1-setup.apk

# Install + soak test
adb install -r build/desi-royale-0.0.1-setup.apk
adb logcat -c && adb shell am start -n com.desiroyale.game/com.godot.game.GodotApp
adb logcat | grep -iE "gl_error|vulkan|crash|desi|perf"
```
