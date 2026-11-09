# Unity 6 + URP architecture (Android-first)

The runtime design of `unity-project/`. It explains the *decisions*, so that a
future change can be made in the right place instead of by accident.

Scope note: this document describes what the code does. Whether it compiles,
runs and hits its budget is a separate question answered honestly in
`docs/migration/REPORT.md`.

---

## 1. Why URP, and why the numbers look "low"

The Godot original runs on **GL Compatibility** for a Unisoc T606 with 4 GB RAM.
That renderer has no compute, no HDR, no real-time GI, cheap shadow maps and a
hard 30 fps cap. HDRP requires compute, a deferred/clustered pipeline and ~4× the
bandwidth — it cannot run on that class of device at all, which is why it is
forbidden here.

URP's mobile path is the closest analogue: Forward rendering, one directional
light, realtime shadows optional, lightmap-style baked lighting, no post-processing
in the Low profile. So:

| Decision | Value | Reason |
| --- | --- | --- |
| Renderer | **URP Forward** (`UniversalRenderer`) | GLES3-class hardware |
| HDR | **off** | no HDR framebuffer on low-end GLES; saves bandwidth |
| MSAA | **1×** (Low), 2× (High) | set on the active URP asset when a profile is applied |
| Shadow cascades | **2** (Low), 4 (High) | set on the active URP asset when a profile is applied |
| Shadow distance / map | **60 m / 512 px** (Low), **120 m / 1024 px** (High) | set on the active URP asset; hard shadows in both |
| Additional lights | **per-vertex, 2/object** (Low), 4/object (High) | active-asset cap is switched with the profile |
| Colour space | **Linear** | correct vertex-colour + lighting maths |
| Render scale | **0.85** (Low) / 1.0 (High) | set on the active URP asset when a profile is applied |
| Post-processing | **none** in either profile | no Volume/profile is configured |
| Depth texture / opaque texture | **off** | nothing needs them; each costs a full-screen pass |

The two profiles are *the same renderer asset with different budgets*, not two
pipelines. `QualitySettings.asset` holds `BattleHorizons_Low` (index 0) and
`BattleHorizons_High` (index 1); Android defaults to 0, so a device that is never
detected still boots onto the safe profile. `QualityProfiles.Apply()` updates the
active `UniversalRenderPipelineAsset` after choosing the quality level. This code
has not yet been compiled or exercised in Unity.

Selection order in `GameBootstrap.Awake()`:

1. `forcedQuality` serialized field (used by tests and by a scene override),
2. the persisted player choice (`PlayerPrefs "bh.quality"`, the settings screen),
3. `QualityProfiles.DetectFromDevice()` — RAM ≥ 5.5 GB **and** ≥ 8 cores
   **and** (unknown VRAM or ≥ 1.5 GB) → High, else Low.

`Detect()` is a pure function of three integers, which is why it is unit-testable
without a device.

**No frame rate is claimed.** `QualityProfiles.ApplyFrameRate()` sets
`Application.targetFrameRate` to 30 (Low) or 60 (High) with `vSyncCount = 0`,
because on Android `targetFrameRate` is the only reliable pacing control. Whether
the device reaches it is a hardware question.

---

## 2. Assembly layout

| Assembly | Path | Platforms | Purpose |
| --- | --- | --- | --- |
| `BattleHorizons.Runtime` | `Assets/Scripts/**` | all | everything that ships in the APK |
| `BattleHorizons.Editor` | `Assets/Editor/**` | Editor only | setup, validation, build, import rules |
| `BattleHorizons.Tests.EditMode` | `Assets/Tests/EditMode` | Editor only | pure-logic tests |
| `BattleHorizons.Tests.PlayMode` | `Assets/Tests/PlayMode` | all | scene/physics tests |

The editor assembly references the runtime assembly, never the other way round.
Editor-only APIs (`UnityEditor.*`, `AssetDatabase`) are therefore impossible to
leak into the APK — the compiler stops it.

