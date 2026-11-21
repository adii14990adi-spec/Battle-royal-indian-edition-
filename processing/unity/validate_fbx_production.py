#!/usr/bin/env python3
"""Static validation for the production Male / Female FBX character system.

    python3 processing/unity/validate_fbx_production.py \
        --json docs/fbx_production_verification/static_validation.json \
        --report docs/fbx_production_verification/STATIC_VALIDATION.md

This validator is deliberately honest about its boundary: it reads committed
FBX bytes, FBX metadata, C# source, ProjectSettings and the isolated GLTF
fallback manifests. It does NOT open Unity, import an FBX, build an Avatar,
render a frame, build an APK or test a phone.

History: this validator used to prove the 74 imported Mixamo animation clips,
their Humanoid import metadata and the generated Animator Controllers. All
animation FBX clips were removed from the project on 2026-11-20 by owner
decision ("remove all animations"), so the clip gates were replaced by a
negative gate that proves `unity-project/Assets/Animations` stays free of FBX
clips, and the licensed-Unity FBX verifier/capture tools were retired with
them.

A `NOT VERIFIED` row is never counted as a pass. A `FAIL` row makes the process
exit non-zero; known source defects are reported, not hidden.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
from pathlib import Path
from typing import Any

REPO_ROOT = Path(__file__).resolve().parents[2]
PROCESSING = REPO_ROOT / "processing" / "unity"
UNITY_ROOT = REPO_ROOT / "unity-project"
ASSETS_ROOT = UNITY_ROOT / "Assets"
PROJECT_SETTINGS = UNITY_ROOT / "ProjectSettings" / "ProjectSettings.asset"
FBX_BASELINE = PROCESSING / "fbx-baseline.json"
PROTECTED_MANIFEST = PROCESSING / "gltf_import_test_protected_assets.json"
TEXTURE_MANIFEST = PROCESSING / "fbx_production_textures.json"
MATERIAL_MANIFEST = PROCESSING / "fbx_production_materials.json"
GITATTRIBUTES = REPO_ROOT / ".gitattributes"
GLTF_SOURCE_MANIFEST = PROCESSING / "gltf_import_test_source_manifest.json"
FEMALE_NORMAL_SHA = "cf922460b43ccd31e983e34db05514c9d451dd2f9cdd01a843978e797719f859"
LFS_EXPECTED = {
    "unity-project/Assets/Resources/Art/Textures/water_normal_256.png":
        ("e1067e8199f050f9d4e912c1af189a72a6f2dc0d7d89372ec0d074ef4b4ca4a7", 137293),
    "unity-project/Assets/Resources/UI/battle_horizons_loading_bar.png":
        ("28e6e78493e9d018e596012a4647bfa595b01e4b333ed1ee0ce0d75b26b2605f", 3242),
    "unity-project/Assets/Resources/UI/battle_horizons_loading_spinner.png":
        ("545cc7fea9b72aa2e90a52cb5bd906b85875c266bc94069db661c16d594e2e15", 6881),
    "unity-project/Assets/Resources/UI/battle_horizons_logo.png":
        ("b8c062fdb0a29b6e66297d2d1c4f7004da7ccbf291e6bc109d951ad34a92a8ce", 312823),
    "unity-project/Assets/UI/Icons/battle_horizons_icon.png":
        ("5c3e6ef0224050b3c1a460c653edbce4f6e518ef9bdb92ccb18515f0e0838b1f", 205254),
}
BASE_ALLOWLIST = {
    "unity-project/Assets/Tests/EditMode/BattleHorizons.EditModeTests.asmdef",
    "unity-project/Assets/Tests/PlayMode/BattleHorizons.PlayModeTests.asmdef",
}

sys.path.insert(0, str(PROCESSING))
from fbx_inspect import read_fbx, as_text  # noqa: E402
import validate_gltf_import_test as gltf_validator  # noqa: E402

CHARACTERS = {
    "Male": {
        "fbx": ASSETS_ROOT / "Characters" / "Male" / "Male character.fbx",
        "guid": "c1867a8de9f4b1f1b76e69e097d73103",
    },
    "Female": {
        "fbx": ASSETS_ROOT / "Characters" / "Female" / "Female character.fbx",
        "guid": "103ec3786913589cd676c3534c5074e7",
    },
}

ANIMATIONS_ROOT = ASSETS_ROOT / "Animations"

CHARACTER_META_EXPECTED = {
    "animationType": "3",                         # Humanoid
    "avatarSetup": "1",                           # Create From This Model
    "autoGenerateAvatarMappingIfUnspecified": "1",
    "isReadable": "0",
    "maxBonesPerVertex": "4",
    "materialImportMode": "1",                    # ImportStandard
    "materialName": "1",                          # BasedOnMaterialName
    "materialSearch": "0",                        # Local
    "materialLocation": "1",                      # InPrefab / embedded source material names
    "animationCompression": "3",                  # Optimal
    "importAnimation": "1",
}


class Report:
    def __init__(self) -> None:
        self.rows: list[dict[str, str]] = []

    def add(self, name: str, status: str, detail: str) -> None:
        self.rows.append({"check": name, "status": status, "detail": detail})

    def check(self, name: str, ok: bool, good: str, bad: str) -> None:
        self.add(name, "PASS" if ok else "FAIL", good if ok else bad)

    def not_verified(self, name: str, detail: str) -> None:
        self.add(name, "NOT VERIFIED", detail)

    def info(self, name: str, detail: str) -> None:
        self.add(name, "INFO", detail)

    @property
    def failures(self) -> list[dict[str, str]]:
        return [row for row in self.rows if row["status"] == "FAIL"]

    @property
    def passes(self) -> int:
        return sum(1 for row in self.rows if row["status"] == "PASS")

    @property
    def not_verified_count(self) -> int:
        return sum(1 for row in self.rows if row["status"] == "NOT VERIFIED")

    def markdown(self) -> str:
        lines = [
            "# Static validation — production FBX characters",
            "",
            "Static only: this validator reads committed bytes and source. It does not claim Unity import, Avatar validity, animation playback, rendering, APK output or device behaviour.",
            "",
            "| Check | Status | Detail |",
            "| --- | --- | --- |",
        ]
        for row in self.rows:
            detail = str(row["detail"]).replace("|", "\\|").replace("\r", " ").replace("\n", " ")
            lines.append(f"| {row['check']} | {row['status']} | {detail} |")
        lines += [
            "",
            "## Summary",
            "",
            f"- PASS: {self.passes}",
            f"- FAIL: {len(self.failures)}",
            f"- NOT VERIFIED: {self.not_verified_count}",
            "",
            "Licensed Unity Editor evidence is written to `EDITOR_VALIDATION.md`; software-GL visual evidence, when captured, is written to `CAPTURE_RESULT.md` and the `screenshots/` directory.",
        ]
        return "\n".join(lines) + "\n"

    def json(self) -> dict[str, Any]:
        return {
            "summary": {
                "pass": self.passes,
                "fail": len(self.failures),
                "not_verified": self.not_verified_count,
            },
            "rows": self.rows,
        }


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def load_json(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8"))


def display(path: Path) -> str:
    try:
        return path.relative_to(REPO_ROOT).as_posix()
    except ValueError:
        return str(path)


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def basename(path: str) -> str:
    return re.split(r"[\\/]", path)[-1]


def lfs_pointer(data: bytes) -> tuple[str, int] | None:
    if not data.startswith(b"version https://git-lfs.github.com/spec/v1"):
        return None
    match = re.search(rb"oid sha256:([0-9a-f]{64})\s+size (\d+)", data)
    return None if match is None else (match.group(1).decode("ascii"), int(match.group(2)))


def has_lfs_rule(relative: str) -> bool:
    if not GITATTRIBUTES.is_file():
        return False
    needle = f'"{relative}"'
    return any(needle in line and "filter=lfs" in line and "diff=lfs" in line
               and "merge=lfs" in line
               for line in GITATTRIBUTES.read_text(encoding="utf-8").splitlines())


def material_remaps(text: str) -> dict[str, str]:
    return {
        name: guid
        for name, guid in re.findall(
            r"name:\s*(\S+)\s*\n\s*second:\s*\{fileID:\s*2100000,\s*guid:\s*([0-9a-f]{32}),\s*type:\s*2\}",
            text,
        )
    }


def fbx_video_bytes(doc: Any) -> dict[str, bytes]:
    """Exact Video Content bytes keyed by FBX texture filename."""
    result: dict[str, bytes] = {}
    for obj in doc.objects:
        if obj.name != "Video":
            continue
        filename = ""
        content = b""
        for child in obj.children:
            if child.name in ("FileName", "RelativeFilename") and child.properties:
                filename = as_text(child.properties[0])
            if child.name == "Content" and child.properties and isinstance(child.properties[0], bytes):
                content = child.properties[0]
        if filename:
            normalized = filename.replace("\\", "/")
            result[normalized] = content
            result[basename(normalized)] = content
    return result


def current_identity(path: Path) -> tuple[int, str]:
    data = path.read_bytes()
    return len(data), sha256_bytes(data)


def meta_value(text: str, key: str) -> str | None:
    match = re.search(r"^\s*" + re.escape(key) + r":\s*(.*)$", text, re.M)
    return match.group(1).strip() if match else None


def check_meta_settings(report: Report, name: str, meta_path: Path, expected: dict[str, str]) -> None:
    if not meta_path.is_file():
        report.check(name, False, "", f"missing {display(meta_path)}")
        return
    text = meta_path.read_text(encoding="utf-8")
    mismatches: list[str] = []
    for key, expected_value in expected.items():
        actual = meta_value(text, key)
        if actual != expected_value:
            mismatches.append(f"{key}={actual!r}, expected {expected_value!r}")
    report.check(
        name,
        not mismatches,
        f"{len(expected)} importer settings match the committed Humanoid policy in {display(meta_path)}",
        "; ".join(mismatches),
    )


def check_protection(report: Report) -> None:
    baseline = load_json(FBX_BASELINE)
    fbx_files = baseline.get("files", {})
    problems: list[str] = []
    for rel, record in fbx_files.items():
        path = REPO_ROOT / rel
        if not path.is_file():
            problems.append(f"missing {rel}")
            continue
        if path.stat().st_size != record["bytes"] or sha256(path) != record["sha256"]:
            problems.append(f"changed {rel}")
    report.check(
        f"all {len(fbx_files)} production FBX files immutable",
        not problems and len(fbx_files) == 2,
        f"{len(fbx_files)}/{len(fbx_files)} committed production FBX files (the Male and Female character models) match processing/unity/fbx-baseline.json byte-for-byte",
        "; ".join(problems[:8]) or f"unexpected baseline size {len(fbx_files)} (expected exactly 2 character FBX files)",
    )

    manifest = load_json(PROTECTED_MANIFEST)
    protected_by_path = {entry["path"]: entry for entry in manifest.get("files", [])}
    allowlisted = set(manifest.get("allowlisted_modified_paths", []))
    protected_problems: list[str] = []
    for entry in manifest.get("files", []):
        rel = entry["path"]
        if rel in allowlisted:
            continue
        path = REPO_ROOT / rel
        if not path.is_file():
            protected_problems.append(f"missing {rel}")
            continue
        if path.stat().st_size != entry["size"] or sha256(path) != entry["sha256"]:
            protected_problems.append(f"changed {rel}")
    checked = len(manifest.get("files", [])) - len(allowlisted)
    report.check(
        "all other production Unity assets and FBX metadata unchanged",
        not protected_problems,
        f"{checked} non-allowlisted protected Assets/ProjectSettings files are byte-identical; FBX binaries and both character GUIDs are unchanged",
        "; ".join(protected_problems[:8]),
    )

    unexpected_allowlist = sorted(allowlisted - BASE_ALLOWLIST)
    report.check(
        "protected manifest allowlist is minimal",
        not unexpected_allowlist,
        f"{len(allowlisted)} allowlisted path(s): only the two test asmdefs are exempt from the byte-identity gate",
        "unexpected allowlisted paths: " + ", ".join(unexpected_allowlist[:6]),
    )

    animations_fbx = sorted(ANIMATIONS_ROOT.rglob("*.fbx")) if ANIMATIONS_ROOT.is_dir() else []
    report.check(
        "animation FBX clips removed",
        not animations_fbx,
        f"no FBX animation clip remains under {display(ANIMATIONS_ROOT)}",
        f"{len(animations_fbx)} animation FBX still present: "
        + ", ".join(display(f) for f in animations_fbx[:4]),
    )

    gltf_report = gltf_validator.run_validation()
    report.check(
        "GLTF fallback files and test scene preserved",
        not gltf_report.failures,
        f"isolated GLTF validator: {gltf_report.passes} PASS / {len(gltf_report.failures)} FAIL / {gltf_report.not_verified_count} NOT VERIFIED; original GLTF/BIN/PNG files and test scene are present",
        "; ".join(row["detail"] for row in gltf_report.failures[:4]),
    )


def check_lfs_rules(report: Report) -> None:
    problems: list[str] = []
    states: list[str] = []
    for rel, (oid, size) in LFS_EXPECTED.items():
        path = REPO_ROOT / rel
        if not path.is_file():
            problems.append(f"missing {rel}")
            continue
        data = path.read_bytes()
        pointer = lfs_pointer(data)
        if pointer is not None:
            states.append(rel.rsplit("/", 1)[-1] + "=pointer")
            if pointer != (oid, size):
                problems.append(f"{rel} pointer oid/size mismatch")
        else:
            states.append(rel.rsplit("/", 1)[-1] + "=smudged")
            if len(data) != size or sha256_bytes(data) != oid:
                problems.append(f"{rel} smudged SHA-256/size mismatch")
        if not has_lfs_rule(rel):
            problems.append(f"{rel} has no LFS checkout rule")
    report.check(
        "Git LFS object identity and checkout smudge rules",
        not problems,
        "5/5 committed PNG blobs match their LFS oid/size and declare filter/diff/merge=lfs; CI checkout lfs:true smudges the exact objects (" + ", ".join(states) + ")",
        "; ".join(problems[:8]),
    )


def check_production_texture_manifest(report: Report) -> None:
    manifest = load_json(TEXTURE_MANIFEST)
    entries = manifest.get("textures", [])
    videos = {sex: fbx_video_bytes(read_fbx(info["fbx"])) for sex, info in CHARACTERS.items()}
    source_manifest = load_json(GLTF_SOURCE_MANIFEST)
    gltf_normal_source = next((entry for entry in source_manifest.get("files", [])
                               if entry.get("path") == "Female/T_Superhero_Female_Normal.png"), None)
    problems: list[str] = []
    if len(entries) != 14:
        problems.append(f"texture_count={len(entries)}, expected 14")
    for entry in entries:
        path = REPO_ROOT / entry["path"]
        if not path.is_file():
            problems.append(f"missing {entry['path']}")
            continue
        data = path.read_bytes()
        if len(data) != entry["size"] or sha256_bytes(data) != entry["sha256"]:
            problems.append(f"changed {entry['path']}")
        meta = Path(str(path) + ".meta")
        if not meta.is_file():
            problems.append(f"missing {display(meta)}")
            continue
        meta_text = meta.read_text(encoding="utf-8")
        if meta_value(meta_text, "guid") != entry["guid"]:
            problems.append(f"GUID mismatch {display(meta)}")
        expected_type = "1" if entry["texture_type"] == "NormalMap" else "0"
        if meta_value(meta_text, "textureType") != expected_type:
            problems.append(f"textureType mismatch {display(meta)}")
        if entry["texture_type"] == "NormalMap":
            if meta_value(meta_text, "sRGBTexture") != "0" or meta_value(meta_text, "convertToNormalMap") != "0":
                problems.append(f"normal-map settings mismatch {display(meta)}")
        if entry["source"] == "fbx_embedded":
            video = videos.get(entry["sex"], {}).get(basename(entry["fbx_filename"]))
            if video is None or sha256_bytes(video) != entry["sha256"]:
                problems.append(f"embedded bytes mismatch {entry['path']}")
        elif entry["source"] == "existing_gltf_fallback":
            if entry["sex"] != "Female" or basename(entry["path"]) != "T_Superhero_Female_Normal.png":
                problems.append(f"unexpected external source {entry['path']}")
            if entry["sha256"] != FEMALE_NORMAL_SHA:
                problems.append("Female normal is not the exact existing texture")
            source = REPO_ROOT / entry["source_path"]
            if not source.is_file() or sha256(source) != FEMALE_NORMAL_SHA:
                problems.append("GLTF fallback normal source was modified")
            if gltf_normal_source is None or gltf_normal_source.get("sha256") != FEMALE_NORMAL_SHA:
                problems.append("GLTF source manifest no longer records the exact normal map")
        else:
            problems.append(f"unexpected source_kind {entry['source']}")
    report.check(
        "14 exact production character textures and importer settings",
        not problems,
        "7 Male + 7 Female PNGs are byte-identical to FBX Video Content or, for the one unresolved Female normal, the unchanged existing GLTF fallback; all normal maps are TextureImporterType.NormalMap with linear/no-convert settings",
        "; ".join(problems[:8]),
    )


def check_production_material_manifest(report: Report) -> None:
    materials = load_json(MATERIAL_MANIFEST).get("materials", [])
    textures = {entry["path"]: entry for entry in load_json(TEXTURE_MANIFEST).get("textures", [])}
    problems: list[str] = []
    if len(materials) != 6:
        problems.append(f"material_count={len(materials)}, expected 6")
    for entry in materials:
        path = REPO_ROOT / entry["path"]
        meta = Path(str(path) + ".meta")
        if not path.is_file() or not meta.is_file():
            problems.append(f"missing {entry['path']} or meta")
            continue
        mat_text = path.read_text(encoding="utf-8")
        if f'm_Name: {entry["name"]}' not in mat_text:
            problems.append(f"material name mismatch {entry['path']}")
        if entry["shader_guid"] not in mat_text:
            problems.append(f"URP/Lit shader GUID missing {entry['path']}")
        for key in ("base_texture_guid", "normal_texture_guid"):
            if entry[key] not in mat_text:
                problems.append(f"{key} missing {entry['path']}")
        for key in ("base_texture", "normal_texture"):
            texture = textures.get(entry[key])
            guid_key = "base_texture_guid" if key == "base_texture" else "normal_texture_guid"
            if texture is None or texture["guid"] != entry[guid_key]:
                problems.append(f"{key} manifest mismatch {entry['path']}")
        if meta_value(meta.read_text(encoding="utf-8"), "guid") != entry["guid"]:
            problems.append(f"material GUID mismatch {entry['path']}")
    for sex, info in CHARACTERS.items():
        meta = Path(str(info["fbx"]) + ".meta")
        remaps = material_remaps(meta.read_text(encoding="utf-8"))
        expected = {entry["name"]: entry["guid"] for entry in materials if entry["sex"] == sex}
        if remaps != expected:
            problems.append(f"{sex} .fbx.meta externalObjects remaps do not match the material manifest")
    report.check(
        "6 deterministic URP/Lit material remaps",
        not problems,
        "3 Male + 3 Female MI_* FBX material names remap by exact GUID to committed URP/Lit materials with exact base/normal texture GUIDs",
        "; ".join(problems[:8]),
    )


def check_character_fbx(report: Report, sex: str, info: dict[str, Any]) -> None:
    fbx = info["fbx"]
    meta = Path(str(fbx) + ".meta")
    label = f"{sex} production FBX"
    if not fbx.is_file() or not meta.is_file():
        report.check(label, False, "", f"missing {display(fbx)} or its .meta")
        return

    meta_text = meta.read_text(encoding="utf-8")
    guid = meta_value(meta_text, "guid")
    report.check(
        f"{sex} FBX GUID unchanged",
        guid == info["guid"],
        f"{display(meta)} has stable GUID {info['guid']}",
        f"GUID is {guid!r}, expected {info['guid']!r}",
    )
    check_meta_settings(report, f"{sex} Unity importer settings", meta, CHARACTER_META_EXPECTED)
    material_mode = meta_value(meta_text, "materialImportMode")
    report.check(
        f"{sex} Unity material import mode",
        material_mode == "1",
        f"materialImportMode={material_mode} (ImportStandard); exact committed URP/Lit external material remaps are active",
        f"materialImportMode={material_mode}; ImportStandard=1 is required for deterministic FBX material remaps",
    )

    doc = read_fbx(fbx)
    binary_ok = (
        doc.version == 7400
        and doc.unit_scale == 100.0
        and doc.up_axis == 2
        and len(doc.mesh_names) == 3
        and doc.skin_count == 3
        and len(doc.bone_names) == 65
        and len(doc.humanoid_mapping()) >= 22
        and not doc.missing_required_bones()
        and doc.core_hierarchy_connected()
        and not doc.stacks
    )
    report.check(
        f"{sex} FBX binary structure",
        binary_ok,
        f"FBX 7400, Z-up, unit scale 100, 3 skinned meshes, 65 bones, 22 canonical humanoid bones mapped, 0 missing required bones, no embedded AnimationStack",
        f"version={doc.version}, up={doc.up_axis}, unit={doc.unit_scale}, meshes={len(doc.mesh_names)}, skins={doc.skin_count}, bones={len(doc.bone_names)}, mapped={len(doc.humanoid_mapping())}, missing={doc.missing_required_bones()}, connected={doc.core_hierarchy_connected()}, stacks={len(doc.stacks)}",
    )

    embedded = [texture for texture in doc.textures if texture.embedded_bytes > 0 and not texture.external]
    external = [texture for texture in doc.textures if texture.external or texture.embedded_bytes == 0]
    if sex == "Male":
        source_ok = len(embedded) == 7 and not external
        source_detail = f"{len(embedded)}/7 embedded PNGs ({sum(t.embedded_bytes for t in embedded):,} bytes); no unresolved external path"
        source_bad = f"{len(embedded)} embedded, {len(external)} external"
    else:
        normal_path = ASSETS_ROOT / "Characters" / "Female" / "Textures" / "T_Superhero_Female_Normal.png"
        normal_ok = normal_path.is_file() and sha256(normal_path) == FEMALE_NORMAL_SHA
        source_ok = len(embedded) == 6 and len(external) == 1 and normal_ok
        source_detail = (f"{len(embedded)}/7 embedded PNGs ({sum(t.embedded_bytes for t in embedded):,} bytes); "
                         "the immutable FBX's one external T_Superhero_Female_Normal.png is resolved at import by the byte-identical existing GLTF fallback texture")
        source_bad = f"{len(embedded)} embedded, {len(external)} external, production normal SHA ok={normal_ok}"
    report.add(f"{sex} embedded source textures", "PASS" if source_ok else "FAIL",
               source_detail if source_ok else source_bad)

    texture_entries = [e for e in load_json(TEXTURE_MANIFEST)["textures"] if e["sex"] == sex]
    material_entries = [e for e in load_json(MATERIAL_MANIFEST)["materials"] if e["sex"] == sex]
    expected = set()
    for entry in texture_entries + material_entries:
        expected.add(entry["path"])
        expected.add(entry["meta_path"])
    material_dir = fbx.parent / "Materials"
    texture_dir = fbx.parent / "Textures"
    actual = {
        display(path)
        for root in (material_dir, texture_dir)
        for path in root.rglob("*")
        if path.is_file() and path.name != ".gitkeep"
    }
    missing = sorted(expected - actual)
    unexpected = sorted(actual - expected)
    keep_files = (material_dir / ".gitkeep").is_file() and (texture_dir / ".gitkeep").is_file()
    assets_ok = not missing and not unexpected and keep_files and len(texture_entries) == 7 and len(material_entries) == 3
    report.check(
        f"{sex} exact targeted material/texture asset scope",
        assets_ok,
        f"7 PNGs + 7 PNG metas + 3 .mat + 3 .mat metas, no unexpected files, both .gitkeep markers retained",
        f"missing={missing[:4]}, unexpected={unexpected[:4]}, gitkeep={keep_files}, textures={len(texture_entries)}, materials={len(material_entries)}",
    )


COMMENT_BLOCK = re.compile(r"/\*.*?\*/", re.DOTALL)
COMMENT_LINE = re.compile(r"//[^\n]*")


def strip_comments(source: str) -> str:
    """Remove C# block and line comments so history notes never read as code."""
    return COMMENT_LINE.sub("", COMMENT_BLOCK.sub("", source))


