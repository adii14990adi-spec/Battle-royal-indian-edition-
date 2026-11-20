# Map & POI Layout — Battle Horizons: Last Survive

Task 1 of the gameplay-foundation milestone: terrain creation steps and the
point-of-interest (POI) plan with its loot-tier contract.

## The map this repository ships

The playable map is **Siran Island**, procedurally generated from the committed
data in `unity-project/Assets/Resources/Config/` — not a hand-sculpted Unity
`Terrain`. Canonical constants live in
[`SiranMapConfig.cs`](../../unity-project/Assets/Scripts/World/SiranMapConfig.cs):

| Property | Value |
| --- | --- |
| Map size | **2 × 2 km** (`MapSizeM = 2000`), centred on the origin — the 2.0 rebake |
| Island radius | 975 m coastline (noise ±85 m) |
| Chunk streaming | 250 m tiles, 50 m full-detail disk around the player |
| Height range | −12 m … 23.3 m, sea level 0 m |
| Water | Sea, Siran Lake (inland), Siran River, Iron Mill channel |

The original brief asked for a 2 × 2 km terrain, and the 2.0 rebake
(`processing/unity/generate_siran_island.py`) delivers exactly that: the whole
island now IS the 2 × 2 km battlefield at 5 m height resolution. POI positions
below are the rebaked coordinates; the tier contract is unchanged.

## POI plan (mapped onto Siran Island zones)

The loot system resolves a container's POI through `SiranMapConfig.ZoneAt(x, z)`
using the zone ids below, so **POI identity = zone id** everywhere in code
(`DefaultLootContent.ZoneMilitaryBase == "central_state"`, …).

| POI (brief) | Siran zone id | Position | Loot tier cap | Notes |
| --- | --- | --- | --- | --- |
| **Military Base** (centre) | `central_state` | (20, −15), 575 × 575 m | **Tier 3** | High-tier loot only here: assault rifles, snipers, riot helmets. Build concrete bunkers, watchtowers, parade ground with ProBuilder inside the Central State urban pad. |
| **Abandoned Village** (west→NW) | `factory_island` | (−625, −650), 380 × 320 m | Tier 2 | 15 modular houses: reuse one ProBuilder house prefab, vary rotation/mirror. Medium-tier loot + bandages. |
| **Forest & Quarry** (east) | `graveyard_center` | (675, −40), 410 × 450 m | Tier 1 (resources only) | Resource nodes: Wood and Stone clusters (`res_wood`, `res_stone`, `res_scrap_metal`). Trees must carry LOD Groups (see MOBILE_OPTIMIZATION.md). |
| **Docks** (brief: south → actual: west coast) | `konkan_docks` | (−735, 65), 390 × 270 m | Tier 2 | Boat spawns and water access already exist on Siran's west coast; the southern pad is the Airfield instead. |
| **Airfield Hangars** (south, map-specific extra) | `aircraft_industry` | (60, 740), 510 × 370 m | Tier 2 | Hangars + runway; second mid-tier hotspot so the south is not empty. |

Deviation from the brief, stated explicitly: Siran Island's water POI (Konkan
Docks) is on the **west** coast and the **south** pad is the airfield. The tier
contract from the brief is preserved exactly: Tier 3 = Military Base only,
Tier 1/2 = village, resources = Forest & Quarry.

### ProBuilder greybox workflow (per POI)

1. Package Manager → install **ProBuilder** (not yet in `manifest.json`; it is
   an editor-side greybox tool, keep it out of the shipping build).
2. Tools → ProBuilder → ProBuilder Window → **New Shape**.
3. Military Base: box shells 12 × 4 × 8 m for barracks, extrude 8 m watchtowers
   with a 2 m parapet; Merge Faces on the ground slab to keep one draw call per
   building.
4. Village: build **one** 6 × 3 × 5 m house, then Tools → ProBuilder →
   **Vertices/Mirror** copies; 15 instances via prefab variant, never 15
   unique meshes.
5. Add a `LootContainer` component to each building root (or an empty
   "LootVolume" child) and size its bounds to the interior. The container
   resolves its zone — and therefore its tier cap — automatically from its
   world position.
