#!/usr/bin/env python3
"""Read the committed Godot 4 bake and turn it into engine-neutral map data.

Why this exists
---------------
The Godot project (`/`, the repo root) already contains a *finished, committed*
4 km Siran Island bake:

    scenes/world/siran_island_baked.tscn     64 terrain chunks + 18 road ribbons
    resources/world/baked/siran_river_surface.res
    resources/world/baked/siran_island_summary.tres

Re-generating that island in C#/Unity would mean re-deriving FastNoiseLite noise,
which cannot be guaranteed to match Godot's, and would throw away the terrain
somebody already reviewed. So the Unity side *imports the existing bake*:

  1. this module parses the Godot text scene (no Godot installation needed),
  2. `convert_godot_island.py` writes engine-neutral data
     (`unity-project/Assets/Resources/Config/siran_island_terrain.json`
     + 64 + 18 Unity `.asset` meshes + a manifest),
  3. `SiranIslandTerrainBuilder` (C#, Editor) turns that into a Unity Terrain
     data asset + prefabs at import/build time.

Format notes (all reverse-engineered from the committed file and verified by
the assertions below — see docs/migration/BAKE_FORMAT.md):

  * An `ArrayMesh` in a Godot 4 text scene stores each surface as a dictionary
    with `aabb`, `attribute_data`, `format`, `index_count`, `index_data`,
    `primitive`, `uv_scale`, `vertex_count`, `vertex_data`.
  * `format` = `ARRAY_FORMAT_*` bitfield. `34359742479` = `0x1000` (bit 12 =
    `ARRAY_FLAG_COMPRESS_ATTRIBUTES`) | VERTEX | NORMAL | TANGENT | COLOR |
    `ARRAY_FLAG_FORMAT_VERSION_BASE` (bit 35).
  * Because the compressed-attributes flag is set, `vertex_data` is **not**
    interleaved: it is all `vertex_count` positions first (float32 x 3, so
    `vertex_count * 12` bytes), then all packed attributes (here normal as
    2 x int16 octahedral + tangent as 2 x int16 = 8 bytes per vertex). Colours
    are pulled out into `attribute_data` (4 bytes per vertex, R8G8B8A8).
    The non-compressed (array-of-structures) layout is supported too.
  * `attribute_data` is 4 bytes per vertex (RGBA8) when COLOR is present.
  * `primitive` 3 = TRIANGLES. Godot's front face is clockwise, Unity's is
    clockwise too (both use a left-handed Y-up space), so the winding is
    preserved unchanged.
  * `HeightMapShape3D` (`map_width` x `map_depth` x `map_data`) is the chunk
    collider, already sampled on the exact same grid as the mesh.

Vertex *normals* in the terrain meshes are deliberately treated as unusable:
they are stored in the packed (non-color) layout and many of them decode to
garbage for vertices whose Z is negative. Unity recomputes terrain normals from
the height field, so nothing is lost — and `verify` asserts that every vertex
position round-trips through the height field exactly.
"""

from __future__ import annotations

import base64
import json
import math
import re
from dataclasses import dataclass, field
from pathlib import Path

# --- ArrayMesh format bitfield (Godot 4.4, mesh.h) ---------------------------
ARRAY_FORMAT_VERTEX = 1 << 0
ARRAY_FORMAT_NORMAL = 1 << 1
ARRAY_FORMAT_TANGENT = 1 << 2
ARRAY_FORMAT_COLOR = 1 << 3
ARRAY_FORMAT_TEX_UV = 1 << 4
ARRAY_FORMAT_INDEX = 1 << 6
ARRAY_FORMAT_VERSION_BITS = 1 << 35

PRIMITIVE_TRIANGLES = 3


class BakeFormatError(RuntimeError):
    """The committed bake does not look like what this reader understands."""


@dataclass
class Surface:
    """One Godot surface (a chunk terrain mesh or a road ribbon)."""

    name: str
    vertex_count: int
    index_count: int
    positions: list[tuple[float, float, float]] = field(default_factory=list)
    colors: list[tuple[int, int, int, int]] = field(default_factory=list)
    indices: list[int] = field(default_factory=list)
    aabb: tuple[float, float, float, float, float, float] = (0, 0, 0, 0, 0, 0)
    format_bits: int = 0
    has_normals: bool = False
    normal_stride: int = 0
    primitive: int = PRIMITIVE_TRIANGLES

    @property
    def triangles(self) -> int:
        return self.index_count // 3


