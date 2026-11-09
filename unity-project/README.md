# Unity 6 project

This is the Unity project used by the repository. Editor version: `6000.0.32f1`;
render pipeline: URP; Android target: ARM64.

## First open

1. Open this directory in Unity Hub with Android Build Support installed.
2. Run **Battle Horizons > Project Setup > Configure**.
3. Open and play the generated MainMenu or PlayerTest scene.

Project setup generates its URP assets, materials and scenes. The island data is
committed under `Assets/Resources/Config/`; no external map source is needed.

## Android APK

Use **Battle Horizons > Build > Android APK** after setup. The default output is
`Builds/battle-horizons.apk`. The local build is debug-signed unless release
signing environment variables are supplied; do not publish a debug APK.

CI is defined in `../.github/workflows/unity.yml` and uses GameCI. It configures
the project and runs EditMode/PlayMode tests when a Unity license secret is
available. A manual workflow dispatch with `build_apk: true` requests an Android
test package. Static data, repository and C# checks run separately without Unity.

## World and streaming

The committed map is a 4 km island with 500 m source tiles. The streamer retains
tiles whose rectangles intersect a fixed 50 m circle around its focus. The
`BattleHorizons/Radius Clipped Lit` URP shader clips terrain, roads and water to
the circle. Unity Terrain remains as a collider, but its full-map renderer is
always disabled so it cannot bypass the visual boundary.

## Layout

```text
Assets/Editor/      project setup, validation, import rules and Android build
Assets/Resources/   committed map data and textures
Assets/Scripts/     runtime world, player, camera, UI and diagnostics
Assets/Shaders/     URP shaders, including the radial stream clip
Assets/Tests/       EditMode and PlayMode tests
Packages/           Unity package manifest and lock
ProjectSettings/    Unity project and Android settings
```

See the root `README.md`, `CONTRIBUTING.md` and `docs/architecture/UNITY_ARCHITECTURE.md`.