6. Export finished greybox to OBJ/GLTF into `Assets/UserAssets/Models/` when it
   is time for art; the spawner does not care what the visual is.

## Recreating a dedicated 2 × 2 km terrain from scratch (Unity Terrain tool)

Only needed if the project ever moves off the procedural Siran pipeline:

1. GameObject → 3D Object → **Terrain**. In Terrain Settings set Width/Length =
   **2000**, Height = 150; Position = (−1000, 0, −1000) so the map is
   centred on the origin like Siran.
2. Heightmap Resolution 1025 × 1025 (≈2 m/sample), Detail Resolution 512.
3. Paint Height: raise a central plateau (Military Base, ~40 m), gentle western
   terraces (village), eastern ridge (quarry rim), flatten the south to sea
   level (docks). Use **Smooth** between biomes.
4. Paint Texture: 4 terrain layers max on mobile (grass, rock, sand, concrete) —
   each extra layer is a full texture sample per pixel.
5. Trees: Terrain → Tree Prototype must have a **LOD Group** with 3 LODs and a
   billboard; set Tree Distance ≈ 120 m, Billboards ≈ 60.
6. Bake a TerrainCollider-aware NavMesh only if bots arrive; the current player
   uses `CharacterController` + the terrain collider directly.

## Where the systems live

```
unity-project/Assets/Scripts/
├── Core/
│   └── RandomStream.cs          # seeded xoshiro128** — the ONLY RNG gameplay may use
└── Gameplay/
    ├── Loot/                    # Task 2a: items, tables, zone rules, spawner
    │   ├── ItemDefinition.cs    #   ScriptableObject: id, category, tier, stack, prefab
    │   ├── LootTable.cs         #   weighted bag, rolls honour tier cap + category filter
    │   ├── LootSpawnConfig.cs   #   seed + per-zone LootZoneRule (the tier contract)
    │   ├── DefaultLootContent.cs#   code-authored starter set (15 items, 4 tables)
    │   ├── LootContainer.cs     #   building-bounds marker + static registry
    │   ├── LootPickup.cs        #   world item instance, trigger pickup, Collected event
    │   └── LootSpawner.cs       #   rolls every container from the match seed
    ├── Match/                   # Task 2b/2c: spawns + safe zone
    │   ├── IDamageable.cs       #   health contract ("safe_zone" damage source)
    │   ├── PlayerSpawnService.cs#   50 deterministic edge spawn points (pure math)
    │   ├── PlayerSpawnController.cs # scene wrapper: seed → ring → local player
    │   ├── SafeZoneMath.cs      #   circle placement/lerp/containment (pure math)
    │   ├── SafeZoneSettings.cs  #   ScriptableObject phase schedule
    │   └── SafeZoneController.cs#   wait/shrink state machine, damage, greybox wall
    └── Combat/                  # shooting milestone
        ├── WeaponData.cs        #   ScriptableObject gun stats (damage/RPM/mag/ADS/recoil)
        ├── DefaultWeaponContent.cs # code stats for the 4 loot guns
        ├── WeaponRuntime.cs     #   pure fire-rate/mag/reload machine (clock injected)
        ├── RecoilModel.cs       #   seeded procedural recoil + recovery
        ├── CombatPlaceholderFx.cs # procedural muzzle/gunshot/tracer (zero assets)
        ├── HealthComponent.cs   #   reference IDamageable for players & dummies
        └── WeaponController.cs  #   equip-from-loot, ADS FOV, hitscan, camera kick
```

Related docs: [LOOT_SPAWNING.md](LOOT_SPAWNING.md),
[SHOOTING_MECHANICS.md](SHOOTING_MECHANICS.md),
[SAFE_ZONE_AND_SPAWNS.md](SAFE_ZONE_AND_SPAWNS.md),
[MOBILE_OPTIMIZATION.md](MOBILE_OPTIMIZATION.md),
[../architecture/UNITY_ARCHITECTURE.md](../architecture/UNITY_ARCHITECTURE.md).
