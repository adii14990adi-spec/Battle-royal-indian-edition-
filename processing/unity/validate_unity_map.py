#!/usr/bin/env python3
"""Validate the committed Unity Siran Island resources without Unity installed.

Run from any directory with:
    python3 processing/unity/validate_unity_map.py

The Unity resources are now authoritative; this check deliberately has no
reference to the removed engine project or any external conversion source. It
checks manifest hashes, dimensions, binary headers, index ranges, height stats,
river direction, and the 50 m streaming contract.
"""

from __future__ import annotations

import hashlib
import json
import math
import struct
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
UNITY = REPO_ROOT / "unity-project"
CONFIG = UNITY / "Assets" / "Resources" / "Config"
MANIFEST = CONFIG / "siran_island_manifest.json"
TERRAIN_JSON = CONFIG / "siran_island_terrain.json"


class Report:
    def __init__(self) -> None:
        self.failures: list[str] = []
        self.passes = 0

    def check(self, condition: bool, message: str) -> None:
        if condition:
            self.passes += 1
            print(f"  PASS  {message}")
        else:
            self.failures.append(message)
            print(f"  FAIL  {message}")
            print(f"::error::{message}")

    def summary(self) -> int:
        print(f"\n{self.passes} passed, {len(self.failures)} failed")
        return 1 if self.failures else 0


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for block in iter(lambda: handle.read(1 << 20), b""):
            digest.update(block)
    return digest.hexdigest()


