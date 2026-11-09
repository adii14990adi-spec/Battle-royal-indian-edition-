#!/usr/bin/env python3
"""Convert the committed Godot Siran Island bake into Unity-ready map data.

Input (Godot project, never modified)
------------------------------------
    scenes/world/siran_island_baked.tscn        64 terrain chunks + 18 road ribbons
    resources/world/baked/siran_island_summary.tres
    resources/world/baked/siran_river_surface.res   (binary -> rebuilt from config)
    assets/textures/water_normal_256.png

Output (Unity project)
----------------------
    unity-project/Assets/Resources/Config/siran_island_terrain.json
    unity-project/Assets/Resources/Config/siran_island_heights.bytes
    unity-project/Assets/Resources/Config/siran_island_colors.bytes
    unity-project/Assets/Resources/Config/siran_island_roads.bytes
    unity-project/Assets/Resources/Art/Textures/water_normal_256.png  (copied, hashed)
    unity-project/Assets/Resources/Config/siran_island_bake_manifest.json

Why this shape
--------------
The 64 chunk meshes are *redundant* over their shared edges: every chunk is a
51 x 51 grid that shares its outer row/column with its neighbour. Collapsing the
edges into one 401 x 401 grid is therefore lossless — and `verify_scene()` proves
it (no conflicting height *or* colour at any shared sample). A single grid lets
Unity rebuild either a Terrain (cheap height-field collision) or the original
64 chunk meshes (identical visuals) from one small blob, instead of shipping
9.5 MB of duplicated text-encoded geometry.

Coordinate mapping
------------------
Godot and Unity both use metres, Y-up and a left-handed XZ plane here, so **no
axis flip is applied**: world (x, y, z) passes through unchanged. Godot's +Z is
south in this project's convention, Unity's +Z is forward; the harness keeps the
Godot convention so every number in docs/PHASE3_WORLD.md stays true. Winding is
left as authored (both engines treat clockwise-from-the-front as front-facing).
"""

from __future__ import annotations

import argparse
import hashlib
import json
import struct
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

import godot_bake_reader as bake  # noqa: E402

REPO_ROOT = Path(__file__).resolve().parents[2]
GODOT_BAKED_SCENE = REPO_ROOT / "scenes" / "world" / "siran_island_baked.tscn"
GODOT_SUMMARY = REPO_ROOT / "resources" / "world" / "baked" / "siran_island_summary.tres"
GODOT_RIVER = REPO_ROOT / "resources" / "world" / "baked" / "siran_river_surface.res"
GODOT_WATER_NORMAL = REPO_ROOT / "assets" / "textures" / "water_normal_256.png"

UNITY_CONFIG_DIR = REPO_ROOT / "unity-project" / "Assets" / "Resources" / "Config"
UNITY_TEXTURE_DIR = REPO_ROOT / "unity-project" / "Assets" / "Resources" / "Art" / "Textures"

TERRAIN_JSON = UNITY_CONFIG_DIR / "siran_island_terrain.json"
HEIGHTS_BIN = UNITY_CONFIG_DIR / "siran_island_heights.bytes"
COLORS_BIN = UNITY_CONFIG_DIR / "siran_island_colors.bytes"
ROADS_BIN = UNITY_CONFIG_DIR / "siran_island_roads.bytes"
MANIFEST_JSON = UNITY_CONFIG_DIR / "siran_island_bake_manifest.json"
WATER_NORMAL_COPY = UNITY_TEXTURE_DIR / "water_normal_256.png"


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for block in iter(lambda: handle.read(1 << 20), b""):
            digest.update(block)
    return digest.hexdigest()


def build_grid(scene: bake.BakedScene, grid_size: int, half_extent: float, spacing: float):
    """Collapse the 64 overlapping chunk meshes into one global grid.

    Returns (heights, colours, conflicts). Any disagreement at a shared sample
    is reported rather than silently resolved: it would mean the chunk meshes
    are not actually a single continuous surface.
    """
    heights = [float("nan")] * (grid_size * grid_size)
    colours = [None] * (grid_size * grid_size)
    height_conflicts = 0
    colour_conflicts = 0
    fill = 0

    for name, chunk in scene.terrain_chunks.items():
        origin = scene.chunk_transforms.get(name)
        if origin is None:
            raise SystemExit(f"::error::no node transform for {name}")
        _, oy, _ = origin
        for index, (x, y, z) in enumerate(chunk.positions):
            ix = round((origin[0] + x + half_extent) / spacing)
            iz = round((origin[2] + z + half_extent) / spacing)
            if not (0 <= ix < grid_size and 0 <= iz < grid_size):
                raise SystemExit(f"::error::{name} vertex outside the map grid")
            cell = iz * grid_size + ix
            world_height = oy + y
            colour = chunk.colors[index] if chunk.colors else (255, 255, 255, 255)
            if heights[cell] != heights[cell]:  # NaN -> first write
                heights[cell] = world_height
                colours[cell] = colour
                fill += 1
            else:
                if abs(heights[cell] - world_height) > 1e-3:
                    height_conflicts += 1
                if colours[cell] != colour:
                    colour_conflicts += 1

    missing = [i for i, value in enumerate(heights) if value != value]
    return heights, colours, {
        "filled_samples": fill,
        "missing_samples": len(missing),
        "height_conflicts": height_conflicts,
        "colour_conflicts": colour_conflicts,
    }


