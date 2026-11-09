# Android graphics profiles — Low and High

Two profiles, one renderer. The brief asks for **Low** (reduced render
resolution, lower shadows, reduced vegetation, aggressive LOD, texture-memory
optimisation, minimal post) and **High** (higher resolution, better shadows,
better materials, more detail, selective post). This document is the exact
contract for both, plus the honesty rules about what is measured and what is not.

> **No FPS claim.** Nothing here says the game *runs* at 30 or 60 fps on any
> device. These are budgets and settings. Verifying them requires the hardware and
> is listed as an open item in `docs/migration/REPORT.md`.

---

## 1. Where each setting lives

| Layer | File | Holds |
| --- | --- | --- |
| Unity quality levels | `ProjectSettings/QualitySettings.asset` | two levels: `BattleHorizons_Low` (0), `BattleHorizons_High` (1); Android default = 0 |
| URP pipeline asset | `Assets/Settings/BattleHorizonsURP.asset` (created by **Project Setup > Configure**) | HDR, MSAA, shadow cascades/distance, render scale, additional lights, depth/opaque texture |
| Runtime | `Assets/Scripts/Core/QualityProfile.cs` | detection, persistence, `targetFrameRate`, and the numbers the streamer/camera read |
| Import rules | `Assets/Editor/AssetImportRules.cs` | ASTC 6×6, 512² cap, mip/aniso, Vorbis/mono, 4 bone influences |
| Camera | `ThirdPersonCameraController` + `SiranChunkStreamer` | far plane, chunk stream radius |

The URP asset is generated, not committed, so it always matches the code that
configures it. `ProjectValidator` re-reads it and fails if a serialized value
drifts from this table.

---

## 2. Low profile (the Unisoc T606 floor — the safe default)

Inherited from `docs/UNISOC_BUDGET.md`, which the Godot build shipped against.

| Knob | Value | Why |
| --- | --- | --- |
| Render scale | **0.85** | 1280×720 requested → ~1088×612 actual; the brief's "reduced render resolution" |
| HDR | off | GLES3-class devices; no HDR framebuffer, saves bandwidth |
| MSAA | off | 15–30 % frame time on tiled mobile GPUs |
| Post-processing | **none** | no SMAA, no bloom, no vignette; matches the Godot build |
| Shadows | on, **1 cascade** requested via URP (quality level caps at 2), 60 m distance, 512 px map, hard PCF | the Godot project used 512 px hard shadows |
| Additional lights | 2 per object, per-vertex | GLES3 forward re-shades per light |
| Pixel light count | 2 | `QualitySettings` |
| Shadow resolution | from quality settings, low tier | — |
| Anisotropic filtering | **0** | most expensive filter on Mali |
| Texture budget | 512 × 512 cap, ASTC 6×6, mip on | VRAM ≤ 180 MB |
| Streaming mipmaps | on, 96 MB budget, 256 renderers/frame | keeps peak texture residency down |
| LOD bias / max LOD | **0.6** / allow 1 level down | aggressive LOD, as required |
| Terrain pixel error / basemap | 8 / 300 m | Unity Terrain LOD is the main triangle saver |
| Camera far plane | **900 m** | fog hides the cut; halves overdraw vs 1600 |
| Chunk stream radius | **1100 m** | ~12–16 active 500 m chunks |
| Frame rate target | **30** (`targetFrameRate`, vSync off) | the limiter is what stops thermal throttle |
| Vegetation | detail density 0.5, 40 m detail distance, 250 m tree distance, max 60 trees | "reduced vegetation" |
| Occlusion culling | **off** | a 4 km outdoor scene has almost no occluders at 60 draw calls; the bake cost is not worth it |
| Baked lighting | static lightmap-ready; island uses vertex colour + one directional light | avoid realtime GI entirely |
| Expected draws | ≤ 60 | 16 chunks + 18 roads + water + skybox + UI |

## 3. High profile (8 GB class: Snapdragon 6s Gen 3 and up)

| Knob | Value | Why |
| --- | --- | --- |
| Render scale | **1.0** | "higher resolution" |
| HDR | off (still) | URP mobile HDR doubles the target; not worth it at this art level |
| MSAA | **2×** | cheap on tile-based GPUs, fixes the worst terrain-edge shimmer |
| Post-processing | **selective**: SMAA 2× quality + optional vignette; no bloom, no DOF, no motion blur | "selective post" without paying for a full HDR chain |
| Shadows | **4 cascades**, **120 m**, 1024 px, soft (2-tap PCF) | "better shadows" |
| Additional lights | 4 per object | — |
| Pixel light count | 4 | — |
| Anisotropic filtering | 2× | roads and runway markings at grazing angles |
| Texture budget | 1024 × 1024 cap, ASTC 6×6 | ≤ 400 MB VRAM |
| Streaming mipmaps | on, 192 MB budget, 512 renderers/frame | — |
| LOD bias / max LOD | **1.0** / no reduction | full detail |
| Terrain pixel error / basemap | 5 / 1000 m | finer silhouette |
| Camera far plane | **1600 m** | — |
| Chunk stream radius | **1600 m** | up to ~40 active chunks |
| Frame rate target | **60** (capped to display refresh) | — |
| Vegetation | full density, 120 m detail, 400 m trees, max 150 trees | "more detail" |
| Expected draws | ≤ 150 | 40 chunks + 18 roads + water + lights + UI |

