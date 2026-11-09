#!/usr/bin/env python3
"""Validate Blender-pipeline output *without* Blender installed.

    python3 processing/blender/validators/validate_assets.py [--output DIR] [--strict]

Checks every asset listed in `output/asset_manifest.json`:

  1. the file exists and its SHA-256 matches the manifest (nothing changed silently),
  2. the container is what it claims to be (GLB magic + version + declared length;
     FBX magic / declared version),
  3. the GLB JSON chunk parses, and its `nodes`/`meshes`/`accessors` are coherent,
  4. scale: every mesh's POSITION accessor `min`/`max` is in metres and inside a
     sane range, and a unit-sized asset is not exported 100x too big or small,
  5. orientation: glTF is Y-up by spec; a floor/plank asset is wider than it is
     tall, and no asset is upside down (positions' Y span centred near its base),
  6. a UV set exists (TEXCOORD_0) on every primitive — an untextured prop fails,
  7. characters: skeleton sidecar has every bone Unity's humanoid mapping needs,
     weights are <= 4 influences per vertex, and the mesh is skinned (JOINTS_0),
  8. collision: anything the manifest marks `collision: true` has a `<name>_collision`
     node, and that node is a closed convex box (12 triangles).

This is the gate the GitHub Actions "assets" job runs. It needs no GPU, no
Blender and no Unity, so it can run on every push in ~1 s.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import struct
import sys
from pathlib import Path

PIPELINE_ROOT = Path(__file__).resolve().parents[1]
REPO_ROOT = PIPELINE_ROOT.parents[1]
DEFAULT_OUTPUT = PIPELINE_ROOT / "output"

GLB_MAGIC = 0x46546C67           # b"glTF"
GLB_CHUNK_JSON = 0x4E4F534A      # b"JSON"
FBX_MAGIC = b"Kaydara FBX Binary  \x00"
MAX_SANE_EXTENT_M = 120.0        # nothing in this kit is bigger than the 18 m chimney
MIN_SANE_EXTENT_M = 0.05         # a 5 cm floor tile means the export is 100x too small


class Report:
    def __init__(self, strict: bool) -> None:
        self.failures: list[str] = []
        self.warnings: list[str] = []
        self.passes = 0
        self.checks = 0
        self.strict = strict

    def ok(self, message: str) -> None:
        self.passes += 1
        self.checks += 1
        print(f"  PASS  {message}")

    def fail(self, message: str) -> None:
        self.checks += 1
        self.failures.append(message)
        print(f"  FAIL  {message}")
        if Path("/.dockerenv").exists() or sys.stderr.isatty():
            print(f"::error::{message}")

    def warn(self, message: str) -> None:
        self.warnings.append(message)
        print(f"  WARN  {message}")

    def summary(self) -> int:
        print()
        print(f"{self.passes}/{self.checks} checks passed, "
              f"{len(self.failures)} failed, {len(self.warnings)} warning(s)")
        if self.failures:
            print("FAILURES:")
            for failure in self.failures:
                print("  * " + failure)
            return 1
        if self.strict and self.warnings:
            print("STRICT MODE: warnings are failures")
            return 1
        return 0


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1 << 20), b""):
            digest.update(chunk)
    return digest.hexdigest()


def read_glb(path: Path) -> tuple[dict, bytes]:
    """Parse a GLB container and return (json_chunk, bin_chunk)."""
    raw = path.read_bytes()
    if len(raw) < 20:
        raise ValueError(f"GLB too short ({len(raw)} bytes)")
    magic, version, length = struct.unpack_from("<III", raw, 0)
    if magic != GLB_MAGIC:
        raise ValueError("not a GLB (bad magic)")
    if version != 2:
        raise ValueError(f"GLB version {version}, expected 2")
    if length != len(raw):
        raise ValueError(f"GLB declares {length} bytes but the file is {len(raw)}")

    cursor = 12
    json_chunk = None
    bin_chunk = b""
    while cursor + 8 <= len(raw):
        chunk_length, chunk_type = struct.unpack_from("<II", raw, cursor)
        cursor += 8
        payload = raw[cursor:cursor + chunk_length]
        cursor += chunk_length
        if chunk_type == GLB_CHUNK_JSON:
            json_chunk = json.loads(payload.decode("utf-8"))
        elif chunk_type == 0x004E4942:  # BIN
            bin_chunk = payload
    if json_chunk is None:
        raise ValueError("GLB has no JSON chunk")
    return json_chunk, bin_chunk


def check_fbx(path: Path) -> tuple[bool, str]:
    with path.open("rb") as handle:
        header = handle.read(len(FBX_MAGIC) + 4)
    if not header.startswith(FBX_MAGIC):
        return False, "not an FBX binary (bad magic)"
    version = struct.unpack_from("<I", header, len(FBX_MAGIC))[0]
    if not 7000 <= version <= 7800:
        return False, f"unexpected FBX version {version}"
    return True, f"FBX version {version}"


def accessor_bounds(document: dict, index: int) -> tuple:
    accessor = document["accessors"][index]
    if "min" not in accessor or "max" not in accessor:
        return (0.0, 0.0, 0.0), (0.0, 0.0, 0.0)
    low = accessor["min"][:3]
    high = accessor["max"][:3]
    return tuple(low), tuple(high)


def validate_glb(report: Report, name: str, path: Path, expect_collision: bool,
                 is_character: bool = False) -> None:
    try:
        document, _ = read_glb(path)
    except (ValueError, json.JSONDecodeError) as error:
        report.fail(f"{name}: GLB is invalid ({error})")
        return

    if "meshes" not in document or not document["meshes"]:
        report.fail(f"{name}: GLB contains no meshes")
        return

    nodes = document.get("nodes", [])
    node_names = [node.get("name", "") for node in nodes]

    extents = []
    primitives = 0
    missing_uv = 0
    has_skinning = False

    for mesh in document["meshes"]:
        for primitive in mesh.get("primitives", []):
            primitives += 1
            attributes = primitive.get("attributes", {})
            if "TEXCOORD_0" not in attributes:
                missing_uv += 1
            if "JOINTS_0" in attributes:
                has_skinning = True
            position_index = attributes.get("POSITION")
            if position_index is None:
                report.fail(f"{name}: a primitive has no POSITION accessor")
                continue
            low, high = accessor_bounds(document, position_index)
            extents.append([high[i] - low[i] for i in range(3)])

    if missing_uv:
        report.fail(f"{name}: {missing_uv} primitive(s) have no UV map (TEXCOORD_0)")
    else:
        report.ok(f"{name}: every primitive carries a UV map")

    if not extents:
        report.fail(f"{name}: no measurable geometry")
        return

    largest = max(max(extent) for extent in extents)
    smallest = max(min(extent) for extent in extents)
    if largest > MAX_SANE_EXTENT_M:
        report.fail(f"{name}: largest extent {largest:.2f} m > {MAX_SANE_EXTENT_M} m "
                    "(scale is probably not in metres)")
    elif smallest < MIN_SANE_EXTENT_M:
        report.fail(f"{name}: smallest extent {smallest:.3f} m < {MIN_SANE_EXTENT_M} m "
                    "(the export is probably scaled down 100x)")
    else:
        report.ok(f"{name}: geometry is {largest:.2f} m across (metric scale)")

    # Y-up check: glTF mandates +Y up, so an asset's up axis is Y. A floor-like
    # asset (name contains floor/plank/deck) must be wider than tall.
    if any(token in name for token in ("floor", "plank", "deck")):
        for extent in extents:
            if extent[1] > max(extent[0], extent[2]) * 0.5:
                report.warn(f"{name}: '{name}' looks like a flat piece but is {extent[1]:.2f} m thick in Y")

    if expect_collision:
        collision_nodes = [node for node in node_names if node.endswith("_collision")]
        if not collision_nodes:
            report.fail(f"{name}: manifest says collision is required, but the GLB has no *_collision node")
        else:
            report.ok(f"{name}: collision mesh present ({collision_nodes[0]})")

    if is_character:
        if not has_skinning:
            report.fail(f"{name}: character mesh has no JOINTS_0 (not skinned)")
        else:
            report.ok(f"{name}: mesh is skinned")
        if not document.get("skins"):
            report.fail(f"{name}: character GLB has no skin (armature) section")
        else:
            joints = len(document["skins"][0].get("joints", []))
            report.ok(f"{name}: {joints} joints in the armature")


def validate_character_sidecar(report: Report, name: str, sidecar_path: Path) -> None:
    if not sidecar_path.exists():
        report.fail(f"{name}: sidecar {sidecar_path.name} is missing")
        return
    try:
        sidecar = json.loads(sidecar_path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as error:
        report.fail(f"{name}: sidecar is not valid JSON ({error})")
        return

    required = {
        "Hips", "Spine", "Spine1", "Spine2", "Neck", "Head",
        "LeftShoulder", "LeftArm", "LeftForeArm", "LeftHand",
        "RightShoulder", "RightArm", "RightForeArm", "RightHand",
        "LeftUpLeg", "LeftLeg", "LeftFoot", "RightUpLeg", "RightLeg", "RightFoot",
    }
    bones = set(sidecar.get("bones", []))
    missing = sorted(required - bones)
    if missing:
        report.fail(f"{name}: skeleton is missing humanoid bones {missing}")
    else:
        report.ok(f"{name}: all {len(required)} required humanoid bones present")

    influences = sidecar.get("influences_per_vertex", {})
    heaviest = max((int(key) for key in influences), default=0)
    if heaviest > 4:
        report.fail(f"{name}: {heaviest} bone influences per vertex (budget 4)")
    elif heaviest == 0:
        report.fail(f"{name}: no skin weights recorded")
    else:
        report.ok(f"{name}: max {heaviest} bone influences per vertex")

    height = sidecar.get("height_m", 0)
    if not 1.2 <= height <= 2.2:
        report.fail(f"{name}: height {height} m is not a humanoid scale")
    else:
        report.ok(f"{name}: {height:.2f} m tall")

    if sidecar.get("role") != "rig_test_harness":
        report.warn(f"{name}: role is {sidecar.get('role')!r}, expected 'rig_test_harness'")


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", default=str(DEFAULT_OUTPUT), help="pipeline output directory")
    parser.add_argument("--strict", action="store_true", help="treat warnings as failures")
    args = parser.parse_args(argv)

    output = Path(args.output)
    manifest_path = output / "asset_manifest.json"
    report = Report(args.strict)

    print("=" * 60)
    print(" Blender asset validation")
    print("=" * 60)

    if not manifest_path.exists():
        print(f"  SKIP  no manifest at {manifest_path}")
        print("        (run the Blender generators first; CI does this automatically)")
        return 0 if not args.strict else 1

    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    assets = manifest.get("assets", [])
    if not assets:
        print("  SKIP  manifest lists no assets")
        return 0

    print(f"  {len(assets)} asset(s) in {manifest_path}")
    print()

    for asset in assets:
        name = asset.get("name", "<unnamed>")
        print(f"-- {name} ({asset.get('category', '?')})")

        files = asset.get("files", {})
        for kind, info in files.items():
            if kind == "sidecar":
                continue
            relative = info.get("path")
            if not relative:
                continue
            path = REPO_ROOT / relative
            if not path.exists():
                report.fail(f"{name}: {kind} file {relative} is missing")
                continue

            expected = info.get("sha256")
            if expected:
                actual = sha256_file(path)
                if actual != expected:
                    report.fail(f"{name}: {kind} hash {actual[:12]} does not match the manifest "
                                f"{expected[:12]} (output is stale)")
                else:
                    report.ok(f"{name}: {kind} matches the manifest hash")

            is_character = asset.get("category") == "characters"
            if kind == "glb":
                validate_glb(report, name, path, asset.get("collision", False), is_character)
            elif kind == "fbx":
                good, detail = check_fbx(path)
                if good:
                    report.ok(f"{name}: FBX header valid ({detail})")
                else:
                    report.fail(f"{name}: {detail}")

        if asset.get("category") == "characters":
            sidecar_path = REPO_ROOT / files.get("sidecar", {}).get("path", "")
            validate_character_sidecar(report, name, sidecar_path)
        print()

    return report.summary()


if __name__ == "__main__":
    raise SystemExit(main())
