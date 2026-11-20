#!/usr/bin/env python3
"""Deterministic baker for Siran Island 2.0 — "chota par perfect".

Rebakes the committed Unity island resources at HALF the footprint (2 km
instead of 4 km) with TWICE the height resolution (5 m spacing on the same
401x401 grid), gentler shelves (-12 m instead of a -38 m abyss), tuned zone
plateaus, a proportional lake/river/moat and an 18-ribbon road network.

Everything is pure math with fixed seeds: re-running this script reproduces
byte-identical blobs, which is what `validate_unity_map.py` and the EditMode
tests rely on. Outputs:

  unity-project/Assets/Resources/Config/siran_island_heights.bytes
  unity-project/Assets/Resources/Config/siran_island_colors.bytes
  unity-project/Assets/Resources/Config/siran_island_roads.bytes
  unity-project/Assets/Resources/Config/siran_island_river.bytes
  unity-project/Assets/Resources/Config/siran_island_terrain.json
  unity-project/Assets/Resources/Config/siran_island_layout.json
  unity-project/Assets/Resources/Config/siran_island_manifest.json

and prints the stats the C# constants / tests must mirror.
"""
from __future__ import annotations

import hashlib
import json
import math
import struct
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
CONFIG = REPO / "unity-project" / "Assets" / "Resources" / "Config"

SIZE = 401
SPACING = 5.0
HALF = 1000.0
CHUNK = 250.0
CPS = 8
MAP_SIZE = 2000.0
ISLAND_RADIUS = 975.0
COAST_NOISE = 85.0
SEA_SHELF = -12.0
SEED = 20261120
VERT = 0.75  # vertical rescale vs the 4 km bake

ZONES = [
    # id, display, kind, cx, cz, sx, sz, rot, shape, corner, flat, flatness, feather, tint
    ("factory_island", "Factory Island — Iron Mill", "INDUSTRIAL", -625.0, -650.0,
     380.0, 320.0, -12.0, "ELLIPSE", 70.4, 19.5, 1.0, 45.0, (0.41, 0.39, 0.35)),
    ("konkan_docks", "Dock Island — Konkan Docks", "DOCK", -735.0, 65.0,
     390.0, 270.0, 6.0, "ELLIPSE", 59.4, 3.4, 1.0, 55.0, (0.38, 0.40, 0.43)),
    ("central_state", "Central State — Urban", "URBAN", 20.0, -15.0,
     575.0, 575.0, 0.0, "ROUNDED_RECT", 126.5, 12.0, 0.72, 85.0, (0.52, 0.50, 0.45)),
    ("graveyard_center", "Graveyard Center — Foggy Hills", "GRAVEYARD", 675.0, -40.0,
     410.0, 450.0, 12.0, "ELLIPSE", 90.2, 28.0, 0.40, 95.0, (0.33, 0.36, 0.32)),
    ("aircraft_industry", "Aircraft Industry — Hangars & Runway", "AIRFIELD", 60.0, 740.0,
     510.0, 370.0, 0.0, "ROUNDED_RECT", 81.4, 9.0, 1.0, 45.0, (0.61, 0.60, 0.56)),
]

LAKE = dict(id="siran_lake", display_name="Siran Lake", center=(125.0, -575.0),
            radius=(260.0, 190.0), surface=6.0 * VERT + 2.0, depth=8.0, feather=40.0)

RIVER_PTS = [(125.0, -395.0), (250.0, -260.0), (380.0, -150.0), (440.0, -20.0),
             (450.0, 210.0), (480.0, 450.0), (540.0, 690.0), (580.0, 875.0), (600.0, 985.0)]
RIVER_WIDTHS = [35.0, 36.0, 38.0, 40.0, 43.0, 46.0, 50.0, 55.0, 55.0]
RIVER_START = LAKE["surface"]
RIVER_END = -0.4
RIVER_DEPTH = 2.1
RIVER_BANK = 12.0