> Editor asmdefs do **not** inherit the runtime assembly's references. Every
> editor file that touches gameplay types needs an explicit
> `using BattleHorizons.Player;` / `...CameraRig;` / `...UI;` / `...World;`. This
> is the most common way to break this project's compile; `validate_csharp.py`
> checks that every such `using` resolves.

---

## 3. Runtime object graph

```
Boot.unity
└── BattleHorizons (GameBootstrap)          picks profile, sets orientation,
    └── (spawned) PerformanceMonitor        load scene (persistent)
        └── loads → MainMenu.unity | SiranIsland.unity | PlayerTest.unity

MainMenu.unity
└── MainMenuController                      title, profile toggle, buttons

SiranIsland.unity / PlayerTest.unity
└── SiranWorldBuilder                       builds the whole world at runtime
    ├── SiranIslandData                     loads + validates the blobs
    ├── SiranIslandTerrain                  Unity Terrain + heightmap collider
    ├── SiranChunkStreamer                  pooled visual chunk meshes
    ├── SiranIslandWater                    river mesh + scrolling normal map
    └── (PlayerTest only)
        ├── Player (ThirdPersonMotor, PlayerInputRouter)
        ├── CameraRig (ThirdPersonCameraController)
        └── MobileControls (VirtualJoystick ×2, TouchLookArea, buttons)
```

Nothing in the island is authored in a scene file. A `.unity` scene contains the
builder plus a camera/light, and the world appears when you press Play. Two
reasons: 9.2 MB of baked geometry cannot be hand-placed in a scene, and a
data-driven build means the map can be re-converted without touching Unity.

---

## 4. Systems

### 4.1 Island data (`World/SiranIslandData.cs`)

Owns loading and validation of the converted blobs:

* reads `siran_island_terrain.json` for grid metadata and provenance,
* loads `siran_island_heights.bytes` → `float[]` (160,801 samples),
* loads `siran_island_colors.bytes` → `Color32[]`,
* parses the road blob (`BHROAD01`) and river blob (`BHRIVER1`),
* exposes `HeightAt(x, z)` (bilinear, clamped to the grid) and
  `NormalAt(x, z)` (from neighbouring samples).

It **fails loudly** if a blob is missing, truncated, has the wrong magic, or its
sample count disagrees with the JSON. A half-loaded island is worse than a
message.

### 4.2 Terrain (`World/SiranIslandTerrain.cs`)

The physics and macro shape:

* builds a Unity `TerrainData` of `401 × 401` heights at 10 m resolution
  (`size = 4000 × 4000`, heights normalised 0…1 across the real range),
* one `TerrainCollider` — **not** 64 `MeshCollider`s. On a mobile CPU, 64 mesh
  colliders cost more to broadphase than a heightmap ever will, and the bake
  guarantees the shapes agree to 1e-3 m anyway,
* normals are **recomputed from heights**, because the bake's normals are garbage
  (see `BAKE_FORMAT.md` §1),
* terrain sits on layer 8 (`Terrain`), which the player's ground mask uses.

`Terrain` rather than a custom mesh because it gives free LOD + heightmap
collision + occlusion-friendly bounds for 4 km² on a phone.

### 4.3 Visual chunks (`World/SiranChunkStreamer.cs`)

The visual detail is a **pooled ring of 500 m chunk meshes** built from the same
grid:

* 8 × 8 = 64 possible chunks, each `51 × 51` vertices (shares the border row),
* only chunks within `StreamRadius(profile)` (1100 m Low / 1600 m High) plus a
  keep-ring are active — the port of `chunk_manager.gd`'s radius/keep-ring/throttle
  model,
* a fixed pool of meshes is reused; activation never allocates,
* vertex colours come straight from `siran_island_colors.bytes`, so the greybox
  look survives with zero textures,
* distances are squared and compared per frame against the player's position only
  when it has moved more than 8 m, so the streaming cost is amortised, not
  per-frame.

### 4.4 Water (`World/SiranIslandWater.cs`)

