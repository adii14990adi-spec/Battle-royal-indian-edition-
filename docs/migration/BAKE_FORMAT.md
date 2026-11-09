# The bake format, and how the Unity port consumes it

Two questions this document answers:

1. **What is inside `scenes/world/siran_island_baked.tscn`?** (the Godot side)
2. **What did the converter write into `unity-project/Assets/Resources/Config/`?**
   (the Unity side), and how do we know they agree?

Everything here is measured from the committed files. The numbers are reproduced by
`python3 processing/unity/validate_unity_map.py`, which CI runs on every push.

---

## 1. The Godot bake (reference)

`siran_island_baked.tscn` is a text `.tscn` of ~9.2 MB. It is not practical to
read by eye, and it is the reason the Godot project takes a while to open.

### Node graph

```
SiranIslandBaked (Node3D)
├── Terrain
│   ├── Chunk_0_0 … Chunk_7_7     (64 × MeshInstance3D + StaticBody3D/HeightMapShape3D)
│   └── Roads
│       └── road_chunk_A_B …      (18 × MeshInstance3D)
├── Water                          (MeshInstance3D, river surface from .res)
└── (markers, spawn point)
```

Node names are `Chunk_<x>_<z>`; the *surface* resource inside them is named
`terrain_chunk_<x>_<z>`. Chunk `0_0` sits at origin `(-1750, 0, -1750)`, and each
chunk covers 500 m, so the 8 × 8 grid spans −2000 … +2000 m in X and Z.

### `ArrayMesh` wire layout

A baked chunk's `ArrayMesh` is stored with the keys, in this order:

| Key | Meaning |
| --- | --- |
| `aabb` | bounding box `AABB(x, y, z, sx, sy, sz)` |
| `attribute_data` | compression flags for the vertex streams |
| `format` | vertex format bitfield (position, normal, tangent, colour, uv) |
| `index_count`, `index_data` | triangle indices (uint16 in practice) |
| `primitive` | 4 = triangles |
| `uv_scale` | 1 |
| `vertex_count`, `vertex_data` | interleaved vertex buffer |

The vertex buffer is **interleaved with a 20-byte stride** when compressed:

```
offset 0  : 3 × float32   position (x, y, z)      — metres, chunk-local
offset 12 : 1 × uint32    packed normal + tangent (10-10-10-2)
offset 16 : 1 × uint32    packed … (second half of the 8-byte pair)
```

> **Trap that cost a decode iteration:** slicing the buffer as `(nx, ny, nz)` at
> offset 12 yields NaNs and denormals. Positions must be sliced out first, then
> normals decoded from the packed uint32. `goto_bake_reader.py` does this and is
> the only decoder in the repo.

### What is wrong with the bake (and must be repaired in Unity)

| Problem | Evidence | Repair |
| --- | --- | --- |
| Terrain **normals are garbage** | decoded normals are not unit vectors along the slope; `verify_phase3.sh` never checks them | rebuild normals from the height field (`SiranIslandTerrain`) |
| `siran_river_surface.res` is **binary `RSRC`**, not text | file starts with `RSRC` | regenerate the ribbon from the river control points in the config |
| Colours are RGBA8 but the shader is unshaded | `terrain_greybox.gdshader` | keep the colours, feed them to URP Lit via `_VERTEX_COLOR` |
| Winding is clockwise | `verify_phase3.sh` winding gate | **no flip needed** — Unity's front face is also clockwise |

### Colliders

Each chunk has a `HeightMapShape3D` of **51 × 51** samples (500 m at 10 m spacing,
plus the shared edge). The collider heights agree with the mesh to within 1e-3 m.
That agreement is the property worth preserving: if it drifts, you get ground you
can see but fall through.

---

## 2. The conversion

```bash
python3 processing/unity/port_siran_config.py      # GDScript constants -> C# + JSON
python3 processing/unity/convert_godot_island.py   # .tscn bake -> binary blobs
python3 processing/unity/validate_unity_map.py     # prove the two agree
```

Both generators support `--check`, which re-runs the conversion in memory and
fails if the committed output differs. CI uses `--check`, so the Unity project can
never silently diverge from the Godot map.

### Why a single grid instead of 64 meshes

The Godot bake stores 64 chunk meshes that share 401 × 401 corner samples
(the chunk borders are duplicated in each mesh). Storing 64 meshes in Unity would
mean 64 mesh assets, duplicated edge vertices, and no way to build a single
collider. Instead the converter de-duplicates everything into one global grid:

* one height array (`401²` float32 = 643,204 B),
* one colour array (`401²` RGBA8 = 643,204 B),
* per-chunk meshes are built at runtime by `SiranIslandTerrain`, and the *physics*
  is one Unity `Terrain` with a heightmap, not 64 mesh colliders.

That is ~1.4 MB of data for the whole island, versus 9.2 MB of text.

### Duplicate-sample consistency (verified)

64 chunks × 51 × 51 samples = 166,464 samples, which map onto 160,801 unique grid
positions. Every duplicated corner must agree; the converter counts conflicts:

```
filled_samples    160801
missing_samples        0
height_conflicts       0      <- no two chunks disagree about the same corner
colour_conflicts       0
```

If this were non-zero, the converter would have reported it and CI would fail.

---

## 3. Formats written by the converter

All files live in `unity-project/Assets/Resources/Config/`, are little-endian, and
are hashed in `siran_island_bake_manifest.json`.

### `siran_island_heights.bytes`