@dataclass
class ChunkCollider:
    name: str
    width: int
    depth: int
    heights: list[float]


_UNPACK_RE = {
    "PackedByteArray": re.compile(r'"%s": PackedByteArray\("([^"]*)"\)'),
    "PackedFloat32Array": re.compile(r"map_data = PackedFloat32Array\(([^)]*)\)"),
}


def _read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def _block(pattern: str, text: str) -> list[tuple[str, str]]:
    """Return [(id, body)] for every `[sub_resource type="<pattern>" ...]` block."""
    hits = re.findall(
        r'\[sub_resource type="%s" id="([^"]+)"\](.*?)(?=\n\[|\Z)' % pattern,
        text,
        re.S,
    )
    return hits


def _parse_aabb(raw: str) -> tuple[float, float, float, float, float, float]:
    numbers = [float(v) for v in re.findall(r"-?\d+(?:\.\d+)?(?:e-?\d+)?", raw)]
    if len(numbers) != 6:
        raise BakeFormatError(f"unexpected AABB: {raw!r}")
    return tuple(numbers)  # type: ignore[return-value]


def _decode_surface(name: str, body: str) -> Surface:
    def field_value(key: str) -> str:
        # `aabb` is an `AABB(x, y, z, w, h, d)` constructor and therefore
        # contains commas, so it cannot use the simple "stop at a comma" rule.
        if key == "aabb":
            match = re.search(r'"aabb": (AABB\([^)]*\))', body)
        else:
            match = re.search(r'"%s": ([^,\n]+)' % key, body)
        if not match:
            raise BakeFormatError(f"{name}: missing surface key {key!r}")
        return match.group(1).strip()

    def byte_array(key: str) -> bytes:
        match = re.search(r'"%s": PackedByteArray\("([^"]*)"\)' % key, body)
        if not match:
            raise BakeFormatError(f"{name}: missing byte array {key!r}")
        return base64.b64decode(match.group(1))

    vertex_count = int(field_value("vertex_count"))
    index_count = int(field_value("index_count"))
    format_bits = int(field_value("format"))
    primitive = int(field_value("primitive"))
    if primitive != PRIMITIVE_TRIANGLES:
        raise BakeFormatError(f"{name}: primitive {primitive} is not TRIANGLES")

    vertex_data = byte_array("vertex_data")
    index_data = byte_array("index_data")
    attribute_data = (
        byte_array("attribute_data") if '"attribute_data"' in body else b""
    )

    if index_count * 4 != len(index_data) and index_count * 2 != len(index_data):
        raise BakeFormatError(
            f"{name}: index_data is {len(index_data)} bytes for {index_count} indices"
        )
    index_stride = 4 if index_count * 4 == len(index_data) else 2
    if vertex_count <= 0 or len(vertex_data) % vertex_count:
        raise BakeFormatError(
            f"{name}: vertex_data is {len(vertex_data)} bytes for {vertex_count} verts"
        )
    stride = len(vertex_data) // vertex_count

    # Two on-disk layouts:
    #   compressed (ARRAY_FLAG_COMPRESS_ATTRIBUTES, what this bake uses):
    #       [vert_count * 12 bytes of positions][vert_count * attrib_stride bytes]
    #       attrib_stride = 4 per (normal, tangent pair packed as 2 x i16 each)
    #       => 20 bytes/vertex overall for VERTEX|NORMAL|TANGENT with colours in
    #          attribute_data.
    #   non-compressed (array of structures): position then normals packed
    #       straight after each position.
    # The compressed flag is bit 12 of the format field.
    compressed = bool(format_bits & (1 << 12))
    if compressed:
        if len(vertex_data) < vertex_count * 12:
            raise BakeFormatError(f"{name}: vertex_data too short for positions")
        position_base = 0
        attribute_base = vertex_count * 12
        attribute_stride = (len(vertex_data) - attribute_base) // vertex_count
    else:
        position_base = 0
        attribute_base = 12
        attribute_stride = stride - 12
        if attribute_stride < 0:
            raise BakeFormatError(f"{name}: vertex stride {stride} too small")

    if stride not in (12, 20, 24):
        raise BakeFormatError(f"{name}: unsupported vertex stride {stride}")

    positions: list[tuple[float, float, float]] = []
    for i in range(vertex_count):
        base = position_base + i * (12 if compressed else stride)
        x = _f32(vertex_data, base)
        y = _f32(vertex_data, base + 4)
        z = _f32(vertex_data, base + 8)
        positions.append((x, y, z))

    colors: list[tuple[int, int, int, int]] = []
    if attribute_data:
        if len(attribute_data) < vertex_count * 4:
            raise BakeFormatError(
                f"{name}: attribute_data is {len(attribute_data)} bytes, "
                f"need {vertex_count * 4}"
            )
        for i in range(vertex_count):
            base = i * 4
            colors.append(tuple(attribute_data[base:base + 4]))  # type: ignore[arg-type]

    if index_stride == 2:
        indices = list(_u16_many(index_data, index_count))
    else:
        indices = list(_u32_many(index_data, index_count))
    if indices and max(indices) >= vertex_count:
        raise BakeFormatError(
            f"{name}: index {max(indices)} out of range for {vertex_count} verts"
        )

    return Surface(
        name=name,
        vertex_count=vertex_count,
        index_count=index_count,
        positions=positions,
        colors=colors,
        indices=indices,
        aabb=_parse_aabb(field_value("aabb")),
        format_bits=format_bits,
        has_normals=bool(format_bits & ARRAY_FORMAT_NORMAL),
        normal_stride=attribute_stride if compressed else stride - 12,
        primitive=primitive,
    )