The river is a ribbon (348 tris) generated from the control points, plus a
`_BaseMap_ST` scroll of `water_normal_256.png`. It is **opaque** URP Lit: a
transparent pass on a phone costs a full extra pass over every pixel it covers and
buys nothing at this art level. The lake and moat are part of the height field, so
they are simply terrain below sea level with the same water plane on top.

### 4.5 Player (`Player/ThirdPersonMotor.cs`, `PlayerInputRouter.cs`)

Character-controller based third-person movement, written for this port (the Godot
project has no player controller at all):

| Parameter | Value | Source |
| --- | --- | --- |
| Walk / run speed | 3.2 / 6.4 m/s | player feel, mid-range BR values |
| Jump height | 1.1 m | — |
| Gravity | −22 m/s² | snappier than real gravity, standard for shooters |
| Coyote time | 0.35 s | forgiving on touch |
| Jump buffer | 0.12 s | forgiving on touch |
| Crouch | 1.8 m → 1.1 m, 1.6 m/s | fits under a 1.2 m doorway |
| Ground stick | −2 m/s while grounded | keeps a `CharacterController` glued to slopes; cleared on the jump frame so it cannot eat the impulse |
| Locomotion state | Idle / Walk / Run / Crouch / Airborne | exposed for animation and tests; Run is speed-based because a phone stick rarely hits 1.0 |
| Turn smoothing | 12 (exponential) | no snappy direction pops |

`PlayerInputRouter` normalises **one** input model for three devices (touch, gamepad,
keyboard/mouse) through the Input System, so gameplay code never asks "is this a
phone". It also owns the virtual joystick → movement vector conversion, with a dead
zone and a radial clamp.

### 4.6 Camera (`Camera/ThirdPersonCameraController.cs`)

Orbit camera with collision-aware pull-in: a sphere cast from the pivot stops the
camera entering terrain or the player, and the resulting distance is damped back
out (not snapped) so the view does not jitter in tunnels.

### 4.7 Touch controls (`UI/VirtualJoystick.cs`, `UI/TouchLookArea.cs`, `UI/MobileControlsBuilder.cs`, `UI/TouchControlsHost.cs`, `UI/HoldButton.cs`)

Built at runtime to avoid prefab/scene dependencies:

* two `VirtualJoystick` instances (move bottom-left, look right) with a dead zone
  and a visual ring,
* a full-screen `TouchLookArea` created **first** so it sits behind every other
  control: uGUI hit-tests in reverse sibling order, so a drag on a stick or button
  is handled by that control and everything else falls through to the look surface,
* JUMP (`Button`, one-shot) plus CROUCH and SPRINT (`HoldButton`, held state) —
  crouch and sprint are held states, so a click-style button would be wrong,
* touch look is scaled by screen height, so a 1440p phone does not turn twice as
  fast as a 720p one,
* `TouchControlsHost` is the only component a scene needs; the rig itself is built
  at runtime, which keeps prefabs and canvases out of version control.

They are `UnityEngine.UI` (uGUI) rather than UI Toolkit because uGUI's
event-system input path is cheaper per frame on mobile.

### 4.8 Diagnostics (`Core/PerformanceMonitor.cs`, `UI/WorldDebugHud.cs`)

`PerformanceMonitor` keeps a 240-frame ring buffer (average + 99th-percentile
"1 % low"), `ProfilerRecorder` readings for `SetPass Calls Count` (the real
counter name — there is no `Draw Calls Count`), managed-heap-used and Unity
reserved-memory readings. The HUD samples these at 4 Hz and caches them so IMGUI
repaints do not repeatedly sort the frame history. Reserved memory and managed
heap use are not Android PSS; PSS is measured with `dumpsys meminfo`.

The monitor logs a frame-time warning when the one-second mean exceeds 110% of the
target (36.6 ms Low / 18.3 ms High). Set-pass and memory targets are displayed,
not automatically warned on. None of these are performance claims; device tests
remain open in `docs/migration/REPORT.md`.