CHANNEL_PTS = [(-377.4, -702.6), (-372.5, -632.7), (-400.6, -566.0), (-445.0, -507.4),
               (-508.4, -465.6), (-582.8, -451.3), (-654.5, -448.3), (-731.5, -446.5),
               (-806.9, -471.1), (-848.6, -531.5), (-856.7, -600.7), (-856.0, -665.8),
               (-840.9, -730.8), (-799.0, -787.8), (-741.5, -834.1), (-672.2, -871.9),
               (-591.4, -879.8), (-522.1, -846.7), (-471.7, -800.8), (-420.4, -758.4)]
CHANNEL_HALF = 17.0
CHANNEL_DEPTH = 3.4
CHANNEL_BANK = 15.0

SPAWN = (20.0, -15.0)

ROADS = [
    ("docks_central", "Konkan Docks → Central State",
     [(-735.0, 65.0), (-590.0, 55.0), (-410.0, 35.0), (-215.0, 10.0), (20.0, -15.0)], 8.0, 3.5),
    ("factory_central", "Iron Mill → Central State",
     [(-625.0, -650.0), (-540.0, -490.0), (-440.0, -350.0), (-280.0, -210.0), (-80.0, -80.0), (20.0, -15.0)], 8.0, 3.5),
    ("central_airbase", "Central State → Aircraft Industry",
     [(20.0, -15.0), (30.0, 160.0), (40.0, 380.0), (55.0, 560.0), (60.0, 740.0)], 8.0, 3.5),
    ("central_graveyard", "Central State → Graveyard Center",
     [(20.0, -15.0), (190.0, -70.0), (350.0, -80.0), (500.0, -60.0), (675.0, -40.0)], 8.0, 3.5),
    ("coast_highway", "Coast Highway — Aircraft Industry → Konkan Docks",
     [(60.0, 740.0), (-175.0, 665.0), (-450.0, 470.0), (-640.0, 280.0), (-735.0, 65.0)], 8.0, 3.5),
    ("runway_main", "Runway 09/27", [(60.0 - 120.0, 740.0), (60.0 + 120.0, 740.0)], 15.0, 4.0),
    ("runway_cross", "Runway 03/21", [(60.0, 740.0 - 90.0), (60.0, 740.0 + 90.0)], 12.0, 3.0),
    ("taxiway_alpha", "Taxiway Alpha", [(-40.0, 700.0), (60.0, 690.0), (150.0, 700.0)], 6.0, 2.0),
    ("apron_pad", "Hangar Apron", [(120.0, 780.0), (180.0, 780.0)], 20.0, 4.0),
    ("dock_lane", "Konkan Quay Lane", [(-800.0, 20.0), (-735.0, 65.0), (-660.0, 30.0)], 6.0, 2.0),
    ("factory_lane", "Iron Mill Yard", [(-680.0, -690.0), (-625.0, -650.0), (-560.0, -680.0)], 6.0, 2.0),
    ("lake_loop_n", "Lake Loop North", [(20.0, -330.0), (125.0, -360.0), (230.0, -330.0)], 5.0, 2.0),
    ("lake_loop_s", "Lake Loop South", [(20.0, -330.0), (60.0, -480.0), (125.0, -520.0), (200.0, -470.0), (230.0, -330.0)], 5.0, 2.0),
    ("graveyard_path", "Foggy Hills Path", [(600.0, -160.0), (675.0, -40.0), (720.0, 60.0)], 4.0, 1.5),
    ("central_ring_w", "Central Ring West", [(-180.0, -180.0), (-200.0, -15.0), (-180.0, 150.0)], 6.0, 2.0),
    ("central_ring_e", "Central Ring East", [(220.0, -180.0), (240.0, -15.0), (220.0, 150.0)], 6.0, 2.0),
    ("central_ring_n", "Central Ring North", [(-180.0, -180.0), (20.0, -200.0), (220.0, -180.0)], 6.0, 2.0),
    ("central_ring_s", "Central Ring South", [(-180.0, 150.0), (20.0, 170.0), (220.0, 150.0)], 6.0, 2.0),
]