def _f32(buffer: bytes, offset: int) -> float:
    import struct

    return struct.unpack_from("<f", buffer, offset)[0]


def _u16_many(buffer: bytes, count: int):
    import struct

    return struct.unpack_from("<%dH" % count, buffer, 0)


def _u32_many(buffer: bytes, count: int):
    import struct

    return struct.unpack_from("<%dI" % count, buffer, 0)


@dataclass
class BakedScene:
    path: Path
    surfaces: dict[str, Surface]
    colliders: dict[str, ChunkCollider]
    chunk_transforms: dict[str, tuple[float, float, float]]
    materials: dict[str, str]

    @property
    def terrain_chunks(self) -> dict[str, Surface]:
        return {k: v for k, v in self.surfaces.items() if k.startswith("terrain_chunk_")}

    @property
    def road_ribbons(self) -> dict[str, Surface]:
        return {k: v for k, v in self.surfaces.items() if k.startswith("road_chunk_")}


def read_baked_scene(path: Path) -> BakedScene:
    """Parse `siran_island_baked.tscn` without Godot."""
    text = _read_text(path)

    surfaces: dict[str, Surface] = {}
    for surface_id, body in _block("ArrayMesh", text):
        name_match = re.search(r'resource_name = "([^"]*)"', body)
        if not name_match:
            # ArrayMeshes created without a resource_name are not part of the map.
            continue
        surface = _decode_surface(name_match.group(1), body)
        surfaces[surface.name] = surface

    colliders: dict[str, ChunkCollider] = {}
    for _collider_id, body in _block("HeightMapShape3D", text):
        width = int(re.search(r"map_width = (\d+)", body).group(1))
        depth = int(re.search(r"map_depth = (\d+)", body).group(1))
        raw = re.search(r"map_data = PackedFloat32Array\(([^)]*)\)", body).group(1)
        heights = [float(v) for v in raw.split(",")]
        if len(heights) != width * depth:
            raise BakeFormatError(
                f"height map is {len(heights)} samples for {width}x{depth}"
            )
        name_match = re.search(r'resource_name = "([^"]*)"', body)
        name = name_match.group(1) if name_match else "terrain_collision"
        colliders[name] = ChunkCollider(name, width, depth, heights)

    # Node section: `Chunk_x_z` nodes carry the chunk origin in their transform.
    # Node section: `Chunk_x_z` nodes are placed at the chunk centre. The
    # Transform3D is (basis.xx, xy, xz, yx, yy, yz, zx, zy, zz, origin.xyz),
    # stored column-major in the file, so the origin is the last three numbers.
    chunk_transforms: dict[str, tuple[float, float, float]] = {}
    for node_block in re.finditer(
        r'\[node name="Chunk_(\d+)_(\d+)" type="Node3D" parent="\."\]([^\[]*)', text
    ):
        cx, cz, body = node_block.group(1), node_block.group(2), node_block.group(3)
        transform = re.search(r"transform = Transform3D\(([^)]*)\)", body)
        numbers = (
            [float(v) for v in re.findall(r"-?[\d.]+(?:e-?\d+)?", transform.group(1))]
            if transform
            else []
        )
        origin = (numbers[9], numbers[10], numbers[11]) if len(numbers) == 12 else (0.0, 0.0, 0.0)
        chunk_transforms["terrain_chunk_%s_%s" % (cx, cz)] = origin

    materials: dict[str, str] = {}
    for ext_id, res_path in re.findall(
        r'\[ext_resource type="Material" path="res://([^"]+)" id="([^"]+)"\]', text
    ):
        materials[res_path] = ext_id

    if not surfaces:
        raise BakeFormatError(f"{path} contains no named meshes")
    return BakedScene(path, surfaces, colliders, chunk_transforms, materials)


