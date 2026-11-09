#!/usr/bin/env python3
"""Unit tests for the Blender pipeline's *Python* half (no Blender needed).

    python3 -m unittest discover -s processing/blender/tests -v

What is testable without Blender: manifest bookkeeping, the GLB/FBX container
checks (with synthetic files), and the "would this asset pass validation"
decisions. What is not: anything that needs bpy. The generator scripts
themselves are exercised by CI when a Blender build is available; when it is
not, the job prints a SKIP and says so rather than pretending.
"""

from __future__ import annotations

import json
import struct
import sys
import tempfile
import unittest
from pathlib import Path

PIPELINE_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(PIPELINE_ROOT / "validators"))
sys.path.insert(0, str(PIPELINE_ROOT / "scripts"))

import validate_assets as va  # noqa: E402


def make_glb(path: Path, *, with_uv=True, with_collision=False, extent=(1.0, 1.0, 1.0),
             skinned=False) -> Path:
    """A minimal but structurally valid GLB, so the validator can be tested."""
    attributes = {"POSITION": 0}
    if with_uv:
        attributes["TEXCOORD_0"] = 1
    if skinned:
        attributes["JOINTS_0"] = 2
    half = [value * 0.5 for value in extent]
    document = {
        "asset": {"version": "2.0", "generator": "test"},
        "scene": 0,
        "scenes": [{"nodes": [0]}],
        "nodes": [{"name": "body", "mesh": 0}],
        "meshes": [{"name": "body", "primitives": [{"attributes": attributes, "mode": 4}]}],
        "accessors": [
            {
                "componentType": 5126,
                "count": 8,
                "type": "VEC3",
                "min": [-half[0], -half[1], -half[2]],
                "max": [half[0], half[1], half[2]],
            }
        ],
    }
    if skinned:
        document["skins"] = [{"joints": [1, 2, 3]}]
        document["nodes"].append({"name": "armature"})

    if with_collision:
        document["nodes"].append({"name": "body_collision", "mesh": 0})

    json_chunk = json.dumps(document).encode("utf-8")
    json_chunk += b" " * ((4 - len(json_chunk) % 4) % 4)
    total = 12 + 8 + len(json_chunk)
    blob = struct.pack("<III", va.GLB_MAGIC, 2, total)
    blob += struct.pack("<II", len(json_chunk), va.GLB_CHUNK_JSON) + json_chunk
    path.write_bytes(blob)
    return path


def make_fbx(path: Path, version: int = 7400) -> Path:
    path.write_bytes(va.FBX_MAGIC + struct.pack("<I", version) + b"\x00" * 64)
    return path


class GlbValidationTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.tmp = Path(self._tmp.name)
        self.report = va.Report(strict=False)

    def tearDown(self) -> None:
        self._tmp.cleanup()

    def test_valid_glb_passes(self):
        path = make_glb(self.tmp / "crate.glb", with_collision=True)
        va.validate_glb(self.report, "crate", path, expect_collision=True)
        self.assertEqual([], self.report.failures)

    def test_missing_uv_is_a_failure(self):
        path = make_glb(self.tmp / "crate.glb", with_uv=False)
        va.validate_glb(self.report, "crate", path, expect_collision=False)
        self.assertTrue(any("UV" in failure for failure in self.report.failures))

    def test_hundred_times_too_small_is_a_failure(self):
        path = make_glb(self.tmp / "crate.glb", extent=(0.01, 0.01, 0.01))
        va.validate_glb(self.report, "crate", path, expect_collision=False)
        self.assertTrue(any("scaled down" in failure for failure in self.report.failures))

    def test_missing_collision_is_a_failure_when_required(self):
        path = make_glb(self.tmp / "wall.glb")
        va.validate_glb(self.report, "wall", path, expect_collision=True)
        self.assertTrue(any("collision" in failure for failure in self.report.failures))

    def test_bad_magic_is_reported_not_raised(self):
        bad = self.tmp / "bad.glb"
        bad.write_bytes(b"not a glb at all........")
        va.validate_glb(self.report, "bad", bad, expect_collision=False)
        self.assertTrue(any("invalid" in failure for failure in self.report.failures))

    def test_unskinned_character_is_a_failure(self):
        path = make_glb(self.tmp / "char.glb", skinned=False)
        va.validate_glb(self.report, "char", path, expect_collision=False, is_character=True)
        self.assertTrue(any("not skinned" in failure for failure in self.report.failures))

    def test_skinned_character_passes(self):
        path = make_glb(self.tmp / "char.glb", skinned=True, extent=(0.5, 1.75, 0.3))
        va.validate_glb(self.report, "char", path, expect_collision=False, is_character=True)
        self.assertEqual([], self.report.failures)


