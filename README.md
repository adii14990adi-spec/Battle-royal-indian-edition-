# Battle Horizons: Last Survival

Offline Android battle-royale prototype built with **Unity 6 `6000.0.32f1` and URP**.
The only game-engine project in this repository is `unity-project/`.

> No on-device FPS, memory or thermal result is claimed. A test APK is considered
> available only when a Unity build actually completes and uploads an artifact.

## Project setup

1. Install Unity `6000.0.32f1` with Android Build Support (SDK/NDK and OpenJDK).
2. Clone with Git LFS enabled, then open `unity-project/` in Unity Hub.
3. Run **Battle Horizons > Project Setup > Configure**. This creates the URP
   assets, materials, runtime scenes and Android build settings.
4. Play the generated MainMenu or PlayerTest scene.

## Build an Android test APK

From the Unity editor, use **Battle Horizons > Build > Android APK** after project
setup. The default output is `unity-project/Builds/battle-horizons.apk`; without a
release keystore it is debug-signed for testing only.

GitHub Actions uses
[`.github/workflows/unity.yml`](.github/workflows/unity.yml) (GameCI). It runs
static checks and, when a Unity license is available, configures the project,
runs EditMode and PlayMode tests, validates it and can build an APK. To request
the APK, manually dispatch **Unity 6 Android validation** with `build_apk: true`.
Unity editor steps require `UNITY_LICENSE`, or `UNITY_EMAIL` plus
`UNITY_PASSWORD`. The workflow reports a skip if it cannot run Unity; it never
claims a skipped build as success.

## Siran Island and 50 m streaming

The committed Unity data lives under
`unity-project/Assets/Resources/Config/`. It describes a 4 km island on a 401 ×
401 height/color grid, partitioned into 500 m source tiles. Runtime visibility is
a separate fixed **50 m circular radius** around the streamer focus:

* only 500 m tile rectangles intersecting the circle are resident;
* a URP clip shader trims terrain, roads and water to the circle itself;
* the full-map Terrain renderer is always disabled, so it cannot bypass the
  radius; its heightmap collider remains for gameplay.

The map data is authoritative in Unity; no separate engine project or conversion
step is required. Validate the committed blobs and hashes with:

```bash
python3 processing/unity/validate_unity_map.py
```

## Offline and mobile settings

The Android target is ARM64, landscape, ASTC-compressed and does not request a
forced INTERNET permission. Low and High quality profiles adjust render scale,
shadows and frame pacing; both keep the same 50 m world-visibility radius. These
are project settings and targets, not proof of device performance. See
[`docs/performance/ANDROID_PROFILES.md`](docs/performance/ANDROID_PROFILES.md) and
[`docs/UNISOC_BUDGET.md`](docs/UNISOC_BUDGET.md).

## Validation commands

```bash
python3 -m pip install tree-sitter tree-sitter-c-sharp
python3 processing/unity/validate_structure.py
python3 processing/unity/validate_csharp.py
python3 processing/unity/validate_unity_map.py
python3 -m unittest discover -s processing/blender/tests -v
```

Unity editor tests and Android device measurements require Unity and a device.
See [`CONTRIBUTING.md`](CONTRIBUTING.md),
[`docs/architecture/UNITY_ARCHITECTURE.md`](docs/architecture/UNITY_ARCHITECTURE.md),
and [`docs/ASSET_PIPELINE.md`](docs/ASSET_PIPELINE.md) for details.
