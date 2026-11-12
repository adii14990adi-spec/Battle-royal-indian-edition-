# Unity 6 + URP architecture (Android-first)

This document describes the Unity project in `unity-project/`: where its data
lives, how the world is assembled, and how the 50 m visual streaming contract is
implemented. It documents code and configuration, not device performance. See
`docs/performance/ANDROID_PROFILES.md` for the explicit verification limits.

---

## 1. Project and assemblies

The project targets Unity `6000.0.85f2`, URP, Android ARM64 and OpenGLES3/Vulkan.
`Assets/Editor/ProjectBootstrap.cs` creates the URP assets, materials and runtime
scenes when **Battle Horizons > Project Setup > Configure** is run. The scenes
are generated project setup, not authored map data.

| Assembly | Path | Purpose |
| --- | --- | --- |
| `BattleHorizons.Runtime` | `Assets/Scripts/**` | Runtime systems included in the player |
| `BattleHorizons.Editor` | `Assets/Editor/**` | Setup, validation, import rules and Android builds |
| `BattleHorizons.Tests.EditMode` | `Assets/Tests/EditMode` | Geometry and settings tests |
| `BattleHorizons.Tests.PlayMode` | `Assets/Tests/PlayMode` | World, physics, movement and camera tests |

Editor code references runtime code; runtime code never references `UnityEditor`.

---

## 2. Runtime object graph

```text
Boot.unity
└── GameBootstrap                 applies device quality and loads the next scene
    └── PerformanceMonitor        persistent device telemetry

MainMenu.unity
└── MainMenuController            title, quality toggle, scene buttons

SiranIsland.unity / PlayerTest.unity
└── SiranWorldBuilder             creates the world from committed Resources
    ├── SiranIslandData           loads and validates binary map data
    ├── Terrain + TerrainCollider heightmap collision; Terrain rendering hidden
    ├── SiranIslandOverview       20 m full-island shell, drawn outside the 50 m disk
    ├── SiranChunkStreamer        resident 500 m tiles intersecting a 50 m disk
    ├── SiranRadiusClip shader    clips the two terrain LODs and the roads
    ├── Roads + SiranIslandWater  matte ribbons, sea, basin-shaped lake and river
    └── PlayerTest only: motor, camera rig, touch controls and debug HUD
```

The world builder loads the committed map data at runtime. No separate map bake
or source project is needed to open, test or build the Unity project.

---

## 3. Committed island data

`Assets/Resources/Config/` contains the runtime source of truth:

* `siran_island_terrain.json` — schema, grid and height metadata;
* `siran_island_heights.bytes` — 401 × 401 little-endian float32 heights;
* `siran_island_colors.bytes` — 401 × 401 RGBA8 vertex colours;
* `siran_island_roads.bytes` — `BHROAD01` road ribbon directory and payload;
* `siran_island_river.bytes` — `BHRIVER1` river vertices and indices;
* `siran_island_layout.json` — human-readable island design snapshot;
* `siran_island_manifest.json` — byte counts and SHA-256 integrity values.

The map covers 4,000 × 4,000 m at 10 m sample spacing. Its 8 × 8 tiles are
500 m wide and share their boundary samples. The Unity data check is independent
of the Unity editor:

```bash
python3 processing/unity/validate_unity_map.py
```

It verifies hashes, dimensions, finite height values, road/river binary indices,
river direction and the 50 m radius value.

---

## 4. World construction and collision

`SiranWorldBuilder.Build()` loads data, creates a 513 × 513 Unity `TerrainData`,
then assembles the heightmap collider, the full-island shell, road ribbons, water
surfaces and streamer. The single `TerrainCollider` covers the full 4 km map;
collision is not unloaded at the 50 m detail boundary.

The Terrain component's `drawHeightmap` is always disabled. Leaving the Unity
Terrain renderer on would draw a second full-map surface over the meshes (and
URP's Terrain/Lit output on a runtime-generated layer reads as a flat white
sheet). Its full-map collider remains active for gameplay.

Visible terrain is therefore built from the same height grid in two LODs:

* `SiranIslandTerrainBuilder.BuildOverviewMesh` samples every 20 m (201 × 201
  vertices, 80,000 triangles) over the whole island for the shell that is visible
  beyond the detail disk;
* `SiranIslandData.BuildChunkMesh` samples every 10 m for the 500 m tiles inside
  it, matching the 10 m source data the collider is built from.

