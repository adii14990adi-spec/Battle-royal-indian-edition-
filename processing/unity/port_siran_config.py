#!/usr/bin/env python3
"""Port the Siran Island *layout data* from GDScript to C#.

`scripts/world/siran_map_config.gd` is the single source of truth for the island
in the Godot project: zone footprints, lake, river, moat and five roads. The
Unity project must not have a second, hand-typed copy of those numbers — that is
exactly how a runway ends up in the sea after one bad merge.

So the numbers are *read from the GDScript* and emitted as C# (`SiranMapConfig.cs`)
plus a JSON copy for tooling/tests. This file is a deliberately small parser for
the subset of GDScript the config file uses: literals, `_make_zone(...)`,
`_make_road(...)`, `_ring(...)` and the `lake_body` / `river` field assignments.
If the GDScript stops looking like that, the parse fails loudly instead of
silently emitting a half-copied map.

Usage:
    python3 processing/unity/port_siran_config.py            # write C# + JSON
    python3 processing/unity/port_siran_config.py --check    # fail if stale
"""

from __future__ import annotations

import argparse
import json
import math
import re
import sys
from dataclasses import dataclass, field, asdict
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
CONFIG_GD = REPO_ROOT / "scripts" / "world" / "siran_map_config.gd"
OUT_CS = REPO_ROOT / "unity-project" / "Assets" / "Scripts" / "World" / "SiranMapConfig.cs"
OUT_JSON = REPO_ROOT / "unity-project" / "Assets" / "Resources" / "Config" / "siran_island_layout.json"

ZONE_KINDS = ["INDUSTRIAL", "DOCK", "URBAN", "GRAVEYARD", "AIRFIELD"]


class PortError(RuntimeError):
    pass


@dataclass
class Zone:
    id: str
    display_name: str
    kind: str
    center: tuple[float, float]
    size: tuple[float, float]
    rotation_deg: float
    shape: str
    flat_height: float
    flatness: float
    feather_m: float
    tint: tuple[float, float, float]
    corner_radius_m: float


@dataclass
class WaterBody:
    id: str
    display_name: str
    center: tuple[float, float]
    radius: tuple[float, float]
    surface_height: float
    depth: float
    shore_feather_m: float


@dataclass
class River:
    id: str
    display_name: str
    points: list[tuple[float, float]]
    widths: list[float]
    surface_start: float
    surface_end: float
    depth: float
    bank_width_m: float
    source_blend_m: float


@dataclass
class Channel:
    id: str
    display_name: str
    points: list[tuple[float, float]]
    half_width_m: float
    depth_m: float
    bank_width_m: float


@dataclass
class Road:
    id: str
    display_name: str
    points: list[tuple[float, float]]
    width: float
    shoulder_width: float


@dataclass
class Layout:
    display_name: str
    map_size_m: float
    chunk_size_m: float
    vertex_spacing_m: float
    sea_level: float
    island_radius_m: float
    coastline_noise_m: float
    min_height_m: float
    max_height_m: float
    generator_version: int
    noise_seed: int
    spawn_point: tuple[float, float, float]
    zones: list[Zone] = field(default_factory=list)
    lake: WaterBody | None = None
    river: River | None = None
    channels: list[Channel] = field(default_factory=list)
    roads: list[Road] = field(default_factory=list)


# --- Tiny GDScript literal helpers -------------------------------------------

def _numbers(raw: str) -> list[float]:
    return [float(v) for v in re.findall(r"-?\d+(?:\.\d+)?", raw)]


def _vector2(raw: str) -> tuple[float, float]:
    match = re.fullmatch(r"Vector2\(([^)]*)\)", raw.strip())
    values = _numbers(match.group(1) if match else raw)
    if len(values) != 2:
        raise PortError(f"expected Vector2, got {raw!r}")
    return (values[0], values[1])


def _color(raw: str) -> tuple[float, float, float]:
    values = _numbers(raw)
    if len(values) < 3:
        raise PortError(f"expected Color(), got {raw!r}")
    return (values[0], values[1], values[2])


def _vector2_array(raw: str) -> list[tuple[float, float]]:
    return [_vector2(match) for match in re.findall(r"Vector2\(([^)]*)\)", raw)]


def _float_array(raw: str) -> list[float]:
    return _numbers(raw)