# ---------------------------------------------------------------- math helpers
def _hash2(ix: int, iz: int, seed: int) -> float:
    h = (ix * 374761393 + iz * 668265263 + seed * 144665) & 0xFFFFFFFF
    h = (h ^ (h >> 13)) * 1274126177 & 0xFFFFFFFF
    return ((h ^ (h >> 16)) & 0xFFFFFF) / float(0xFFFFFF)


def _smooth(t: float) -> float:
    t = min(1.0, max(0.0, t))
    return t * t * (3 - 2 * t)


def value_noise(x: float, z: float, wavelength: float, seed: int) -> float:
    fx, fz = x / wavelength, z / wavelength
    ix, iz = math.floor(fx), math.floor(fz)
    tx, tz = _smooth(fx - ix), _smooth(fz - iz)
    a = _hash2(ix, iz, seed)
    b = _hash2(ix + 1, iz, seed)
    c = _hash2(ix, iz + 1, seed)
    d = _hash2(ix + 1, iz + 1, seed)
    return (a + (b - a) * tx) + ((c + (d - c) * tx) - (a + (b - a) * tx)) * tz


def zone_sdf(zone, x: float, z: float) -> float:
    _, _, _, cx, cz, sx, sz, rot, shape, corner, *_ = zone
    ang = math.radians(rot)
    dx, dz = x - cx, z - cz
    lx = dx * math.cos(ang) + dz * math.sin(ang)
    lz = -dx * math.sin(ang) + dz * math.cos(ang)
    if shape == "ELLIPSE":
        rx, rz = sx * 0.5, sz * 0.5
        return (math.hypot(lx / rx, lz / rz) - 1.0) * min(rx, rz)
    hx, hz = sx * 0.5 - corner, sz * 0.5 - corner
    qx, qz = abs(lx) - hx, abs(lz) - hz
    outside = math.hypot(max(qx, 0.0), max(qz, 0.0))
    inside = min(max(qx, qz), 0.0)
    return outside + inside - corner


def catmull(points, samples_per_seg=14):
    pts = [points[0]] + list(points) + [points[-1]]
    out = []
    for i in range(1, len(pts) - 2):
        p0, p1, p2, p3 = pts[i - 1], pts[i], pts[i + 1], pts[i + 2]
        for s in range(samples_per_seg):
            t = s / samples_per_seg
            t2, t3 = t * t, t * t * t
            x = 0.5 * ((2 * p1[0]) + (-p0[0] + p2[0]) * t + (2 * p0[0] - 5 * p1[0] + 4 * p2[0] - p3[0]) * t2
                       + (-p0[0] + 3 * p1[0] - 3 * p2[0] + p3[0]) * t3)
            z = 0.5 * ((2 * p1[1]) + (-p0[1] + p2[1]) * t + (2 * p0[1] - 5 * p1[1] + 4 * p2[1] - p3[1]) * t2
                       + (-p0[1] + 3 * p1[1] - 3 * p2[1] + p3[1]) * t3)
            out.append((x, z))
    out.append(points[-1])
    return out


def dist_to_polyline(px: float, pz: float, pts):
    best = 1e9
    best_t = 0.0
    for i in range(len(pts) - 1):
        ax, az = pts[i]
        bx, bz = pts[i + 1]
        abx, abz = bx - ax, bz - az
        length_sq = abx * abx + abz * abz or 1e-6
        t = min(1.0, max(0.0, ((px - ax) * abx + (pz - az) * abz) / length_sq))
        cx, cz = ax + abx * t, az + abz * t
        d = math.hypot(px - cx, pz - cz)
        seg_t = (i + t) / (len(pts) - 1)
        if d < best:
            best, best_t = d, seg_t
    return best, best_t


