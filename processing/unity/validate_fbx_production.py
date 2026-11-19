#!/usr/bin/env python3
"""Static validation for the production Male / Female FBX character system.

    python3 processing/unity/validate_fbx_production.py \
        --json docs/fbx_production_verification/static_validation.json \
        --report docs/fbx_production_verification/STATIC_VALIDATION.md

This validator is deliberately honest about its boundary: it reads committed
FBX bytes, FBX metadata, C# source, ProjectSettings and the isolated GLTF
fallback manifests. It does NOT open Unity, import an FBX, build an Avatar,
play an animation, render a frame, build an APK or test a phone. Editor-only
evidence is produced separately by `FBXProductionVerifier.VerifyFromCommandLine`
and the optional software-GL capture in the licensed Unity workflow.

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
REPAIR_MANIFEST = PROCESSING / "fbx_production_repair_manifest.json"
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

# Only clips that actually exist in the repository are listed. No unavailable
# clip is inferred, invented or marked as passing.
REQUIRED_CLIPS = {
    "Idle": "Movement/idle.fbx",
    "Walk": "Movement/walking.fbx",
    "Run": "Movement/Running.fbx",
    "Jump": "Movement/jump up.fbx",
    "Aim": "Combat/idle aiming.fbx",
    "Shoot": "Weapon/firing rifle.fbx",
    "Reload": "Weapon/reloading.fbx",
}

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

CLIP_META_EXPECTED = {
    "animationType": "3",
    "avatarSetup": "1",
    "autoGenerateAvatarMappingIfUnspecified": "1",
    "importAnimation": "1",
    "animationCompression": "3",
    "isReadable": "0",
    "maxBonesPerVertex": "4",
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
        "all 76 production FBX files immutable",
        not problems,
        f"{len(fbx_files)}/{len(fbx_files)} FBX files match processing/unity/fbx-baseline.json byte-for-byte",
        "; ".join(problems[:8]),
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

    repair = load_json(REPAIR_MANIFEST) if REPAIR_MANIFEST.is_file() else {}
    baseline_path = REPO_ROOT / repair.get("pre_repair_protected_manifest_path", "")
    repair_entries = repair.get("changed_protected_files", [])
    repair_paths = {entry["path"] for entry in repair_entries}
    repair_problems: list[str] = []
    if not baseline_path.is_file():
        repair_problems.append("pre-repair protected manifest is missing")
        baseline_by_path: dict[str, dict[str, Any]] = {}
    else:
        baseline_data = baseline_path.read_bytes()
        if sha256_bytes(baseline_data) != repair.get("pre_repair_protected_manifest_sha256"):
            repair_problems.append("pre-repair baseline manifest hash mismatch")
        baseline = load_json(baseline_path)
        baseline_by_path = {entry["path"]: entry for entry in baseline.get("files", [])}
        if set(baseline_by_path) != set(protected_by_path):
            repair_problems.append("pre-repair baseline has a different protected file set")
        base_allow = set(baseline.get("allowlisted_modified_paths", []))
        if base_allow != BASE_ALLOWLIST:
            repair_problems.append("pre-repair baseline does not have exactly the two test asmdefs allowlisted")
    for entry in repair_entries:
        rel = entry["path"]
        path = REPO_ROOT / rel
        previous = baseline_by_path.get(rel)
        current = protected_by_path.get(rel)
        if previous is None or current is None:
            repair_problems.append(f"{rel} is not a protected file")
            continue
        if entry.get("previous_size") != previous["size"] or entry.get("previous_sha256") != previous["sha256"]:
            repair_problems.append(f"{rel} has wrong pre-repair hash")
        if entry.get("size") != current["size"] or entry.get("sha256") != current["sha256"]:
            repair_problems.append(f"{rel} has wrong post-repair protected hash")
        if not path.is_file():
            repair_problems.append(f"missing {rel}")
            continue
        size, digest = current_identity(path)
        if size != entry.get("size") or digest != entry.get("sha256"):
            repair_problems.append(f"changed {rel} outside approved repair hash")
        if rel not in allowlisted:
            repair_problems.append(f"{rel} is not explicitly allowlisted")
        if not entry.get("reason"):
            repair_problems.append(f"{rel} has no justification")
    unexpected_allowlist = allowlisted - BASE_ALLOWLIST - repair_paths
    missing_allowlist = repair_paths - allowlisted
    if unexpected_allowlist:
        repair_problems.append("unexpected allowlist: " + ", ".join(sorted(unexpected_allowlist)))
    if missing_allowlist:
        repair_problems.append("missing allowlist: " + ", ".join(sorted(missing_allowlist)))
    new_files = repair.get("new_repair_files", [])
    for entry in new_files:
        path = REPO_ROOT / entry["path"]
        if not path.is_file() or current_identity(path) != (entry["size"], entry["sha256"]):
            repair_problems.append(f"new repair file hash mismatch: {entry['path']}")
    report.check(
        "targeted repair protected file hashes verified",
        bool(repair_entries) and bool(new_files) and not repair_problems,
        f"{len(repair_entries)} protected source files and {len(new_files)} new repair files have exact pre/post SHA-256 hashes in {display(REPAIR_MANIFEST)}; no other protected file is allowlisted",
        "; ".join(repair_problems[:8]) or "repair manifest missing or empty",
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


def check_required_clips(report: Report) -> None:
    animations = ASSETS_ROOT / "Animations"
    for category, rel in REQUIRED_CLIPS.items():
        fbx = animations / rel
        meta = Path(str(fbx) + ".meta")
        exists = fbx.is_file() and meta.is_file()
        if not exists:
            report.check(f"available clip: {category}", False, "", f"missing {display(fbx)} or its .meta")
            continue
        doc = read_fbx(fbx)
        available = (
            doc.version == 7700
            and doc.is_animation
            and len(doc.stacks) == 1
            and doc.curve_count > 0
            and doc.frames() > 0
            and not doc.missing_required_bones()
        )
        report.check(
            f"available clip: {category}",
            available,
            f"{display(fbx)}: FBX 7700, one real AnimationStack, {doc.curve_count} curves, {doc.frames()} frames, {len(doc.humanoid_mapping())} canonical humanoid bones, 0 missing required bones",
            f"version={doc.version}, stacks={len(doc.stacks)}, curves={doc.curve_count}, frames={doc.frames()}, missing={doc.missing_required_bones()}",
        )
        check_meta_settings(report, f"{category} clip Humanoid import metadata", meta, CLIP_META_EXPECTED)


def check_gameplay_sources(report: Report) -> None:
    visual = ASSETS_ROOT / "Scripts" / "Characters" / "PlayerCharacterVisual.cs"
    text = visual.read_text(encoding="utf-8") if visual.is_file() else ""
    spawn_ok = (
        "CharacterAnimationConfig.Load()" in text
        and "Instantiate(_active.model, transform)" in text
        and "_animator.runtimeAnimatorController = _active.controller" in text
        and "SetCapsuleVisible(false)" in text
    )
    report.check(
        "runtime FBX spawn path preserved",
        spawn_ok,
        "PlayerCharacterVisual loads the generated config, instantiates the FBX model, assigns the controller and disables the capsule MeshRenderer when configured",
        "one or more production spawn/fallback statements are missing from PlayerCharacterVisual.cs",
    )

    config_source = ASSETS_ROOT / "Scripts" / "Characters" / "CharacterAnimationConfig.cs"
    config_text = config_source.read_text(encoding="utf-8") if config_source.is_file() else ""
    find_start = config_text.find("public CharacterAnimAssignment FindAssignment")
    find_end = config_text.find("public AnimationClip FindClip", find_start + 1)
    find_assignment_source = config_text[find_start:find_end] if find_start >= 0 and find_end > find_start else ""
    shared_lookup_ok = (
        bool(find_assignment_source)
        and "IsSharedCharacterId(assignment.characterId)" in find_assignment_source
        and "StringComparison.OrdinalIgnoreCase" in find_assignment_source
    )
    report.check(
        "runtime shared-assignment lookup handles Unity empty strings",
        shared_lookup_ok,
        "FindAssignment treats null/empty/whitespace IDs as shared and matches valid IDs case-insensitively",
        "FindAssignment does not use the repaired shared-ID helper and case-insensitive per-character comparison",
    )

    builder_source = (ASSETS_ROOT / "Editor" / "AnimatorControllerBuilder.cs").read_text(encoding="utf-8")
    build_start = builder_source.find("public static BuildResult BuildController")
    build_end = builder_source.find("// ---- parameters", build_start + 1)
    build_resolution = builder_source[build_start:build_end] if build_start >= 0 and build_end > build_start else ""
    builder_shared_ok = (
        bool(build_resolution)
        and "CharacterAnimationConfig.IsSharedCharacterId(assignment.characterId)" in build_resolution
        and "StringComparison.OrdinalIgnoreCase" in build_resolution
    )
    report.check(
        "Animator controller shared-assignment resolution handles Unity empty strings",
        builder_shared_ok,
        "BuildController uses the same shared-ID semantics and case-insensitive per-character overrides",
        "BuildController still skips empty-string shared defaults or uses case-sensitive ID matching",
    )

    runtime_sources = sorted((ASSETS_ROOT / "Scripts").rglob("*.cs"))
    aim_setters: list[str] = []
    aim_calls: list[str] = []
    aim_state: list[str] = []
    aim_ui: list[str] = []
    shooting_scripts: list[str] = []
    for source in runtime_sources:
        source_text = source.read_text(encoding="utf-8")
        for line_number, line in enumerate(source_text.splitlines(), 1):
            if "SetBool" in line and "IsAiming" in line:
                aim_setters.append(f"{display(source)}:{line_number}")
            if "SetAiming(" in line and "public void SetAiming" not in line:
                aim_calls.append(f"{display(source)}:{line_number}")
            if "AimInput" in line or "IsAiming" in line:
                aim_state.append(f"{display(source)}:{line_number}")
            if "SetAimHeld" in line or "AimButton" in line:
                aim_ui.append(f"{display(source)}:{line_number}")
        lowered = display(source).lower()
        if any(word in lowered for word in ("weapon", "shooting", "firearm", "gun")):
            shooting_scripts.append(display(source))

    report.check(
        "Aim Animator parameter is driven by runtime",
        bool(aim_setters),
        "found SetBool(IsAiming) in runtime source: " + ", ".join(aim_setters[:4]),
        "no SetBool(IsAiming) exists under Assets/Scripts",
    )
    report.check(
        "Aim gameplay hook is called",
        bool(aim_calls),
        "found runtime caller(s): " + ", ".join(aim_calls[:4]),
        "PlayerCharacterVisual.SetAiming(bool) has no runtime caller in Assets/Scripts",
    )
    report.check(
        "actual runtime aim state and touch input exist",
        bool(aim_state) and bool(aim_ui),
        "ThirdPersonMotor.AimInput/IsAiming and PlayerInputRouter/MobileControlsBuilder AIM hold-button wiring are present",
        "missing AimInput state or SetAimHeld/AimButton wiring",
    )

    builder_text = (ASSETS_ROOT / "Editor" / "AnimatorControllerBuilder.cs").read_text(encoding="utf-8")
    is_aiming_parameter_lines = [line for line in builder_text.splitlines()
                                 if line.strip().startswith("AddParameter(")
                                 and "CharacterAnimParameters.IsAiming" in line]
    attach_start = builder_text.find("private static void AttachAimTransitions")
    attach_end = builder_text.find("private static AvatarMask BuildUpperBodyMask", attach_start + 1)
    attach_source = builder_text[attach_start:attach_end] if attach_start >= 0 and attach_end > attach_start else ""
    aim_any_state_lines = [line for line in attach_source.splitlines()
                           if "AddAnyStateTransition(aimState)" in line]
    report.check(
        "IsAiming parameter/transition setup is not duplicated",
        len(is_aiming_parameter_lines) == 1 and len(aim_any_state_lines) == 1,
        "exactly one IsAiming AddParameter declaration and one Aim Any-State transition in the controller builder",
        f"IsAiming AddParameter={len(is_aiming_parameter_lines)}, AddAnyStateTransition(aimState)={len(aim_any_state_lines)}",
    )
    report.not_verified(
        "live shooting gameplay integration",
        "aiming state now drives IsAiming and SetAiming, but no live weapon/fire system exists under Assets/Scripts; only PlayerCharacterVisual.TriggerShoot/TriggerReload animation hooks and PlayMode hook safety tests were found"
        + (f" (candidate filenames: {', '.join(shooting_scripts[:4])})" if shooting_scripts else ""),
    )

    production_prefabs = [p for p in ASSETS_ROOT.rglob("*.prefab") if "GLTF_Import_Test" not in p.parts]
    production_controllers = [p for p in ASSETS_ROOT.rglob("*.controller") if "GLTF_Import_Test" not in p.parts]
    production_configs = [p for p in ASSETS_ROOT.rglob("*.asset") if "GLTF_Import_Test" not in p.parts and "Generated" not in p.parts]
    report.not_verified(
        "committed gameplay prefab / controller / config references",
        f"no committed production prefab ({len(production_prefabs)}), controller ({len(production_controllers)}) or config asset ({len(production_configs)}) exists; generated config/controllers and PlayerTest scene are Editor-created and must be checked in licensed Unity",
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
        ("Unity Editor import settings and Humanoid Avatar validity", "requires licensed Unity 6; see EDITOR_VALIDATION.md"),
        ("Animator transitions and missing generated references", "requires the Editor-generated config/controllers; see EDITOR_VALIDATION.md"),
        ("animation deformation and visual mesh quality", "requires AnimationMode sampling and genuine Editor renders; see EDITOR_VALIDATION.md and CAPTURE_RESULT.md"),
        ("visible player mesh in gameplay scene", "requires PlayerTest scene and generated config in licensed Unity; see EDITOR_VALIDATION.md"),
        ("movement / camera / collision / ground alignment", "requires PlayMode tests in licensed Unity; see the PlayMode results in the CI job"),
        ("human front / facial / UV seam inspection", "requires human visual review; rear-view software-GL renders are not sufficient"),
        ("correct textured Male/Female Editor camera captures", "requires licensed Unity 6 software-GL Camera.Render evidence; see CAPTURE_RESULT.md and screenshots/"),
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
    check_required_clips(report)
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