`WorldDebugHud` is an IMGUI overlay toggled with **F3** (or a 3-finger tap),
compiled out of release builds unless `BH_DEBUG_HUD` is defined. IMGUI because it
must not depend on a font asset, a canvas or the UI pipeline — it has to work in a
build with no UI at all.

---

## 5. Data flow

```
Godot bake (.tscn)
   │  convert_godot_island.py  (CI: --check drift gate)
   ▼
Assets/Resources/Config/*.bytes  +  *.json  (hashed in bake manifest)
   │  Resources.Load<TextAsset> / <Texture2D>
   ▼
SiranIslandData ──► SiranIslandTerrain   (TerrainData + TerrainCollider)
   │            ──► SiranChunkStreamer   (pooled meshes, vertex colours)
   │            ──► SiranWorldBuilder    (roads parented to chunks)
   │            ──► SiranIslandWater     (river mesh + normal map)
   ▼
ThirdPersonMotor (HeightAt for spawn, collider for walking)
```

Everything above runs on the **main thread at load time** only. Per-frame work is:
chunk ring activation (amortised), one sphere cast for the camera, and the HUD
(debug only).

---

## 6. Android configuration

Set by `ProjectBootstrap.Configure()` and asserted by `ProjectValidator`:

| Setting | Value |
| --- | --- |
| Scripting backend | IL2CPP |
| Target architecture | **ARM64 only** (`AndroidTargetArchitectures = 3`); no ARMv7, no x86 |
| Minimum API | **26** (Android 8.0) |
| Target API | highest installed (34/35 on the CI image) |
| Texture compression | **ASTC 6×6**, ETC2 fallback |
| Graphics API | Vulkan, GLES3 fallback |
| Orientation | landscape left + right, portrait disabled |
| INTERNET permission | **off** (`ForceInternetPermission = 0`) — offline game |
| Managed stripping | Medium; `link.xml` for anything reflected |
| Colour space | Linear |
| Frame pacing | `targetFrameRate`, `vSyncCount = 0` |

ARM64-only halves the APK's native payload and removes a 32-bit address-space
ceiling that a 4 km island with 25 actors will eventually hit.

---

## 7. Conventions

* **Namespaces** match folders: `BattleHorizons.Core`, `.World`, `.Player`,
  `.CameraRig` (not `.Camera`, which would shadow `UnityEngine.Camera`), `.UI`,
  `.EditorTools`.
* **No `Update()` that scans the scene.** No `FindObjectOfType` in a loop, no
  `Camera.main` in `Update()` (cache it in `Awake`).
* **No allocation in the frame loop** in shipped code paths: pools (`ObjectPool`),
  reused `Vector3`s, no LINQ, no string concatenation in the HUD's hot path.
* **`GameLog`** instead of `Debug.Log` in runtime code, so a release build can drop
  the strings.
* **Serialized fields** are `[SerializeField] private` with a `[Tooltip]`; the
  editor tooling uses `SerializedObject.FindProperty` and the static validator
  checks those names exist.
* **Determinism**: seeded `System.Random` or `UnityEngine.Random.InitState`; the
  static validator rejects unseeded randomness in gameplay code.

---

## 8. What is deliberately not here

| Not present | Why |
| --- | --- |
| Networking / multiplayer | the Godot project has none; out of scope |
| Save system | `save_manager.gd` is a skeleton with nothing to save; deferred until there is gameplay state |
| Audio manager | `audio_manager.gd` has buses and no clips; `AssetImportRules` already enforces Vorbis/mono for SFX |
| Weapon/combat systems | no weapons exist in either project. Folders (`Scripts/Weapons`, `Combat`) are reserved, empty |
| Bots / AI | none in the Godot project; 25-bot budget is a target, not code |
| Buildings, props, vegetation | absent from the Godot project; the Blender kit supplies *modular pieces*, not a placed city |
| The mansion at 56.1 % / 31.1 % | no asset and no coordinate exist anywhere in the repository — see the report's gap list |

Keeping these out is a scope decision, not an oversight: the migration ports what
exists and builds the foundation the brief asks for, and the report says exactly
where the line was drawn.
