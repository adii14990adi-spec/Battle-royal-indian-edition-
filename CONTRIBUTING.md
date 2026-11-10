# Contributing to Battle Horizons: Last Survival

This repository contains the Unity 6 URP project only. The project is under
`unity-project/`; CI, validation and asset-pipeline tools are kept outside the
Unity project so they do not become runtime dependencies.

## 1. Development setup

1. Install Unity `6000.0.32f1` with Android Build Support, Android SDK/NDK and
   OpenJDK modules through Unity Hub.
2. Install Git LFS and run `git lfs install` before cloning/pulling binary assets.
3. Open `unity-project/` in Unity Hub. Run **Battle Horizons > Project Setup >
   Configure** to create the URP assets, materials, scenes and build settings.
4. Use the MainMenu or PlayerTest scene for play-mode testing.

## 2. Branches and commits

Use a short-lived feature branch for normal contributions and open a pull request
to the repository's integration branch. Keep commits scoped and use imperative
Conventional Commit subjects, for example:

```text
feat(world): clip streamed tiles to a 50 m disk
fix(android): fail when release signing material is incomplete
docs(build): explain the GameCI APK workflow
```

Do not commit generated Unity `Library/`, local APKs, AABs, Unity license files or
Android keystores. The root `.gitignore` excludes them.

## 3. Verification before a pull request

These checks run without a Unity editor:

```bash
python3 -m pip install tree-sitter tree-sitter-c-sharp
python3 processing/unity/validate_structure.py
python3 processing/unity/validate_csharp.py
python3 processing/unity/validate_unity_map.py
python3 -m unittest discover -s processing/blender/tests -v
```

With Unity installed and licensed, run the EditMode and PlayMode test assemblies,
then **Battle Horizons > Project Setup > Validate**. On a device, also smoke-test
landscape touch input, the circular terrain/water edge, collisions and sustained
frame-time behaviour. Do not report FPS or memory budgets without measurements.

## 4. Unity and Android CI

Unity CI uses two workflows:

* [`.github/workflows/unity.yml`](.github/workflows/unity.yml) runs repository
  checks, validates the committed map data, exercises the Blender pipeline,
  configures Unity and runs EditMode/PlayMode tests. Its editor steps require
  `UNITY_LICENSE`, `UNITY_EMAIL`, and `UNITY_PASSWORD` for Personal, or
  `UNITY_SERIAL`, `UNITY_EMAIL`, and `UNITY_PASSWORD` for Professional. It reports
  an explicit skip when those inputs are missing.
* [`.github/workflows/build.yml`](.github/workflows/build.yml) builds the Android
  APK using Buildalon's online Personal activation and GameCI. It requires
  `UNITY_EMAIL` and `UNITY_PASSWORD`; the activated `.ulf` is passed to GameCI as
  a masked, per-run value rather than saved as a repository secret. It has no
  pull-request trigger because it consumes Unity account credentials.

Run **Build Android APK** manually after confirming the Unity secrets. The build
entry point `BuildCommand.PerformBuild` delegates to the project's configure,
validate and Android APK pipeline. The artifact is uploaded only after a real APK
passes package checks.

Release signing is optional. The workflow accepts either the current or previous
secret names:

* `ANDROID_KEYSTORE_BASE64` or `KEYSTORE_BASE64`
* `ANDROID_KEYSTORE_PASS` or `KEYSTORE_PASS`
* `ANDROID_KEYALIAS_NAME` or `KEYALIAS_NAME`
* `ANDROID_KEYALIAS_PASS` or `KEYALIAS_PASS`

If no signing secrets are set, the result is a debug-signed test APK and must not
be published to a store. The keystore is written temporarily and removed after
the job. Never print, commit or paste secret values into logs or chat.

## 5. Island data and streaming

The Unity Resources under `unity-project/Assets/Resources/Config/` are the map
source of truth. There is no external project or map conversion step. Terrain
source tiles remain 500 m, while a custom URP shader clips terrain, roads and
water to a true 50 m circular visual radius around the focus. The full-map
Terrain renderer is always disabled; its collider remains active.

If map data changes, update its manifest hashes and run
`processing/unity/validate_unity_map.py`. Add or change streaming behaviour with
both an EditMode or PlayMode test and a device visual check for seams.

## 6. Assets

Use the Unity import rules in `Assets/Editor/AssetImportRules.cs` and the pipeline
notes in [`docs/ASSET_PIPELINE.md`](docs/ASSET_PIPELINE.md). Blender output is
reproducible and ignored until reviewed and promoted into `unity-project/Assets/`.
Binary assets use the repository's Git LFS attributes.