# ---------------------------------------------------------------- height field
def bake_heights():
    heights = [0.0] * (SIZE * SIZE)
    for iz in range(SIZE):
        z = -HALF + iz * SPACING
        for ix in range(SIZE):
            x = -HALF + ix * SPACING
            r = math.hypot(x, z)
            theta = math.atan2(z, x)
            coast = 1.0 + (0.055 * math.sin(3 * theta + 1.7)
                           + 0.035 * math.sin(7 * theta + 4.2)
                           + 0.022 * math.sin(13 * theta + 0.6))
            rn = r / (ISLAND_RADIUS * coast)

            dome = max(0.0, 1.0 - rn) ** 1.35 * 34.0 * VERT
            mask = max(0.0, 1.0 - rn) ** 0.7
            n = (value_noise(x, z, 320, SEED) - 0.5) * 2 * 7.0 * VERT
            n += (value_noise(x, z, 140, SEED + 1) - 0.5) * 2 * 3.0 * VERT
            n += (value_noise(x, z, 70, SEED + 2) - 0.5) * 2 * 1.3 * VERT
            n += (value_noise(x, z, 35, SEED + 3) - 0.5) * 2 * 0.55 * VERT
            h = dome + n * mask

            # Graveyard ridges.
            gz = ZONES[3]
            gd = zone_sdf(gz, x, z)
            if gd < 40.0:
                ridge = (1.0 - abs(value_noise(x, z, 90, SEED + 7) * 2 - 1.0)) * 13.0 * VERT
                h += ridge * _smooth(1.0 - max(gd, 0.0) / 40.0) * 0.8

            # Zone plateaus.
            for zone in ZONES:
                sdf = zone_sdf(zone, x, z)
                feather = zone[12]
                if sdf < feather:
                    w = zone[11] * _smooth(1.0 - max(sdf, 0.0) / feather)
                    h = h + (zone[10] - h) * w

            # Lake basin.
            ldx, ldz = x - LAKE["center"][0], z - LAKE["center"][1]
            lr = math.hypot(ldx / LAKE["radius"][0], ldz / LAKE["radius"][1])
            if lr < 1.6:
                core = _smooth(1.0 - max(lr - 0.25, 0.0) / 0.75)
                bed = LAKE["surface"] - LAKE["depth"] * core
                shore = _smooth(1.0 - max(lr - 1.0, 0.0) / 0.6)
                if shore > 0.0:
                    h = h + (min(h, bed) - h) * shore

            # River + moat channels.
            for pts, widths, depth, bank, surf in (
                (RIVER_PTS, RIVER_WIDTHS, RIVER_DEPTH, RIVER_BANK, None),
                (CHANNEL_PTS, [CHANNEL_HALF * 2] * len(CHANNEL_PTS), CHANNEL_DEPTH, CHANNEL_BANK, 0.0),
            ):
                d, t = dist_to_polyline(x, z, pts)
                half = (widths[min(int(t * (len(widths) - 1)), len(widths) - 1)]) * 0.5
                if d < half + bank:
                    c = _smooth(1.0 - max(d - half, 0.0) / bank)
                    if surf is None:
                        surface = RIVER_START + (RIVER_END - RIVER_START) * t
                    else:
                        surface = surf
                    bed = surface - depth
                    h = h + (min(h, bed) - h) * c

            # Sea shelf.
            if rn > 0.985:
                s = _smooth((rn - 0.985) / 0.20)
                h = h + (SEA_SHELF - h) * s

            heights[iz * SIZE + ix] = max(SEA_SHELF, h)
    return heights


def sample_height(heights, x: float, z: float) -> float:
    fx = min(max((x + HALF) / SPACING, 0.0), SIZE - 1)
    fz = min(max((z + HALF) / SPACING, 0.0), SIZE - 1)
    x0, z0 = int(fx), int(fz)
    x1, z1 = min(x0 + 1, SIZE - 1), min(z0 + 1, SIZE - 1)
    tx, tz = fx - x0, fz - z0
    a = heights[z0 * SIZE + x0]
    b = heights[z0 * SIZE + x1]
    c = heights[z1 * SIZE + x0]
    d = heights[z1 * SIZE + x1]
    return (a + (b - a) * tx) + ((c + (d - c) * tx) - (a + (b - a) * tx)) * tz


