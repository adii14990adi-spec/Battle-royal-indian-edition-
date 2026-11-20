<p align="center">
  <img src="unity-project/Assets/Resources/UI/battle_horizons_logo.png" width="220" alt="Battle Horizons Logo" />
</p>

# Battle Horizons: Last Survive

Offline Android battle-royale prototype built with **Unity 6 `6000.0.85f2` and URP**.
The only game-engine project in this repository is `unity-project/`.

> No on-device FPS, memory or thermal result is claimed. A test APK is considered
> available only when a Unity build actually completes and uploads an artifact.

## Project setup

1. Install Unity `6000.0.85f2` with Android Build Support (SDK/NDK and OpenJDK).
2. Clone with Git LFS enabled, then open `unity-project/` in Unity Hub.
3. Run **Battle Horizons > Project Setup > Configure**. This creates the URP
   assets, materials, runtime scenes, Android settings and a black startup/loading
   screen.
4. Play the generated MainMenu or PlayerTest scene.

## Build an Android test APK

From the Unity editor, use **Battle Horizons > Build > Android APK** after project
setup. The default output is `unity-project/Builds/battle-horizons.apk`; without a
release keystore it is debug-signed for testing only.

GitHub Actions provides two GameCI workflows:

* [`.github/workflows/unity.yml`](.github/workflows/unity.yml) runs repository,
  map-data and Blender checks, then configures/tests Unity when a supported
  license is present. Its editor steps require `UNITY_LICENSE`, `UNITY_EMAIL`,
  and `UNITY_PASSWORD` for Personal, or `UNITY_SERIAL`, `UNITY_EMAIL`, and
  `UNITY_PASSWORD` for Professional. Email/password alone are not enough.
* [`.github/workflows/build.yml`](.github/workflows/build.yml) builds the Android
  APK with an online Personal activation. It requires `UNITY_EMAIL` and
  `UNITY_PASSWORD`; Buildalon creates the runner's `.ulf` and passes its masked
  contents to GameCI at runtime, so this workflow does not need a stored
  `UNITY_LICENSE` secret. It has no pull-request trigger because it consumes
  Unity account credentials.