## 4. Selection logic

```csharp
// QualityProfiles.Detect — pure, unit-tested
High  ⇔ systemMemoryMb ≥ 5500  &&  processorCount ≥ 8  &&  (graphicsMemoryMb ≤ 0 || ≥ 1500)
Low   otherwise
```

* `graphicsMemoryMb` is **0 on most Android GLES drivers**; treating 0 as "unknown,
  do not veto" avoids punishing devices that simply do not report it.
* Override order: serialized field → `PlayerPrefs["bh.quality"]` → detection.
  `SetOverride(-1)` clears the player choice (`AutoDetect`).
* Android's default quality level is index **0**, so the very first frame — before
  `GameBootstrap` runs — is already the safe profile. There is no window where a
  4 GB phone renders with 4 cascades and 1024 px shadows.

The baseline target in the brief (Snapdragon 6s Gen 3, 8 GB) reports 8 cores and
~7.5 GB usable → **High**. The Unisoc T606 (8 cores but 4 GB) → **Low**. Detection
is by memory before cores, exactly so that the T606 lands on Low.

## 5. Memory and bandwidth rules (both profiles)

1. **ASTC 6×6 for everything.** ETC2 fallback is configured but ASTC is what ships.
   A 512² ASTC 6×6 texture is 64 KB; the same texture as RGBA8 is 1 MB.
2. **No texture above 512²** in Low — enforced by `AssetImportRules`, not by
   discipline. The water normal map is 256².
3. **Mipmaps always on** except UI sprites.
4. **No realtime GI, no reflection probes** (`realtimeGIUsage = 0`,
   `realtimeReflectionProbes = 0`).
5. **Opaque water.** A transparent surface costs an extra pass and a depth sort.
6. **One directional light** casting shadows; everything else is baked or vertex-lit.
7. **Streaming mipmaps on**, so a 150 MB texture set does not fault in at once.
8. **Chunk pool is pre-allocated**: activation never allocates a `Mesh`, so there is
   no GC spike while a player runs across the island (the classic cause of a 200 ms
   hitch that looks like a "shader compile" bug).
9. **Pooling** for anything spawned in gameplay (`Core/ObjectPool.cs`); the pool
   warns when it has to grow, because growth means the budget was wrong.
10. **Draw-call control**: one material per visual class (terrain, chunk, road,
    water), chunks batched by shared material, road ribbons merged per chunk.

## 6. How to verify on a device

```bash
adb install -r unity-project/Builds/battle-horizons.apk
adb logcat -s Unity            # GameLog lines + PerformanceMonitor budget warnings
adb shell dumpsys meminfo com.aurastudios.battlehorizons | head -30   # PSS
```

In-editor: press **F3** in `PlayerTest` for the HUD (frame time average + 1 % low,
set-pass calls, allocated/reserved memory, active chunk count, profile).

The HUD warns when:
* average frame time > 33.3 ms (Low) / 16.6 ms (High),
* set-pass calls > 60 (Low) / 150 (High),
* reserved memory > 800 MB (Low) / 1400 MB (High).

`SetPass Calls Count` is the real `ProfilerRecorder` counter (there is no
`Draw Calls Count`), and it is read through `ProfilerRecorder` rather than the
profiler UI so it works in a development build on the device.

## 7. What is *not* verified

| Claim | Status |
| --- | --- |
| The URP asset has these values | **Implemented**; asserted by `ProjectValidator` when Unity runs — **not yet run** (no Unity in this environment) |
| Quality levels exist with these numbers | **Implemented + verified statically** (`validate_csharp.py` reads the YAML) |
| Detection picks the right profile on real hardware | **Not tested** — needs a device; the pure function is unit-tested |
| 30 fps on T606 / 60 fps on 6s Gen 3 | **Not tested, not claimed** |
| PSS ≤ 800 MB | **Not tested** |
| Draw calls ≤ 60 | **Not tested** |

The budget is a design contract. Only measurement on the two reference devices can
turn it into a fact, and `docs/migration/REPORT.md` keeps it in the "not tested"
column until someone does.