def _call_args(text: str, start: int) -> tuple[str, int]:
    """Return the arguments of the call whose '(' is at `start`, and the end index."""
    depth = 0
    for index in range(start, len(text)):
        char = text[index]
        if char == "(":
            depth += 1
        elif char == ")":
            depth -= 1
            if depth == 0:
                return text[start + 1:index], index + 1
    raise PortError("unbalanced parentheses while reading a call")


def _split_top_level(raw: str) -> list[str]:
    parts: list[str] = []
    depth = 0
    current = ""
    for char in raw:
        if char in "([{":
            depth += 1
        elif char in ")]}":
            depth -= 1
        if char == "," and depth == 0:
            parts.append(current.strip())
            current = ""
        else:
            current += char
    if current.strip():
        parts.append(current.strip())
    return parts


def _scalar(raw: str):
    raw = raw.strip()
    if raw.startswith("&"):
        raw = raw[1:]
    if raw.startswith('"') and raw.endswith('"'):
        return raw[1:-1]
    if raw.startswith("IslandZone.Kind."):
        return raw.split(".")[-1]
    if raw.startswith("IslandZone.Shape."):
        return raw.split(".")[-1]
    try:
        return int(raw) if re.fullmatch(r"-?\d+", raw) else float(raw)
    except ValueError as exc:  # pragma: no cover
        raise PortError(f"cannot parse scalar {raw!r}") from exc


# --- Parser ------------------------------------------------------------------

def parse_constants(text: str) -> dict[str, float | int]:
    """`const MAP_SIZE_DEFAULT_M: float = 4000.0` -> {"MAP_SIZE_DEFAULT_M": 4000.0}"""
    consts: dict[str, float | int] = {}
    for name, raw in re.findall(r"^const ([A-Z][A-Z0-9_]*)\s*:\s*\w+\s*=\s*([^\s#]+)", text, re.M):
        try:
            consts[name] = float(raw) if "." in raw else int(raw)
        except ValueError:
            continue
    return consts