Both use up-facing clockwise triangles, the baked grass / sand / dirt palette as
vertex colours, and the same matte material settings (`metallic 0`,
`smoothness 0`). The shader converts the palette vertex colours from sRGB to
linear; raw vertex colours are not colour-corrected by Unity in a Linear-space
project, which is what previously washed the ground out to near white.

---

## 5. Circular streaming (50 m over 500 m tiles)

`SiranChunkStreamer` implements a world-space circular radius, not a fixed square
ring:

1. Every 0.2 s it checks candidate tile rectangles around the focus transform.
2. A tile is resident only if the closest point on its 500 × 500 m rectangle is
   at most 50 m from the focus. Depending on the focus and grid boundaries, one
   to four source tiles can intersect the disk.
3. Resident tile meshes are pooled and built from the shared 401 × 401 sample
   grid. The set is recomputed even when the focus has not changed grid cells,
   so a neighbouring tile is added/released as the circle crosses a tile edge.
4. `SiranRadiusClip.shader` clips fragments using world XZ distance to the focus.
   This trims the large source tiles to the actual circular boundary; loading a
   500 m tile does not make its other 450 m visible.
5. The same shader also draws the 20 m island shell with the **inverse** clip
   (outside the circle only). The two terrain LODs therefore meet at the same
   world-space boundary without overlapping, z-fighting or interpenetrating.
6. Road ribbons follow the 10 m grid, so they use the same inside-the-disk clip
   as the detail tiles. The sea, the basin-shaped lake mesh and the river ribbon
   use mode 0 (no clip) so the coast is visible to the horizon.
7. The shader focus is updated every frame; set membership is updated on the
   amortized streaming tick. The radius is fixed across Low and High quality.

`QualityProfiles.StreamRadius()` returns 50 m for both profiles. The debug HUD
reports live resident chunk counts. Automated play-mode tests cover edge
intersection and moving within the same 500 m tile; the shader still needs an
actual Unity render/build pass and device visual check.

---

## 6. Rendering and Android settings

`ProjectBootstrap.Configure()` writes the project settings and creates the
shared URP materials. Every visible terrain tile, the island shell, road ribbons
and water surfaces use `BattleHorizons/Radius Clipped Lit`, which clips fragments
in world space before shading (or not at all for water). The shader multiplies
terrain/road vertex colours by simple URP lighting, keeps the only specular term
proportional to smoothness — so the matte ground has none — and applies an
optional scrolling water normal map. Water uses smoothness `0.28` instead of the
old near-mirror `0.92`. There is no post-processing volume and the camera's
post-processing flag is disabled, so no Bloom can blow the sun-lit ground out to
white. Terrain and road chunks do not cast shadows; fog colour matches the camera
background so the horizon is seamless.

The generated Android settings include IL2CPP, ARM64, minimum API 26, landscape
orientation, ASTC texture compression, Vulkan with OpenGLES3 fallback, Linear
colour space and no forced INTERNET permission. Low and High are distinct
quality profiles; they adjust render scale, shadows and frame pacing but do not
change the world streaming radius.

Project setup and validation entry points:

```text
Battle Horizons > Project Setup > Configure
Battle Horizons > Project Setup > Validate
Battle Horizons > Build > Android APK
```

The APK is debug-signed unless a complete release keystore is provided through
the documented `BH_KEYSTORE_*` environment variables. Never commit signing
material.

---

## 7. Input, camera and diagnostics

`PlayerInputRouter` normalizes touch, gamepad and keyboard input. The
`ThirdPersonMotor` handles walk/run/jump/crouch using a `CharacterController`;
`ThirdPersonCameraController` follows the target and pushes in when an
obstruction is hit. `MobileControlsBuilder` creates virtual sticks, a full-screen
look surface and hold buttons at runtime.

`PerformanceMonitor` caches frame-time samples, ProfilerRecorder values, managed
heap use and Unity-reserved memory. `WorldDebugHud` displays cached information
in development/editor builds. Unity-reserved memory and managed heap are not
Android process PSS; use `adb shell dumpsys meminfo` for PSS. No frame-rate,
thermal or memory result is claimed without a device run.

---

## 8. Deliberate scope limits

Networking, matchmaking, bots, weapons, inventory, placed buildings/vegetation,
production audio and a shippable character asset are not implemented. Blender
scripts provide a reusable generation/validation pipeline, not a placed game
world or finished character art. See the repository README and asset-pipeline
docs for the current boundaries.
