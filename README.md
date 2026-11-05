# Desi Royale

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

CI lives in a single workflow, [`.github/workflows/build.yml`](.github/workflows/build.yml).

| Trigger | Job | Output |
| --- | --- | --- |
| push to `develop`, PR to `develop`/`main` | `build-debug` | `desi-royale-debug-<sha>` (14 days) |
| tag `v*` | `build-release` | `desi-royale-release-<tag>` (90 days) + GitHub Release |

Both jobs: JDK 17 → Android SDK (`build-tools;34.0.0`) → Godot 4.4.1 + export
templates (cached) → `--import` pre-heat → keystore injection → export →
verify → upload. No retries on import/export; every failure is a hard stop with
a `::error::` annotation.

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
APK_SIZE_LIMIT_MB=120 bash tools/verify_apk.sh build/desi-royale-debug.apk
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
