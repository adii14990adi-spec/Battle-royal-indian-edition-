# Safe Zone & Player Spawns

Task 2b/2c of the gameplay-foundation milestone: the 50-point drop ring and the
shrinking blue zone. Both are deterministic (seeded
[`RandomStream`](../../unity-project/Assets/Scripts/Core/RandomStream.cs),
never `UnityEngine.Random`) and their geometry lives in pure static classes so
EditMode tests — and later the server-authoritative match code — share one
implementation.

## 50 edge spawn points — `PlayerSpawnService`

`PlayerSpawnService.GenerateEdgeSpawnPoints(seed, count = 50, island = null)`
returns the drop ring:

* angles evenly divide 360° with a seeded ±3.6° jitter;
* radius = `IslandRadiusM (1950) × 0.78 ≈ 1520 m` with ±6 % variance — players
  land near the coast and rotate inward;
* height = terrain (`SiranIslandData.SampleHeight` when wired) or the zone
  greybox estimate, **+1.2 m** capsule clearance;
* water rejection is deterministic: up to 6 jittered retries (angle wobbles
  ±7° per attempt, radius pulls inland 4 % per attempt), then a last-resort
  inland sweep at 5 % radius steps. Siran Lake and the Siran River cross the
  ring, so this matters — the EditMode test asserts all 50 points are dry.

`PlayerSpawnController` is the scene wrapper: assign the player prefab (tagged
`Player`), set `matchSeed`, and it generates the ring on Start, picks the local
drop index deterministically (`PickDropIndex`) and instantiates the player.
Green gizmo spheres show the ring in the Scene view.

Verified properties (`GameplaySystemsTests`): exactly 50 points, reproducible
per seed, all valid (`IsValidSpawnPoint`), all inside the 1950 m coastline,
none closer than 682 m to the centre (edge-hugging), ≥15 m apart pairwise
(simulated minimum: 25 m).

## Shrinking blue zone — `SafeZoneController`

State machine: `Idle → (Waiting → Shrinking) × N phases → Finished`.

* **`SafeZoneSettings`** (ScriptableObject) holds `startRadiusM` (1900 m —
  covers the island), an array of phases `{waitSeconds, shrinkSeconds,
  radiusFactor, damagePerSecond}` and `finalDamagePerSecond`.
* **`SafeZoneMath.NextCircle`** places the next circle fully inside the current
  one: radius scales by `radiusFactor` and the centre shifts uniformly within
  the remaining slack (`NextPointInUnitDisc × (R − r)`), so the zone never
  exposes open water or splits into unreachable pockets.
* While shrinking, `SafeZoneMath.Lerp` blends centre+radius from the phase
  start circle to the target.
* Damage: tracked players (`AddTrackedPlayer(IDamageable, Transform)`) outside
  the rim take `damagePerSecond × deltaTime` with source id `"safe_zone"` for
  the kill feed. Dead transforms are pruned automatically; the health
  component itself is a later milestone behind `IDamageable`.
* Visual: assign an artist-made `zoneVisual` transform, or the controller builds
  a greybox cylinder (collider removed, translucent blue) and scales it to
  `(2r, height, 2r)` each frame of the shrink. Scene view draws both the live
  circle (blue) and the target circle (white).

### Default schedule (`SafeZoneSettings.CreateDefault()`)

| Phase | Wait | Shrink | Radius factor | DPS outside |
| --- | --- | --- | --- | --- |
| 1 | 90 s | 180 s | 0.65 | 0.4 |
| 2 | 75 s | 150 s | 0.60 | 0.8 |
| 3 | 60 s | 120 s | 0.55 | 1.6 |
| 4 | 45 s | 100 s | 0.50 | 3.0 |
| 5 | 30 s | 80 s | 0.45 | 6.0 |
| 6 | 20 s | 60 s | 0.40 | 12.0 |

Total ≈ **16.8 minutes** from island-wide to a ~37 m final circle, with damage
ramping 0.4 → 12 HP/s. Create an editable copy via
**Battle Horizons → Gameplay → Create Default Safe Zone Settings**
(`Assets/Resources/Config/Match/`).

## Scene wiring

One object per match scene:

```
Gameplay/Match (empty)
├── PlayerSpawnController   (matchSeed = 20261119, playerPrefab = Player_Local)
├── SafeZoneController      (matchSeed = 20261119, settings empty → default)
└── Loot/LootSpawner        (see LOOT_SPAWNING.md — same seed)
```

Share one match seed across all three for a fully reproducible match.