# ---------------------------------------------------------------- colours
def bake_colors(heights):
    colors = bytearray(SIZE * SIZE * 4)
    for iz in range(SIZE):
        z = -HALF + iz * SPACING
        for ix in range(SIZE):
            x = -HALF + ix * SPACING
            i = iz * SIZE + ix
            h = heights[i]
            hl = heights[iz * SIZE + max(ix - 1, 0)]
            hr = heights[iz * SIZE + min(ix + 1, SIZE - 1)]
            hd = heights[max(iz - 1, 0) * SIZE + ix]
            hu = heights[min(iz + 1, SIZE - 1) * SIZE + ix]
            slope = math.hypot(hr - hl, hu - hd) / (2 * SPACING)

            break_up = value_noise(x, z, 24, SEED + 11)
            if h < -4.0:
                col = (0.10, 0.17, 0.21)
            elif h < 0.0:
                t = (h + 4.0) / 4.0
                col = (0.10 + 0.55 * t, 0.17 + 0.45 * t, 0.21 + 0.25 * t)
            elif h < 1.6:
                col = (0.76, 0.70, 0.50)
            elif h < 20.0:
                t = (h - 1.6) / 18.4
                col = (0.28 + 0.10 * t + 0.05 * break_up, 0.42 + 0.05 * t + 0.06 * break_up, 0.20 + 0.06 * t)
            elif h < 30.0:
                col = (0.46 + 0.04 * break_up, 0.44 + 0.04 * break_up, 0.28)
            else:
                col = (0.42, 0.40, 0.37)
            if slope > 0.55 or h > 34.0:
                rock = min(1.0, (slope - 0.45) / 0.35 + max(h - 34.0, 0.0) / 6.0)
                col = (col[0] + (0.40 - col[0]) * rock, col[1] + (0.38 - col[1]) * rock,
                       col[2] + (0.36 - col[2]) * rock)

            # Lake bed tint.
            ldx, ldz = x - LAKE["center"][0], z - LAKE["center"][1]
            if math.hypot(ldx / LAKE["radius"][0], ldz / LAKE["radius"][1]) < 1.0 and h < LAKE["surface"]:
                col = (0.20, 0.30, 0.28)

            # Runway asphalt + dashed centreline.
            ax, az = x - 60.0, z - 740.0
            if abs(ax) < 125.0 and abs(az) < 16.0:
                col = (0.19, 0.19, 0.21)
                if abs(az) < 1.2 and (int(ax) % 24) < 12:
                    col = (0.75, 0.73, 0.66)

            # Zone tint.
            for zone in ZONES:
                sdf = zone_sdf(zone, x, z)
                if sdf < 0.0:
                    t = zone[13]
                    mix = 0.22
                    col = (col[0] + (t[0] - col[0]) * mix, col[1] + (t[1] - col[1]) * mix,
                           col[2] + (t[2] - col[2]) * mix)
                    break

            o = i * 4
            colors[o] = int(min(1.0, max(0.0, col[0])) * 255)
            colors[o + 1] = int(min(1.0, max(0.0, col[1])) * 255)
            colors[o + 2] = int(min(1.0, max(0.0, col[2])) * 255)
            # Alpha = ground-photo blend weight for the URP shader:
            #   255      -> keep the flat baked palette (runway asphalt, lake bed)
            #   0..250   -> blend the sparse grass/dirt photo into the dense
            #               leaf photo by this weight, tinted by the zone colour
            a = 40
            if h < 1.6:
                a = 12                      # beach: mostly bare sparse photo
            elif h < 20.0:
                a = int(35 + 60 * break_up)  # grassland: patchy leaf clumps
            elif h < 30.0:
                a = int(60 + 50 * break_up)  # hills: denser scrub
            else:
                a = 25
            if slope > 0.55 or h > 34.0:
                a = 20                      # rock faces: sparse grit
            if abs(ax) < 125.0 and abs(az) < 16.0:
                a = 255                     # runway asphalt stays painted
            if math.hypot(ldx / LAKE["radius"][0], ldz / LAKE["radius"][1]) < 1.0 and h < LAKE["surface"]:
                a = 255                     # lake bed hides under the water sheet
            for zone in ZONES:
                if zone_sdf(zone, x, z) < 0.0 and zone[2] in ("URBAN", "INDUSTRIAL", "DOCK", "AIRFIELD"):
                    a = min(a, 15)          # compacted yard dirt inside built zones
                    break
            colors[o + 3] = a
    return bytes(colors)