def main() -> int:
    report = Report()
    print("=" * 62)
    print(" Unity Siran Island data integrity")
    print("=" * 62)

    if not MANIFEST.is_file() or not TERRAIN_JSON.is_file():
        report.check(False, "Unity island manifest and metadata are present")
        return report.summary()

    try:
        manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
        terrain = json.loads(TERRAIN_JSON.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        report.check(False, f"manifest/metadata JSON parses ({error})")
        return report.summary()

    report.check(manifest.get("schema_version") == 2,
                 "manifest schema is version 2 and independent of external source files")
    report.check(manifest.get("data_source") == "unity-project/Assets/Resources/Config/siran_island_terrain.json",
                 "manifest points to the committed Unity metadata")
    report.check(manifest.get("streaming_radius_m") == 50,
                 "manifest records the fixed 50 m circular streaming radius")

    manifest_failures = 0
    files = manifest.get("files", {})
    for name, info in files.items():
        path = CONFIG / name
        if name == "water_normal_256.png":
            path = REPO_ROOT / manifest.get("textures", {}).get(name, {}).get("path", "")
        if not path.is_file():
            report.check(False, f"manifest payload {name} exists")
            manifest_failures += 1
            continue
        data = path.read_bytes()
        ok = len(data) == info.get("bytes") and sha256(path) == info.get("sha256")
        report.check(ok, f"{name} matches its recorded byte count and SHA-256")
        manifest_failures += not ok
    report.check(bool(files) and manifest_failures == 0,
                 f"all {len(files)} manifest payloads are present and unchanged")

    grid = terrain.get("grid", {})
    size = int(grid.get("size", 0))
    spacing = float(grid.get("spacing_m", 0))
    half_extent = float(grid.get("half_extent_m", 0))
    chunk_size = float(grid.get("chunk_size_m", 0))
    chunks_per_side = int(grid.get("chunks_per_side", 0))
    cells_per_chunk = round(chunk_size / spacing) if spacing > 0 else 0
    report.check(size == 401 and spacing == 10.0 and half_extent == 2000.0,
                 "terrain metadata describes a 401 x 401 grid at 10 m spacing over a 4 km map")
    report.check(chunk_size == 500.0 and chunks_per_side == 8 and
                 cells_per_chunk * chunks_per_side == size - 1,
                 "the 500 m chunk grid covers the complete terrain with shared edges")
    report.check(terrain.get("provenance", {}).get("note", "").startswith("These Unity Resources"),
                 "terrain metadata names the Unity resources as the current source of truth")

    heights_path = CONFIG / "siran_island_heights.bytes"
    colors_path = CONFIG / "siran_island_colors.bytes"
    roads_path = CONFIG / "siran_island_roads.bytes"
    river_path = CONFIG / "siran_island_river.bytes"
    expected_samples = size * size

    heights: tuple[float, ...] = ()
    if heights_path.is_file() and expected_samples > 0:
        raw = heights_path.read_bytes()
        shape_ok = len(raw) == expected_samples * 4
        report.check(shape_ok, f"height blob has {expected_samples} little-endian float32 samples")
        if shape_ok:
            heights = struct.unpack(f"<{expected_samples}f", raw)
            stats = terrain.get("heights", {})
            finite = all(math.isfinite(value) for value in heights)
            report.check(finite, "all height samples are finite")
            if finite:
                expected_min = float(stats.get("min_m", 0))
                expected_max = float(stats.get("max_m", 0))
                actual_min, actual_max = min(heights), max(heights)
                report.check(abs(actual_min - expected_min) < 1e-3 and
                             abs(actual_max - expected_max) < 1e-3,
                             f"height range {actual_min:.4f}..{actual_max:.4f} m matches metadata")
    else:
        report.check(False, "height blob is present")

    if colors_path.is_file() and expected_samples > 0:
        color_size = colors_path.stat().st_size
        report.check(color_size == expected_samples * 4,
                     f"colour blob has {expected_samples} RGBA8 samples")
    else:
        report.check(False, "colour blob is present")

    # Road blob: magic/count, fixed-width directory, then packed vertices and uint16 indices.
    if roads_path.is_file():
        raw = roads_path.read_bytes()
        road_ok = len(raw) >= 12 and raw[:8] == b"BHROAD01"
        report.check(road_ok, "road blob has a BHROAD01 header")
        if road_ok:
            count = struct.unpack_from("<I", raw, 8)[0]
            directory_end = 12 + count * 44
            entries_ok = count == len(terrain.get("roads", {}).get("ribbons", [])) and directory_end <= len(raw)
            report.check(entries_ok, f"road directory contains {count} in-range ribbon entries")
            total_triangles = 0
            all_indices_valid = entries_ok
            if entries_ok:
                data_start = directory_end
                for index in range(count):
                    entry = 12 + index * 44
                    vertex_count, index_count, offset = struct.unpack_from("<III", raw, entry + 32)
                    end = data_start + offset + vertex_count * 16 + index_count * 2
                    if vertex_count < 3 or index_count < 3 or index_count % 3 or end > len(raw):
                        all_indices_valid = False
                        continue
                    index_base = data_start + offset + vertex_count * 16
                    indices = struct.unpack_from(f"<{index_count}H", raw, index_base)
                    if max(indices, default=0) >= vertex_count:
                        all_indices_valid = False
                    total_triangles += index_count // 3
                report.check(all_indices_valid and total_triangles == 4212,
                             f"all road indices are valid ({total_triangles} triangles across {count} ribbons)")
    else:
        report.check(False, "road blob is present")

    # River blob: magic, vertex/index counts, float3 vertices and int32 indices.
    if river_path.is_file():
        raw = river_path.read_bytes()
        river_ok = len(raw) >= 16 and raw[:8] == b"BHRIVER1"
        report.check(river_ok, "river blob has a BHRIVER1 header")
        if river_ok:
            vertex_count, index_count = struct.unpack_from("<II", raw, 8)
            expected_length = 16 + vertex_count * 12 + index_count * 4
            river_ok = vertex_count >= 4 and index_count >= 6 and index_count % 3 == 0 and len(raw) == expected_length
            report.check(river_ok, f"river blob has {vertex_count} vertices and {index_count // 3} triangles")
            if river_ok:
                vertices = [struct.unpack_from("<3f", raw, 16 + index * 12) for index in range(vertex_count)]
                index_base = 16 + vertex_count * 12
                indices = struct.unpack_from(f"<{index_count}i", raw, index_base)
                indices_ok = min(indices) >= 0 and max(indices) < vertex_count
                finite_vertices = all(math.isfinite(value) for vertex in vertices for value in vertex)
                report.check(indices_ok and finite_vertices,
                             "river indices are valid and all vertex coordinates are finite")
                left = [vertices[index][1] for index in range(0, vertex_count, 2)]
                right = [vertices[index][1] for index in range(1, vertex_count, 2)]
                descending = all(left[i] >= left[i + 1] - 1e-3 for i in range(len(left) - 1))
                level = len(left) == len(right) and all(abs(a - b) < 1e-3 for a, b in zip(left, right))
                report.check(descending and level,
                             f"river surface is level across each section and descends {left[0]:.1f} to {left[-1]:.1f} m")
    else:
        report.check(False, "river blob is present")

    texture = manifest.get("textures", {}).get("water_normal_256.png", {})
    texture_path = REPO_ROOT / texture.get("path", "")
    report.check(texture_path.is_file() and sha256(texture_path) == texture.get("sha256"),
                 "water normal texture is present at its Unity path and matches its recorded SHA-256")

    return report.summary()


if __name__ == "__main__":
    raise SystemExit(main())
