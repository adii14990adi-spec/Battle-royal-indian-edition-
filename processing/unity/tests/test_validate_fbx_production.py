#!/usr/bin/env python3
"""Unit tests for the production-FBX static validator helpers."""

from __future__ import annotations

import sys
import tempfile
import unittest
from pathlib import Path

PROCESSING = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(PROCESSING))

import validate_fbx_production as validator  # noqa: E402


class ProductionFbxValidatorTests(unittest.TestCase):
    def test_meta_value_reads_indented_yaml_scalar(self) -> None:
        text = "fileFormatVersion: 2\nguid: abcdef\n  animationType: 3\n  avatarSetup: 1\n"
        self.assertEqual(validator.meta_value(text, "guid"), "abcdef")
        self.assertEqual(validator.meta_value(text, "animationType"), "3")
        self.assertIsNone(validator.meta_value(text, "missing"))

    def test_check_meta_settings_reports_mismatch_as_fail(self) -> None:
        report = validator.Report()
        with tempfile.TemporaryDirectory() as directory:
            meta = Path(directory) / "model.fbx.meta"
            meta.write_text("animationType: 2\navatarSetup: 1\n", encoding="utf-8")
            validator.check_meta_settings(
                report,
                "test settings",
                meta,
                {"animationType": "3", "avatarSetup": "1"},
            )
        self.assertEqual(len(report.rows), 1)
        self.assertEqual(report.rows[0]["status"], "FAIL")
        self.assertIn("animationType", report.rows[0]["detail"])

    def test_all_required_clip_paths_exist_on_disk(self) -> None:
        for relative in validator.REQUIRED_CLIPS.values():
            fbx = validator.ASSETS_ROOT / "Animations" / relative
            self.assertTrue(fbx.is_file(), f"missing required clip: {fbx}")
            self.assertTrue(Path(str(fbx) + ".meta").is_file(), f"missing meta: {fbx}.meta")

    def test_static_report_accepts_only_scoped_repairs(self) -> None:
        report = validator.run_validation()
        statuses = {row["check"]: row["status"] for row in report.rows}
        self.assertEqual(statuses["Female embedded source textures"], "PASS")
        self.assertEqual(statuses["Male Unity material import mode"], "PASS")
        self.assertEqual(statuses["Female Unity material import mode"], "PASS")
        self.assertEqual(statuses["runtime shared-assignment lookup handles Unity empty strings"], "PASS")
        self.assertEqual(statuses["Animator controller shared-assignment resolution handles Unity empty strings"], "PASS")
        self.assertEqual(statuses["Aim Animator parameter is driven by runtime"], "PASS")
        self.assertEqual(statuses["Aim gameplay hook is called"], "PASS")
        self.assertEqual(statuses["14 exact production character textures and importer settings"], "PASS")
        self.assertEqual(statuses["6 deterministic URP/Lit material remaps"], "PASS")
        self.assertEqual(statuses["targeted repair protected file hashes verified"], "PASS")
        self.assertEqual(statuses["Git LFS object identity and checkout smudge rules"], "PASS")
        self.assertEqual(statuses["all 76 production FBX files immutable"], "PASS")
        self.assertEqual(statuses["all other production Unity assets and FBX metadata unchanged"], "PASS")
        self.assertEqual(statuses["GLTF fallback files and test scene preserved"], "PASS")
        self.assertEqual(statuses["live shooting gameplay integration"], "NOT VERIFIED")
        self.assertEqual(statuses["Android device rendering / shader variants / FPS / memory / thermal"], "NOT VERIFIED")
        self.assertEqual(statuses["correct textured Male/Female Editor camera captures"], "NOT VERIFIED")
        self.assertFalse(report.failures, "\n".join(str(row) for row in report.failures))


if __name__ == "__main__":
    unittest.main(verbosity=2)
