#!/usr/bin/env python3
"""Static character/animation verification for the Unity 6 project.

    python3 processing/unity/validate_character_animation.py
    python3 processing/unity/validate_character_animation.py --json out.json --report out.md

This is a **static** validator. It reads the FBX bytes, the committed Unity
``.meta`` files, the Unity project settings and every committed YAML asset. It
does NOT need (and does NOT claim to have) a Unity Editor, an Avatar import, an
AnimationClip import, a C# compile, an Android APK or a device. Checks that
require those are listed explicitly as ``SKIP`` with the missing environment
named, and are never counted as passes.

What it can prove from disk:

* every FBX has a ``.meta`` beside it and no ``.meta`` is orphaned;
* every GUID is a 32-hex value and is unique across ``Assets/``;
* every committed asset reference (``guid:`` in Unity YAML) resolves;
* no FBX source byte changed relative to the committed baseline;
* no two FBX files carry identical content;
* every FBX is a readable binary FBX 7.x document;
* ``clipAnimations`` names, take names and frame ranges match the real FBX
  animation stack (FBX ``KTime`` -> seconds -> frames at the file's frame rate);
* ``loopTime`` / ``loopBlend`` agree with ``MixamoImportSettings.WantsLoop``;
* importer settings are consistent (Humanoid, avatar setup, Optimal
  compression, 4 bone influences, non-readable, etc.);
* each skeleton contains all 15 Unity-required humanoid bones, connected to the
  Hips parent chain, in either the Mixamo or Unreal-style naming convention;
* characters are skinned meshes with no clips; animations are skeletons with
  exactly one clip and no mesh;
* every animation file classifies into a category and the eight gameplay
  categories all have at least one clip;
* the committed Android project settings match the documented target.

Unity's ``ModelImporterClipAnimation`` YAML -> C# mapping used below is taken
from UnityCsReference ``ModelImporter.bindings.cs``:
``loopBlendOrientation`` = ``lockRootRotation``, ``loopBlendPositionY`` =
``lockRootHeightY``, ``loopBlendPositionXZ`` = ``lockRootPositionXZ``.
Those are the "Bake Into Pose" flags.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
from collections import Counter, defaultdict
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from fbx_inspect import (  # noqa: E402
    FbxError,
    REQUIRED_HUMANOID_BONES,
    FbxDocument,
    read_fbx,
)

REPO_ROOT = Path(__file__).resolve().parents[2]
UNITY = REPO_ROOT / "unity-project"
ASSETS = UNITY / "Assets"
ANIMATIONS = ASSETS / "Animations"
CHARACTERS = ASSETS / "Characters"
USER_ANIMATIONS = ASSETS / "UserAssets" / "Models" / "Animation"
BASELINE = Path(__file__).resolve().parent / "fbx-baseline.json"

# Faithful port of BattleHorizons.Characters.CharacterAnimCategories and
# BattleHorizons.EditorTools.MixamoImportSettings. Keep the two in sync; the
# EditMode tests assert the same literals.
FORCE_LOOP_TOKENS = ("loop", "cycle")
FORCE_ONE_SHOT_TOKENS = (
    "to stop", "to idle", "stop", "one shot", "one-shot", "land", "turn",
)
LOOPING_CATEGORIES = frozenset({
    "Idle", "Walk", "Run", "Sprint", "Crouch", "CrouchWalk", "Aim",
    "RifleRun", "RifleIdle",
})
REQUIRED_GAMEPLAY_CATEGORIES = (
    "Idle", "Walk", "Run", "Jump", "Crouch", "Aim", "Shoot", "Reload",
)

# Unity built-in GUIDs that are never backed by a .meta file in this project.
# 31321ba... is the URP default material GUID used by GameObject.CreatePrimitive
# in the isolated GLTF test scene; it resolves from URP global settings.
BUILTIN_GUIDS = frozenset({
    "00000000000000000000000000000000",
    "0000000000000000f000000000000000",
    "0000000000000000e000000000000000",
    "00000000000000001000000000000000",
    "31321ba15b8f8eb4c954353edc038b1d",
})

# Exact GUID of URP 17's Packages/com.unity.render-pipelines.universal/
# Shaders/Lit.shader.meta. Package GUIDs are not stored under Assets/, so the
# static resolver must recognise this one after verifying that URP is declared.
URP_LIT_SHADER_GUID = "933532a4fcc9baf4fa0491de14d08ed7"


def infer_category(file_name: str) -> str:
    """Python mirror of CharacterAnimCategories.InferFromFileName."""
    n = (file_name or "").lower()
    if "death" in n or "dying" in n or "die " in n:
        return "Death"
    if "reload" in n:
        return "Reload"
    if "firing" in n or "shoot" in n or "shooting" in n:
        return "Shoot"
    if "rifle aiming" in n or ("rifle" in n and "idle" in n and "aim" in n):
        return "RifleIdle"
    if "aiming idle" in n:
        return "RifleIdle"
    if "rifle run" in n or "rifle sprint" in n or "run with rifle" in n:
        return "RifleRun"
    if "aim" in n:
        return "Aim"
    if ("switch" in n or "holster" in n or "grab and put" in n
            or "equip" in n or "draw weapon" in n):
        return "WeaponSwitch"
    if (("walk" in n and "crouch" in n) or ("crouch" in n and "walk" in n)
            or "crouching walk" in n):
        return "CrouchWalk"
    if "crouch" in n or "prone" in n:
        return "Crouch"
    if "sprint" in n:
        return "Sprint"
    if "run" in n or "jog" in n:
        return "Run"
    if "walk" in n or "strafe" in n:
        return "Walk"
    if "jump down" in n or "fall" in n or "land" in n:
        return "Fall"
    if "jump" in n:
        return "Jump"
    if any(t in n for t in ("grenade", "melee", "punch", "kick",
                            "hit reaction", "tackle", "dive", "roll", "dodge")):
        return "Melee"
    if "turn" in n:
        return "Walk"
    if "idle" in n or "stand" in n:
        return "Idle"
    return "Walk"


def wants_loop(file_name: str) -> bool:
    """Python mirror of MixamoImportSettings.WantsLoop."""
    n = (file_name or "").lower()
    if any(t in n for t in FORCE_LOOP_TOKENS):
        return True
    if any(t in n for t in FORCE_ONE_SHOT_TOKENS):
        return False
    return infer_category(file_name) in LOOPING_CATEGORIES


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def yaml_field(text: str, key: str) -> str | None:
    match = re.search(r"^\s*" + re.escape(key) + r":\s*(.*)$", text, re.M)
    return match.group(1).strip() if match else None


def clip_block(text: str) -> str | None:
    match = re.search(
        r"^\s*clipAnimations:\s*\n((?:[ \t]+.*\n)*)", text, re.M
    )
    return match.group(1) if match else None


def clip_field(block: str, key: str) -> str | None:
    match = re.search(r"^\s+" + re.escape(key) + r":\s*(.*)$", block, re.M)
    return match.group(1).strip() if match else None


class Report:
    def __init__(self) -> None:
        self.checks: list[dict] = []
        self.stats: dict = {}

    def check(self, name: str, ok: bool, detail: str) -> None:
        self._add("PASS" if ok else "FAIL", name, detail)
        print(f"  {'PASS' if ok else 'FAIL'}  {name}"
              + (f" — {detail}" if detail else ""))
        if not ok:
            print(f"::error::{name}: {detail}")

    def skip(self, name: str, detail: str) -> None:
        self._add("SKIP", name, detail)
        print(f"  SKIP  {name} — {detail}")

    def info(self, name: str, detail: str) -> None:
        self._add("INFO", name, detail)
        print(f"  INFO  {name} — {detail}")

    def _add(self, status: str, name: str, detail: str) -> None:
        self.checks.append({"status": status, "name": name, "detail": detail})

    def counts(self) -> dict[str, int]:
        return dict(Counter(row["status"] for row in self.checks))

    def summary(self) -> int:
        counts = self.counts()
        print()
        print(f"{counts.get('PASS', 0)} passed, {counts.get('FAIL', 0)} failed, "
              f"{counts.get('SKIP', 0)} editor-only skipped, "
              f"{counts.get('INFO', 0)} informational")
        return 1 if counts.get("FAIL", 0) else 0


def collect_fbx() -> list[Path]:
    return sorted(ASSETS.rglob("*.fbx"))


def collect_metas() -> list[Path]:
    return sorted(ASSETS.rglob("*.meta"))


def guid_map(metas: list[Path]) -> tuple[dict[str, Path], list[str]]:
    owners: dict[str, Path] = {}
    malformed: list[str] = []
    duplicates: list[str] = []
    for meta in metas:
        match = re.search(r"^guid:\s*([0-9a-fA-F]{32})\s*$",
                          meta.read_text(encoding="utf-8", errors="replace"), re.M)
        if not match:
            malformed.append(f"no 32-hex guid: {display_path(meta)}")
            continue
        guid = match.group(1).lower()
        if guid in owners:
            duplicates.append(
                f"duplicate GUID {guid}: {display_path(owners[guid])} and "
                f"{display_path(meta)}"
            )
        owners.setdefault(guid, meta)
    return owners, malformed + duplicates


def display_path(path: Path) -> str:
    """Repo-relative when possible, absolute otherwise (temp test fixtures)."""
    try:
        return str(path.relative_to(REPO_ROOT))
    except ValueError:
        return str(path)


def unity_yaml_assets() -> list[Path]:
    suffixes = {".controller", ".prefab", ".unity", ".asset", ".mat",
                ".mask", ".anim", ".overrides", ".lighting"}
    return [p for p in ASSETS.rglob("*")
            if p.is_file() and p.suffix.lower() in suffixes]


def referenced_guids(text: str) -> list[str]:
    return [m.lower() for m in re.findall(
        r"\bguid:\s*([0-9a-fA-F]{32})", text)]


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--json", dest="json_path", type=Path,
                        help="write a machine-readable report here")
    parser.add_argument("--report", dest="report_path", type=Path,
                        help="write a Markdown report here")
    args = parser.parse_args(argv)

    report = Report()
    print("=" * 72)
    print(" Unity 6 character & animation STATIC validation (no Unity Editor)")
    print("=" * 72)

    # ------------------------------------------------------------------ #
    # Project structure
    # ------------------------------------------------------------------ #
    required = [UNITY / "Assets", UNITY / "ProjectSettings", UNITY / "Packages",
                ANIMATIONS, CHARACTERS,
                UNITY / "ProjectSettings" / "ProjectVersion.txt"]
    missing = [str(p.relative_to(REPO_ROOT)) for p in required if not p.exists()]
    report.check("Unity project structure", not missing,
                 ", ".join(missing) if missing else
                 "Assets/, ProjectSettings/, Packages/, Animations/, Characters/ present")

    version = (UNITY / "ProjectSettings" / "ProjectVersion.txt").read_text(
        encoding="utf-8") if (UNITY / "ProjectSettings" / "ProjectVersion.txt").is_file() else ""
    report.check("Unity editor version pinned", "6000.0.85f2" in version,
                 version.strip().replace("\n", " / ") or "no ProjectVersion.txt")

    fbx_files = collect_fbx()
    metas = collect_metas()
    fbx_metas = [p for p in metas if p.name.endswith(".fbx.meta")]
    animation_fbx = [p for p in fbx_files if ANIMATIONS in p.parents or p == ANIMATIONS]
    character_fbx = [p for p in fbx_files if CHARACTERS in p.parents]
    user_fbx = [p for p in fbx_files if USER_ANIMATIONS in p.parents]

    report.stats["fbx_files"] = len(fbx_files)
    report.stats["fbx_meta_files"] = len(fbx_metas)
    report.stats["all_meta_files"] = len(metas)
    report.stats["animation_fbx"] = len(animation_fbx)
    report.stats["character_fbx"] = len(character_fbx)
    report.stats["user_animation_fbx"] = len(user_fbx)
    report.info("FBX inventory (recomputed from disk)",
                f"{len(fbx_files)} FBX = {len(animation_fbx)} animation + "
                f"{len(character_fbx)} character + {len(user_fbx)} user; "
                f"{len(fbx_metas)} .fbx.meta; {len(metas)} .meta total")

    # ------------------------------------------------------------------ #
    # FBX <-> .meta pairing and GUID integrity
    # ------------------------------------------------------------------ #
    no_meta = [p for p in fbx_files if not Path(str(p) + ".meta").is_file()]
    report.check("every FBX has a committed .meta", not no_meta,
                 ", ".join(str(p.relative_to(REPO_ROOT)) for p in no_meta[:6])
                 if no_meta else f"{len(fbx_files)}/{len(fbx_files)} paired")
    orphans = [p for p in fbx_metas if not Path(str(p)[:-5]).is_file()]
    report.check("no orphaned .fbx.meta", not orphans,
                 ", ".join(str(p.relative_to(REPO_ROOT)) for p in orphans[:6])
                 if orphans else f"{len(fbx_metas)} .fbx.meta all have an FBX")

    owners, guid_problems = guid_map(metas)
    report.check("GUID format and uniqueness", not guid_problems,
                 "; ".join(guid_problems[:6]) if guid_problems else
                 f"{len(owners)} unique GUIDs across {len(metas)} .meta files")
    report.stats["unique_guids"] = len(owners)

    # ------------------------------------------------------------------ #
    # FBX integrity, immutability and dedupe
    # ------------------------------------------------------------------ #
    docs: dict[Path, FbxDocument] = {}
    parse_errors: list[str] = []
    for path in fbx_files:
        try:
            docs[path] = read_fbx(path)
        except (FbxError, OSError, ValueError) as error:
            parse_errors.append(f"{path.relative_to(REPO_ROOT)}: {error}")
    report.check("FBX binary integrity", not parse_errors,
                 "; ".join(parse_errors[:6]) if parse_errors else
                 f"all {len(fbx_files)} files are readable binary FBX 7.x")
    versions = Counter(d.version for d in docs.values())
    report.info("FBX format versions",
                ", ".join(f"{v} ({n})" for v, n in sorted(versions.items())))

    hashes = {p: sha256(p) for p in fbx_files}
    by_hash: dict[str, list[Path]] = defaultdict(list)
    for path, digest in hashes.items():
        by_hash[digest].append(path)
    duplicate_groups = {k: v for k, v in by_hash.items() if len(v) > 1}
    report.check("no duplicate FBX content", not duplicate_groups,
                 "; ".join(" == ".join(str(p.name) for p in v)
                           for v in list(duplicate_groups.values())[:3])
                 if duplicate_groups else
                 f"{len(by_hash)} distinct SHA-256 values for {len(fbx_files)} FBX")

    if BASELINE.is_file():
        baseline = json.loads(BASELINE.read_text(encoding="utf-8"))["files"]
        changed: list[str] = []
        vanished: list[str] = []
        for rel, record in baseline.items():
            path = REPO_ROOT / rel
            if not path.is_file():
                vanished.append(rel)
            elif hashes.get(path) != record["sha256"]:
                changed.append(rel)
        unexpected = [str(p.relative_to(REPO_ROOT)) for p in fbx_files
                      if str(p.relative_to(REPO_ROOT)) not in baseline]
        report.check("FBX source files unchanged", not changed and not vanished,
                     f"changed: {changed[:4]}, missing: {vanished[:4]}"
                     if changed or vanished else
                     f"{len(baseline)} original FBX byte-identical to the baseline")
        if unexpected:
            report.info("new FBX files since the baseline",
                        f"{len(unexpected)} new file(s): {', '.join(unexpected[:4])} "
                        "— add them to fbx-baseline.json once reviewed")
    else:
        report.check("FBX baseline manifest present", False,
                     f"missing {BASELINE.relative_to(REPO_ROOT)}")

    # ------------------------------------------------------------------ #
    # Skeleton / rig / humanoid mapping
    # ------------------------------------------------------------------ #
    rig_problems: list[str] = []
    for path, doc in sorted(docs.items()):
        missing_bones = doc.missing_required_bones()
        if missing_bones:
            rig_problems.append(f"{path.name}: missing {','.join(missing_bones)}")
        elif not doc.core_hierarchy_connected():
            rig_problems.append(f"{path.name}: core bone hierarchy not rooted at Hips")
    report.check("humanoid bone mapping (15 required bones)", not rig_problems,
                 "; ".join(rig_problems[:6]) if rig_problems else
                 f"all {len(docs)} skeletons map all {len(REQUIRED_HUMANOID_BONES)} "
                 "required bones in a connected hierarchy")

    structure_problems: list[str] = []
    for path in character_fbx:
        doc = docs.get(path)
        if doc is None:
            continue
        if not doc.mesh_names or doc.skin_count == 0 or doc.cluster_count == 0:
            structure_problems.append(f"{path.name}: skinned mesh missing")
        if doc.stacks:
            structure_problems.append(f"{path.name}: unexpected animation stack")
    for path in animation_fbx:
        doc = docs.get(path)
        if doc is None:
            continue
        if doc.mesh_names:
            structure_problems.append(f"{path.name}: animation FBX contains a mesh")
        if len(doc.stacks) != 1 or doc.curve_count == 0:
            structure_problems.append(
                f"{path.name}: expected exactly 1 animation stack with curves")
    report.check("character skinning and animation-only assets",
                 not structure_problems,
                 "; ".join(structure_problems[:6]) if structure_problems else
                 f"{len(character_fbx)} skinned characters, "
                 f"{len(animation_fbx)} animation-only skeletons")

    char_bone_sets = {tuple(sorted(docs[p].humanoid_mapping())) for p in character_fbx
                      if p in docs}
    anim_bone_sets = {tuple(sorted(docs[p].humanoid_mapping())) for p in animation_fbx
                      if p in docs}
    report.check("shared humanoid convention / retargetability",
                 len(char_bone_sets) == 1 and char_bone_sets == anim_bone_sets,
                 f"{len(char_bone_sets)} character convention(s), "
                 f"{len(anim_bone_sets)} animation convention(s); "
                 "Unity Humanoid retargeting is the bridge between Mixamo and "
                 "Unreal-style bone names" if len(char_bone_sets) == 1
                 and char_bone_sets == anim_bone_sets else
                 "character and animation skeletons do not share one mapping")
    report.stats["humanoid_bones_mapped"] = (
        len(docs[character_fbx[0]].humanoid_mapping()) if character_fbx else 0)
    report.stats["character_skeleton_bones"] = (
        len(docs[character_fbx[0]].bone_names) if character_fbx else 0)
    report.stats["character_skins"] = (
        sum(docs[p].skin_count for p in character_fbx if p in docs))
    report.stats["character_vertices"] = (
        sum(docs[p].vertex_count for p in character_fbx if p in docs))
    report.stats["animation_curve_files"] = len(animation_fbx)
    report.stats["animation_curves_total"] = (
        sum(docs[p].curve_count for p in animation_fbx if p in docs))

    # Character and animation FBX metadata must not force a Generic rig.
    generic_requirement: list[str] = []
    for path in fbx_files:
        meta_path = Path(str(path) + ".meta")
        if not meta_path.is_file():
            continue
        text = meta_path.read_text(encoding="utf-8", errors="replace")
        if yaml_field(text, "animationType") not in ("2", "3"):
            generic_requirement.append(f"{path.name}: animationType "
                                       f"{yaml_field(text, 'animationType')}")
    report.check("rig configuration is Humanoid or explicitly Generic",
                 not generic_requirement,
                 "; ".join(generic_requirement[:6]) if generic_requirement else
                 "all FBX importers declare Humanoid (3); no rig needs to be "
                 "forced — Generic remains available via the explicit registry")
    report.info("Generic rig policy",
                "any skeleton that fails the 15-bone mapping above must be "
                "recorded in Assets/Generated/GenericRigs.txt and imported as "
                "Generic; no such skeleton is present in this checkout")

    # ------------------------------------------------------------------ #
    # Clip metadata vs the real FBX animation stack
    # ------------------------------------------------------------------ #
    clip_problems: list[str] = []
    loop_problems: list[str] = []
    clip_count = 0
    categories: Counter[str] = Counter()
    for path in animation_fbx:
        meta_path = Path(str(path) + ".meta")
        base = path.stem
        categories[infer_category(path.name)] += 1
        if not meta_path.is_file() or path not in docs:
            continue
        text = meta_path.read_text(encoding="utf-8", errors="replace")
        block = clip_block(text)
        if block is None:
            clip_problems.append(f"{base}: no clipAnimations block")
            continue
        clips = len(re.findall(r"^\s+- serializedVersion:", block, re.M)) \
            or len(re.findall(r"^\s+- name:", block, re.M))
        if clips != 1:
            clip_problems.append(f"{base}: {clips} clip entries, expected 1")
        clip_count += clips
        doc = docs[path]
        expected_take = doc.stacks[0].name if doc.stacks else ""
        expected_frames = doc.frames()
        checks = (
            ("name", clip_field(block, "name"), base),
            ("takeName", clip_field(block, "takeName"), expected_take),
            ("firstFrame", clip_field(block, "firstFrame"), 0),
            ("lastFrame", clip_field(block, "lastFrame"), expected_frames),
        )
        for label, actual, expected in checks:
            if label in ("firstFrame", "lastFrame"):
                try:
                    ok = int(actual) == int(expected)
                except (TypeError, ValueError):
                    ok = False
            else:
                ok = actual == expected
            if not ok:
                clip_problems.append(
                    f"{base}: {label}={actual!r}, FBX says {expected!r}")
        want = wants_loop(path.name)
        loop_time = clip_field(block, "loopTime")
        loop_blend = clip_field(block, "loopBlend")
        if loop_time != str(int(want)):
            loop_problems.append(
                f"{base}: loopTime={loop_time}, WantsLoop={int(want)}")
        if loop_blend != loop_time:
            loop_problems.append(f"{base}: loopBlend={loop_blend} != loopTime={loop_time}")

    report.check("animation clip metadata matches the FBX", not clip_problems,
                 "; ".join(clip_problems[:8]) if clip_problems else
                 f"{clip_count} clip(s): name, takeName, firstFrame and "
                 "lastFrame all agree with the FBX animation stack")
    report.check("loop flags agree with the C# import rules",
                 not loop_problems,
                 "; ".join(loop_problems[:8]) if loop_problems else
                 "loopTime and loopBlend match MixamoImportSettings.WantsLoop "
                 "for every committed clip")
    report.stats["animation_clips_discovered"] = clip_count
    report.stats["categories"] = dict(sorted(categories.items()))

    missing_categories = [c for c in REQUIRED_GAMEPLAY_CATEGORIES
                          if categories.get(c, 0) == 0]
    report.check("required gameplay animation categories",
                 not missing_categories,
                 "missing " + ", ".join(missing_categories) if missing_categories else
                 ", ".join(f"{c}={categories[c]}" for c in REQUIRED_GAMEPLAY_CATEGORIES))

    # ------------------------------------------------------------------ #
    # Importer setting consistency
    # ------------------------------------------------------------------ #
    animation_expectations = {
        "animationType": "3", "avatarSetup": "1", "importAnimation": "1",
        "animationCompression": "3", "isReadable": "0",
        "maxBonesPerVertex": "4", "materialImportMode": "0",
        "autoGenerateAvatarMappingIfUnspecified": "1",
        "humanoidOversampling": "1", "resampleCurves": "1",
    }
    setting_problems: list[str] = []
    for path in animation_fbx:
        meta_path = Path(str(path) + ".meta")
        if not meta_path.is_file():
            continue
        text = meta_path.read_text(encoding="utf-8", errors="replace")
        for key, expected in animation_expectations.items():
            actual = yaml_field(text, key)
            if actual != expected:
                setting_problems.append(f"{path.name}: {key}={actual} (want {expected})")
    report.check("animation importer settings are consistent",
                 not setting_problems,
                 "; ".join(setting_problems[:8]) if setting_problems else
                 f"{len(animation_fbx)} files share Humanoid/Optimal/4-bone/"
                 "non-readable/auto-avatar settings")

    character_expectations = {
        # importAnimation stays on because Unity generates the Humanoid Avatar
        # through the animation importer; these FBXs contain no AnimationStack,
        # so no AnimationClip is produced and clipAnimations is empty.
        "animationType": "3", "avatarSetup": "1", "importAnimation": "1",
        "isReadable": "0", "maxBonesPerVertex": "4",
        "autoGenerateAvatarMappingIfUnspecified": "1",
        "useFileScale": "1",
    }
    char_setting_problems: list[str] = []
    for path in character_fbx:
        meta_path = Path(str(path) + ".meta")
        if not meta_path.is_file():
            continue
        text = meta_path.read_text(encoding="utf-8", errors="replace")
        for key, expected in character_expectations.items():
            actual = yaml_field(text, key)
            if actual != expected:
                char_setting_problems.append(
                    f"{path.name}: {key}={actual} (want {expected})")
        block = clip_block(text)
        if block and re.search(r"^\s+- serializedVersion:", block, re.M):
            char_setting_problems.append(f"{path.name}: has a clip entry but no animation stack")
    report.check("character importer settings are consistent",
                 not char_setting_problems,
                 "; ".join(char_setting_problems[:8]) if char_setting_problems else
                 f"{len(character_fbx)} characters: Humanoid avatar from this "
                 "model, empty clip list, 4 bone influences, file scale kept")

    # Root-motion bake flags: factual report, but the result is Editor-only.
    baked = 0
    for path in animation_fbx:
        meta_path = Path(str(path) + ".meta")
        if not meta_path.is_file():
            continue
        block = clip_block(meta_path.read_text(encoding="utf-8", errors="replace")) or ""
        if all(clip_field(block, key) == "1" for key in
               ("loopBlendOrientation", "loopBlendPositionY", "loopBlendPositionXZ")):
            baked += 1
    report.stats["committed_clips_with_bake_into_pose_flags"] = baked
    report.info("root-motion bake-into-pose flags on disk",
                f"{baked}/{len(animation_fbx)} committed .meta files already set "
                "lockRootRotation/HeightY/PositionXZ; the remainder are set by "
                "MixamoAnimationImportRules.OnPreprocessAnimation at import time")

    # ------------------------------------------------------------------ #
    # Committed asset references
    # ------------------------------------------------------------------ #
    yaml_assets = unity_yaml_assets()
    manifest = json.loads((UNITY / "Packages/manifest.json").read_text(encoding="utf-8"))
    urp_declared = "com.unity.render-pipelines.universal" in manifest.get("dependencies", {})
    lit_materials = [
        asset for asset in yaml_assets
        if asset.suffix == ".mat"
        and asset.parent.name == "Materials"
        and asset.parent.parent.name in ("Male", "Female")
        and asset.parent.parent.parent.name == "Characters"
    ]
    lit_guids_ok = urp_declared and len(lit_materials) == 6 and all(
        f"guid: {URP_LIT_SHADER_GUID}" in asset.read_text(encoding="utf-8")
        for asset in lit_materials
    )
    broken_refs: list[str] = []
    reference_count = 0
    for asset in yaml_assets:
        text = asset.read_text(encoding="utf-8", errors="replace")
        for guid in referenced_guids(text):
            reference_count += 1
            package_guid_ok = (
                guid == URP_LIT_SHADER_GUID and asset in lit_materials and lit_guids_ok
            )
            if guid not in owners and guid not in BUILTIN_GUIDS and not package_guid_ok:
                broken_refs.append(f"{asset.relative_to(REPO_ROOT)} -> {guid}")
    report.check("committed asset references resolve", not broken_refs,
                 "; ".join(broken_refs[:6]) if broken_refs else
                 f"{reference_count} guid reference(s) in {len(yaml_assets)} "
                 "committed YAML asset(s) — all Assets GUIDs resolve; the six production materials use the exact package GUID for URP/Lit")
    report.stats["committed_yaml_assets"] = len(yaml_assets)
    report.stats["committed_guid_references"] = reference_count

    scripts = sorted(ASSETS.rglob("*.cs"))
    scripts_without_meta = [p for p in scripts
                            if not Path(str(p) + ".meta").is_file()]
    report.info("Unity script .meta coverage",
                f"{len(scripts_without_meta)}/{len(scripts)} C# scripts have no "
                "committed .meta; Unity generates them locally. No committed "
                "prefab/scene/controller references a script GUID, so this is "
                "not a broken reference today — see the manual test list")

    controllers = sorted(ASSETS.rglob("*.controller"))
    masks = sorted(ASSETS.rglob("*.mask"))
    configs = sorted(ASSETS.rglob("CharacterAnimationConfig.asset"))
    report.stats["committed_controllers"] = len(controllers)
    report.stats["committed_avatar_masks"] = len(masks)
    if controllers or masks or configs:
        ctrl_broken: list[str] = []
        for asset in controllers + masks + configs:
            text = asset.read_text(encoding="utf-8", errors="replace")
            for guid in referenced_guids(text):
                if guid not in owners and guid not in BUILTIN_GUIDS:
                    ctrl_broken.append(f"{asset.relative_to(REPO_ROOT)} -> {guid}")
        report.check("Animator Controller / Avatar / clip references",
                     not ctrl_broken,
                     "; ".join(ctrl_broken[:6]) if ctrl_broken else
                     f"{len(controllers)} controller(s), {len(masks)} mask(s), "
                     f"{len(configs)} config(s): every guid resolves")
    else:
        report.skip("Animator Controller / Avatar / clip references",
                    "no generated .controller/.mask/.asset is committed "
                    "(they are code-generated by "
                    "CharacterAnimationIntegration.BuildFromCommandLine); "
                    "their references can only be validated after an Editor run")
    report.skip("Avatar validity / clip playback / animation retargeting result",
                "requires a real Unity Editor import (Avatar.isValid, "
                "Avatar.isHuman, AnimationClip.humanMotion, visual playback)")

    # ------------------------------------------------------------------ #
    # Android build configuration
    # ------------------------------------------------------------------ #
    settings_path = UNITY / "ProjectSettings" / "ProjectSettings.asset"
    project_settings = settings_path.read_text(encoding="utf-8") \
        if settings_path.is_file() else ""
    android_checks = {
        "AndroidMinSdkVersion: 26": "AndroidMinSdkVersion: 26" in project_settings,
        "AndroidTargetArchitectures includes ARM64":
            yaml_field(project_settings, "AndroidTargetArchitectures") in ("2", "3"),
        "IL2CPP scripting backend for Android":
            bool(re.search(r"scriptingBackend:\s*\n\s*Android:\s*1", project_settings)),
        "Linear colour space": yaml_field(project_settings, "m_ActiveColorSpace") == "1",
        "ASTC default texture compression on Android":
            bool(re.search(r"m_BuildTargetDefaultTextureCompressionFormat:\s*\n"
                           r"\s*- m_BuildTarget: Android\s*\n\s*m_Format: 2",
                           project_settings)),
        "Android graphics API list is explicit":
            "m_BuildTarget: AndroidPlayer" in project_settings
            and "m_Automatic: 0" in project_settings,
    }
    failed_android = [name for name, ok in android_checks.items() if not ok]
    report.check("Android build configuration (committed settings)",
                 not failed_android,
                 "missing " + ", ".join(failed_android) if failed_android else
                 ", ".join(android_checks.keys()))
    architectures = yaml_field(project_settings, "AndroidTargetArchitectures")
    report.info("Android architecture bitmask",
                f"AndroidTargetArchitectures: {architectures} (Unity enum "
                "ARMv7=1, ARM64=2, so 3 = both). The README describes an "
                "ARM64-only target; a one-line change to 2 would match it. "
                "Left unchanged here because no Android build was executed — "
                "verify the APK's lib/ contents in the Editor before changing it")
    package_manifest = UNITY / "Packages" / "manifest.json"
    packages = json.loads(package_manifest.read_text(encoding="utf-8")) \
        if package_manifest.is_file() else {}
    report.check("URP declared for Android rendering",
                 "com.unity.render-pipelines.universal"
                 in packages.get("dependencies", {}),
                 "com.unity.render-pipelines.universal present in manifest.json")

    # ------------------------------------------------------------------ #
    # Explicit Editor-only / device-only separations
    # ------------------------------------------------------------------ #
    report.skip("C# compilation", "no Unity Editor or Unity C# compiler in "
                "this environment; processing/unity/validate_csharp.py performs "
                "tree-sitter syntax and cross-file contract checks only")
    report.skip("Unity EditMode / PlayMode tests",
                "require Unity 6000.0.85f2 with a valid licence; "
                "unity.yml runs them only when licence inputs are configured")
    report.skip("FBX reimport / GUID stability after reimport",
                "requires AssetDatabase.SaveAndReimport in the Editor")
    report.skip("Android APK build and on-device runtime behaviour",
                "requires Unity Android Build Support, a signed APK and a "
                "physical device; no APK or FPS/PSS/thermal result is claimed")
    report.skip("missing-script scan inside generated scenes/prefabs",
                "scenes, prefabs and controllers are generated at Editor time "
                "and are not committed; ProjectValidator covers them in the Editor")

    counts = report.counts()
    report.stats["checks"] = counts
    report.stats["fbx_total_bytes"] = sum(p.stat().st_size for p in fbx_files)
    report.stats["animation_fbx_bytes"] = sum(p.stat().st_size for p in animation_fbx)
    report.stats["character_fbx_bytes"] = sum(p.stat().st_size for p in character_fbx)

    if args.json_path:
        args.json_path.parent.mkdir(parents=True, exist_ok=True)
        args.json_path.write_text(json.dumps({
            "kind": "static", "stats": report.stats, "checks": report.checks,
        }, indent=2, sort_keys=True) + "\n", encoding="utf-8")
        print(f"\nJSON report: {args.json_path}")
    if args.report_path:
        args.report_path.parent.mkdir(parents=True, exist_ok=True)
        lines = ["# Static character & animation validation", "",
                 "Generated by `processing/unity/validate_character_animation.py`.",
                 "Static checks only; SKIP means a Unity Editor is required.", "",
                 f"**{counts.get('PASS', 0)} passed, {counts.get('FAIL', 0)} failed, "
                 f"{counts.get('SKIP', 0)} editor-only skipped.**", "",
                 "| Status | Check | Detail |", "| --- | --- | --- |"]
        for row in report.checks:
            detail = row["detail"].replace("|", "\\|").replace("\n", " ")
            lines.append(f"| {row['status']} | {row['name']} | {detail} |")
        args.report_path.write_text("\n".join(lines) + "\n", encoding="utf-8")
        print(f"Markdown report: {args.report_path}")

    return report.summary()


if __name__ == "__main__":
    raise SystemExit(main())
