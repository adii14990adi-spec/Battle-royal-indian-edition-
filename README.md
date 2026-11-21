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
`unity-project/Assets/Resources/Config/`. It describes a 2 km island on a 401 ×
401 height/color grid at 5 m spacing, partitioned into 250 m source tiles.

The ground is drawn as two complementary levels of detail, both matte
(`metallic 0`, `smoothness 0`) and both cut by the same world-space circle:

* **inside 50 m** — the streamed 250 m tiles at the full 5 m sample spacing
  (only tile rectangles intersecting the circle are resident). This is also the
  surface the `TerrainCollider` matches, so the player never floats or sinks;
* **outside 50 m** — one static 10 m full-island shell (201 × 201 vertices,
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
  points on a coastal ring (~760 m radius), water-rejected against the sea,
  lake and river, placed by `PlayerSpawnController`.
* **Safe zone** (`Gameplay/Match/SafeZoneController`) — seeded shrinking-circle
  state machine (wait → shrink × 6 phases ≈ 16.8 min to a ~37 m final circle),
  ramping 0.4 → 12 HP/s damage outside the rim through `IDamageable`, with a
  greybox cylinder wall and Scene-view gizmos.
* **Character visuals** (`Scripts/Characters`) — `PlayerCharacterVisual`
  spawns the configured Male/Female model under the player and hides the
  capsule, `CharacterPreviewController` renders it live into the Character
  Bay and the Home Screen showcase, and `CharacterBayMenu` builds the modal in
  code. **The animation system was removed on 2026-11-20 at the owner's
  request**: the 74 Mixamo FBX clips under `Assets/Animations/`, the generated
  Animator Controllers, the category/clip config data and the IK + procedural
  motion components (`FootIK`, `HeadLookIK`, `WeaponHandIK`, `ProceduralLean`,
  `CharacterMotionRig`, `PlayerLocomotion`) are gone, so characters hold their
  static imported pose while `ThirdPersonMotor` moves them. `Assets/Animations/`
  must stay free of FBX files — the validators fail if a clip reappears.
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

## Environment textures & material pipeline

`unity-project/Assets/UserAssets/Textures/` carries the AI-generated POI
texture set (red/white brick, dark concrete, grass/dirt, wood, terracotta
roof tiles, asphalt, hazard-striped loot crate, equirectangular morning
skybox, decals). **Battle Horizons > Assets > Build Materials From User
Textures** derives Sobel normal maps (`*_n.png`, imported as linear
NormalMaps), keys black-background decals to alpha, and writes URP
Lit/Decal/Panoramic materials with the matte house style and the brief's
4×4 brick tiling. The import gate keeps every environment texture at
512² ASTC 6×6 on Android per the UNISOC budget. Guide:
[`docs/asset-pipeline/USER_TEXTURE_PIPELINE.md`](docs/asset-pipeline/USER_TEXTURE_PIPELINE.md).

## Offline and mobile settings

The Android target is ARM64, landscape, ASTC-compressed and does not request a
forced INTERNET permission. Low and High quality profiles adjust render scale,
shadows, view distance and frame pacing; both keep the same 50 m detail-disk
radius. These
are project settings and targets, not proof of device performance. See
[`docs/performance/ANDROID_PROFILES.md`](docs/performance/ANDROID_PROFILES.md) and
[`docs/UNISOC_BUDGET.md`](docs/UNISOC_BUDGET.md).

## Home screen (code-built lobby)

The lobby is 100% procedural: `LobbyArtFactory` paints the sunset vista,
panels, chips and every glyph at runtime and `MainMenuController` lays them
out as real widgets (logo, profile/coin/gem chips, Store…Events nav, loadout
bar, Solo/Duo/Squad selector, golden START). No baked UI artwork — see
[`docs/gameplay/HOME_SCREEN.md`](docs/gameplay/HOME_SCREEN.md).

## Validation commands

```bash
python3 -m pip install tree-sitter tree-sitter-c-sharp
python3 processing/unity/validate_structure.py
python3 processing/unity/validate_csharp.py
python3 processing/unity/validate_fbx_production.py         # production character FBX + protection
python3 processing/unity/validate_gltf_import_test.py       # isolated GLTF import experiment
python3 processing/unity/validate_unity_map.py
python3 -m unittest discover -s processing/unity/tests -v
python3 -m unittest discover -s processing/blender/tests -v
```

`validate_fbx_production.py` reads the FBX bytes directly (no Unity, no
third-party packages) and prints `PASS` / `FAIL` / `NOT VERIFIED`. A
`NOT VERIFIED` row is never counted as a pass. Unity editor tests and Android
device measurements require Unity and a device.

## Animation status

There is no animation system in this project any more. On 2026-11-20 the owner
asked to remove all animations, so the 74 Mixamo FBX clips under
`unity-project/Assets/Animations/`, their `.meta` importer settings, the clip
import service, the generated Animator Controllers, the IK/procedural-motion
components and the `.fbx.meta` metadata-sync workflow were all deleted.
Character models still import as Humanoid (that rig check stays), but nothing
plays: the character holds its imported pose while the motor moves the
transform. Static validators now assert that `Assets/Animations/` contains no
FBX and that no runtime script references an `Animator` or an `AnimationClip`.

See [`CONTRIBUTING.md`](CONTRIBUTING.md),
[`docs/architecture/UNITY_ARCHITECTURE.md`](docs/architecture/UNITY_ARCHITECTURE.md),
and [`docs/ASSET_PIPELINE.md`](docs/ASSET_PIPELINE.md) for details.
