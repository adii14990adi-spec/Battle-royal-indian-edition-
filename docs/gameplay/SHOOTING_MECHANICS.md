# Shooting Mechanics — Aiming, Recoil, Hitscan

Milestone: make the loot usable. Everything lives in
`unity-project/Assets/Scripts/Gameplay/Combat/` and plugs into the existing
input router, orbit camera, loot event and character-animation hooks — no new
packages, no committed audio/art assets (FX are procedural greybox).

## Architecture

```
Gameplay/Combat/
├── WeaponData.cs            # ScriptableObject stats: damage, RPM, mag, reload,
│                            #   spread, ADS FOV, recoil curve numbers, fire mode
├── DefaultWeaponContent.cs  # code-authored stats for the 4 loot guns (itemId-keyed)
├── WeaponRuntime.cs         # PURE trigger/mag/reload state machine (clock injected)
├── RecoilModel.cs           # PURE seeded recoil: kick per shot + recovery bleed
├── CombatPlaceholderFx.cs   # procedural muzzle flash, gunshot PCM, tracer line
├── HealthComponent.cs       # reference IDamageable (bullets + blue zone feed it)
└── WeaponController.cs      # MonoBehaviour glue on the player
```

Flow: `PlayerInputRouter.FireHeld/AimHeld` → `WeaponController.Update` →
`WeaponRuntime.Tick(Time.time, fireHeld)` (fire rate, semi edge, auto-reload) →
`FireShot()`: hitscan raycast from the crosshair, seeded cone spread, damage via
`IDamageable` (source = `weaponId` for the kill feed), `RecoilModel.ApplyShot`
kicks the camera, muzzle flash + gunshot + tracer, and
`PlayerCharacterVisual.TriggerShoot()` fires the Animator one-shot when a
character is equipped (the greybox capsule just skips it).

Weapons arrive through the static `LootPickup.Collected` event: any item with
`category == Weapon` equips the matching `WeaponData` by `itemId`; `Ammo`
items top up the reserve. The player also starts with the Viper M9 pistol
(`startWeaponId`) so an empty scene is immediately testable.

## Balance sheet (greybox, `DefaultWeaponContent`)

| Weapon | Tier | Mode | Dmg | RPM | Mag | Range | ADS FOV | Hip/ADS spread | Recoil pitch |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Viper M9 Pistol | 1 | Semi | 22 | 300 | 12 | 90 m | 50° | 2.0° / 0.4° | 1.0° |
| Kestrel SMG | 2 | Auto | 17 | 750 | 30 | 110 m | 48° | 2.6° / 0.7° | 0.9° |
| Bhairav AR | 3 | Auto | 28 | 600 | 30 | 220 m | 45° | 2.2° / 0.35° | 1.4° |
| Horizon M82 Sniper | 3 | Semi | 85 | 45 | 5 | 600 m | 30° | 5.0° / 0.02° | 4.5° |

Tier 3 rows only ever spawn in the Military Base — same contract
the loot tests lock, now asserted across loot **and** combat by
`CombatSystemsTests.EveryLootWeaponHasCombatStats`.

## Step-by-step Unity setup

### 1. Player rig (once per scene)

The generated `SiranIsland`/`PlayerTest` scenes already have: player capsule
(tag `Player`) with `PlayerController` + `ThirdPersonMotor`, orbit
`ThirdPersonCameraController` (Camera on the same GameObject), and
`MobileControlsBuilder` UI. Add to the player root:

1. **`WeaponController`** component.
2. **`HealthComponent`** component (so the safe zone and enemy fire have a
   target; also makes the player a valid `IDamageable` for tests).
3. Leave the reference fields empty — `Start()` finds `PlayerInputRouter` and
   `ThirdPersonCameraController` once (never per frame) and builds the rest.

### 2. Weapon mount point

1. Select the player (or the character visual root once
   `PlayerCharacterVisual` spawns an FBX character) → Create Empty child named
   **`WeaponMount`**.
2. Position it at the character's right hand for the equipped rifle pose —
   greybox values that read well on the capsule: position `(0.45, 1.25, 0.35)`,
   rotation `(0, 0, 0)`.
3. Assign it to `WeaponController.weaponMount`. The controller auto-creates a
   **`MuzzlePoint`** child at its tip (or parents one to the camera when no
   mount exists) — that is where the muzzle flash particle system lives.
4. When gun models arrive, instantiate the mesh under `WeaponMount` on
   `WeaponEquipped`; hide/show per `weaponId`.

### 3. Crosshair (UGUI, 5 minutes)

1. On the existing `MobileControls` canvas → Create Empty **`Crosshair`**,
   anchor centre (`0.5, 0.5`), pivot centre, size `64 × 64`.
