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
├── scenes/      core (boot), gameplay, characters, weapons, loot, ui
├── scripts/     autoload, systems, player, ui
├── resources/   loot_tables, themes, config (.tres tunables)
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
