# User Texture & Material Pipeline (AI-generated environment art)

The environment texture set for the POI greybox lives in
`unity-project/Assets/UserAssets/Textures/` and is turned into URP materials by
one editor menu. Everything here is offline and deterministic — the builder
derives normal maps itself, so no external baking tool is needed.

## What is committed

| Texture | File | Use (POI brief) | Material tiling |
| --- | --- | --- | --- |
| Red brick | `T_RedBrick.png` | Village "Family House" walls (Block 17 style) | 4 × 4 |
| White brick | `T_WhiteBrick.png` | Village houses variant (Block 15 style) | 4 × 4 |
| Dark concrete | `T_DarkConcrete.png` | Military Base bunkers, watchtowers | 4 × 4 |
| Grass/dirt | `T_GrassDirt.png` | Ground patches, terrain layer, quarry rim | 6 × 6 |
| Wood planks | `T_WoodPlanks.png` | Doors, fences, dock planking, crate trim | 3 × 3 |
| Roof tiles | `T_RoofTiles.png` | Terracotta roofs on the village houses | 4 × 4 |
| Asphalt road | `T_AsphaltRoad.png` | POI connector roads (village ↔ base ↔ docks) | 2 × 6 (along road) |
| Loot crate | `T_LootCrate.png` | Tier-3 military crate props (hazard stripes) | 1 × 1 |
| Morning sky | `T_SkyboxMorning.png` | Equirectangular 2:1 skybox, soft clouds | panoramic |
| Bullet hole | `Decals/D_BulletHole.png` | Wall impact decal (black-background keyed) | decal |

`Decals/D_BloodSplatter.png` and `Decals/D_DangerSign.png` follow the same
convention (generate/drop them into `Textures/Decals/`, rerun the builder).

## One-menu material build

Unity → **Battle Horizons → Assets → Build Materials From User Textures**.
For every source PNG the builder
(`Assets/Editor/UserTextureMaterialBuilder.cs`):

1. **Derives a normal map** `*_n.png` — Sobel pass over albedo luminance,
   strength 2.0, written next to the source. The import gate
   (`AssetImportRules`) recognises the `_n` suffix and imports it as a
   **linear NormalMap** automatically; you never set Texture Type by hand.
2. **Creates a URP/Lit material** `Assets/UserAssets/Materials/M_<name>.mat`
   with Base Map + Normal Map assigned, `_BumpScale 0.7`, and the committed
   matte house style (`metallic 0`, `smoothness 0.08`) so walls never glare
   like the terrain convention in `UserAssets/README.md`.
3. **Decals**: derives `*_d.png` by keying the black background to alpha
   (alpha = max channel) and builds a **URP/Decal** material.
4. **Skybox**: builds a **Skybox/Panoramic** material (latitude-longitude
   mapping) from `T_SkyboxMorning`.

Then **Battle Horizons → Assets → Apply Morning Skybox** assigns it to the
open scene's `RenderSettings.skybox` (save the scene). The orbit camera
switches its clear flags to Skybox automatically when a skybox exists —
no camera edits needed.

Per-face tiling variants (e.g. one brick material at 4×4 and another at 2×2
for pillar trims): select the material → **Battle Horizons → Assets → Create
Tiled Variant Of Selected Material** → set Main Map tiling in the inspector.

## Mobile import budget — 512², ASTC 6×6 (not 1024²)

The brief asked for a 1024² cap; this repository deliberately enforces
**512 × 512 max** for every non-character texture
(`AssetImportRules.MaxTextureSize`, from `docs/ASSET_PIPELINE.md` and
`docs/UNISOC_BUDGET.md`): a 1024² ASTC 6×6 texture costs the VRAM of four
512²s (~466 KB vs ~116 KB with mips), and the baseline device is a Unisoc
T606 class SoC. The importer applies it on every (re)import, together with:
mipmaps on, anisotropic 0, NPOT→nearest POT, wrap Repeat (decals Clamp),
Android override ASTC 6×6. Source PNGs stay 1024² in git so a future
high-end profile can raise the cap in one place.

The committed character textures are the only sanctioned exception (they keep
their authored 256²–2048² resolution) and the test
`UserAssetPipelineTests.ProductionCharacterScopingIsUnaffectedByDerivedMaps`
locks environment maps out of that exception.

## ProBuilder wall workflow (brick at 4×4, no stretching)

