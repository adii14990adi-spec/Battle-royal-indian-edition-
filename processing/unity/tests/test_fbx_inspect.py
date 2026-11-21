"""Unit tests for the dependency-free FBX reader (no Unity required)."""

from __future__ import annotations

import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from fbx_inspect import (  # noqa: E402
    FbxError,
    HUMANOID_ALIASES,
    REQUIRED_HUMANOID_BONES,
    normalize_bone,
    read_fbx,
)

REPO_ROOT = Path(__file__).resolve().parents[3]
MALE = REPO_ROOT / "unity-project/Assets/Characters/Male/Male character.fbx"


class BoneConventionTests(unittest.TestCase):
    def test_normalization_merges_mixamo_and_unreal_names(self):
        self.assertEqual(normalize_bone("mixamorig:LeftArm"), "leftarm")
        self.assertEqual(normalize_bone("mixamorig1:LeftArm"), "leftarm")
        self.assertEqual(normalize_bone("upperarm_l"), "upperarml")
        self.assertEqual(normalize_bone("UpperArm_L"), "upperarml")
        self.assertEqual(normalize_bone("spine_01"), "spine01")

    def test_every_required_bone_has_an_alias(self):
        for bone in REQUIRED_HUMANOID_BONES:
            self.assertIn(bone, HUMANOID_ALIASES, bone)
            self.assertTrue(HUMANOID_ALIASES[bone], bone)

    def test_non_fbx_is_rejected(self):
        import tempfile
        with tempfile.NamedTemporaryFile(suffix=".fbx", delete=False) as handle:
            handle.write(b"this is not an FBX")
            name = handle.name
        try:
            with self.assertRaises(FbxError):
                read_fbx(name)
        finally:
            Path(name).unlink()

    def test_truncated_header_is_rejected(self):
        import tempfile
        with tempfile.NamedTemporaryFile(suffix=".fbx", delete=False) as handle:
            handle.write(b"Kaydara FBX Binary  \x00\x1a\x00")
            name = handle.name
        try:
            with self.assertRaises(FbxError):
                read_fbx(name)
        finally:
            Path(name).unlink()


@unittest.skipUnless(MALE.is_file(), "character FBX not present in this checkout")
class RealFbxTests(unittest.TestCase):
    def test_character_skeleton_is_a_connected_humanoid_skinned_mesh(self):
        doc = read_fbx(MALE)
        self.assertEqual(doc.version, 7400)
        self.assertEqual(len(doc.bone_names), 65)
        self.assertEqual(doc.missing_required_bones(), [])
        self.assertTrue(doc.core_hierarchy_connected())
        self.assertGreater(doc.skin_count, 0)
        self.assertGreater(doc.cluster_count, 0)
        self.assertGreater(doc.vertex_count, 0)
        self.assertEqual(doc.stacks, [])
        self.assertTrue(doc.is_character)
        self.assertFalse(doc.is_animation)
        self.assertEqual(doc.unit_scale, 100.0)
        self.assertGreater(len(doc.textures), 0)

    def test_animation_library_is_removed(self):
        animations = REPO_ROOT / "unity-project/Assets/Animations"
        clips = sorted(animations.rglob("*.fbx")) if animations.is_dir() else []
        self.assertEqual([], clips,
                         "the 74 Mixamo animation FBX clips were removed on 2026-11-20 "
                         "and must not come back")

    def test_all_fbx_files_in_the_repository_parse(self):
        files = sorted((REPO_ROOT / "unity-project").rglob("*.fbx"))
        self.assertEqual(2, len(files), "only the two production character FBX remain")
        for path in files:
            with self.subTest(path=path.name):
                doc = read_fbx(path)
                self.assertEqual(doc.missing_required_bones(), [])
                self.assertTrue(doc.core_hierarchy_connected())


if __name__ == "__main__":
    unittest.main()