2. Add four children — thin `Image` bars (2 × 14 px, white, alpha ≈ 0.85):
   Top `(0, 14)`, Bottom `(0, −14)`, Left `(−14, 0)` rotated 90°, Right
   `(14, 0)` rotated 90°. Add a 2 × 2 px centre dot.
3. Optional dynamic spread: subscribe to `WeaponController.AmmoChanged` /
   read `IsAiming` in a tiny view script and scale `Crosshair` between
   `1.0` (ADS) and `1 + hipSpreadDeg / 10` (hip). Keep it in the UI layer —
   gameplay never touches the canvas directly.
4. **Do not** put the crosshair in world space; it must sit exactly on the
   camera centre because `FireShot` raycasts `ViewportPointToRay(0.5, 0.5)`.

### 4. Input map (already wired)

| Action | Touch | Keyboard/mouse | Gamepad |
| --- | --- | --- | --- |
| Aim (ADS) | **AIM** hold button → `router.SetAimHeld` | `Q` | Left trigger |
| Fire | **FIRE** hold button → `router.SetFireHeld` | Left mouse button | Right trigger |
| Reload | (UI hook: `WeaponController.RequestReload()`) | `R` | — |

`MobileControlsBuilder` now emits a red **FIRE** button under AIM; it is part
of the generated rig (`Rig.FireButton`), no manual UI work needed.

### 5. ADS camera behaviour

`WeaponController.UpdateAdsFov` lerps `Camera.fieldOfView` from **60° (hip)**
to `WeaponData.adsFovDeg` (**45°** rifle / 48° SMG / 50° pistol / **30°**
sniper) at `adsEnterSeconds`/`adsExitSeconds` speeds. The orbit distance stays;
a shoulder-zoom (shortening `ThirdPersonCameraController.distance` while
aiming) is the next polish item — it needs one public setter on the rig.

### 6. Recoil feel & tuning

Per shot, `RecoilModel.ApplyShot` returns a seeded kick:
`pitch = recoilPitchDeg × rng(0.85…1.15) × (ADS ? adsRecoilScale : 1)` plus
`yaw = ±recoilYawJitterDeg`. It is applied through `cameraRig.AddLook`
(pitch-up convention), and `Recover` bleeds the banked kick back at
`recoveryDegPerSec`, returning `recoveryReturnRatio` (0.7) of it — so the
camera climbs, then settles ~70 % back without the player pulling down
manually; the remaining 30 % is the spray-control skill gap. Accumulated kick
is capped (24° pitch / 12° yaw) so holding LMB can never orbit the sky.

Tuning loop: adjust numbers in the Weapon asset (menu **Battle Horizons →
Gameplay → Create Default Weapon Assets** persists all four to
`Assets/Resources/Config/Combat/` for editing, or drag overrides into
`WeaponController.weaponOverrides`). If it feels floaty, raise
`recoilPitchDeg`; if recovery feels robotic, lower `recoveryReturnRatio`.

### 7. VFX & SFX (placeholder, procedural)

* **Muzzle flash** — `CombatPlaceholderFx.BuildMuzzleFlash`: 10-burst cone
  particle, 60 ms, world-space, no texture. Swap by assigning a real
  prefab/VFX under `MuzzlePoint` later.
* **Gunshot audio** — synthesised PCM (noise crack + 85 Hz thump, 0.3 s,
  seeded → identical on every device), cached `AudioClip`. Replace with real
  clips in `Assets/UserAssets/` and a permanent `AudioSource`; the controller
  only calls `PlayOneShot`.
* **Tracer** — 80 ms line from muzzle to impact. Pool these (`ObjectPool`)
  once fire rates get SMG-hot on low-end devices.
* **Impact** — none yet; add a decal/puff when the art pass lands.

## Targets & verification

* Put a cube with `HealthComponent` in the scene → shoot it → console shows
  damage (enable `logDamage`) and `Died` fires at 0 HP.
* `CombatSystemsTests` (EditMode, CI-safe) locks: loadout ↔ loot parity and
  tier match, fire modes, ADS FOVs < 60° and tighter spread, 600 RPM cadence,
  semi trigger-edge, empty-mag auto-reload timing, reserve clamping, recoil
  determinism/climb/cap, ADS recoil scale, recovery ratio, and gunshot PCM
  determinism.
* Static validator: `python3 processing/unity/validate_csharp.py` — combat code
  uses only seeded `RandomStream` (rule 8) and no per-frame scene scans.

## Known next steps (not in this milestone)

* Shoulder-zoom ADS distance on the camera rig; weapon model + holster swap;
* ammo/health HUD (subscribe `AmmoChanged`, `HealthComponent.Damaged`);
* ballistics (projectile drop) for the sniper; hit-marker + damage numbers;
* bots/zombies as the survival pressure (they reuse `HealthComponent`).