1. ProBuilder window → select faces → **Material Editor** → assign
   `M_T_RedBrick` (or `M_T_WhiteBrick`). The material already carries 4×4
   tiling, so a standard 3 m wall course reads at ~0.75 m per tile.
2. If a wall stretches (non-uniform face size), open **UV Editor** →
   planar project → set the face's UV scale so one brick course ≈ 0.08 m
   tall; never scale the shared material per face — use a *Tiled variant*
   (menu above) when a second scale is genuinely needed.
3. Roofs: assign `M_T_RoofTiles` and rotate UVs 90° in the UV Editor so tile
   rows run down the slope.
4. Roads: build the POI connectors as ProBuilder ribbons (or quads on the
   terrain) with `M_T_AsphaltRoad`; set tiling Y along the road length.

## Terrain painting (grass/dirt)

Two cases in this repo:

* **Siran Island (shipping map)** — the ground is *not* a Unity `Terrain`
  paint surface; it is the procedural chunk mesh with committed vertex
  colours (`SiranRadiusClip` shader, `drawHeightmap` disabled — see
  `docs/architecture/UNITY_ARCHITECTURE.md`). Use `T_GrassDirt` on ground
  *props*: ProBuilder ground patches, quarry floors, compound slabs, and the
  terrain-adjacent set dressing. Painting over the procedural ground would
  break the 50 m detail-disk contract.
* **A hand-built 2×2 km terrain** (the from-scratch option in
  `docs/gameplay/MAP_POI_LAYOUT.md`) — Terrain Inspector → **Paint
  Texture** → Edit Terrain Layers → Add Layer → albedo `T_GrassDirt`
  (normal map: the derived `T_GrassDirt_n`, tile 6×6), then paint dry grass
  on slopes/hills and greener patches on flat ground with a soft brush at
  ~0.4 opacity. Keep ≤ 4 terrain layers total on mobile.

## Water

The island water already animates with the committed
`Assets/Resources/Art/Textures/water_normal_256.png` normal map inside
`SiranIslandWater`/its URP shader — do not replace it with the skybox
texture. For custom water bodies (village pond, dock basins) reuse that same
normal map on a URP/Lit material with `Smoothness ≈ 0.9`, `Metallic ≈ 0`,
Base Color teal-blue, and Normal Map scale ≈ 0.25.

## URP Decal Projector setup (bullet holes, blood, DANGER signs)

1. Select the active **URP Renderer asset** (created by Project Setup) →
   Add Renderer Feature → **Decal**. Technique: **Screen Space** on Low
   profile (cheapest), DBuffer only if you need normal-blended wall decals
   on High.
2. Scene: GameObject → Light → **Decal Projector**. Assign
   `M_D_BulletHole` (URP/Decal shader from the builder).
3. Orient the projector box into the wall/ground, size ≈ 0.35 × 0.35 m for
   bullet holes, ≈ 0.8 m for blood splatter; set `Draw Distance` ≈ 40 m so
   distant decals cost nothing.
4. The Military Base **DANGER** sign is best as a quad/plane with its own
   material (a readable sign needs geometry, not a floor decal); the builder
   handles it identically once `D_DangerSign.png` lands in `Textures/Decals/`.
5. Runtime impact decals: the hook point is `WeaponController.FireShot()` —
   pool `DecalProjector`s through `BattleHorizons.Core.ObjectPool` (budget
   rule: no per-shot allocation) and place at `hit.point` aligned to
   `hit.normal`. Deliberately not wired yet; it lands with the impact-VFX
   pass so the Low profile keeps its 40 m draw-distance budget.

## Screenshots & honest evidence

This repository only claims engine evidence that a licensed Unity editor
actually produced (`docs/fbx_production_verification/` etc.). The CI here has
no Unity licence, so **no in-engine terrain/house screenshot can be produced
from this environment**. To capture them locally:

1. Open `unity-project/` in Unity Hub (6000.0.85f2) → run
   **Battle Horizons → Project Setup → Configure**, then
   **Battle Horizons → Assets → Build Materials From User Textures** and
   **Apply Morning Skybox**.
2. Open the generated `SiranIsland_Greybox` scene, drop one ProBuilder house
   with `M_T_RedBrick` walls + `M_T_RoofTiles` roof, and take the Game-view
   capture. Share it in the PR description; the repo convention is that
   device/editor captures live under `docs/` with their validator report.