```
float32[size * size]   // 643,204 bytes for size = 401
index = iz * size + ix // ix grows east (+X), iz grows south (+Z)
world x = ix * spacing - half_extent_m      // spacing 10, half_extent 2000
world z = iz * spacing - half_extent_m
value   = height in metres, sea level 0
```

Range: **−38.00 … 64.8359 m**. No axis flip: Godot's `+X`/`+Z` are Unity's `+X`/`+Z`
and both are right-handed with +Y up, so a sample at `(x, z)` stays at `(x, z)`.

### `siran_island_colors.bytes`

```
uint8[size * size * 4]  // RGBA, same indexing as heights
```

Produced by Godot's `IslandHeightmap.color_at()`: biome palette + zone tint +
runway markings. The port uses them as vertex colours on a URP Lit material, so
the greybox look survives with zero textures.

### `siran_island_roads.bytes` (magic `BHROAD01`)

```
header (12 B)
  0  char[8]  "BHROAD01"
  8  uint32   ribbonCount                       // 18
entries (44 B each)
  0  char[32] name (UTF-8, zero padded)
  32 uint32   vertexCount
  36 uint32   indexCount
  40 uint32   dataOffset                        // from start of file
vertex (16 B):
  0  float32 x, y, z
  12 uint8   r, g, b, a
index (uint16)
```

18 ribbons, **4,212 triangles**, all indices in range, normals flat-up (a road is
a ribbon; the converter asserts up-facing winding).

### `siran_island_river.bytes` (magic `BHRIVER1`)

```
header (16 B)
  0  char[8] "BHRIVER1"
  8  uint32  vertexCount            // 350
  12 uint32  indexCount             // 1044  (int32 indices follow)
vertex (12 B): float32 x, y, z
index  (4 B): int32
```

350 vertices = 175 cross-sections × 2 banks, 348 triangles, 3,124.97 m long. The
surface descends **9.0 m → −0.5 m**, which is the gradient that makes the river
flow from the lake to the sea; the validator asserts monotonic descent and that
both banks are level at each cross-section.

### `siran_island_layout.json`

A faithful dump of every constant in `siran_map_config.gd`: sea level, island
radius, noise seed, generator version, spawn point, the 5 zones (centre, size,
rotation, flat height, kind, corner radius), lake, moat, river control points and
widths, and the 5 roads. Everything the C# port exposes as `SiranMapConfig` is
here in data form, so tooling (and future content placement) does not need to read
C#.

### `siran_island_terrain.json`

Metadata for the blobs: grid description, provenance (source scene path +
SHA-256 + generator version + bake timestamp), per-ribbon road table, river stats,
and the sample-consistency counters. Anything reading a `.bytes` file should read
this first rather than hard-coding sizes.

### `water_normal_256.png`

Copied **verbatim** from `assets/textures/water_normal_256.png` (same SHA-256 on
both sides). It lives under `Assets/Resources/Art/Textures/` so `Resources.Load`
can find it without a scene reference, and so the Godot copy stays untouched.

---

## 4. Proof that the port is the same island

`processing/unity/validate_unity_map.py` (CI: `data` job) hashes every manifest
entry, then:

| Check | Result |
| --- | --- |
| manifest SHA-256 for all 6 files | match |
| 166,464 chunk vertices vs the height grid | worst delta **0.000008 m** |
| 64 height-map colliders vs the grid, sample for sample | match |
| vertex colours vs the Godot bake | match |
| road ribbons / triangles | 18 / 4,212 |
| river descent and cross-section levelness | 9.0 → −0.5 m over 175 sections |

The 8 µm worst-case delta is float32 rounding at the 10 m grid, i.e. the two files
are the same surface.

---

## 5. Runtime consumption (Unity)

| Blob | Read by | Notes |
| --- | --- | --- |
| `siran_island_heights.bytes` | `SiranIslandData` / `SiranIslandTerrain` | `TextAsset.bytes` → `float[]`; one `TerrainData` height map |
| `siran_island_colors.bytes` | `SiranIslandTerrain` | baked into per-chunk vertex colour arrays |
| `siran_island_roads.bytes` | `SiranWorldBuilder` | rebuilt as one mesh per ribbon, parented to the chunk it belongs to |
| `siran_island_river.bytes` | `SiranIslandWater` | single mesh; `_BaseMap_ST` scrolls the normal map |
| `siran_island_layout.json` | `SiranIslandData` (validation) | cross-checks the compiled C# constants |
| `Assets/Resources/Art/Textures/water_normal_256.png` | `SiranIslandWater` | `Resources.Load<Texture2D>` |

`Resources.Load<TextAsset>` will **not** load `.bin`, `.rgba` or extension-less
files. That is why the converter writes `.bytes` and why each one has a committed
`.meta` with a `TextScriptImporter` and a stable GUID. If a `.meta` is missing you
get `null` after a fresh clone, which is the single most common way this port can
break; `processing/unity/validate_csharp.py` fails the build if a `.bytes` file has
no `.meta`, or if two assets share a GUID.

---

## 6. Re-baking

The bake is **not** reimplemented in Unity. If the island design changes:

```bash
# in the Godot project (unchanged pipeline)
godot --headless --path . --script tools/bake/generate_siran_island.gd
bash tools/verify_phase3.sh

# then re-import into Unity
python3 processing/unity/port_siran_config.py
python3 processing/unity/convert_godot_island.py
python3 processing/unity/validate_unity_map.py
```

Keeping one baker avoids the classic two-implementations-of-the-same-noise
divergence, and it means the Godot build remains a working fallback that renders
exactly the same island.
