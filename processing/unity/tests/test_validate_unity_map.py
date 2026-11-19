#!/usr/bin/env python3
"""Unit tests for the explicit Git LFS pointer / smudge validation boundary."""

from __future__ import annotations

import sys
import unittest
from pathlib import Path

PROCESSING = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(PROCESSING))

import validate_unity_map as map_validator  # noqa: E402


class GitLfsValidationTests(unittest.TestCase):
    def test_pointer_parsing_is_exact(self) -> None:
        pointer = (b"version https://git-lfs.github.com/spec/v1\n"
                   b"oid sha256:" + b"a" * 64 + b"\nsize 137293\n")
        self.assertEqual(map_validator.lfs_pointer(pointer), ("a" * 64, 137293))
        self.assertIsNone(map_validator.lfs_pointer(b"\x89PNG\r\n\x1a\n"))

    def test_all_pointer_paths_have_lfs_rules(self) -> None:
        for path in map_validator.LFS_EXPECTED:
            self.assertTrue(map_validator.has_lfs_rule(path), path)

    def test_committed_water_pointer_matches_manifest_identity(self) -> None:
        path = Path("unity-project/Assets/Resources/Art/Textures/water_normal_256.png")
        data = path.read_bytes()
        pointer = map_validator.lfs_pointer(data)
        self.assertIsNotNone(pointer, "local checkout should preserve the exact Git LFS pointer")
        oid, size = pointer
        self.assertEqual(map_validator.LFS_EXPECTED[map_validator.repo_relative(path)], (oid, size))


if __name__ == "__main__":
    unittest.main(verbosity=2)