# ---------------------------------------------------------------- ribbons
def ribbon_mesh(centerline, width, color):
    verts: list[tuple[float, float, float]] = []
    cols: list[tuple[int, int, int, int]] = []
    indices: list[int] = []
    for i, (x, z) in enumerate(centerline):
        if i == 0:
            dx, dz = centerline[1][0] - x, centerline[1][1] - z
        elif i == len(centerline) - 1:
            dx, dz = x - centerline[i - 1][0], z - centerline[i - 1][1]
        else:
            dx, dz = centerline[i + 1][0] - centerline[i - 1][0], centerline[i + 1][1] - centerline[i - 1][1]
        length = math.hypot(dx, dz) or 1.0
        nx, nz = -dz / length, dx / length
        verts.append((x + nx * width * 0.5, 0.0, z + nz * width * 0.5))
        verts.append((x - nx * width * 0.5, 0.0, z - nz * width * 0.5))
        cols.append(color)
        cols.append(color)
    for i in range(len(centerline) - 1):
        a, b, c, d = 2 * i, 2 * i + 1, 2 * i + 3, 2 * i + 2
        for tri in ((a, b, c), (a, c, d)):
            va, vb, vc = verts[tri[0]], verts[tri[1]], verts[tri[2]]
            cross_y = (vb[0] - va[0]) * (vc[2] - va[2]) - (vb[2] - va[2]) * (vc[0] - va[0])
            # Baked counter-clockwise: after Unity's MakeClockwiseFromAbove swap
            # the geometric normal must point up (cross.y > 0), so bake < 0.
            if cross_y > 0:
                tri = (tri[0], tri[2], tri[1])
            indices.extend(tri)
    return verts, cols, indices


def bake_roads(heights):
    ribbons = []
    for rid, name, points, width, shoulder in ROADS:
        line = catmull(points, 12) if len(points) > 2 else [points[0], points[1]]
        for label, w, col in ((rid, width, (44, 44, 48, 255)),
                              (rid + "_shoulder", width + shoulder * 2, (96, 88, 72, 255))):
            verts, cols, indices = ribbon_mesh(line, w, col)
            for i, (vx, _, vz) in enumerate(verts):
                verts[i] = (vx, sample_height(heights, vx, vz) + 0.12, vz)
            ribbons.append((label, verts, cols, indices))
    return ribbons


def write_roads(ribbons) -> bytes:
    header = b"BHROAD01" + struct.pack("<I", len(ribbons))
    directory = b""
    payload = b""
    offset = 0
    for name, verts, cols, indices in ribbons:
        directory += name.encode("utf-8")[:31].ljust(32, b"\0")
        directory += struct.pack("<III", len(verts), len(indices), offset)
        for (x, y, z), (r, g, b, a) in zip(verts, cols):
            payload += struct.pack("<3f4B", x, y, z, r, g, b, a)
        for idx in indices:
            payload += struct.pack("<H", idx)
        offset += len(verts) * 16 + len(indices) * 2
    return header + directory + payload