GODOT_BINARY_MAGIC = b"RSRC"


def read_river_surface(path: Path) -> Surface | None:
    """Parse `siran_river_surface.res`.

    Godot writes `.res` as a **binary** resource (magic ``RSRC``), so this returns
    ``None`` for it and the converter rebuilds the ribbon from the authored river
    data instead (exactly what `SurfaceMeshBuilder.build_river_mesh` does). If a
    future bake commits the text form, it is parsed here.
    """
    if not path.exists():
        return None
    with path.open("rb") as handle:
        magic = handle.read(4)
    if magic == GODOT_BINARY_MAGIC:
        return None
    text = _read_text(path)
    name = "siran_river_surface"
    body_match = re.search(r"\[resource\](.*)", text, re.S)
    if not body_match:
        raise BakeFormatError(f"{path} has no [resource] section")
    body = body_match.group(1)
    # The saved mesh uses the same surface dictionary shape as a sub_resource.
    return _decode_surface(name, body)


def read_summary(path: Path) -> dict:
    """Parse `siran_island_summary.tres` into plain python (best effort)."""
    text = _read_text(path)
    summary: dict = {}
    for key in (
        "display_name",
        "generator_version",
        "noise_seed",
        "baked_at_utc",
        "map_size_m",
        "chunk_size_m",
        "vertex_spacing_m",
        "chunk_count",
        "grid_vertices_per_axis",
        "terrain_vertices_total",
        "terrain_triangles_total",
        "min_height_m",
        "max_height_m",
        "sea_level",
        "spawn_ground_height_m",
    ):
        match = re.search(r"^%s = (.+)$" % key, text, re.M)
        if not match:
            continue
        raw = match.group(1).strip().strip('"')
        try:
            summary[key] = float(raw) if "." in raw or "e" in raw else int(raw)
        except ValueError:
            summary[key] = raw
    zones = re.search(r"zone_stats = \{(.*?)\n\}", text, re.S)
    if zones:
        try:
            summary["zone_stats"] = json.loads("{" + zones.group(1) + "}")
        except json.JSONDecodeError:
            summary["zone_stats"] = {}
    return summary


# --- Validation --------------------------------------------------------------

@dataclass
class Issue:
    severity: str  # "error" | "warning"
    where: str
    message: str

    def __str__(self) -> str:
        return f"[{self.severity}] {self.where}: {self.message}"