def check_gameplay_sources(report: Report) -> None:
    visual = ASSETS_ROOT / "Scripts" / "Characters" / "PlayerCharacterVisual.cs"
    text = visual.read_text(encoding="utf-8") if visual.is_file() else ""
    spawn_ok = (
        "CharacterAnimationConfig.Load()" in text
        and "Instantiate(_active.model, transform)" in text
        and "SetCapsuleVisible(false)" in text
    )
    report.check(
        "runtime FBX spawn path preserved",
        spawn_ok,
        "PlayerCharacterVisual loads the generated roster config, instantiates the FBX model and disables the capsule MeshRenderer when configured",
        "one or more production spawn/fallback statements are missing from PlayerCharacterVisual.cs",
    )

    # Comments legitimately mention the removed Animator wiring, so only code
    # (comments stripped) is scanned.
    code_only = strip_comments(text)
    animator_free = not re.search(
        r"\bAnimator\b|runtimeAnimatorController|AnimationClip", code_only)
    report.check(
        "player visual carries no Animator wiring",
        animator_free,
        "PlayerCharacterVisual.cs holds no Animator / AnimatorController / AnimationClip reference after the animation removal",
        "PlayerCharacterVisual.cs still references Animator, runtimeAnimatorController or AnimationClip",
    )

    config_source = ASSETS_ROOT / "Scripts" / "Characters" / "CharacterAnimationConfig.cs"
    config_text = config_source.read_text(encoding="utf-8") if config_source.is_file() else ""
    roster_ok = (
        "public List<CharacterDefinition> characters" in config_text
        and "public CharacterDefinition SelectedCharacter()" in config_text
        and "AnimationClip" not in config_text
    )
    report.check(
        "config asset is a character roster without clips",
        roster_ok,
        "CharacterAnimationConfig keeps the character roster and selection helpers and no longer stores AnimationClip assignments or generated controllers",
        "CharacterAnimationConfig still carries clip/controller data or lost its roster API",
    )

    runtime_sources = sorted((ASSETS_ROOT / "Scripts").rglob("*.cs"))
    aim_state: list[str] = []
    aim_ui: list[str] = []
    clip_users: list[str] = []
    for source in runtime_sources:
        source_text = source.read_text(encoding="utf-8")
        for line_number, line in enumerate(source_text.splitlines(), 1):
            if "AimInput" in line or "IsAiming" in line:
                aim_state.append(f"{display(source)}:{line_number}")
            if "SetAimHeld" in line or "AimButton" in line:
                aim_ui.append(f"{display(source)}:{line_number}")
        if re.search(r"\bAnimator\b|runtimeAnimatorController|AnimationClip|SampleAnimationClip",
                     strip_comments(source_text)):
            clip_users.append(display(source))

    report.check(
        "actual runtime aim state and touch input exist",
        bool(aim_state) and bool(aim_ui),
        "ThirdPersonMotor.AimInput/IsAiming and PlayerInputRouter/MobileControlsBuilder AIM hold-button wiring are present",
        "missing AimInput state or SetAimHeld/AimButton wiring",
    )
    report.check(
        "no runtime script plays animation clips",
        not clip_users,
        "no file under Assets/Scripts references an Animator, runtimeAnimatorController or AnimationClip (comments excluded)",
        "clip playback survives in: " + ", ".join(clip_users[:4]),
    )
    report.not_verified(
        "live shooting gameplay integration",
        "aiming state exists and the visual hooks are no-ops, but this validator cannot exercise a live match; only PlayMode hook-safety tests cover the call sites",
    )

    production_prefabs = [p for p in ASSETS_ROOT.rglob("*.prefab") if "GLTF_Import_Test" not in p.parts]
    production_controllers = [p for p in ASSETS_ROOT.rglob("*.controller") if "GLTF_Import_Test" not in p.parts]
    production_configs = [p for p in ASSETS_ROOT.rglob("*.asset") if "GLTF_Import_Test" not in p.parts and "Generated" not in p.parts]
    report.not_verified(
        "committed gameplay prefab / controller / config references",
        f"no committed production prefab ({len(production_prefabs)}), controller ({len(production_controllers)}) or config asset ({len(production_configs)}) exists; the generated roster config and PlayerTest scene are Editor-created and must be checked in licensed Unity",
    )


