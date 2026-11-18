#!/usr/bin/env python3
"""Tests for the animation-metadata sync.

    python3 -m unittest discover -s processing/animation-metadata/tests -v

Everything here runs on synthetic files: no Unity editor, no network, no FBX
parser. The point is to prove the rules that protect the project — exact
name matching, GUID preservation and uniqueness, "never touch an .fbx", and
"never overwrite metadata a human wrote".
"""

from __future__ import annotations

import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import sync_animation_meta as sync  # noqa: E402

GUID_A = "0123456789abcdef0123456789abcdef"
GUID_B = "fedcba9876543210fedcba9876543210"
GUID_C = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"


def meta_text(guid: str, *, first_frame: int = 0, last_frame: int = 30) -> str:
    """A minimal but realistically shaped ModelImporter .meta."""
    return (
        "fileFormatVersion: 2\n"
        f"guid: {guid}\n"
        "ModelImporter:\n"
        "  serializedVersion: 22200\n"
        "  importAnimation: 1\n"
        "  animationType: 3\n"
        "  clipAnimations:\n"
        "    - serializedVersion: 16\n"
        "      name: take\n"
        "      takeName: mixamo.com\n"
        f"      firstFrame: {first_frame}\n"
        f"      lastFrame: {last_frame}\n"
        "      loopTime: 1\n"
        "  userData: \n"
    )


class Workspace:
    """A throw-away source repo + target repo pair on disk."""

    def __init__(self, root: Path) -> None:
        self.root = root
        self.source = root / "source" / "animations"
        self.target = root / "target" / "unity-project" / "Assets" / "Animations"
        self.source.mkdir(parents=True)
        self.target.mkdir(parents=True)
        self.output = root / "out"
        self.output.mkdir()

    def add_source(self, fbx_name: str, guid: str, **kwargs) -> Path:
        path = self.source / f"{fbx_name}.meta"
        path.write_bytes(meta_text(guid, **kwargs).replace("\n", "\r\n").encode("utf-8"))
        return path

    def add_fbx(self, fbx_name: str, subfolder: str = "Movement", payload: bytes | None = None) -> Path:
        folder = self.target / subfolder
        folder.mkdir(parents=True, exist_ok=True)
        path = folder / fbx_name
        path.write_bytes(payload if payload is not None else f"FBX-CONTENT-{fbx_name}".encode())
        return path

    def add_local_meta(self, fbx_name: str, guid: str, subfolder: str = "Movement") -> Path:
        path = self.target / subfolder / f"{fbx_name}.meta"
        path.write_bytes(meta_text(guid, first_frame=7).encode("utf-8"))
        return path

    def run(self, *extra: str) -> tuple[int, str]:
        report = self.output / "report.json"
        summary = self.output / "summary.md"
        code = sync.main(
            [
                "--repo-root", str(self.root / "target"),
                "--source-dir", str(self.root / "source"),
                "--report", str(report),
                "--summary", str(summary),
                *extra,
            ]
        )
        return code, summary.read_text(encoding="utf-8") if summary.is_file() else ""

    def report(self) -> dict:
        return json.loads((self.output / "report.json").read_text(encoding="utf-8"))

    def manifest(self) -> dict:
        path = self.root / "target" / sync.DEFAULT_MANIFEST
        return json.loads(path.read_text(encoding="utf-8")) if path.is_file() else {}

    def guid_of(self, fbx_name: str, subfolder: str = "Movement") -> str | None:
        path = self.target / subfolder / f"{fbx_name}.meta"
        return sync.parse_guid(path.read_text(encoding="utf-8")) if path.is_file() else None


class InitialSyncTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.ws = Workspace(Path(self._tmp.name))

    def tearDown(self) -> None:
        self._tmp.cleanup()

    def test_copies_metadata_beside_the_matching_fbx_in_any_subfolder(self) -> None:
        self.ws.add_fbx("idle.fbx", "Movement")
        self.ws.add_fbx("Dive Roll.fbx", "Combat")
        self.ws.add_source("idle.fbx", GUID_A)
        self.ws.add_source("Dive Roll.fbx", GUID_B)

        code, summary = self.ws.run()
        self.assertEqual(0, code, summary)

        self.assertEqual(GUID_A, self.ws.guid_of("idle.fbx", "Movement"))
        self.assertEqual(GUID_B, self.ws.guid_of("Dive Roll.fbx", "Combat"))
        self.assertEqual(2, self.ws.report()["counts"]["added"])
        self.assertIn(GUID_A, (self.ws.target / "Movement/idle.fbx.meta").read_text())

    def test_unmatched_fbx_and_unmatched_metadata_are_left_alone(self) -> None:
        self.ws.add_fbx("idle.fbx", "Movement")
        unmatched = self.ws.add_fbx("toss grenade.fbx", "Combat", payload=b"do-not-touch")
        self.ws.add_source("idle.fbx", GUID_A)
        self.ws.add_source("not-in-this-project.fbx", GUID_B)

        code, summary = self.ws.run()
        self.assertEqual(0, code, summary)

        self.assertFalse((self.ws.target / "Combat/toss grenade.fbx.meta").exists())
        self.assertEqual(b"do-not-touch", unmatched.read_bytes())
        self.assertEqual(1, self.ws.report()["counts"]["added"])
        self.assertEqual(1, self.ws.report()["counts"]["skipped"])
        self.assertEqual("not-in-this-project.fbx",
                         self.ws.report()["files"][sync.ORPHAN][0]["fbx"])

    def test_matching_is_case_sensitive_and_by_exact_name(self) -> None:
        self.ws.add_fbx("Idle.fbx", "Movement")
        self.ws.add_source("idle.fbx", GUID_A)  # same name, different case

        code, _ = self.ws.run()
        self.assertEqual(0, code)
        self.assertFalse((self.ws.target / "Movement/Idle.fbx.meta").exists())
        self.assertEqual(0, self.ws.report()["counts"]["added"])

    def test_meta_lands_next_to_its_fbx_not_at_the_root(self) -> None:
        self.ws.add_fbx("Dive Roll.fbx", "Combat")
        self.ws.add_source("Dive Roll.fbx", GUID_A)
        self.ws.run()
        self.assertTrue((self.ws.target / "Combat" / "Dive Roll.fbx.meta").is_file())
        self.assertFalse((self.ws.target / "Dive Roll.fbx.meta").exists())

    def test_line_endings_are_normalized_to_lf(self) -> None:
        self.ws.add_fbx("idle.fbx")
        self.ws.add_source("idle.fbx", GUID_A)  # written with CRLF
        self.ws.run()
        data = (self.ws.target / "Movement/idle.fbx.meta").read_bytes()
        self.assertNotIn(b"\r", data)
        self.assertTrue(data.endswith(b"\n"))

    def test_fbx_bytes_are_never_modified(self) -> None:
        self.ws.add_fbx("idle.fbx", payload=b"\x00\x01FBX binary")
        self.ws.add_source("idle.fbx", GUID_A)
        before = self.ws.target / "Movement/idle.fbx"
        digest = sync.sha256_bytes(before.read_bytes())
        self.ws.run()
        self.assertEqual(digest, sync.sha256_bytes(before.read_bytes()))

    def test_dry_run_writes_nothing(self) -> None:
        self.ws.add_fbx("idle.fbx")
        self.ws.add_source("idle.fbx", GUID_A)
        code, summary = self.ws.run("--dry-run")
        self.assertEqual(0, code, summary)
        self.assertFalse((self.ws.target / "Movement/idle.fbx.meta").exists())
        self.assertFalse(self.ws.manifest())  # no manifest is written at all
        self.assertEqual(1, self.ws.report()["counts"]["added"])

    def test_expect_count_guards_the_promised_total(self) -> None:
        self.ws.add_fbx("idle.fbx")
        self.ws.add_source("idle.fbx", GUID_A)
        self.assertEqual(0, self.ws.run("--expect-count", "1")[0])
        # A second run is a no-op, so the file count still matches.
        self.assertEqual(0, self.ws.run("--expect-count", "1")[0])
        # Promising two when only one can be synced must fail.
        self.assertEqual(1, self.ws.run("--expect-count", "2")[0])


class IdempotenceTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.ws = Workspace(Path(self._tmp.name))
        self.ws.add_fbx("idle.fbx")
        self.ws.add_fbx("run forward.fbx")
        self.ws.add_source("idle.fbx", GUID_A)
        self.ws.add_source("run forward.fbx", GUID_B)
        self.assertEqual(0, self.ws.run()[0])

    def tearDown(self) -> None:
        self._tmp.cleanup()

    def test_second_run_is_a_no_op(self) -> None:
        code, summary = self.ws.run()
        self.assertEqual(0, code, summary)
        self.assertEqual(0, self.ws.report()["counts"]["added"])
        self.assertEqual(0, self.ws.report()["counts"]["updated"])
        self.assertEqual(2, self.ws.report()["counts"]["unchanged"])
        self.assertFalse(self.ws.report()["changes"])
        self.assertIn("No metadata changes", summary)

    def test_manifest_is_stable_across_runs(self) -> None:
        path = Path(self._tmp.name) / "target" / sync.DEFAULT_MANIFEST
        first = path.read_bytes()
        self.ws.run()
        self.assertEqual(first, path.read_bytes())


class UpdateTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.ws = Workspace(Path(self._tmp.name))
        self.ws.add_fbx("idle.fbx")
        self.ws.add_source("idle.fbx", GUID_A, last_frame=30)
        self.assertEqual(0, self.ws.run()[0])

    def tearDown(self) -> None:
        self._tmp.cleanup()

    def test_source_change_updates_the_metadata_and_keeps_the_guid(self) -> None:
        self.ws.add_source("idle.fbx", GUID_A, last_frame=99)
        code, summary = self.ws.run()
        self.assertEqual(0, code, summary)
        self.assertEqual(1, self.ws.report()["counts"]["updated"])
        text = (self.ws.target / "Movement/idle.fbx.meta").read_text(encoding="utf-8")
        self.assertIn("lastFrame: 99", text)
        self.assertEqual(GUID_A, self.ws.guid_of("idle.fbx"), "GUID must survive an update")

    def test_local_edit_with_unchanged_source_is_preserved(self) -> None:
        local = self.ws.target / "Movement/idle.fbx.meta"
        local.write_text(local.read_text(encoding="utf-8").replace("loopTime: 1", "loopTime: 0"),
                         encoding="utf-8")
        code, summary = self.ws.run()
        self.assertEqual(0, code, summary)
        self.assertEqual(1, self.ws.report()["counts"]["skipped"])
        self.assertIn("loopTime: 0", local.read_text(encoding="utf-8"),
                      "a local edit must not be reverted while the source is unchanged")

    def test_local_guid_wins_over_the_source_guid(self) -> None:
        local = self.ws.target / "Movement/idle.fbx.meta"
        # Simulate Unity having generated a different GUID locally, e.g. on first import.
        local.write_text(meta_text(GUID_C, last_frame=30).replace("lastFrame: 30", "lastFrame: 25"),
                         encoding="utf-8")
        manifest_path = Path(self._tmp.name) / "target" / sync.DEFAULT_MANIFEST
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        entry = manifest["entries"]["unity-project/Assets/Animations/Movement/idle.fbx.meta"]
        entry["guid"] = GUID_C
        manifest_path.write_text(json.dumps(manifest), encoding="utf-8")

        self.ws.add_source("idle.fbx", GUID_A, last_frame=99)
        code, summary = self.ws.run()
        self.assertEqual(0, code, summary)
        self.assertEqual(1, self.ws.report()["counts"]["updated"])
        self.assertEqual(GUID_C, self.ws.guid_of("idle.fbx"), "local GUID must be preserved")
        self.assertIn("lastFrame: 99", local.read_text(encoding="utf-8"))

    def test_foreign_metadata_is_never_overwritten(self) -> None:
        self.ws.add_fbx("extra.fbx")
        # Committed by hand, not by the sync: no manifest record exists for it.
        self.ws.add_local_meta("extra.fbx", GUID_C)
        self.ws.add_source("extra.fbx", GUID_B, last_frame=55)

        code, summary = self.ws.run()
        self.assertEqual(0, code, summary)
        self.assertEqual(GUID_C, self.ws.guid_of("extra.fbx"),
                         "metadata the sync did not write must be reported, not replaced")
        self.assertNotIn("lastFrame: 55", (self.ws.target / "Movement/extra.fbx.meta").read_text())
        self.assertIn("Skipped — local metadata not created by the sync", summary)
        self.assertNotIn("unity-project/Assets/Animations/Movement/extra.fbx.meta",
                         self.ws.manifest()["entries"])

    def test_changed_fbx_is_reported_without_being_touched(self) -> None:
        fbx = self.ws.target / "Movement/idle.fbx"
        fbx.write_bytes(b"a new export of the same clip")
        code, _ = self.ws.run()
        self.assertEqual(0, code)
        self.assertEqual(1, self.ws.report()["counts"]["fbx_changed"])
        self.assertEqual(b"a new export of the same clip", fbx.read_bytes())


class GuidTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.ws = Workspace(Path(self._tmp.name))

    def tearDown(self) -> None:
        self._tmp.cleanup()

    def test_duplicate_guids_between_two_fbx_files_abort_the_run(self) -> None:
        self.ws.add_fbx("idle.fbx")
        self.ws.add_fbx("running.fbx")
        self.ws.add_source("idle.fbx", GUID_A)
        self.ws.add_source("running.fbx", GUID_A)  # same GUID twice in the source

        code, summary = self.ws.run()
        self.assertEqual(2, code, "a GUID clash must abort, not partially sync")
        self.assertFalse((self.ws.target / "Movement/idle.fbx.meta").exists())
        self.assertFalse((self.ws.target / "Movement/running.fbx.meta").exists())
        self.assertIn("duplicate Unity GUID", summary)

    def test_clash_with_an_unrelated_project_asset_aborts_the_run(self) -> None:
        self.ws.add_fbx("idle.fbx")
        self.ws.add_source("idle.fbx", GUID_A)
        other = Path(self._tmp.name) / "target/unity-project/Assets/Resources/Config"
        other.mkdir(parents=True)
        (other / "siran_island_heights.bytes.meta").write_text(
            f"fileFormatVersion: 2\nguid: {GUID_A}\n", encoding="utf-8")

        code, _ = self.ws.run()
        self.assertEqual(2, code)
        self.assertFalse((self.ws.target / "Movement/idle.fbx.meta").exists())

    def test_invalid_guid_in_source_is_reported_and_skipped(self) -> None:
        self.ws.add_fbx("idle.fbx")
        (self.ws.source / "idle.fbx.meta").write_text(
            "fileFormatVersion: 2\nguid: NOT-A-GUID\n", encoding="utf-8")
        code, _ = self.ws.run()
        self.assertEqual(0, code)
        self.assertFalse((self.ws.target / "Movement/idle.fbx.meta").exists())
        self.assertEqual(0, self.ws.report()["counts"]["added"])

    def test_ambiguous_fbx_name_is_reported_not_guessed(self) -> None:
        self.ws.add_fbx("idle.fbx", "Movement")
        self.ws.add_fbx("idle.fbx", "Weapon")
        self.ws.add_source("idle.fbx", GUID_A)
        code, summary = self.ws.run()
        self.assertEqual(0, code, summary)
        self.assertFalse((self.ws.target / "Movement/idle.fbx.meta").exists())
        self.assertFalse((self.ws.target / "Weapon/idle.fbx.meta").exists())
        self.assertIn("ambiguous", summary)


class ForceTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.ws = Workspace(Path(self._tmp.name))
        self.ws.add_fbx("idle.fbx")
        self.ws.add_source("idle.fbx", GUID_A)

    def tearDown(self) -> None:
        self._tmp.cleanup()

    def test_force_reapplies_owned_metadata_without_losing_the_guid(self) -> None:
        self.assertEqual(0, self.ws.run()[0])
        local = self.ws.target / "Movement/idle.fbx.meta"
        local.write_text(local.read_text(encoding="utf-8").replace("loopTime: 1", "loopTime: 0"),
                         encoding="utf-8")

        code, summary = self.ws.run("--force")
        self.assertEqual(0, code, summary)
        self.assertEqual(1, self.ws.report()["counts"]["updated"])
        self.assertIn("loopTime: 1", local.read_text(encoding="utf-8"))
        self.assertEqual(GUID_A, self.ws.guid_of("idle.fbx"))

    def test_force_never_adopts_foreign_metadata(self) -> None:
        self.ws.add_fbx("extra.fbx")
        self.ws.add_local_meta("extra.fbx", GUID_C)
        self.ws.add_source("extra.fbx", GUID_B)

        code, _ = self.ws.run("--force")
        self.assertEqual(0, code)
        self.assertEqual(GUID_C, self.ws.guid_of("extra.fbx"))
        self.assertEqual(0, self.ws.report()["counts"]["updated"])


class PostRunVerificationTests(unittest.TestCase):
    """The sync verifies its own result before claiming success."""

    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.ws = Workspace(Path(self._tmp.name))

    def tearDown(self) -> None:
        self._tmp.cleanup()

    def test_run_with_changed_fbx_passes_and_reports_it(self) -> None:
        self.ws.add_fbx("idle.fbx")
        self.ws.add_source("idle.fbx", GUID_A)
        self.assertEqual(0, self.ws.run()[0])
        (self.ws.target / "Movement/idle.fbx").write_bytes(b"a new export")
        code, summary = self.ws.run()
        self.assertEqual(0, code, summary)
        self.assertEqual(1, self.ws.report()["counts"]["fbx_changed"])
        self.assertEqual(b"a new export", (self.ws.target / "Movement/idle.fbx").read_bytes())


class VerifyTests(unittest.TestCase):
    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.ws = Workspace(Path(self._tmp.name))
        self.ws.add_fbx("idle.fbx")
        self.ws.add_source("idle.fbx", GUID_A)
        self.assertEqual(0, self.ws.run()[0])

    def tearDown(self) -> None:
        self._tmp.cleanup()

    def test_verify_passes_on_a_synced_project(self) -> None:
        self.assertEqual(0, self.ws.run("--verify-only", "--expect-count", "1")[0])

    def test_verify_fails_when_metadata_is_missing(self) -> None:
        (self.ws.target / "Movement/idle.fbx.meta").unlink()
        self.assertEqual(1, self.ws.run("--verify-only")[0])

    def test_verify_fails_on_a_duplicate_guid(self) -> None:
        other = Path(self._tmp.name) / "target" / "unity-project" / "Assets" / "Shaders"
        other.mkdir(parents=True)
        (other / "SiranRadiusClip.shader.meta").write_text(
            f"fileFormatVersion: 2\nguid: {GUID_A}\n", encoding="utf-8")
        self.assertEqual(1, self.ws.run("--verify-only")[0])

    def test_verify_fails_on_a_hand_edited_guid(self) -> None:
        path = self.ws.target / "Movement/idle.fbx.meta"
        path.write_text(meta_text(GUID_C), encoding="utf-8")
        self.assertEqual(1, self.ws.run("--verify-only")[0])


if __name__ == "__main__":
    unittest.main()
