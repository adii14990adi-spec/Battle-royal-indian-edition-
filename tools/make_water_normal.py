#!/usr/bin/env python3
"""Generate the tiling water normal map used by `assets/shaders/water_surface.gdshader`.

Output: assets/textures/water_normal_256.png (256x256, RGB8, non-interlaced).

Why 256 and why generated:

  * The Unisoc budget caps textures at 512^2 (docs/UNISOC_BUDGET.md). Water is
    the one surface the player stares at for a whole match, so it gets the
    sharpest thing we can afford: 256^2 + ASTC is ~32 KB of VRAM.
  * Every ripple is a sum of *integer* frequency sines, so the tile is seamless
    in both directions with no edge fix-up and no authoring tool in the loop.
  * One texture is sampled twice at different scales and scroll speeds by the
    shader (A/B layers), which looks like two maps for the price of one fetch
    budget.

The map is stored in OpenGL/tangent-space convention (+Y up in texture space),
which is what Godot expects when `process/normal_map_invert_y=false`.

No third-party dependencies (stdlib zlib only) so it runs anywhere.

    python3 tools/make_water_normal.py
"""

from __future__ import annotations

import math
import struct
import zlib
from pathlib import Path

SIZE = 256

# (frequency_x, frequency_y, amplitude, phase) — integers keep the tile seamless.
WAVES = (
    (1, 2, 1.00, 0.00),
    (2, -1, 0.75, 1.10),
    (3, 4, 0.45, 2.40),
    (5, -3, 0.28, 0.60),
    (7, 6, 0.16, 3.70),
    (11, -9, 0.09, 5.10),
)

# Slope -> normal scale. Higher = choppier water.
RELIEF = 3.4


def build_pixels() -> list[list[tuple[int, int, int]]]:
    tau = 2.0 * math.pi
    heights = [[0.0] * SIZE for _ in range(SIZE)]
    for y in range(SIZE):
        row = heights[y]
        for x in range(SIZE):
            total = 0.0
            for fx, fy, amplitude, phase in WAVES:
                total += amplitude * math.sin(tau * (fx * x + fy * y) / SIZE + phase)
            row[x] = total

    pixels: list[list[tuple[int, int, int]]] = []
    for y in range(SIZE):
        row: list[tuple[int, int, int]] = []
        for x in range(SIZE):
            # Central differences, wrapping: the derivative of a seamless tile
            # is seamless too.
            left = heights[y][(x - 1) % SIZE]
            right = heights[y][(x + 1) % SIZE]
            back = heights[(y - 1) % SIZE][x]
            front = heights[(y + 1) % SIZE][x]
            dx = (left - right) * 0.5
            dy = (back - front) * 0.5
            nx = -dx * RELIEF
            ny = -dy * RELIEF
            nz = 1.0
            length = math.sqrt(nx * nx + ny * ny + nz * nz)
            nx /= length
            ny /= length
            nz /= length
            row.append(
                (
                    int(round((nx * 0.5 + 0.5) * 255)),
                    int(round((ny * 0.5 + 0.5) * 255)),
                    int(round((nz * 0.5 + 0.5) * 255)),
                )
            )
        pixels.append(row)
    return pixels


def write_png(path: Path, pixels: list[list[tuple[int, int, int]]]) -> None:
    raw = bytearray()
    for row in pixels:
        raw.append(0)  # filter type 0 (None)
        for r, g, b in row:
            raw += bytes((r, g, b))

    def chunk(tag: bytes, data: bytes) -> bytes:
        return (
            struct.pack(">I", len(data))
            + tag
            + data
            + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)
        )

    ihdr = struct.pack(">IIBBBBB", SIZE, SIZE, 8, 2, 0, 0, 0)  # 8-bit RGB
    png = (
        b"\x89PNG\r\n\x1a\n"
        + chunk(b"IHDR", ihdr)
        + chunk(b"IDAT", zlib.compress(bytes(raw), 9))
        + chunk(b"IEND", b"")
    )
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(png)


def main() -> None:
    root = Path(__file__).resolve().parent.parent
    out = root / "assets" / "textures" / "water_normal_256.png"
    write_png(out, build_pixels())
    print(f"wrote {out} ({out.stat().st_size} bytes)")


if __name__ == "__main__":
    main()