def check_android_configuration(report: Report) -> None:
    project = PROJECT_SETTINGS.read_text(encoding="utf-8") if PROJECT_SETTINGS.is_file() else ""
    manifest = json.loads((UNITY_ROOT / "Packages" / "manifest.json").read_text(encoding="utf-8"))
    bootstrap = (ASSETS_ROOT / "Editor" / "ProjectBootstrap.cs").read_text(encoding="utf-8")
    checks = {
        "Linear colour space": meta_value(project, "m_ActiveColorSpace") == "1",
        "GPU skinning enabled": meta_value(project, "gpuSkinning") == "1",
        "Android min SDK 26": meta_value(project, "AndroidMinSdkVersion") == "26",
        "ARMv7 + ARM64 target": meta_value(project, "AndroidTargetArchitectures") == "3",
        "IL2CPP scripting backend": bool(re.search(r"scriptingBackend:\s*\n\s*Android:\s*1", project)),
        "URP 17 declared": manifest.get("dependencies", {}).get("com.unity.render-pipelines.universal", "").startswith("17."),
        "ASTC configured at Editor bootstrap time": "MobileTextureSubtarget.ASTC" in bootstrap,
    }
    failed = [name for name, ok in checks.items() if not ok]
    report.check(
        "Android-compatible rendering configuration",
        not failed,
        "Linear, GPU skinning, min SDK 26, ARMv7+ARM64, IL2CPP, URP 17 and ASTC source settings are present",
        "missing or unexpected: " + ", ".join(failed),
    )
    report.not_verified(
        "Android device rendering / shader variants / FPS / memory / thermal",
        "configuration is static evidence only; no APK was built or installed and no physical Android device measurement was performed by this validator",
    )


