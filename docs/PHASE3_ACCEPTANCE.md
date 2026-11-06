# Phase 3 acceptance gates — status & runbook

Phase 3 is "done" when the 4 km greybox island streams, collides and holds
30 FPS on a Unisoc-class phone **without** generating anything at runtime.
Nine of the gates below are machine-checkable; four need a device.

```bash
bash tools/verify_phase3.sh        # static gates (source, scenes, baked output)
bash tools/bake_siran_island.sh    # re-bake + in-engine self-check
```

| # | Gate | Status | Where it is proven |
| --- | --- | --- | --- |
| 1 | Terrain is 4000 × 4000 m, uneven but walkable | **PASS (data)** | `siran_map_config.gd`; preview PNG; self-check height range |
| 2 | Terrain collision is `HeightMapShape3D`, no trimesh | **PASS** | `verify_phase3.sh` gate 1 + self-check raycasts |
| 3 | Terrain is baked to disk, not generated at runtime | **PASS** | `verify_phase3.sh` gate 2 (FastNoiseLite confined to `scripts/world/bake/`) |
| 4 | Five zones with flat-coloured markers, correct heights | **PASS (data)** | `verify_phase3.sh` gate 4 + selftest flatness assertion |
| 5 | Lake (north-middle) + river through the map | **PASS (data)** | config + bake summary; visible in preview PNG |
| 6 | Water is unshaded/vertex-lit with a scrolling normal map | **PASS** | `verify_phase3.sh` gate 5 |
| 7 | Chunks of 500 m, threaded loads inside a 50 m radius | **PASS** | `verify_phase3.sh` gate 3 + `compute_desired_for()` self-check |
| 8 | Roads connect the main zones | **PASS (data)** | 5 roads in config, baked per chunk |
| 9 | A fresh clone can open the world scene (no broken `[ext_resource]`) | **PASS** | `verify_phase3.sh` gate 6 |
| 10 | 30 FPS sustained over 10 minutes on a T606 | **PENDING — needs hardware** | runbook §3 |
| 11 | Draw calls ≤ 60, VRAM ≤ 180 MB, PSS ≤ 800 MB with 9 chunks resident | **PENDING — needs hardware** | runbook §4 |
| 12 | Chunk streaming visible while flying: no hitch > 100 ms, no holes | **PENDING — needs hardware** | runbook §5 |
| 13 | No GL errors / crashes in a 10-minute flight | **PENDING — needs hardware** | runbook §5 |

"PASS (data)" means the numbers are asserted in code and the self-check; the
visual result is judged from the preview image and the device run.

### Packaging regression (2026-11-06)

The first APK built from `main` installed, drew the boot screen, and did nothing
when the tester tapped **ENTER SIRAN ISLAND**. The bake itself was fine — the
files were simply not in the pack. `export_filter="all_resources"` derives its
list from the editor's scanned filesystem, and the 64 chunk scenes are written
by `tools/bake_siran_island.sh` *after* that scan.

Fixed on three layers, each independently sufficient:

1. `export_presets.cfg` — `include_filter="*.tscn,*.res,*.tres,*.gd,scenes/world/*,resources/world/*"`.
   Godot applies it by walking the directory tree from `res://`
   (`EditorExportPlatform::_edit_files_with_filter` → `DirAccess`), so what is
   on disk is what ships.
2. `.github/workflows/build.yml` — a `--headless --import` step after the bake,
   so the editor's file list contains the new files before the export runs.
3. `scripts/ui/boot_world_link.gd` — the button reports
   `world scene packed: … | baked chunks: N/64` on the boot screen and prints
   any `change_scene_to_file` failure as text, instead of a silent `return`.

Proven by `tools/verify_apk.sh` gate E, which lists the APK and requires
`assets/scenes/world/terrain/chunk_*.tscn` ×64 plus the matching
`*_collision.res` ×64 (`verify_phase3.sh` gate 11 pins the preset settings).

---

## 1. Before the bake

```bash
pip install gdtoolkit            # gdparse/gdlint gates in verify_phase3.sh
bash tools/verify_phase3.sh      # static gates — green before introducing assets
```