def encode_roads(scene: bake.BakedScene) -> tuple[bytes, list[dict]]:
    """Pack every road ribbon into one blob: u32 header + f32/rgba/u16 payload."""
    ribbons: list[dict] = []
    payload = bytearray()
    # Layout: magic, ribbon count, then per ribbon
    #   name(32 bytes, padded) | vertex_count u32 | index_count u32 |
    #   data_offset u32 (from the start of the data section)
    header = bytearray(b"BHROAD01")
    ordered = sorted(scene.road_ribbons.items())
    header += struct.pack("<I", len(ordered))
    data = bytearray()
    for name, ribbon in ordered:
        vertex_offset = len(data)
        for i, (x, y, z) in enumerate(ribbon.positions):
            data += struct.pack("<3f", x, y, z)
            colour = ribbon.colors[i] if i < len(ribbon.colors) else (255, 255, 255, 255)
            data += bytes(colour)
        for index in ribbon.indices:
            data += struct.pack("<H", index)
        name_bytes = name.encode("utf-8")[:31].ljust(32, b"\0")
        header += name_bytes
        header += struct.pack("<III", ribbon.vertex_count, ribbon.index_count, vertex_offset)
        ribbons.append(
            {
                "name": name,
                "vertex_count": ribbon.vertex_count,
                "index_count": ribbon.index_count,
                "triangles": ribbon.triangles,
                "offset": vertex_offset,
                "bytes": ribbon.vertex_count * 16 + ribbon.index_count * 2,
            }
        )
    payload += header
    payload += data
    return bytes(payload), ribbons