Run **Build Android APK** manually after configuring the Unity secrets. Optional
release signing accepts `ANDROID_KEYSTORE_BASE64`, `ANDROID_KEYSTORE_PASS`,
`ANDROID_KEYALIAS_NAME`, and `ANDROID_KEYALIAS_PASS`; the previous `KEYSTORE_*`
secret names are also accepted. If signing values are omitted, the result is a
debug-signed test APK and must not be published to a store. Pushing a `v*` tag
creates or updates a GitHub Release named **Battle Horizons: Last Survive (Build
#N)** and attaches the signed APK; this release step is skipped unless all four
signing secrets are present. The build name remains `BattleHorizons`, and the
workflow artifact is named `BattleHorizons-Android-APK-N`.

## Siran Island, terrain LODs and 50 m streaming

The committed Unity data lives under
`unity-project/Assets/Resources/Config/`. It describes a 4 km island on a 401 ×
401 height/color grid, partitioned into 500 m source tiles.

The ground is drawn as two complementary levels of detail, both matte
(`metallic 0`, `smoothness 0`) and both cut by the same world-space circle:

* **inside 50 m** — the streamed 500 m tiles at the full 10 m sample spacing
  (only tile rectangles intersecting the circle are resident). This is also the
  surface the `TerrainCollider` matches, so the player never floats or sinks;
* **outside 50 m** — one static 20 m full-island shell (201 × 201 vertices,
  80,000 triangles, a single draw call) that shows the hills, flat zone pads,
  coastline and inland lake out to the view distance.

The `SiranRadiusClip` shader draws the detail disk only inside the circle and the
shell only outside it, so the two never overlap or z-fight. The Unity `Terrain`
renderer (`drawHeightmap`) stays disabled; its heightmap collider covers the
whole map. The sea, lake and river are unclipped so the island reads as an island
rather than a floating circle. See
[`docs/architecture/UNITY_ARCHITECTURE.md`](docs/architecture/UNITY_ARCHITECTURE.md).

Your own buildings, trees and props go in `unity-project/Assets/UserAssets/Models`
and `.../Materials`; nothing is spawned there by the project.

The map data is authoritative in Unity; no separate engine project or conversion
step is required. Validate the committed blobs and hashes with:

```bash
python3 processing/unity/validate_unity_map.py
```

## Gameplay systems: loot, spawns and the safe zone

The match foundations live in `unity-project/Assets/Scripts/Gameplay/`:

* **Loot** (`Gameplay/Loot`) — ScriptableObject items, weighted loot tables and
  a per-POI `LootSpawnConfig`. Tier contract: Tier 3 (snipers/assault rifles)
  spawns only in the Military Base (`central_state`); the Abandoned Village and
  Docks cap at Tier 2; the Forest & Quarry is resources only. `LootSpawner`
  rolls every `LootContainer` (building bounds) from the match seed; greybox
  tier-coloured cubes stand in until art prefabs exist. A starter set of 15
  items and 4 tables is authored in code (`DefaultLootContent`) and can be
  persisted as editable assets via **Battle Horizons > Gameplay > Create Default
  Loot Assets**.
* **Spawns** (`Gameplay/Match/PlayerSpawnService`) — 50 deterministic drop
  points on a coastal ring (~1520 m radius), water-rejected against the sea,
  lake and river, placed by `PlayerSpawnController`.
* **Safe zone** (`Gameplay/Match/SafeZoneController`) — seeded shrinking-circle
  state machine (wait → shrink × 6 phases ≈ 16.8 min to a ~37 m final circle),
  ramping 0.4 → 12 HP/s damage outside the rim through `IDamageable`, with a
  greybox cylinder wall and Scene-view gizmos.
* **Combat** (`Gameplay/Combat`) — hitscan shooting: `WeaponData` stats per
  loot gun (itemId-keyed), a pure fire-rate/magazine/reload state machine
  (`WeaponRuntime`), ADS with a seeded 60° → 45° FOV pull-in, deterministic
  procedural recoil with recovery bleed (`RecoilModel` → camera `AddLook`),
  `HealthComponent` (the shared `IDamageable` for bullets and the blue zone)
  and zero-asset placeholder FX (procedural muzzle flash, synthesised gunshot
  PCM, tracer). Weapons equip straight off the `LootPickup.Collected` event,
  and a touch FIRE button joined the generated mobile rig. See
  [`docs/gameplay/SHOOTING_MECHANICS.md`](docs/gameplay/SHOOTING_MECHANICS.md).

All randomness goes through the seeded `Core/RandomStream` (never
`UnityEngine.Random`), so one match seed reproduces loot, drops and zone
shifts exactly — enforced by `validate_csharp.py` and by
`Assets/Tests/EditMode/GameplaySystemsTests.cs`. See
[`docs/gameplay/`](docs/gameplay/MAP_POI_LAYOUT.md) for the POI layout, loot,
safe-zone and mobile-optimization guides.

## Offline and mobile settings

The Android target is ARM64, landscape, ASTC-compressed and does not request a
forced INTERNET permission. Low and High quality profiles adjust render scale,
shadows, view distance and frame pacing; both keep the same 50 m detail-disk
radius. These
are project settings and targets, not proof of device performance. See
[`docs/performance/ANDROID_PROFILES.md`](docs/performance/ANDROID_PROFILES.md) and
[`docs/UNISOC_BUDGET.md`](docs/UNISOC_BUDGET.md).

## Validation commands

```bash
python3 -m pip install tree-sitter tree-sitter-c-sharp
python3 processing/unity/validate_structure.py
python3 processing/unity/validate_csharp.py
python3 processing/unity/validate_character_animation.py   # FBX / .meta / GUID / clips / rigs
python3 processing/unity/validate_unity_map.py
python3 -m unittest discover -s processing/unity/tests -v
python3 -m unittest discover -s processing/blender/tests -v
python3 -m unittest discover -s processing/animation-metadata/tests -v
```

`validate_character_animation.py` reads the FBX bytes directly (no Unity, no
third-party packages) and prints `PASS` / `FAIL` / `SKIP`. `SKIP` is an
Editor-only check and is never counted as a pass. Unity editor tests and
Android device measurements require Unity and a device. See
[`docs/verification/UNITY_CHARACTER_ANIMATION_VERIFICATION.md`](docs/verification/UNITY_CHARACTER_ANIMATION_VERIFICATION.md)
for the full verification status and the remaining manual tests.

## Animation metadata

The Unity importer settings for the Mixamo clips in
`unity-project/Assets/Animations/` are authored in the public companion
repository
[`Animation-.meta-`](https://github.com/adii14990adi-spec/Animation-.meta-) and
synced here by exact file name. The
[`Sync animation metadata`](.github/workflows/sync-animation-metadata.yml)
workflow polls it daily and on demand, and proposes the result as a pull request
on `chore/sync-animation-metadata`; nothing is ever pushed to `main` and no
`.fbx` is modified. See
[`docs/asset-pipeline/ANIMATION_METADATA_SYNC.md`](docs/asset-pipeline/ANIMATION_METADATA_SYNC.md).

See [`CONTRIBUTING.md`](CONTRIBUTING.md),
[`docs/architecture/UNITY_ARCHITECTURE.md`](docs/architecture/UNITY_ARCHITECTURE.md),
and [`docs/ASSET_PIPELINE.md`](docs/ASSET_PIPELINE.md) for details.
