# Unisoc T606 performance budget

Everything in `project.godot`, `export_presets.cfg` and this document exists to
keep Desi Royale at a **30 FPS floor on a Unisoc T606 with 4 GB of RAM**. If a
change breaks one of the numbers below, it does not ship — no exceptions for
"it looks better on my Redmi".

## Target hardware

| | Unisoc T606 (reference device: Samsung Galaxy A03 / Moto E20 / Realme C35) |
| --- | --- |
| CPU | 2 × Cortex-A75 @1.6 GHz + 6 × Cortex-A55 @1.6 GHz |
| GPU | Mali-G57 MP1 (GLES3.2 capable, very low fill-rate) |
| RAM | 4 GB LPDDR4X, ~800 MB usable before Android starts killing us |
| Storage | eMMC 5.1 — asset *load* time matters as much as asset size |
| Display | typically 720 × 1600, so 1280 × 720 render target is 1:1-ish |
| Second target | T310-class 32-bit devices (hence `armeabi-v7a` stays enabled) |

## Hard budgets

| Budget | Limit | Where it is measured |
| --- | --- | --- |
| Process memory (PSS) | ≤ 800 MB | `adb shell dumpsys meminfo com.desiroyale.game` |
| GPU memory | ≤ 180 MB | `Performance.RENDER_VIDEO_MEM_USED` (logged by PerfManager) |
| Draw calls | ≤ 60 | `Performance.RENDER_TOTAL_DRAW_CALLS_IN_FRAME` |
| Frame time | ≤ 33.3 ms (30 FPS) | `Performance.TIME_FPS` |
| Bots per match | 25 | match config in `resources/config/` |
| Texture size | ≤ 512 × 512 | import check in `docs/ASSET_PIPELINE.md` |
| Physics | 30 ticks/s | `physics/common/physics_ticks_per_second` |
| Lights per object | 2 | `rendering/limits/opengl/max_lights_per_object` |

## Why each `project.godot` value is set the way it is

| Setting | Value | Reason |
| --- | --- | --- |
| `renderer/rendering_method(.mobile)` | `gl_compatibility` | Mali-G57 + Unisoc GL driver: Vulkan Mobile is a crash/latency lottery on T606; GLES3 is the only path that hits 30 FPS reliably. |
| `application/run/max_fps` | `30` | Frame limiter also keeps the GPU clock (and heat) down; uncapped, the SoC throttles within 3 minutes. |
| `anti_aliasing/quality/msaa_3d` | `0` | MSAA on a 1-core GPU costs 15–30 % frame time on tiled mobile GPUs. |
| `anti_aliasing/quality/screen_space_aa` | `0` | FXAA costs a full-screen pass; unusable at 180 MB VRAM. |
| `textures/default_filters/anisotropic_filtering_level` | `0` | Anisotropic sampling is the single most expensive filter on Mali. |
| `textures/default_filters/texture_mipmap_bias` | `0.75` | Sharpen slightly after mip reduction — free perceived detail instead of resolution. |
| `textures/vram_compression/import_etc2_astc` | `true` | **Must be set before importing textures.** ASTC/ETC2 halves VRAM vs. RGBA8 and is the only way to stay under 180 MB. Missing it fails silently at export time. |
| `lights_and_shadows/directional_shadow/size` | `512` | 1024+ doubles the shadow pass bandwidth for almost no visual gain at 720p. |
| `lights_and_shadows/directional_shadow/soft_shadow_filter_quality` | `0` | Hard shadows only (PCF cost scales with kernel size). |
| `lights_and_shadows/positional_shadow/atlas_size` | `512` | Same reasoning for spot/omni shadows. |
| `limits/opengl/max_lights_per_object` | `2` | GLES3 forward rendering re-shades per light; 2 is the ceiling we can afford. |
| `limits/opengl/max_renderable_elements` | `100000` | Headroom for the merged static meshes of a whole map at once. Note: the editor's slider stops at 65536, the value is applied as written. |
| `occlusion_culling/use_occlusion_culling` | `false` | Occlusion culling builds a BVH (memory + CPU) that costs more than it saves at our draw-call count; use manual visibility ranges instead. |
| `physics/common/physics_ticks_per_second` | `30` | Half the CPU of 60 Hz, invisible at 30 FPS rendering. |
| `physics/3d/physics_engine` | `Jolt Physics` | Faster broadphase and cheaper character bodies than GodotPhysics3D on weak CPUs. |
| `display/window/stretch/mode` | `canvas_items` | Keeps UI crisp across the 720p–1080p range without re-laying out 3D. |
| `display/window/handheld/orientation` | `4` (`SCREEN_SENSOR_LANDSCAPE`) | Landscape both ways; portrait must never be reachable. |
| `input_devices/pointing/emulate_touch_from_mouse` | `true` | Editor/test on desktop; `emulate_mouse_from_touch` stays `false` so on-device touch events are never synthesised into mouse input. |

## Rules that are easy to forget

1. **Shaders:** only `unshaded` / vertex-lit materials in `assets/shaders/`. No
   screen-space effects, no post-processing, no SSAO/SSIL/SSR.
2. **Shadows:** at most one shadow-casting directional light per scene; every
   other light is `shadow_enabled = false`.
3. **Textures:** ≤512², power-of-two, VRAM-compressed, mipmaps on.
4. **Audio:** `.ogg`, 22050 Hz, mono for SFX; music streamed, never `preload`ed.
5. **Nodes:** prefer merged static meshes + `MultiMesh` over hundreds of
   `MeshInstance3D` — the draw-call budget is 60, not 600.
6. **GC:** no per-frame `Array`/`Dictionary`/string building in `_process()` or
   `_physics_process()`. Godot's allocator is the top cause of frame spikes on
   4 GB devices.
7. **Loading:** no scene larger than ~40 MB decompressed; stream the map by biome.

## Measuring

```bash
# Frame stats come straight from PerfManager (once per second, tag "[perf]")
adb logcat -s godot | grep "\[perf\]"

# Process memory
adb shell dumpsys meminfo com.desiroyale.game | grep -E "TOTAL PSS|TOTAL RSS"

# Sustained performance (watch for thermal throttling over 10 minutes)
adb shell dumpsys gfxinfo com.desiroyale.game framestats | head -40
```