def check_editor_boundary(report: Report) -> None:
    for name, detail in (
        ("Unity Editor import settings and Humanoid Avatar validity", "requires licensed Unity 6"),
        ("visible player mesh in gameplay scene", "requires the PlayerTest scene and generated roster config in licensed Unity"),
        ("movement / camera / collision / ground alignment", "requires PlayMode tests in licensed Unity; see the PlayMode results in the CI job"),
        ("human front / facial / UV seam inspection", "requires human visual review; rear-view software-GL renders are not sufficient"),
        ("correct textured Male/Female Editor camera captures", "requires licensed Unity 6 software-GL Camera.Render evidence"),
    ):
        report.not_verified(name, detail)


def run_validation() -> Report:
    report = Report()
    report.add(
        "verification boundary",
        "PASS",
        "Static validator reads committed bytes and source only; it never claims Unity import, rendering, Avatar validity, animation playback, APK output or device behaviour.",
    )
    check_protection(report)
    check_lfs_rules(report)
    check_production_texture_manifest(report)
    check_production_material_manifest(report)
    for sex, info in CHARACTERS.items():
        check_character_fbx(report, sex, info)
    check_gameplay_sources(report)
    check_android_configuration(report)
    check_editor_boundary(report)
    return report


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--json", dest="json_path", type=Path, help="write JSON results")
    parser.add_argument("--report", dest="report_path", type=Path, help="write Markdown results")
    args = parser.parse_args()

    report = run_validation()
    markdown = report.markdown()
    if args.report_path:
        args.report_path.parent.mkdir(parents=True, exist_ok=True)
        args.report_path.write_text(markdown, encoding="utf-8")
    if args.json_path:
        args.json_path.parent.mkdir(parents=True, exist_ok=True)
        args.json_path.write_text(json.dumps(report.json(), indent=2) + "\n", encoding="utf-8")
    print(markdown)
    print(f"\n{report.passes} passed, {len(report.failures)} failed, {report.not_verified_count} not verified")
    return 1 if report.failures else 0


if __name__ == "__main__":
    raise SystemExit(main())