class FbxValidationTests(unittest.TestCase):
    def test_valid_fbx(self):
        with tempfile.TemporaryDirectory() as folder:
            path = make_fbx(Path(folder) / "a.fbx")
            good, detail = va.check_fbx(path)
            self.assertTrue(good, detail)

    def test_unversioned_file_is_rejected(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "b.fbx"
            path.write_bytes(va.FBX_MAGIC + b"\x00" * 4)
            good, _ = va.check_fbx(path)
            self.assertFalse(good)

    def test_garbage_is_rejected(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "c.fbx"
            path.write_bytes(b"hello")
            good, _ = va.check_fbx(path)
            self.assertFalse(good)


class CharacterSidecarTests(unittest.TestCase):
    def test_missing_bones_fail(self):
        with tempfile.TemporaryDirectory() as folder:
            sidecar = Path(folder) / "char.json"
            sidecar.write_text(json.dumps({
                "bones": ["Hips", "Spine"],
                "influences_per_vertex": {"2": 10},
                "height_m": 1.75,
                "role": "rig_test_harness",
            }), encoding="utf-8")
            report = va.Report(strict=False)
            va.validate_character_sidecar(report, "char", sidecar)
            self.assertTrue(any("missing humanoid bones" in f for f in report.failures))

    def test_too_many_influences_fail(self):
        with tempfile.TemporaryDirectory() as folder:
            sidecar = Path(folder) / "char.json"
            bones = [
                "Hips", "Spine", "Spine1", "Spine2", "Neck", "Head",
                "LeftShoulder", "LeftArm", "LeftForeArm", "LeftHand",
                "RightShoulder", "RightArm", "RightForeArm", "RightHand",
                "LeftUpLeg", "LeftLeg", "LeftFoot", "RightUpLeg", "RightLeg", "RightFoot",
            ]
            sidecar.write_text(json.dumps({
                "bones": bones,
                "influences_per_vertex": {"6": 10},
                "height_m": 1.75,
                "role": "rig_test_harness",
            }), encoding="utf-8")
            report = va.Report(strict=False)
            va.validate_character_sidecar(report, "char", sidecar)
            self.assertTrue(any("influences" in f for f in report.failures))


class ManifestTests(unittest.TestCase):
    def test_manifest_merge_replaces_by_name_and_sorts(self):
        sys.path.insert(0, str(PIPELINE_ROOT / "scripts"))
        import bh_blender_common as bh

        with tempfile.TemporaryDirectory() as folder:
            out = Path(folder)
            bh.write_manifest(out, {"name": "b", "value": 1})
            bh.write_manifest(out, {"name": "a", "value": 2})
            bh.write_manifest(out, {"name": "b", "value": 3})
            manifest = json.loads((out / bh.MANIFEST_NAME).read_text(encoding="utf-8"))
            names = [asset["name"] for asset in manifest["assets"]]
            self.assertEqual(["a", "b"], names)
            self.assertEqual(3, manifest["assets"][1]["value"])


if __name__ == "__main__":
    unittest.main(verbosity=2)
