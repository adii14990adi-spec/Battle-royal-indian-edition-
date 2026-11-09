# Android low-end performance targets

This document records the project's intended low-end Android budget. These are
acceptance targets, not measured results. No FPS, PSS or GPU-memory claim should
be made until the built Unity APK has been profiled on the corresponding device.

## Target class

| Item | Target |
| --- | --- |
| CPU | Unisoc T606-class, 8 cores |
| GPU | Mali-G57-class mobile GPU |
| System memory | 4 GB |
| Display | Landscape phone display, commonly 720p-class rendering |
| Minimum Android API | 26 (Android 8.0) |
| Architecture | ARM64 |

## Working budgets

| Budget | Target | Measurement |
| --- | --- | --- |
| Frame pacing | 30 fps floor | device profiler / `adb shell dumpsys gfxinfo` over a sustained run |
| Process memory | ≤ 800 MB PSS | `adb shell dumpsys meminfo com.aurastudios.battlehorizons` |
| GPU memory | ≤ 180 MB target | Unity Profiler / device GPU tooling; not currently measured |
| Set-pass calls | ≤ 60 target | Unity ProfilerRecorder in a development build |
| Texture dimensions | ≤ 512 × 512 | enforced by `AssetImportRules` |
| Streamed visible world | 50 m circular radius | Unity PlayMode/render test and device visual check |

## Unity implementation constraints

* URP forward rendering; HDR and post-processing are disabled in the current
  mobile profiles.
* Android is configured for ARM64, ASTC texture compression, landscape and no
  forced INTERNET permission.
* Low profile targets 30 fps and uses 0.85 render scale, two hard-shadow
  cascades, 60 m shadow distance and a 512 px shadow map.
* The island has 500 m source tiles, but the active visual region is a clipped
  50 m world-space circle. The full-map Terrain renderer is always disabled; its
  collider remains active.
* Keep per-frame allocations and scene searches out of gameplay hot paths. Pool
  streamed chunk views and cache diagnostics.
* Prefer a small number of shared materials and keep optional effects off unless
  a device measurement demonstrates that they fit.

## Device runbook

```bash
adb install -r build/battle-horizons.apk
adb logcat -s Unity
adb shell dumpsys meminfo com.aurastudios.battlehorizons | head -30
adb shell dumpsys gfxinfo com.aurastudios.battlehorizons framestats | head -40
```

Record the phone model, Android build, quality profile, resolution, ambient
temperature, run duration, average frame time and low-percentile frame time.
Run for at least ten minutes to expose thermal throttling. The in-game HUD's
managed heap and Unity-reserved memory are not equivalent to Android PSS.

## Current evidence boundary

The repository contains Unity-side settings, code and automated tests. Until a
Unity editor build and on-device run actually complete, the frame-rate, memory,
thermal, touch-response and shader-cost targets above remain **unverified**.