def parse_config(text: str) -> Layout:
    consts = parse_constants(text)

    def require(pattern: str, flags: int = 0) -> str:
        match = re.search(pattern, text, flags)
        if not match:
            raise PortError(f"siran_map_config.gd no longer matches {pattern!r}")
        return match.group(1)

    def require_number(pattern: str, flags: int = 0) -> float:
        """Read a numeric assignment that may be written as a named constant."""
        raw = require(pattern, flags).strip()
        try:
            return float(raw)
        except ValueError:
            pass
        if raw not in consts:
            raise PortError(f"{raw!r} is neither a number nor a const in siran_map_config.gd")
        return float(consts[raw])

    def require_int(pattern: str, flags: int = 0) -> int:
        return int(round(require_number(pattern, flags)))

    layout = Layout(
        display_name=require(r'config\.display_name = "([^"]+)"'),
        map_size_m=require_number(r"config\.map_size_m = ([A-Z0-9_.]+)"),
        chunk_size_m=require_number(r"config\.chunk_size_m = ([A-Z0-9_.]+)"),
        vertex_spacing_m=require_number(r"config\.vertex_spacing_m = ([A-Z0-9_.]+)"),
        sea_level=require_number(r"config\.sea_level = (-?[A-Z0-9_.]+)"),
        island_radius_m=require_number(r"config\.island_radius_m = ([A-Z0-9_.]+)"),
        coastline_noise_m=require_number(r"config\.coastline_noise_m = ([A-Z0-9_.]+)"),
        min_height_m=require_number(r"config\.min_height_m = (-?[A-Z0-9_.]+)"),
        max_height_m=require_number(r"config\.max_height_m = (-?[A-Z0-9_.]+)"),
        generator_version=require_int(r"config\.generator_version = (-?\d+)"),
        noise_seed=require_int(r"config\.noise_seed = (-?\d+)"),
        spawn_point=(0.0, 0.0, 0.0),
    )
    spawn = require(r"config\.spawn_point = Vector3\(([^)]+)\)")
    values = _numbers(spawn)
    layout.spawn_point = (values[0], values[1], values[2])

    # Zones: `_make_zone(&"id", "Display", IslandZone.Kind.X, Vector2, Vector2,
    #                   rot, IslandZone.Shape.Y, flat, flatness, feather, Color)`
    zones_block = _slice(text, "config.zones = [", "]")
    for match in re.finditer(r"_make_zone\(", zones_block):
        args, _ = _call_args(zones_block, match.end() - 1)
        parts = _split_top_level(args)
        # _make_zone(id, display_name, kind, center, size, rotation, shape,
        #             flat_height, flatness, feather_m, tint)  == 11 arguments
        if len(parts) != 11:
            raise PortError(f"_make_zone takes 11 arguments, got {len(parts)}: {args!r}")
        size = _vector2(parts[4])
        zone = Zone(
            id=_scalar(parts[0]),
            display_name=_scalar(parts[1]),
            kind=_scalar(parts[2]),
            center=_vector2(parts[3]),
            size=size,
            rotation_deg=float(parts[5]),
            shape=_scalar(parts[6]),
            flat_height=float(parts[7]),
            flatness=float(parts[8]),
            feather_m=float(parts[9]),
            tint=_color(parts[10]),
            # Mirrors `_make_zone`: corner_radius_m = min(size) * 0.22
            corner_radius_m=min(size) * 0.22,
        )
        layout.zones.append(zone)

    lake_id = require(r'lake_body\.id = &"([^"]+)"')
    layout.lake = WaterBody(
        id=lake_id,
        display_name=require(r'lake_body\.display_name = "([^"]+)"'),
        center=_vector2(require(r"lake_body\.center = Vector2\(([^)]+)\)")),
        radius=_vector2(require(r"lake_body\.radius = Vector2\(([^)]+)\)")),
        surface_height=float(require(r"lake_body\.surface_height = (-?[\d.]+)")),
        depth=float(require(r"lake_body\.depth = (-?[\d.]+)")),
        shore_feather_m=float(require(r"lake_body\.shore_feather_m = ([\d.]+)")),
    )

    layout.river = River(
        id=require(r'siran_river\.id = &"([^"]+)"'),
        display_name=require(r'siran_river\.display_name = "([^"]+)"'),
        points=_vector2_array(require(r"siran_river\.points = PackedVector2Array\(\[(.*?)\]\)", re.S)),
        widths=_float_array(require(r"siran_river\.widths = PackedFloat32Array\(\[(.*?)\]\)", re.S)),
        surface_start=float(require(r"siran_river\.surface_start = (-?[\d.]+)")),
        surface_end=float(require(r"siran_river\.surface_end = (-?[\d.]+)")),
        depth=float(require(r"siran_river\.depth = (-?[\d.]+)")),
        bank_width_m=float(require(r"siran_river\.bank_width_m = ([\d.]+)")),
        source_blend_m=float(require(r"siran_river\.source_blend_m = ([\d.]+)")),
    )

    moat_center = _vector2(require(r"_ring\(Vector2\(([^)]*)\), Vector2\("))
    ring_args = re.search(
        r"_ring\(Vector2\([^)]*\), Vector2\(([^)]*)\), (-?[\d.]+), (\d+), ([\d.]+)\)", text
    )
    if not ring_args:
        raise PortError("could not read the Iron Mill _ring(...) call")
    radii = _vector2(ring_args.group(1))
    rotation = float(ring_args.group(2))
    segments = int(ring_args.group(3))
    wobble = float(ring_args.group(4))
    layout.channels.append(
        Channel(
            id=require(r'moat\.id = &"([^"]+)"'),
            display_name=require(r'moat\.display_name = "([^"]+)"'),
            points=ring(moat_center, radii, rotation, segments, wobble),
            half_width_m=float(require(r"moat\.half_width_m = ([\d.]+)")),
            depth_m=float(require(r"moat\.depth_m = ([\d.]+)")),
            bank_width_m=float(require(r"moat\.bank_width_m = ([\d.]+)")),
        )
    )

    # Slice the array first: the file also *defines* `_make_road(...)`, and a
    # whole-file regex would happily parse the function signature as a road.
    roads_block = _slice(text, "config.roads = [", "]")
    for match in re.finditer(r"_make_road\(", roads_block):
        args, _ = _call_args(roads_block, match.end() - 1)
        parts = _split_top_level(args)
        if len(parts) != 3:
            raise PortError(f"_make_road takes 3 arguments, got {args!r}")
        layout.roads.append(
            Road(
                id=_scalar(parts[0]),
                display_name=_scalar(parts[1]),
                points=_vector2_array(parts[2]),
                width=16.0,          # `_make_road` hard-codes 16.0 / 7.0
                shoulder_width=7.0,
            )
        )

    problems = validate(layout)
    if problems:
        raise PortError("; ".join(problems))
    return layout