def build_river_ribbon(layout: dict, terrain_heights, grid_size: int, half_extent: float,
                       spacing: float) -> tuple[bytes, dict] | None:
    """Rebuild the river water ribbon from the authored centreline.

    `siran_river_surface.res` is a *binary* Godot resource, so it cannot be read
    without Godot. The ribbon is fully determined by the layout data
    (`IslandRiver.points` + `widths` + `surface_start/end`), which is exactly what
    `SurfaceMeshBuilder.build_river_mesh()` consumes, so it is rebuilt here with
    the same 18 m sample step and the same eased surface profile.
    """
    river = layout.get("river")
    if not river or len(river["points"]) < 2:
        return None

    points = [(p[0], p[1]) for p in river["points"]]
    widths = river["widths"]
    length = 0.0
    for i in range(1, len(points)):
        length += ((points[i][0] - points[i - 1][0]) ** 2
                   + (points[i][1] - points[i - 1][1]) ** 2) ** 0.5
    step = 18.0  # SurfaceMeshBuilder.RIVER_SAMPLE_STEP_M
    count = max(2, int(-(-length // step)) + 1)

    def point_at(distance_m: float) -> tuple[float, float]:
        remaining = max(0.0, min(distance_m, length))
        for i in range(1, len(points)):
            ax, az = points[i - 1]
            bx, bz = points[i]
            segment = ((bx - ax) ** 2 + (bz - az) ** 2) ** 0.5
            if remaining <= segment and segment > 0:
                t = remaining / segment
                return (ax + (bx - ax) * t, az + (bz - az) * t)
            remaining -= segment
        return points[-1]

    def direction_at(t: float) -> tuple[float, float]:
        span = 6.0 / max(length, 1.0)
        bx, bz = point_at(length * max(t - span, 0.0))
        ax, az = point_at(length * min(t + span, 1.0))
        dx, dz = ax - bx, az - bz
        norm = (dx * dx + dz * dz) ** 0.5
        return (1.0, 0.0) if norm < 1e-4 else (dx / norm, dz / norm)

    vertices: list[tuple[float, float, float]] = []
    indices: list[int] = []
    for i in range(count):
        t = i / (count - 1)
        cx, cz = point_at(length * t)
        tx, tz = direction_at(t)
        across = (-tz, tx)
        position = t * (len(widths) - 1)
        low = int(min(max(position, 0.0), len(widths) - 1))
        high = min(low + 1, len(widths) - 1)
        width = widths[low] + (widths[high] - widths[low]) * (position - low)
        half = width * 0.5
        surface = river["surface_start"] + (river["surface_end"] - river["surface_start"]) * bake.smooth01(t)
        vertices.append((cx - across[0] * half, surface, cz - across[1] * half))
        vertices.append((cx + across[0] * half, surface, cz + across[1] * half))

    for i in range(count - 1):
        a = i * 2
        b = a + 1
        c = a + 2
        d = a + 3
        indices += [a, c, b, b, c, d]

    blob = bytearray(b"BHRIVER1")
    blob += struct.pack("<II", len(vertices), len(indices))
    for x, y, z in vertices:
        blob += struct.pack("<3f", x, y, z)
    for index in indices:
        blob += struct.pack("<I", index)

    return bytes(blob), {
        "vertex_count": len(vertices),
        "index_count": len(indices),
        "triangles": len(indices) // 3,
        "length_m": round(length, 2),
    }


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true",
                        help="fail if the committed Unity data is out of date")
    parser.add_argument("--strict-warnings", action="store_true",
                        help="treat non-fatal bake warnings as errors")
    args = parser.parse_args(argv)

    for required in (GODOT_BAKED_SCENE, GODOT_SUMMARY):
        if not required.exists():
            print(f"::error::missing Godot bake input {required.relative_to(REPO_ROOT)}")
            return 2

    scene = bake.read_baked_scene(GODOT_BAKED_SCENE)
    summary = bake.read_summary(GODOT_SUMMARY)
    layout = json.loads(
        (REPO_ROOT / "unity-project" / "Assets" / "Resources" / "Config"
         / "siran_island_layout.json").read_text(encoding="utf-8")
    )

    chunks_per_side = round(layout["map_size_m"] / layout["chunk_size_m"])
    issues = bake.verify_scene(scene, chunks_per_side)
    errors = [issue for issue in issues if issue.severity == "error"]
    warnings = [issue for issue in issues if issue.severity == "warning"]
    for issue in issues:
        stream = sys.stderr if issue.severity == "error" else sys.stdout
        print(f"{'::error::' if issue.severity == 'error' else '::warning::'}{issue}",
              file=stream)
    if errors or (args.strict_warnings and warnings):
        print("::error::the Godot bake failed conversion preconditions", file=sys.stderr)
        return 1

    map_size = float(layout["map_size_m"])
    spacing = float(layout["vertex_spacing_m"])
    chunk_size = float(layout["chunk_size_m"])
    half_extent = map_size * 0.5
    grid_size = round(map_size / spacing) + 1  # 401 samples across 400 cells

    heights, colours, grid_stats = build_grid(scene, grid_size, half_extent, spacing)
    if grid_stats["missing_samples"]:
        print(f"::error::{grid_stats['missing_samples']} grid samples were never written",
              file=sys.stderr)
        return 1
    if grid_stats["height_conflicts"] or grid_stats["colour_conflicts"]:
        print(f"::error::chunk meshes disagree at shared edges "
              f"({grid_stats['height_conflicts']} heights, "
              f"{grid_stats['colour_conflicts']} colours); a single grid would not be lossless",
              file=sys.stderr)
        return 1

    heights_blob = struct.pack("<%df" % len(heights), *[round(h, 5) for h in heights])
    colours_blob = bytearray()
    for colour in colours:
        colours_blob += bytes(colour if colour else (255, 255, 255, 255))

    roads_blob, road_manifest = encode_roads(scene)
    river = build_river_ribbon(layout, heights, grid_size, half_extent, spacing)

    min_height = min(heights)
    max_height = max(heights)
    terrain_doc = {
        "schema_version": 1,
        "generated_by": "processing/unity/convert_godot_island.py",
        "provenance": {
            "godot_scene": str(GODOT_BAKED_SCENE.relative_to(REPO_ROOT)),
            "godot_scene_sha256": sha256(GODOT_BAKED_SCENE),
            "godot_summary": str(GODOT_SUMMARY.relative_to(REPO_ROOT)),
            "godot_generator_version": summary.get("generator_version"),
            "godot_noise_seed": summary.get("noise_seed"),
            "godot_baked_at_utc": summary.get("baked_at_utc"),
            "note": (
                "Converted from the committed Godot bake. The island geometry is "
                "reused as-is: no terrain is regenerated, so the Unity and Godot "
                "maps are the same map."
            ),
        },
        "grid": {
            "size": grid_size,
            "spacing_m": spacing,
            "half_extent_m": half_extent,
            "chunk_size_m": chunk_size,
            "chunks_per_side": chunks_per_side,
            "index_order": "iz * size + ix, ix grows east (+X), iz grows south (+Z)",
            "sample_centre_offset_m": 0.0,
        },
        "heights": {
            "file": HEIGHTS_BIN.name,
            "dtype": "float32-le",
            "count": len(heights),
            "min_m": round(min_height, 4),
            "max_m": round(max_height, 4),
            "sea_level_m": float(layout["sea_level"]),
        },
        "colors": {
            "file": COLORS_BIN.name,
            "dtype": "rgba8",
            "count": len(colours),
            "note": (
                "Greybox vertex colours baked by IslandHeightmap.color_at: biome "
                "palette + zone tint + runway markings. Zero textures."
            ),
        },
        "roads": {"file": ROADS_BIN.name, "ribbons": road_manifest},
        "stats": {
            "grid": grid_stats,
            "terrain_vertices": summary.get("terrain_vertices_total"),
            "terrain_triangles": summary.get("terrain_triangles_total"),
            "godot_chunk_count": summary.get("chunk_count"),
            "spawn_ground_height_m": summary.get("spawn_ground_height_m"),
        },
        "warnings": [str(issue) for issue in warnings],
    }

    river_bytes = b""
    if river:
        river_bytes, river_info = river
        terrain_doc["river"] = {"file": "siran_island_river.bytes", **river_info}

    manifest = {
        "bake_source": str(GODOT_BAKED_SCENE.relative_to(REPO_ROOT)),
        "bake_source_sha256": terrain_doc["provenance"]["godot_scene_sha256"],
        "files": {},
        "textures": {
            "water_normal_256.png": {
                "source": str(GODOT_WATER_NORMAL.relative_to(REPO_ROOT)),
                "sha256": sha256(GODOT_WATER_NORMAL),
                "note": "copied verbatim from the Godot project; both copies are hashed",
            }
        },
    }

    if args.check:
        stale: list[str] = []
        expected = {
            HEIGHTS_BIN: heights_blob,
            COLORS_BIN: bytes(colours_blob),
            ROADS_BIN: roads_blob,
        }
        if river_bytes:
            expected[UNITY_CONFIG_DIR / "siran_island_river.bytes"] = river_bytes
        for path, blob in expected.items():
            if not path.exists() or path.read_bytes() != blob:
                stale.append(str(path.relative_to(REPO_ROOT)))
        if stale:
            for path in stale:
                print(f"::error::{path} is out of date; run convert_godot_island.py")
            return 1
        print("Unity map data matches the Godot bake.")
        return 0

    UNITY_CONFIG_DIR.mkdir(parents=True, exist_ok=True)
    UNITY_TEXTURE_DIR.mkdir(parents=True, exist_ok=True)
    HEIGHTS_BIN.write_bytes(heights_blob)
    COLORS_BIN.write_bytes(bytes(colours_blob))
    ROADS_BIN.write_bytes(roads_blob)
    if river_bytes:
        (UNITY_CONFIG_DIR / "siran_island_river.bytes").write_bytes(river_bytes)
    WATER_NORMAL_COPY.write_bytes(GODOT_WATER_NORMAL.read_bytes())

    for path in (HEIGHTS_BIN, COLORS_BIN, ROADS_BIN, UNITY_CONFIG_DIR / "siran_island_river.bytes",
                 WATER_NORMAL_COPY):
        if path.exists():
            manifest["files"][path.name] = {"bytes": path.stat().st_size, "sha256": sha256(path)}

    TERRAIN_JSON.write_text(json.dumps(terrain_doc, indent=2) + "\n", encoding="utf-8")
    manifest["files"][TERRAIN_JSON.name] = {
        "bytes": TERRAIN_JSON.stat().st_size,
        "sha256": sha256(TERRAIN_JSON),
    }
    MANIFEST_JSON.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")

    print(f"grid {grid_size} x {grid_size} @ {spacing} m "
          f"({grid_stats['filled_samples']} samples, 0 conflicts)")
    print(f"heights {min_height:.2f} .. {max_height:.2f} m above sea level")
    print(f"road ribbons {len(road_manifest)} ({sum(r['triangles'] for r in road_manifest)} tris)")
    if river:
        print(f"river ribbon {terrain_doc['river']['triangles']} tris, "
              f"{terrain_doc['river']['length_m']} m long")
    print(f"wrote {TERRAIN_JSON.relative_to(REPO_ROOT)}")
    print(f"wrote {HEIGHTS_BIN.relative_to(REPO_ROOT)} ({HEIGHTS_BIN.stat().st_size} bytes)")
    print(f"wrote {COLORS_BIN.relative_to(REPO_ROOT)} ({COLORS_BIN.stat().st_size} bytes)")
    print(f"wrote {ROADS_BIN.relative_to(REPO_ROOT)} ({ROADS_BIN.stat().st_size} bytes)")
    print(f"wrote {MANIFEST_JSON.relative_to(REPO_ROOT)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