def bake_river():
    line = catmull(RIVER_PTS, 12)
    verts: list[tuple[float, float, float]] = []
    indices: list[int] = []
    for i, (x, z) in enumerate(line):
        t = i / (len(line) - 1)
        y = RIVER_START + (RIVER_END - RIVER_START) * t
        w = RIVER_WIDTHS[min(int(t * (len(RIVER_WIDTHS) - 1)), len(RIVER_WIDTHS) - 1)]
        if i == 0:
            dx, dz = line[1][0] - x, line[1][1] - z
        elif i == len(line) - 1:
            dx, dz = x - line[i - 1][0], z - line[i - 1][1]
        else:
            dx, dz = line[i + 1][0] - line[i - 1][0], line[i + 1][1] - line[i - 1][1]
        length = math.hypot(dx, dz) or 1.0
        nx, nz = -dz / length, dx / length
        verts.append((x + nx * w * 0.5, y, z + nz * w * 0.5))
        verts.append((x - nx * w * 0.5, y, z - nz * w * 0.5))
    for i in range(len(line) - 1):
        a, b, c, d = 2 * i, 2 * i + 1, 2 * i + 3, 2 * i + 2
        indices.extend((a, b, c, a, c, d))
    blob = b"BHRIVER1" + struct.pack("<II", len(verts), len(indices))
    for x, y, z in verts:
        blob += struct.pack("<3f", x, y, z)
    for idx in indices:
        blob += struct.pack("<i", idx)
    return blob, len(verts), len(indices)