def verify_scene(scene: BakedScene, expected_chunks_per_side: int = 8) -> list[Issue]:
    """Structural checks that must pass before anything is written to Unity.

    These are the checks that catch the failure this project already shipped
    once on the Godot side: a map that looks baked but is not really there.
    """
    issues: list[Issue] = []
    expected_chunks = expected_chunks_per_side * expected_chunks_per_side

    chunks = scene.terrain_chunks
    if len(chunks) != expected_chunks:
        issues.append(
            Issue(
                "error",
                "scene",
                f"{len(chunks)} terrain chunks, expected {expected_chunks}",
            )
        )
    if len(scene.colliders) != expected_chunks:
        issues.append(
            Issue(
                "error",
                "scene",
                f"{len(scene.colliders)} height map colliders, expected {expected_chunks}",
            )
        )

    for name, chunk in sorted(chunks.items()):
        if chunk.triangles != (51 - 1) * (51 - 1) * 2:
            issues.append(
                Issue("error", name, f"{chunk.triangles} triangles, expected 5000")
            )
        xs = [p[0] for p in chunk.positions]
        ys = [p[1] for p in chunk.positions]
        zs = [p[2] for p in chunk.positions]
        if any(abs(v) > 1e6 or v != v for v in xs + ys + zs):
            issues.append(Issue("error", name, "corrupt vertex positions"))
            continue
        span_x = max(xs) - min(xs)
        span_z = max(zs) - min(zs)
        if abs(span_x - 500.0) > 0.01 or abs(span_z - 500.0) > 0.01:
            issues.append(
                Issue("error", name, f"chunk spans {span_x:.1f} x {span_z:.1f} m")
            )
        if not chunk.colors:
            issues.append(Issue("warning", name, "no vertex colours (greybox look)"))

        collider = scene.colliders.get("terrain_collision_" + name.split("_", 2)[2])
        if collider is None:
            issues.append(Issue("error", name, "no matching height map collider"))
            continue
        if (collider.width, collider.depth) != (51, 51):
            issues.append(
                Issue(
                    "error",
                    collider.name,
                    f"height map is {collider.width}x{collider.depth}, expected 51x51",
                )
            )
            continue
        # The mesh vertex -> collider sample mapping. Vertex (lx, lz) of the
        # chunk is sample index lz * 51 + lx (row-major, X fastest).
        worst = 0.0
        bad = 0
        for vertex_index, (x, y, z) in enumerate(chunk.positions):
            lx = round((x + 250.0) / 10.0)
            lz = round((z + 250.0) / 10.0)
            if not (0 <= lx < 51 and 0 <= lz < 51):
                bad += 1
                continue
            sample = collider.heights[lz * collider.width + lx]
            worst = max(worst, abs(sample - y))
        if bad:
            issues.append(
                Issue("error", name, f"{bad} vertices fall off the 51x51 sample grid")
            )
        if worst > 1e-3:
            issues.append(
                Issue(
                    "error",
                    name,
                    f"mesh and collision disagree by {worst:.4f} m "
                    "(the terrain would be drawn where you cannot stand)",
                )
            )

    for name, road in sorted(scene.road_ribbons.items()):
        if not road.colors:
            issues.append(Issue("warning", name, "road ribbon without colours"))
        ys = [p[1] for p in road.positions]
        if any(abs(v) > 200 or v != v for v in ys):
            issues.append(Issue("error", name, "road ribbon has corrupt heights"))

    return issues


def chunk_grid(scene: BakedScene, chunks_per_side: int) -> dict[tuple[int, int], Surface]:
    """Map (cx, cz) -> surface, ordered the way the Godot baker wrote them."""
    grid: dict[tuple[int, int], Surface] = {}
    for name, surface in scene.terrain_chunks.items():
        parts = name.split("_")
        if len(parts) != 4:
            raise BakeFormatError(f"unexpected chunk mesh name {name!r}")
        grid[(int(parts[2]), int(parts[3]))] = surface
    if len(grid) != chunks_per_side * chunks_per_side:
        raise BakeFormatError("chunk grid is not square/complete")
    return grid


def chunk_origin(cx: int, cz: int, map_size: float, chunk_size: float) -> tuple[float, float]:
    half = map_size * 0.5
    return (-half + cx * chunk_size, -half + cz * chunk_size)


def smooth01(t: float) -> float:
    x = 0.0 if t < 0.0 else (1.0 if t > 1.0 else t)
    return x * x * (3.0 - 2.0 * x)


def _selftest() -> None:  # pragma: no cover - exercised by the CLI
    assert math.isclose(smooth01(0.5), 0.5)
    print("godot_bake_reader self-test ok")
