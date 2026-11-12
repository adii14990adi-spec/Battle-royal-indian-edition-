# Siran Island Unity data formats

This document describes the committed Unity Resources used at runtime. They are
the source of truth for the Unity-only repository; no external engine project or
conversion script is needed. Integrity and bounds checks are run by
`python3 processing/unity/validate_unity_map.py`.

All map payloads are under
`unity-project/Assets/Resources/Config/`. Values use metres in world space; X is
east, Y is up and Z is south. The island is centered at the world origin.

## Grid metadata

`siran_island_terrain.json` records the grid and payload layout:

* 401 × 401 samples, indexed `iz * size + ix` (X changes fastest);
* 10 m sample spacing over a 4,000 × 4,000 m map;
* height range −38.0 m to 64.8359 m, sea level 0 m;
* 500 m source tiles, 8 × 8 tiles with shared boundary samples.

At sample `(ix, iz)`:

```text
world_x = ix * spacing_m - half_extent_m
world_z = iz * spacing_m - half_extent_m
```

The Unity runtime builds each 500 m tile as 51 × 51 vertices. Tile renderers use
the shared 50 m radial shader clip (inside only), while a 20 m full-island shell
uses the same shader with the inverse clip; the separate heightmap collider
covers the full map. The road and river blobs are baked counter-clockwise and are
flipped to Unity's clockwise-up winding when loaded.

## `siran_island_heights.bytes`

```text
float32[size * size]  // little-endian, row-major iz * size + ix
```

For a 401 × 401 grid this is 643,204 bytes. Values are heights in metres.

## `siran_island_colors.bytes`

```text
uint8[size * size * 4]  // RGBA8, same sample ordering as heights
```

The colour of each grid sample is copied to the generated tile mesh as a vertex
colour and used by the streamed URP material.

## `siran_island_roads.bytes` (`BHROAD01`)

```text
header (12 bytes)
  char[8]   magic = "BHROAD01"
  uint32    ribbon_count

directory (44 bytes per ribbon)
  char[32]  UTF-8 name, zero-padded
  uint32    vertex_count
  uint32    index_count
  uint32    data_offset, relative to the data section

data section (per ribbon)
  vertex_count × (float32 x, y, z + uint8 r, g, b, a)
  index_count × uint16
```

The committed file contains 18 road ribbons and 4,212 triangles. The validator
checks each offset, payload length and index range before the Unity runtime loads
the data.

## `siran_island_river.bytes` (`BHRIVER1`)

```text
header (16 bytes)
  char[8]   magic = "BHRIVER1"
  uint32    vertex_count
  uint32    index_count

payload
  vertex_count × float32 x, y, z
  index_count × int32
```

The committed river has 350 vertices and 348 triangles. Sections are left/right
pairs; their heights are level across the ribbon and descend along its length.

## Manifest and validation

`siran_island_manifest.json` records byte counts and SHA-256 hashes for the map
metadata, binary payloads, layout snapshot and water texture. It also states the
fixed 50 m streaming radius.

Run:

```bash
python3 processing/unity/validate_unity_map.py
```

The validator uses only Python's standard library. It verifies hashes and file
sizes; grid/tiling dimensions; finite height values and their range; road and
river headers, indices and bounds; river descent; and the water texture path/hash.
It does not compare against an external project or claim Unity engine execution.
