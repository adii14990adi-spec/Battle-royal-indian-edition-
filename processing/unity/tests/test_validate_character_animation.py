"""Unit tests for the static character/animation validator.

The category and loop rules are mirrored from the C# source; these tests pin
the mirror so a future C# edit cannot silently diverge from CI's static gate.
"""

from __future__ import annotations

import contextlib
import io
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import validate_character_animation as vca  # noqa: E402

REPO_ROOT = Path(__file__).resolve().parents[3]


class RuleMirrorTests(unittest.TestCase):
    def test_category_inference_matches_the_csharp_test_cases(self):
        # Same literals as CharacterPipelineTests.InferFromFileName_...
        cases = {
            "death from the front.fbx": "Death",
            "Dying.fbx": "Death",
            "Crouch Walking.fbx": "CrouchWalk",
            "idle crouching.fbx": "Crouch",
            "Prone Idle.fbx": "Crouch",
            "sprint forward.fbx": "Sprint",
            "rifle run.fbx": "RifleRun",
            "rifle aiming idle.fbx": "RifleIdle",
            "reloading.fbx": "Reload",
            "firing rifle.fbx": "Shoot",
            "Grab And Put Back Rifle.fbx": "WeaponSwitch",
            "hit reaction.fbx": "Melee",
            "toss grenade.fbx": "Melee",
            "idle aiming.fbx": "Aim",
            "jump up.fbx": "Jump",
            "walking.fbx": "Walk",
        }
        for name, expected in cases.items():
            with self.subTest(name=name):
                self.assertEqual(vca.infer_category(name), expected)

    def test_loop_rules(self):
        self.assertTrue(vca.wants_loop("walking.fbx"))
        self.assertTrue(vca.wants_loop("Running.fbx"))
        self.assertTrue(vca.wants_loop("jump loop.fbx"))
        self.assertTrue(vca.wants_loop("idle aiming.fbx"))
        self.assertFalse(vca.wants_loop("jump up.fbx"))
        self.assertFalse(vca.wants_loop("jump down.fbx"))
        self.assertFalse(vca.wants_loop("death from the front.fbx"))
        self.assertFalse(vca.wants_loop("reloading.fbx"))
        self.assertFalse(vca.wants_loop("Running To Stop.fbx"))
        for turn in ("turn 90 left.fbx", "turn 90 right.fbx", "turn left.fbx",
                     "turning right 45 degrees.fbx",
                     "crouching turn 90 left.fbx", "crouching turn 90 right.fbx"):
            with self.subTest(turn=turn):
                self.assertFalse(vca.wants_loop(turn), turn)

    def test_all_committed_clip_loop_flags_match_the_mirror(self):
        animations = REPO_ROOT / "unity-project/Assets/Animations"
        metas = sorted(animations.rglob("*.fbx.meta"))
        if not metas:
            self.skipTest("animation metadata not present in this checkout")
        self.assertEqual(len(metas), 74)
        for meta in metas:
            block = vca.clip_block(meta.read_text(encoding="utf-8")) or ""
            fbx_name = meta.name[:-5]
            with self.subTest(meta=meta.name):
                self.assertEqual(
                    vca.clip_field(block, "loopTime"),
                    str(int(vca.wants_loop(fbx_name))),
                )
                self.assertEqual(vca.clip_field(block, "loopBlend"),
                                 vca.clip_field(block, "loopTime"))

    def test_clip_field_and_yaml_field_parsing(self):
        text = ("ModelImporter:\n"
                "  animationType: 3\n"
                "  clipAnimations:\n"
                "  - serializedVersion: 16\n"
                "    name: idle\n"
                "    firstFrame: 0\n"
                "    lastFrame: 63\n")
        self.assertEqual(vca.yaml_field(text, "animationType"), "3")
        block = vca.clip_block(text)
        self.assertIsNotNone(block)
        self.assertEqual(vca.clip_field(block, "name"), "idle")
        self.assertEqual(vca.clip_field(block, "lastFrame"), "63")

    def test_duplicate_guid_detection(self):
        import tempfile
        with tempfile.TemporaryDirectory() as folder:
            first = Path(folder) / "a.meta"
            second = Path(folder) / "b.meta"
            good = "0123456789abcdef0123456789abcdef"
            first.write_text("guid: " + good + "\n")
            second.write_text("guid: " + good + "\n")
            _, problems = vca.guid_map([first, second])
            self.assertTrue(any("duplicate" in p.lower() or ":" in p
                                for p in problems), problems)
            second.write_text("guid: not-a-guid\n")
            _, problems = vca.guid_map([first, second])
            self.assertEqual(len(problems), 1)


@unittest.skipUnless(
    (REPO_ROOT / "unity-project/Assets/Animations").is_dir(),
    "Unity project not present in this checkout")
class RepositorySmokeTests(unittest.TestCase):
    def test_static_validator_passes_on_the_repository(self):
        with contextlib.redirect_stdout(io.StringIO()) as captured:
            code = vca.main([])
        self.assertEqual(code, 0, captured.getvalue())
        self.assertIn("0 failed", captured.getvalue())

    def test_every_fbx_has_a_meta_and_every_guid_is_unique(self):
        assets = REPO_ROOT / "unity-project/Assets"
        fbx = sorted(assets.rglob("*.fbx"))
        metas = sorted(assets.rglob("*.meta"))
        self.assertEqual(len(fbx), len([m for m in metas
                                        if m.name.endswith(".fbx.meta")]))
        for path in fbx:
            with self.subTest(fbx=path.name):
                self.assertTrue(Path(str(path) + ".meta").is_file())
        _, problems = vca.guid_map(metas)
        self.assertEqual(problems, [])


if __name__ == "__main__":
    unittest.main()