# ---------------------------------------------------------------- main
def main() -> None:
    heights = bake_heights()
    colors = bake_colors(heights)
    ribbons = bake_roads(heights)
    roads_blob = write_roads(ribbons)
    river_blob, river_verts, river_indices = bake_river()

    min_h, max_h = min(heights), max(heights)
    spawn_ground = sample_height(heights, SPAWN[0], SPAWN[1])
    total_tris = sum(len(r[3]) // 3 for r in ribbons)

    terrain = {
        "schema_version": 1,
        "generated_by": "processing/unity/generate_siran_island.py (Siran Island 2.0 bake)",
        "provenance": {
            "historical_source": "Siran Island greybox data, originally converted from the project "
                                 "predecessor before the repository became Unity-only; rebaked smaller "
                                 "and denser by the in-repo generator (2 km map, 5 m spacing).",
            "note": "These Unity Resources are the source of truth. No external engine project or "
                    "conversion step is required.",
        },
        "grid": {
            "size": SIZE,
            "spacing_m": SPACING,
            "half_extent_m": HALF,
            "chunk_size_m": CHUNK,
            "chunks_per_side": CPS,
            "index_order": "iz * size + ix, ix grows east (+X), iz grows south (+Z)",
            "sample_centre_offset_m": 0.0,
        },
        "heights": {
            "file": "siran_island_heights.bytes",
            "dtype": "float32-le",
            "count": SIZE * SIZE,
            "min_m": round(min_h, 4),
            "max_m": round(max_h, 4),
            "sea_level_m": 0.0,
        },
        "colors": {
            "file": "siran_island_colors.bytes",
            "dtype": "rgba8",
            "count": SIZE * SIZE,
            "note": "Vertex colours baked by generate_siran_island.py: biome palette + slope rock + "
                    "zone tint + runway markings. Zero textures.",
        },
        "roads": {
            "file": "siran_island_roads.bytes",
            "magic": "BHROAD01",
            "ribbons": [{"id": name, "vertices": len(v), "indices": len(ix)}
                        for name, v, c, ix in ribbons],
        },
        "river": {
            "file": "siran_island_river.bytes",
            "magic": "BHRIVER1",
            "vertices": river_verts,
            "indices": river_indices,
            "surface_start_m": RIVER_START,
            "surface_end_m": RIVER_END,
        },
        "stats": {"spawn_ground_height_m": round(spawn_ground, 4)},
        "warnings": [],
    }

    layout = {
        "display_name": "Siran Island",
        "map_size_m": MAP_SIZE,
        "chunk_size_m": CHUNK,
        "vertex_spacing_m": SPACING,
        "sea_level": 0.0,
        "island_radius_m": ISLAND_RADIUS,
        "coastline_noise_m": COAST_NOISE,
        "min_height_m": round(min_h, 4),
        "max_height_m": round(max_h, 4),
        "generator_version": 4,
        "noise_seed": SEED,
        "spawn_point": [SPAWN[0], 20.0, SPAWN[1]],
        "zones": [
            {
                "id": z[0], "display_name": z[1], "kind": z[2], "center": [z[3], z[4]],
                "size": [z[5], z[6]], "rotation_deg": z[7], "shape": z[8],
                "flat_height": z[10], "flatness": z[11], "feather_m": z[12],
                "tint": list(z[13]), "corner_radius_m": z[9],
            }
            for z in ZONES
        ],
        "lake": {
            "id": LAKE["id"], "display_name": LAKE["display_name"],
            "center": list(LAKE["center"]), "radius": list(LAKE["radius"]),
            "surface_height": round(LAKE["surface"], 4), "depth": LAKE["depth"],
            "shore_feather_m": LAKE["feather"],
        },
        "river": {
            "id": "siran_river", "display_name": "Siran River",
            "points": [list(p) for p in RIVER_PTS], "widths": RIVER_WIDTHS,
            "surface_start": RIVER_START, "surface_end": RIVER_END,
            "depth": RIVER_DEPTH, "bank_width_m": RIVER_BANK, "source_blend_m": 45.0,
        },
        "channels": [
            {
                "id": "iron_mill_moat", "display_name": "Iron Mill Channel",
                "points": [list(p) for p in CHANNEL_PTS],
                "half_width_m": CHANNEL_HALF, "depth_m": CHANNEL_DEPTH,
                "bank_width_m": CHANNEL_BANK,
            }
        ],
        "roads": [
            {"id": r[0], "display_name": r[1], "points": [list(p) for p in r[2]],
             "width": r[3], "shoulder_width": r[4]}
            for r in ROADS
        ],
    }

    payloads = {
        "siran_island_heights.bytes": struct.pack(f"<{SIZE * SIZE}f", *heights),
        "siran_island_colors.bytes": colors,
        "siran_island_roads.bytes": roads_blob,
        "siran_island_river.bytes": river_blob,
        "siran_island_terrain.json": (json.dumps(terrain, indent=1) + "\n").encode(),
        "siran_island_layout.json": (json.dumps(layout, indent=1) + "\n").encode(),
    }
    for name, blob in payloads.items():
        (CONFIG / name).write_bytes(blob)

    manifest = {
        "schema_version": 2,
        "data_source": "unity-project/Assets/Resources/Config/siran_island_terrain.json",
        "streaming_radius_m": 50,
        "files": {
            name: {"bytes": len(blob), "sha256": hashlib.sha256(blob).hexdigest()}
            for name, blob in payloads.items()
        },
        "textures": {
            "water_normal_256.png": {
                "path": "unity-project/Assets/Resources/Art/Textures/water_normal_256.png",
                "sha256": hashlib.sha256(
                    (REPO / "unity-project/Assets/Resources/Art/Textures/water_normal_256.png").read_bytes()
                ).hexdigest() if not (REPO / "unity-project/Assets/Resources/Art/Textures/water_normal_256.png").read_bytes().startswith(b"version ") else "e1067e8199f050f9d4e912c1af189a72a6f2dc0d7d89372ec0d074ef4b4ca4a7",
                "note": "Committed Unity water texture asset.",
            }
        },
    }
    # The water normal is an LFS pointer in the checkout: keep the recorded oid.
    manifest["textures"]["water_normal_256.png"]["sha256"] = \
        "e1067e8199f050f9d4e912c1af189a72a6f2dc0d7d89372ec0d074ef4b4ca4a7"
    manifest["textures"]["water_normal_256.png"]["bytes"] = 137293
    (CONFIG / "siran_island_manifest.json").write_bytes(
        (json.dumps(manifest, indent=2) + "\n").encode())

    print(f"heights  min={min_h:.4f} max={max_h:.4f} spawn_ground={spawn_ground:.4f}")
    print(f"ribbons  {len(ribbons)}  triangles={total_tris}")
    print(f"river    verts={river_verts} indices={river_indices} tris={river_indices // 3}")
    print(f"blobs    " + ", ".join(f"{n}={len(b)}B" for n, b in payloads.items()))


if __name__ == "__main__":
    main()