def _slice(text: str, start_marker: str, end_marker: str) -> str:
    start = text.find(start_marker)
    if start < 0:
        raise PortError(f"missing {start_marker!r}")
    start += len(start_marker)
    depth = 1
    for index in range(start, len(text)):
        char = text[index]
        if char in "([":
            depth += 1
        elif char in ")]":
            depth -= 1
            if depth == 0:
                return text[start:index]
    raise PortError(f"unbalanced block after {start_marker!r}")


def ring(center, radii, rotation_deg, segments, wobble) -> list[tuple[float, float]]:
    """Verbatim port of `SiranMapConfig._ring`."""
    points: list[tuple[float, float]] = []
    for index in range(segments):
        angle = 2.0 * math.pi * index / segments
        scale = 1.0 + wobble * math.sin(angle * 3.0) + wobble * 0.6 * math.cos(angle * 5.0)
        local = (math.cos(angle) * radii[0] * scale, math.sin(angle) * radii[1] * scale)
        rotated = rotate_xz(local, rotation_deg)
        points.append((center[0] + rotated[0], center[1] + rotated[1]))
    return points


def rotate_xz(point, degrees: float) -> tuple[float, float]:
    """Verbatim port of `IslandShapeMath.rotate_xz`."""
    if abs(degrees) < 1e-5:
        return point
    radians = math.radians(degrees)
    c, s = math.cos(radians), math.sin(radians)
    return (point[0] * c - point[1] * s, point[0] * s + point[1] * c)


def validate(layout: Layout) -> list[str]:
    """Same relationships `SiranMapConfig.validate()` asserts, checked here."""
    problems: list[str] = []
    chunks = layout.map_size_m / layout.chunk_size_m
    if abs(chunks - round(chunks)) > 1e-3:
        problems.append("map_size_m is not a whole number of chunks")
    cells = layout.chunk_size_m / layout.vertex_spacing_m
    if abs(cells - round(cells)) > 1e-3:
        problems.append("chunk_size_m is not a whole number of cells")
    if not layout.zones:
        problems.append("no zones")
    half = layout.map_size_m * 0.5
    for zone in layout.zones:
        if abs(zone.center[0]) + zone.size[0] * 0.5 > half:
            problems.append(f"zone {zone.id} extends past the map edge (X)")
        if abs(zone.center[1]) + zone.size[1] * 0.5 > half:
            problems.append(f"zone {zone.id} extends past the map edge (Z)")
        if zone.flat_height <= layout.sea_level + 1.0 and zone.kind != "DOCK":
            problems.append(f"zone {zone.id} sits at or below sea level")
    if layout.river:
        if len(layout.river.points) != len(layout.river.widths):
            problems.append("river widths must match points")
        if layout.river.surface_start < layout.river.surface_end:
            problems.append("river flows uphill")
    for road in layout.roads:
        if len(road.points) < 2:
            problems.append(f"road {road.id} needs at least 2 points")
    return problems


# --- Emitters ----------------------------------------------------------------

def to_json(layout: Layout) -> dict:
    data = asdict(layout)
    data["provenance"] = {
        "generated_by": "processing/unity/port_siran_config.py",
        "source": "scripts/world/siran_map_config.gd",
        "note": (
            "Read-only port of the Godot layout. Do not hand-edit: edit the "
            "GDScript and re-run the porter, or the two maps will diverge."
        ),
    }
    return data


def _f(value: float) -> str:
    text = f"{value:.6f}".rstrip("0").rstrip(".")
    if text in ("-0", ""):
        return "0"
    if "." not in text:
        text += ".0"
    return text + "f"


def _v2(p) -> str:
    return f"new Vector2({_f(p[0])}, {_f(p[1])})"


