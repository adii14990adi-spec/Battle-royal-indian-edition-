#!/usr/bin/env python3
"""Regenerate processing/unity/ground_palette.json (needs Pillow).

    python3 -m pip install Pillow
    python3 processing/unity/make_ground_palette.py

The JSON holds the average sRGB colour of the two authored ground photos.
`validate_unity_map.py` replays the SiranRadiusClip ground-albedo maths with
those averages plus the baked land zone colour and fails when the resulting
land albedo is not green and bright enough — the regression lock added after
the 2026-11-21 device screenshot showed grey "cement" grass.

CI never runs this script (Pillow is not a CI dependency); the committed JSON
is the source of truth and this script only refreshes it when the photos change.
"""
from __future__ import annotations

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
TEXTURES = ROOT / "unity-project" / "Assets" / "Resources" / "Art" / "Textures"
OUT = Path(__file__).resolve().parent / "ground_palette.json"

PHOTOS = {
    "sparse": "ground_grass_dirt.jpg",
    "dense": "ground_leaves_dense.jpg",
}


def average_srgb(path: Path) -> list[float]:
    from PIL import Image  # local-only dependency

    image = Image.open(path).convert("RGB").resize((256, 256))
    pixels = list(image.getdata())
    count = len(pixels)
    return [round(sum(p[channel] for p in pixels) / count, 2) for channel in range(3)]


def main() -> int:
    data = {
        "generated_by": "processing/unity/make_ground_palette.py (needs Pillow; CI never runs it)",
        "purpose": (
            "Average sRGB colour of the two authored ground photos, used by "
            "validate_unity_map.py to prove the shader's land albedo stays green "
            "at every mip level."
        ),
    }
    for key, name in PHOTOS.items():
        data[key + "_srgb_avg"] = average_srgb(TEXTURES / name)
        print(key, data[key + "_srgb_avg"])
    OUT.write_text(json.dumps(data, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(f"wrote {OUT.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