## 2. Bake and self-check

```bash
bash tools/bake_siran_island.sh
echo $?                          # 0 = baked + self-check green
ls docs/images/siran_island_preview.png
```

The self-check prints one line per gate. The two that matter most:

```
  PASS  all 25 rays hit the chunk collider
  PASS  collision height matches the rendered vertex (worst 0.03 m)
  PASS  aircraft industry is perfectly flat (spread 0.000 m)
```

Anything that fails here fails in the *bake*, not on the phone. Fix it there.

## 3. Device run — build, install, enter the world

You do not have to build it yourself: every push runs the bake and uploads
`battle-horizons.apk` (plus the generated island preview) as the
`battle-horizons-apk-<sha>` artifact of the `Build APK` workflow. Use the local
commands below when you want to iterate on the phone without a CI round-trip.

Same toolchain as Phase 1 (`docs/PHASE1_ACCEPTANCE.md` §2):

```bash
godot --headless --path . --export-release "Android_APK_Unisoc" \
      build/battle-horizons.apk
adb install -r build/battle-horizons.apk
adb logcat -c
adb shell am start -n com.aurastudios.battlehorizons/com.godot.game.GodotApp
```

Tap **ENTER SIRAN ISLAND — PHASE 3 GREYBOX** on the boot screen. Expect: ocean,
the island with five coloured pads, the lake, the river, roads, and a HUD that
starts at "chunks loaded 1".

Motion without a keyboard: **TOUR** (automatic 1100 m circle at 55 m altitude,
~2.6°/s), **ZONE ▶** (teleport between the five zones), **STOP**, **RESET**.
Desktop also gets WASD/QE, Shift to sprint, drag to look, F/N/R/Esc.

## 4. Numbers to capture (gates 10–11)

```bash
# Frame rate and draw calls, once per second, from the PerfManager autoload
adb logcat -s godot | grep "\[perf\]"

# Streaming HUD values are on screen; capture a screenshot at the same time
adb exec-out screencap -p > phase3-tour.png

# Memory
adb shell dumpsys meminfo com.aurastudios.battlehorizons | grep -E "TOTAL PSS|TOTAL RSS"

# Sustained frame time (thermal throttling shows up here, not in the average)
adb shell dumpsys gfxinfo com.aurastudios.battlehorizons framestats | head -40
```

| Metric | Threshold | Unisoc T606 | Adreno 619 |
| --- | --- | --- | --- |
| FPS, 10 min tour | ≥ 30 sustained | ______ | 30 (Phase 1 baseline) |
| Draw calls | ≤ 60 | ______ | ______ |
| PSS | ≤ 800 MB | ______ | 50 MB (Phase 1 baseline) |
| Video memory | ≤ 180 MB | ______ | ______ |
| Worst chunk-load frame | < 100 ms | ______ | ______ |
| GL errors / crashes in 10 min | 0 | ______ | ______ |

## 5. Streaming behaviour to verify (gate 12)

| Action | Expected |
| --- | --- |
| Start on Central State | "chunks loaded 1" → grows to 9 within ~1 s |
| ZONE ▶ to the Aircraft Industry | old chunks drop out of the count, new ones arrive; no fall-through, runway flat under the camera |
| TOUR for a full lap (~2 min) | count stays 4–9, never 0; no visible hole in the terrain behind the camera |
| Fly (WASD) across a chunk border at 40 m/s | at most one 2-chunk rebuild, no frame longer than 100 ms |
| Fly to the map edge | ocean beyond the shore, no missing chunk, no crash |
| Esc / EXIT | returns to the boot screen, PerfManager keeps logging |

If the HUD says `NO BAKED CHUNKS`, the APK was built from a tree without the
bake: re-run `tools/bake_siran_island.sh` and rebuild.

## 6. What is explicitly out of scope for Phase 3

No buildings, houses, monuments, vegetation, loot, weapons, characters or
gameplay. A PR that adds geometry to this milestone is out of scope — the point
of the greybox is that the numbers in §4 mean something.