def emit_cs(layout: Layout) -> str:
    lines: list[str] = []
    add = lines.append
    add("// <auto-generated>")
    add("//   Generated by processing/unity/port_siran_config.py from")
    add("//   scripts/world/siran_map_config.gd (the Godot project's single source of")
    add("//   truth for the Siran Island layout).")
    add("//")
    add("//   Do NOT hand-edit this file: change the GDScript and re-run")
    add("//       python3 processing/unity/port_siran_config.py")
    add("//   The Unity CI job fails when the two drift apart.")
    add("// </auto-generated>")
    add("")
    add("using System.Collections.Generic;")
    add("using UnityEngine;")
    add("")
    add("namespace BattleHorizons.World")
    add("{")
    add('    /// <summary>Footprint shape of a zone, mirroring IslandZone.Shape.</summary>')
    add("    public enum ZoneShape { Ellipse = 0, RoundedRect = 1 }")
    add("")
    add('    /// <summary>What a zone is, mirroring IslandZone.Kind.</summary>')
    add("    public enum ZoneKind { Industrial = 0, Dock = 1, Urban = 2, Graveyard = 3, Airfield = 4 }")
    add("")
    add("    public sealed class IslandZone")
    add("    {")
    add("        public string Id;")
    add("        public string DisplayName;")
    add("        public ZoneKind Kind;")
    add('        /// <summary>World XZ centre in metres (X = east, Z = south).</summary>')
    add("        public Vector2 Center;")
    add("        public Vector2 Size;")
    add("        public float RotationDeg;")
    add("        public ZoneShape Shape;")
    add("        public float CornerRadiusM;")
    add("        public float FlatHeight;")
    add("        public float Flatness;")
    add("        public float FeatherM;")
    add("        public Color Tint;")
    add("    }")
    add("")
    add("    public sealed class IslandWaterBody")
    add("    {")
    add("        public string Id;")
    add("        public string DisplayName;")
    add("        public Vector2 Center;")
    add("        public Vector2 Radius;")
    add("        public float SurfaceHeight;")
    add("        public float Depth;")
    add("        public float ShoreFeatherM;")
    add("    }")
    add("")
    add("    public sealed class IslandRiver")
    add("    {")
    add("        public string Id;")
    add("        public string DisplayName;")
    add("        public Vector2[] Points;")
    add("        public float[] Widths;")
    add("        public float SurfaceStart;")
    add("        public float SurfaceEnd;")
    add("        public float Depth;")
    add("        public float BankWidthM;")
    add("        public float SourceBlendM;")
    add("    }")
    add("")
    add("    public sealed class IslandChannel")
    add("    {")
    add("        public string Id;")
    add("        public string DisplayName;")
    add("        public Vector2[] Points;")
    add("        public float HalfWidthM;")
    add("        public float DepthM;")
    add("        public float BankWidthM;")
    add("    }")
    add("")
    add("    public sealed class IslandRoad")
    add("    {")
    add("        public string Id;")
    add("        public string DisplayName;")
    add("        public Vector2[] Points;")
    add("        public float Width = 16f;")
    add("        public float ShoulderWidth = 7f;")
    add("        public float SurfaceOffset = 0.18f;")
    add("")
    add("        public float TotalHalfWidth => Width * 0.5f + ShoulderWidth;")
    add("    }")
    add("")
    add("    /// <summary>")
    add("    /// The whole layout of Siran Island in one place, ported 1:1 from the")
    add("    /// Godot project's SiranMapConfig. World space: metres, X = east,")
    add("    /// Y = up, Z = south, map centred on the origin.")
    add("    /// </summary>")
    add("    public static class SiranMapConfig")
    add("    {")
    add(f"        public const string DisplayName = {json.dumps(layout.display_name)};")
    add(f"        public const float MapSizeM = {_f(layout.map_size_m)};")
    add(f"        public const float ChunkSizeM = {_f(layout.chunk_size_m)};")
    add(f"        public const float VertexSpacingM = {_f(layout.vertex_spacing_m)};")
    add(f"        public const float SeaLevel = {_f(layout.sea_level)};")
    add(f"        public const float IslandRadiusM = {_f(layout.island_radius_m)};")
    add(f"        public const float CoastlineNoiseM = {_f(layout.coastline_noise_m)};")
    add(f"        public const float MinHeightM = {_f(layout.min_height_m)};")
    add(f"        public const float MaxHeightM = {_f(layout.max_height_m)};")
    add(f"        public const int GeneratorVersion = {layout.generator_version};")
    add(f"        public const int NoiseSeed = {layout.noise_seed};")
    add("        /// <summary>Where a match starts, world metres (Y is a guess: use the ground).</summary>")
    sp = layout.spawn_point
    add(f"        public static readonly Vector3 SpawnPoint = new Vector3({_f(sp[0])}, {_f(sp[1])}, {_f(sp[2])});")
    add("")
    add("        public static int ChunksPerSide => Mathf.Max(1, Mathf.RoundToInt(MapSizeM / ChunkSizeM));")
    add("        public static int ChunkCount => ChunksPerSide * ChunksPerSide;")
    add("        public static int ChunkVerticesPerSide => Mathf.Max(2, Mathf.RoundToInt(ChunkSizeM / VertexSpacingM) + 1);")
    add("        public static float HalfExtent => MapSizeM * 0.5f;")
    add("")
    add("        // --- Zones -------------------------------------------------------")
    add("        public static readonly IslandZone[] Zones =")
    add("        {")
    for zone in layout.zones:
        add("            new IslandZone")
        add("            {")
        add(f"                Id = {json.dumps(zone.id)},")
        add(f"                DisplayName = {json.dumps(zone.display_name)},")
        add(f"                Kind = ZoneKind.{zone.kind.capitalize() if zone.kind != 'AIRFIELD' else 'Airfield'},")
        add(f"                Center = {_v2(zone.center)},")
        add(f"                Size = {_v2(zone.size)},")
        add(f"                RotationDeg = {_f(zone.rotation_deg)},")
        add(f"                Shape = ZoneShape.{'Ellipse' if zone.shape == 'ELLIPSE' else 'RoundedRect'},")
        add(f"                CornerRadiusM = {_f(zone.corner_radius_m)},")
        add(f"                FlatHeight = {_f(zone.flat_height)},")
        add(f"                Flatness = {_f(zone.flatness)},")
        add(f"                FeatherM = {_f(zone.feather_m)},")
        add(f"                Tint = new Color({_f(zone.tint[0])}, {_f(zone.tint[1])}, {_f(zone.tint[2])}, 1f),")
        add("            },")
    add("        };")
    add("")
    if layout.lake:
        lake = layout.lake
        add("        // --- Water -------------------------------------------------------")
        add("        public static readonly IslandWaterBody Lake = new IslandWaterBody")
        add("        {")
        add(f"            Id = {json.dumps(lake.id)},")
        add(f"            DisplayName = {json.dumps(lake.display_name)},")
        add(f"            Center = {_v2(lake.center)},")
        add(f"            Radius = {_v2(lake.radius)},")
        add(f"            SurfaceHeight = {_f(lake.surface_height)},")
        add(f"            Depth = {_f(lake.depth)},")
        add(f"            ShoreFeatherM = {_f(lake.shore_feather_m)},")
        add("        };")
        add("")
    if layout.river:
        river = layout.river
        add("        public static readonly IslandRiver River = new IslandRiver")
        add("        {")
        add(f"            Id = {json.dumps(river.id)},")
        add(f"            DisplayName = {json.dumps(river.display_name)},")
        add("            Points = new[]")
        add("            {")
        for point in river.points:
            add(f"                {_v2(point)},")
        add("            },")
        add("            Widths = new[] { " + ", ".join(_f(w) for w in river.widths) + " },")
        add(f"            SurfaceStart = {_f(river.surface_start)},")
        add(f"            SurfaceEnd = {_f(river.surface_end)},")
        add(f"            Depth = {_f(river.depth)},")
        add(f"            BankWidthM = {_f(river.bank_width_m)},")
        add(f"            SourceBlendM = {_f(river.source_blend_m)},")
        add("        };")
        add("")
    add("        public static readonly IslandChannel[] Channels =")
    add("        {")
    for channel in layout.channels:
        add("            new IslandChannel")
        add("            {")
        add(f"                Id = {json.dumps(channel.id)},")
        add(f"                DisplayName = {json.dumps(channel.display_name)},")
        add("                Points = new[]")
        add("                {")
        for point in channel.points:
            add(f"                    {_v2(point)},")
        add("                },")
        add(f"                HalfWidthM = {_f(channel.half_width_m)},")
        add(f"                DepthM = {_f(channel.depth_m)},")
        add(f"                BankWidthM = {_f(channel.bank_width_m)},")
        add("            },")
    add("        };")
    add("")
    add("        public static readonly IslandRoad[] Roads =")
    add("        {")
    for road in layout.roads:
        add("            new IslandRoad")
        add("            {")
        add(f"                Id = {json.dumps(road.id)},")
        add(f"                DisplayName = {json.dumps(road.display_name)},")
        add("                Points = new[]")
        add("                {")
        for point in road.points:
            add(f"                    {_v2(point)},")
        add("                },")
        add(f"                Width = {_f(road.width)},")
        add(f"                ShoulderWidth = {_f(road.shoulder_width)},")
        add("            },")
    add("        };")
    add("")
    add("        /// <summary>Innermost zone containing a world XZ point (smallest footprint wins).</summary>")
    add("        public static IslandZone ZoneAt(float x, float z)")
    add("        {")
    add("            IslandZone best = null;")
    add("            var bestArea = float.MaxValue;")
    add("            for (var i = 0; i < Zones.Length; i++)")
    add("            {")
    add("                var zone = Zones[i];")
    add("                if (IslandShapeMath.SignedDistance(zone, x, z) > 0f) continue;")
    add("                var area = zone.Size.x * zone.Size.y;")
    add("                if (area >= bestArea) continue;")
    add("                bestArea = area;")
    add("                best = zone;")
    add("            }")
    add("            return best;")
    add("        }")
    add("")
    add("        public static IslandZone ZoneById(string id)")
    add("        {")
    add("            for (var i = 0; i < Zones.Length; i++)")
    add("            {")
    add("                if (Zones[i].Id == id) return Zones[i];")
    add("            }")
    add("            return null;")
    add("        }")
    add("")
    add("        /// <summary>Chunk index containing a world XZ point, clamped to the grid.</summary>")
    add("        public static Vector2Int WorldToChunk(float x, float z)")
    add("        {")
    add("            var side = ChunksPerSide;")
    add("            var cx = Mathf.Clamp(Mathf.FloorToInt((x + HalfExtent) / ChunkSizeM), 0, side - 1);")
    add("            var cz = Mathf.Clamp(Mathf.FloorToInt((z + HalfExtent) / ChunkSizeM), 0, side - 1);")
    add("            return new Vector2Int(cx, cz);")
    add("        }")
    add("")
    add("        /// <summary>World XZ origin of a chunk (its north-west corner).</summary>")
    add("        public static Vector2 ChunkOrigin(int cx, int cz)")
    add("        {")
    add("            return new Vector2(-HalfExtent + cx * ChunkSizeM, -HalfExtent + cz * ChunkSizeM);")
    add("        }")
    add("        public static Vector2 ChunkCenter(int cx, int cz)")
    add("        {")
    add("            return ChunkOrigin(cx, cz) + new Vector2(ChunkSizeM * 0.5f, ChunkSizeM * 0.5f);")
    add("        }")
    add("")
    add("        public static readonly List<string> ZoneIds = new List<string>")
    add("        {")
    for zone in layout.zones:
        add(f"            {json.dumps(zone.id)},")
    add("        };")
    add("    }")
    add("}")
    return "\n".join(lines) + "\n"


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="verify the outputs are up to date")
    args = parser.parse_args(argv)

    if not CONFIG_GD.exists():
        print(f"::error:: {CONFIG_GD} not found", file=sys.stderr)
        return 2

    layout = parse_config(CONFIG_GD.read_text(encoding="utf-8"))
    cs = emit_cs(layout)
    js = json.dumps(to_json(layout), indent=2) + "\n"

    if args.check:
        stale = []
        if not OUT_CS.exists() or OUT_CS.read_text(encoding="utf-8") != cs:
            stale.append(str(OUT_CS.relative_to(REPO_ROOT)))
        if not OUT_JSON.exists() or OUT_JSON.read_text(encoding="utf-8") != js:
            stale.append(str(OUT_JSON.relative_to(REPO_ROOT)))
        if stale:
            for path in stale:
                print(f"::error::{path} is out of date with scripts/world/siran_map_config.gd")
            return 1
        print("SiranMapConfig.cs and siran_island_layout.json are in sync with the GDScript.")
        return 0

    OUT_CS.parent.mkdir(parents=True, exist_ok=True)
    OUT_JSON.parent.mkdir(parents=True, exist_ok=True)
    OUT_CS.write_text(cs, encoding="utf-8")
    OUT_JSON.write_text(js, encoding="utf-8")
    print(f"wrote {OUT_CS.relative_to(REPO_ROOT)} ({len(cs)} bytes)")
    print(f"wrote {OUT_JSON.relative_to(REPO_ROOT)} ({len(js)} bytes)")
    print(
        f"  {len(layout.zones)} zones, {len(layout.roads)} roads, "
        f"{len(layout.channels)} channel(s), lake + river"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
