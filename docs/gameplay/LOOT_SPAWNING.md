# Loot Spawning & Tier Logic

The loot layer is ScriptableObject-driven with a code-authored default, and it
is **deterministic**: one seed produces the identical loot layout on every
device, which `validate_csharp.py` (rule 8) enforces by banning unseeded
`UnityEngine.Random`/`System.Random`. All rolls go through
[`RandomStream`](../../unity-project/Assets/Scripts/Core/RandomStream.cs)
(xoshiro128**, splitmix32 seeding).

## Data model

```
LootSpawnConfig (ScriptableObject)
├── seed                     # match loot seed (default 20261119)
├── defaultMaxTier           # cap outside mapped zones (Tier 1)
├── defaultTable             # fallback LootTable
└── zoneRules[]              # one LootZoneRule per POI zone id
    ├── zoneId               # "central_state", "factory_island", …
    ├── poiName              # "Military Base" (HUD/minimap label)
    ├── maxTier              # HARD tier cap  ← the contract
    ├── allowedCategories[]  # empty = all; Forest & Quarry = {Resource, Healing}
    ├── table                # LootTable for this POI
    └── min/maxDropsPerContainer
```

* **`ItemDefinition`** — itemId (stable snake_case), displayName, category
  (Weapon/Ammo/Armor/Healing/Resource/Attachment), tier (1–3), maxStack,
  optional `worldPrefab` + `icon`.
* **`LootTable`** — weighted entries `{item, weight, minCount, maxCount}`.
  `RollOne(rng, maxTier, allowedCategories)` filters by tier cap *and* category
  before the weighted pick; `RollMany` fills a list without allocation churn.

### The tier contract (tested in EditMode)

| POI | zone id | cap |
| --- | --- | --- |
| Military Base | `central_state` | **Tier 3** — snipers/ARs spawn here and nowhere else |
| Abandoned Village | `factory_island` | Tier 2 |
| Konkan Docks | `konkan_docks` | Tier 2 |
| Forest & Quarry | `graveyard_center` | Tier 1, `Resource`+`Healing` only |
| Airfield Hangars | `aircraft_industry` | Tier 2 |
| anywhere else | — | `defaultMaxTier` = Tier 1 |

`GameplaySystemsTests.MilitaryBaseIsTheOnlyTier3Zone` fails the build if any
other rule ever gets Tier 3.

## Starter content

[`DefaultLootContent`](../../unity-project/Assets/Scripts/Gameplay/Loot/DefaultLootContent.cs)
authors 15 items (4 weapons, 2 ammo, 4 armor, 2 healing, 3 resources) and 4
tables (military/village/docks/resources) **in code**, so a fresh clone has a
playable loot layer with zero `.asset` files. To make them editable:

* Unity menu → **Battle Horizons → Gameplay → Create Default Loot Assets**
  writes the same content to `Assets/Resources/Config/Loot/` (re-runnable; it
  updates the assets in place via `EditorUtility.CopySerialized`).

## Scene wiring (5 minutes)

1. In the SiranIsland gameplay scene create an empty `Gameplay/Loot` object
   with **`LootSpawner`**. Leave `config` empty to use the code defaults, or
   drag in the generated `LootSpawnConfig_Default` asset.
2. Add **`LootContainer`** to each building root (ProBuilder house, bunker,
   crate row). Default bounds come from the object's BoxCollider, or from the
   serialized local center/size. Selected containers draw yellow gizmo boxes.
3. Play. `LootSpawner.SpawnAll()` runs on Start: every container resolves its
   zone via `SiranMapConfig.ZoneAt`, the zone rule caps the tier and picks the
   table, and `rng.Range(minDrops, maxDrops)` items land at random positions
   inside the bounds.
4. Greybox visuals: items without a `worldPrefab` spawn as 0.35 m cubes tinted
   by tier (grey/blue/gold). Assign real prefabs later — the spawner prefers
   `ItemDefinition.worldPrefab`, then the spawner's `pickupPrefab` override.
5. Pickup: any collider tagged `Player` entering the trigger collects the item
   and fires the static `LootPickup.Collected` event — the inventory UI
   (next milestone) subscribes there; nothing else needs to change.

## Determinism rules for contributors

* Never call `UnityEngine.Random.*` or `new System.Random()` in gameplay code —
  the static validator fails the build.
* Pass one `RandomStream` through a whole spawn pass (`SpawnAll` does this);
  don't re-seed mid-pass.
* The match seed should be shared: `LootSpawner`, `PlayerSpawnController` and
  `SafeZoneController` all take the same seed so "match 4711 / seed 20261119"
  is fully reproducible from a bug report.

## Performance notes (mobile)

* Containers register in a static list on enable — no `FindObjectOfType` scans.
* One `Instantiate` per pickup at match start only; the greybox cube shares a
  single prototype. When art prefabs arrive, pool them through
  `BattleHorizons.Core.ObjectPool<LootPickup>` (docs/UNISOC_BUDGET.md rule 5/6).
* Loot volume is ~2–4 drops per container; with 15 village houses + a military
  base that is a few hundred pickups — fine for ARM64 with trigger colliders,
  but keep pickup colliders on a dedicated physics layer once combat raycasts
  land so bullet queries can skip them.
