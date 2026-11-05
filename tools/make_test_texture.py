#!/usr/bin/env python3
"""Generate the Phase 1 "hello Unisoc" test texture.

Output: assets/textures/test_pattern_256.png (256x256, RGB8, non-interlaced).

The image is deliberately cheap to compress but full of hard edges and pure
primaries, so it is easy to spot ASTC/ETC2 block artefacts on a Unisoc T606:

  * 8 px tricolour frame (saffron / white / green)      -> channel sanity
  * 32 px checkerboard interior                          -> UV / mip sanity
  * magenta crosshair + ring in the middle               -> geometry sanity
  * four corner swatches (R, G, B, black)                -> ASTC colour sanity
  * horizontal 11-step grey ramp along the bottom        -> banding check

No third-party dependencies (stdlib zlib only) so it runs anywhere.
"""

from __future__ import annotations

import struct
import zlib
from pathlib import Path

SIZE = 256

SAFFRON = (255, 153, 51)
WHITE = (255, 255, 255)
GREEN = (19, 136, 8)
DARK_A = (32, 32, 40)
DARK_B = (60, 60, 76)
MAGENTA = (255, 0, 160)
BLACK = (0, 0, 0)
RED = (230, 30, 30)
BLUE = (30, 80, 230)


def build_pixels() -> list[list[tuple[int, int, int]]]:
    px = [[DARK_A for _ in range(SIZE)] for _ in range(SIZE)]

    # Checkerboard interior.
    checker = 32
    for y in range(SIZE):
        row = px[y]
        for x in range(SIZE):
            row[x] = DARK_A if ((x // checker) + (y // checker)) % 2 == 0 else DARK_B

    # Tricolour frame: saffron (top), white (sides), green (bottom).
    frame = 8
    for y in range(SIZE):
        for x in range(SIZE):
            if y < frame:
                px[y][x] = SAFFRON
            elif y >= SIZE - frame:
                px[y][x] = GREEN
            elif x < frame or x >= SIZE - frame:
                px[y][x] = WHITE

    # Corner swatches, 24x24, inset so the frame stays intact.
    swatches = {
        (14, 14): RED,
        (SIZE - 38, 14): GREEN,
        (14, SIZE - 38): BLUE,
        (SIZE - 38, SIZE - 38): BLACK,
    }
    for (ox, oy), colour in swatches.items():
        for y in range(oy, oy + 24):
            for x in range(ox, ox + 24):
                px[y][x] = colour

    # Crosshair + ring in the middle so the box centre is easy to aim at.
    centre = SIZE // 2
    for d in range(-40, 41):
        px[centre][centre + d] = MAGENTA
        px[centre + d][centre] = MAGENTA
    for angle_step in range(720):
        import math

        a = angle_step * math.pi / 360.0
        rx = centre + int(round(56 * math.cos(a)))
        ry = centre + int(round(56 * math.sin(a)))
        px[ry][rx] = MAGENTA
        px[ry + 1][rx] = MAGENTA

    # 11-step grey ramp along the bottom edge (above the green frame).
    ramp_y0 = SIZE - frame - 20
    steps = 11
    for i in range(steps):
        value = int(round(255 * i / (steps - 1)))
        x0 = frame + 8 + i * 20
        for y in range(ramp_y0, ramp_y0 + 12):
            for x in range(x0, x0 + 20):
                if 0 <= x < SIZE:
                    px[y][x] = (value, value, value)

    return px


def write_png(path: Path, pixels: list[list[tuple[int, int, int]]]) -> None:
    raw = bytearray()
    for row in pixels:
        raw.append(0)  # filter type 0 (None) keeps the file tiny and lossless
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
    out = root / "assets" / "textures" / "test_pattern_256.png"
    write_png(out, build_pixels())
    print(f"wrote {out} ({out.stat().st_size} bytes)")


if __name__ == "__main__":
    main()
