# Phase 1 acceptance gates — status & runbook

Phase 1 is "done" only when all ten gates in the spec pass. Eight of them are
machine-checkable; two need a physical Unisoc T606 in airplane mode.

```bash
bash tools/verify_phase1.sh     # runs gates 1–5, 8, 10 (+ hygiene checks)
```

| # | Gate | Status | Evidence |
| --- | --- | --- | --- |
| 1 | `git ls-files \| grep -c '^\.godot/'` → 0 | **PASS** | nothing under `.godot/` is tracked; `.gitignore` ignores it (`.godot/` is Godot 4's import cache and can exceed 500 MB) |
| 2 | `git ls-files \| grep -c 'export/'` → 0 | **PASS** | `export/`, `build/`, `*.apk`, `*.aab`, `*.pck` all ignored |
| 3 | `git lfs ls-files` shows all binary assets | **WIRED, NO OBJECTS YET** | LFS filter lines are committed in `.gitattributes` for `png/jpg/glb/ogg/wav/exr`; see caveat C2 |
| 4 | `project.godot` has `rendering_method="gl_compatibility"` + `import_etc2_astc=true` | **PASS** | verified by script (plus 14 other Unisoc-safe values) |
| 5 | `export_presets.cfg` has `permissions/internet=false` + `export_format=0` | **PASS** | preset `Android_APK_Unisoc`, APK format, no INTERNET |
| 6 | APK built, installed, running on physical Unisoc T606 in airplane mode | **PENDING — needs hardware** | runbook §2 |
| 7 | `adb logcat` free of `gl_error` / `vulkan` / `crash` after 60 s | **PENDING — needs hardware** | runbook §3 |
| 8 | Folder structure matches §5 exactly | **PASS** | 25 leaf dirs exist and are tracked (`.gitkeep` in the empty ones) |
| 9 | `v0.0.1-setup` tag pushed to remote | **PASS** | annotated tag on the setup commit |
| 10 | `README.md` + `CONTRIBUTING.md` committed | **PASS** | plus `docs/UNISOC_BUDGET.md`, `docs/ASSET_PIPELINE.md` |

## 1. What "hello Unisoc" proves

`scenes/core/boot.tscn` is the smallest scene that can fail loudly on a T606:

- **Node3D root** + **Camera3D at (0, 1, 3)** looking exactly at the origin.
- **MeshInstance3D** with a `BoxMesh` and an **unshaded** `StandardMaterial3D`
  textured by `assets/textures/test_pattern_256.png` (256×256, imported as
  **VRAM compressed** → ASTC on device, ETC2 fallback). The pattern has hard
  edges, pure primaries and a grey ramp so ASTC block artefacts are visible.
- **DirectionalLight3D with `shadow_enabled = false`** — ambient-only lighting,
  per the Unisoc budget.
- **Control → Label** printing `Device: … | GPU: … | GLES3 OK` plus live FPS /
  static memory / video memory / draw calls, sourced from the `PerfManager`
  autoload.
- The box **spins at 0.6 rad/s**: a frozen box means a stalled render loop, which
  a still screenshot would hide.
- `scripts/autoload/perf_manager.gd` logs
  `Performance.get_monitor(Performance.MEMORY_STATIC)` **once per second** to the
  console (`adb logcat -s godot | grep '\[perf\]'`).

## 2. Device runbook — build & install (gate 6)

Prerequisites on the build machine, not in the repo:

1. Godot 4.4+ **with Android export templates** (`Editor > Manage Export Templates`).
2. Android SDK + build-tools + platform-tools (`adb`), `JAVA_HOME` pointing at a JDK 17.
3. Debug keystore **outside the repo**:
   ```bash
   keytool -genkey -v -keystore ~/.android/desi-royale-debug.keystore \
     -alias androiddebugkey -storepass android -keypass android \
     -keyalg RSA -keysize 2048 -validity 10000 \
     -dname "CN=Android Debug,O=Android,C=US"
   ```
   Godot's default debug keystore (`~/.android/debug.keystore`) is fine too. The
   path/password live in `export_credentials.cfg` — never commit that file.

Build and install:

```bash
# 1. Import once so .godot/imported exists (ASTC/ETC2 requires the setting to be
#    on *before* the first import — it is, in project.godot).
godot --headless --path . --import

# 2. Release export (this is the artefact the gate is about — see caveat C3)
mkdir -p build
godot --headless --path . --export-release "Android_APK_Unisoc" build/desi-royale-0.0.1-setup.apk

# 3. Install on the phone over USB (turn airplane mode ON afterwards)
adb install -r build/desi-royale-0.0.1-setup.apk
```

Confirm the APK really is offline, before you even launch it:

```bash
# aapt2 (build-tools 30+) — expect NO android.permission.INTERNET line
"$ANDROID_HOME"/build-tools/*/aapt2 dump permissions build/desi-royale-0.0.1-setup.apk

# or apkanalyzer
apkanalyzer manifest permissions build/desi-royale-0.0.1-setup.apk

# or unzip + decode
unzip -p build/desi-royale-0.0.1-setup.apk AndroidManifest.xml | strings | grep -i internet
# expected: no output
```

## 3. Device runbook — soak test (gates 6 & 7)

```bash
# Airplane mode on, then:
adb logcat -c
adb shell am start -n com.desiroyale.game/com.godot.game.GodotApp
sleep 60

# Gate 7: this must print nothing at all
adb logcat -d | grep -iE "gl_error|vulkan|crash|fatal" || echo "CLEAN"

# Proof the GLES3 pipeline is up (PerfManager banner + per-second memory line)
adb logcat -d | grep "\[perf\]" | head -20
adb logcat -d | grep "\[boot\]"

# Budget snapshot
adb shell dumpsys meminfo com.desiroyale.game | grep -E "TOTAL PSS|TOTAL RSS"
```

Pass criteria:

- [ ] Label is readable and shows the real device model + GPU (e.g.
      `Device: SM-A035F | GPU: Mali-G57 | GLES3 OK`).
- [ ] Textured box is visible and **rotating**.
- [ ] `[perf] static_mem=…` lines appear once per second for the whole run.
- [ ] No `gl_error`, no `vulkan`, no crash/ANR in `logcat` during 60 s.
- [ ] `TOTAL PSS` ≤ 800 MB, `video_mem` ≤ 180 MB, draw calls ≤ 60 (boot scene
      should be ~1 draw call).
- [ ] Phone stays in landscape when rotated 180°, never portrait.

Record the result in the PR description (device model, Android version, GPU
string, PSS, FPS) so the numbers are traceable in the phase tag.

## 4. Known caveats / deviations

**C1 — Settings cross-checked against engine source, not a running editor.**
The environment that created this baseline had no Godot binary, no Android SDK
and no phone, so the project was never opened in the editor here. Every setting
name and every export option was verified against the Godot **4.4.1** source
(`platform/android/export/export_plugin.cpp`, `core/config/project_settings.cpp`,
`servers/rendering_server.cpp`, `scene/main/scene_tree.cpp`, `main/main.cpp`), and
`assets/textures/test_pattern_256.png.import` uses the same path-hash scheme the
importer itself uses. Expected first-open behaviour: Godot regenerates `.godot/`
and may rewrite the `.import` file once — that rewrite is a no-op churn, just
commit it.

**C2 — No LFS objects yet.** `git lfs` was not installed in the setup
environment, so the single binary in the repo (`test_pattern_256.png`, 1.8 KB) was
committed as a plain blob; it is byte-identical either way and does not break
clones. On any machine with `git lfs install`, convert it once:

```bash
git lfs install
git add --renormalize assets/textures/test_pattern_256.png
git commit -m "chore(lfs): store boot test texture in LFS"
git lfs ls-files          # gate 3 then lists it
```

**C3 — One-click deploy injects INTERNET.** Godot adds
`android.permission.INTERNET` when it builds for a *remote debugger* (dumb
client / remote debug flags). That is exactly why gate 6/7 must be checked with a
**release export APK**, not with `Editor > Run on device`. A release export of
this preset has no INTERNET permission — verified against the engine's export
path and the export template manifest, which declares none.

**C4 — `max_renderable_elements=100000` exceeds the editor's slider.** The
setting's UI range is 1024–65536; the value written in `project.godot` is still
applied as-is by the engine. If the editor clamps it on save, keep 65536 — the
difference is irrelevant at our draw-call budget.

**C5 — `build/` is gitignored.** Export output goes to `build/`, so `export_path`
in the preset points there. Nothing under `build/` is ever committed (gate 2).
