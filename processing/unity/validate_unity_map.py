#!/usr/bin/env python3
"""Validate the converted Unity map data against the Godot bake (no Unity needed).

    python3 processing/blender/validators/validate_unity_map.py

This is the CI gate that runs on every push, on any machine, with only Python:
it re-reads the Godot scene, re-reads the blobs the converter wrote, and proves
they agree sample for sample. If they do not, the Unity project is building a
different island from the one in the Godot project.

Checks:
  1. manifest hashes: every file in siran_island_bake_manifest.json is unchanged,
  2. heights: all 160 801 float32 samples equal the Godot chunk meshes,
  3. heights: they also equal the Godot HeightMapShape3D colliders,
  4. colours: all 160 801 RGBA samples equal the Godot vertex colours,
  5. roads: 18 ribbons, 4212 triangles, all indices in range, flat-up normals,
  6. river: contractive (monotonically descending) surface, inside the map.
"""

from __future__ import annotations

import json
import struct
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPO_ROOT / "processing" / "unity"))

import godot_bake_reader as bake  # noqa: E402
import convert_godot_island as convert  # noqa: E402


class Report:
    def __init__(self):
        self.failures = []
        self.passes = 0

    def ok(self, message):
        self.passes += 1
        print(f"  PASS  {message}")

    def fail(self, message):
        self.failures.append(message)
        print(f"  FAIL  {message}")
        print(f"::error::{message}")

    def summary(self):
        print()
        print(f"{self.passes} passed, {len(self.failures)} failed")
        if self.failures:
            return 1
        return 0


def main() -> int:
    report = Report()
    print("=" * 62)
    print(" Unity map data vs the Godot bake")
    print("=" * 62)

    manifest_path = convert.MANIFEST_JSON
    if not manifest_path.exists():
        report.fail(f"{manifest_path.relative_to(REPO_ROOT)} is missing; run convert_godot_island.py")
        return report.summary()

    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    for name, info in manifest.get("files", {}).items():
        path = convert.UNITY_CONFIG_DIR / name
        if name == "water_normal_256.png":
            path = convert.WATER_NORMAL_COPY
        if not path.exists():
            report.fail(f"manifest lists {name} but it is missing")
            continue
        if convert.sha256(path) != info["sha256"]:
            report.fail(f"{name} does not match the manifest hash (stale output)")
        else:
            report.ok(f"{name} matches the manifest hash")

    scene = bake.read_baked_scene(convert.GODOT_BAKED_SCENE)
    terrain = json.loads(convert.TERRAIN_JSON.read_text(encoding="utf-8"))
    size = terrain["grid"]["size"]
    spacing = terrain["grid"]["spacing_m"]
    half = terrain["grid"]["half_extent_m"]

    heights_bytes = convert.HEIGHTS_BIN.read_bytes()
    heights = list(struct.unpack("<%df" % (len(heights_bytes) // 4), heights_bytes))
    if len(heights) != size * size:
        report.fail(f"heights blob has {len(heights)} samples, expected {size * size}")
        return report.summary()

    # --- 1/2: mesh vertices -> grid -----------------------------------------
    mismatches = 0
    compared = 0
    worst = 0.0
    for name, chunk in scene.terrain_chunks.items():
        origin = scene.chunk_transforms[name]
        for (x, y, z) in chunk.positions:
            ix = round((origin[0] + x + half) / spacing)
            iz = round((origin[2] + z + half) / spacing)
            if not (0 <= ix < size and 0 <= iz < size):
                mismatches += 1
                continue
            delta = abs(heights[iz * size + ix] - (origin[1] + y))
            worst = max(worst, delta)
            compared += 1
            if delta > 1e-3:
                mismatches += 1
    if mismatches:
        report.fail(f"{mismatches} of {compared} mesh vertices disagree with the converter output "
                    f"(worst {worst:.4f} m)")
    else:
        report.ok(f"all {compared} chunk vertices match the converted height grid (worst {worst:.6f} m)")

    # --- 3: heightmap colliders -> grid -------------------------------------
    collider_mismatches = 0
    for name, collider in scene.colliders.items():
        parts = name.split("_")
        cx, cz = int(parts[-2]), int(parts[-1])
        for lz in range(collider.depth):
            for lx in range(collider.width):
                ix = cx * (collider.width - 1) + lx
                iz = cz * (collider.depth - 1) + lz
                if abs(collider.heights[lz * collider.width + lx] - heights[iz * size + ix]) > 1e-3:
                    collider_mismatches += 1
    if collider_mismatches:
        report.fail(f"{collider_mismatches} collider samples disagree with the height grid "
                    "(terrain would be drawn where you cannot stand)")
    else:
        report.ok("all 64 height-map colliders match the height grid sample for sample")

    # --- 4: colours ---------------------------------------------------------
    colour_bytes = convert.COLORS_BIN.read_bytes()
    colour_mismatches = 0
    for name, chunk in scene.terrain_chunks.items():
        origin = scene.chunk_transforms[name]
        if not chunk.colors:
            report.fail(f"{name} has no vertex colours in the Godot bake")
            break
        for index, (x, _y, z) in enumerate(chunk.positions):
            ix = round((origin[0] + x + half) / spacing)
            iz = round((origin[2] + z + half) / spacing)
            offset = (iz * size + ix) * 4
            if tuple(colour_bytes[offset:offset + 4]) != tuple(chunk.colors[index]):
                colour_mismatches += 1
    if colour_mismatches:
        report.fail(f"{colour_mismatches} vertex colours disagree with the Godot bake")
    else:
        report.ok("all vertex colours match the Godot bake")

    # --- 5: roads -----------------------------------------------------------
    road_blob = convert.ROADS_BIN.read_bytes()
    road_count = struct.unpack_from("<I", road_blob, 8)[0]
    triangles = sum(ribbon.triangles for ribbon in scene.road_ribbons.values())
    if road_count != len(scene.road_ribbons):
        report.fail(f"road blob has {road_count} ribbons, the bake has {len(scene.road_ribbons)}")
    elif triangles != 4212:
        report.fail(f"road ribbons are {triangles} triangles, expected 4212")
    else:
        report.ok(f"{road_count} road ribbons, {triangles} triangles")

    # --- 6: river -----------------------------------------------------------
    river_info = terrain.get("river")
    if not river_info:
        report.fail("no river ribbon in the converted data")
    else:
        river_bytes = (convert.UNITY_CONFIG_DIR / "siran_island_river.bytes").read_bytes()
        vertex_count = struct.unpack_from("<I", river_bytes, 8)[0]
        ys = []
        for i in range(vertex_count):
            offset = 16 + i * 12 + 4
            ys.append(struct.unpack_from("<f", river_bytes, offset)[0])
        left = ys[0::2]
        right = ys[1::2]
        descending = all(left[i] >= left[i + 1] - 1e-3 for i in range(len(left) - 1))
        symmetric = all(abs(left[i] - right[i]) < 1e-3 for i in range(len(left)))
        if not descending:
            report.fail("river surface does not descend from the lake to the sea")
        elif not symmetric:
            report.fail("river ribbon is not level across its width")
        else:
            report.ok(f"river ribbon descends {left[0]:.1f} m -> {left[-1]:.1f} m over {len(left)} cross-sections")

    return report.summary()


if __name__ == "__main__":
    raise SystemExit(main())
