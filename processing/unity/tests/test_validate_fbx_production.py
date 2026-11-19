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

    def test_static_report_counts_known_failures_explicitly(self) -> None:
        report = validator.run_validation()
        statuses = {row["check"]: row["status"] for row in report.rows}
        self.assertEqual(statuses["Female embedded source textures"], "FAIL")
        self.assertEqual(statuses["Aim Animator parameter is driven by runtime"], "FAIL")
        self.assertEqual(statuses["live shooting gameplay integration"], "NOT VERIFIED")
        self.assertEqual(statuses["all 76 production FBX files immutable"], "PASS")


if __name__ == "__main__":
    unittest.main(verbosity=2)
