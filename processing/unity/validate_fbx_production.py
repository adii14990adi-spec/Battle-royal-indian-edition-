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

sys.path.insert(0, str(PROCESSING))
from fbx_inspect import read_fbx  # noqa: E402
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
    "materialImportMode": "0",
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
        "production Unity assets and FBX metadata unchanged",
        not protected_problems,
        f"{checked} protected Assets/ProjectSettings files are byte-identical; both character .fbx.meta GUIDs are unchanged",
        "; ".join(protected_problems[:8]),
    )

    gltf_report = gltf_validator.run_validation()
    report.check(
        "GLTF fallback files and test scene preserved",
        not gltf_report.failures,
        f"isolated GLTF validator: {gltf_report.passes} PASS / {len(gltf_report.failures)} FAIL / {gltf_report.not_verified_count} NOT VERIFIED; original GLTF/BIN/PNG files and test scene are present",
        "; ".join(row["detail"] for row in gltf_report.failures[:4]),
    )


def check_character_fbx(report: Report, sex: str, info: dict[str, Any]) -> None:
    fbx = info["fbx"]
    meta = Path(str(fbx) + ".meta")
    label = f"{sex} production FBX"
    if not fbx.is_file() or not meta.is_file():
        report.check(label, False, "", f"missing {display(fbx)} or its .meta")
        return

    guid = meta_value(meta.read_text(encoding="utf-8"), "guid")
    report.check(
        f"{sex} FBX GUID unchanged",
        guid == info["guid"],
        f"{display(meta)} has stable GUID {info['guid']}",
        f"GUID is {guid!r}, expected {info['guid']!r}",
    )
    check_meta_settings(report, f"{sex} Unity importer settings", meta, CHARACTER_META_EXPECTED)

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
    report.add(
        f"{sex} embedded source textures",
        "PASS" if len(embedded) == 7 and not external else "FAIL",
        f"{len(embedded)}/{len(doc.textures)} texture nodes carry embedded PNG bytes ({sum(t.embedded_bytes for t in embedded):,} bytes); "
        + ("no unresolved external texture path" if not external else "unresolved external path(s): " + "; ".join(t.filename for t in external)),
    )

    material_dir = fbx.parent / "Materials"
    texture_dir = fbx.parent / "Textures"
    committed_external_assets = [
        path for root in (material_dir, texture_dir)
        for path in root.rglob("*")
        if path.is_file() and path.name != ".gitkeep"
    ]
    report.check(
        f"{sex} no dangling committed material/texture asset reference",
        not committed_external_assets,
        f"{display(material_dir)} and {display(texture_dir)} contain only .gitkeep; FBX is the self-contained production source",
        "unexpected assets: " + ", ".join(display(p) for p in committed_external_assets[:8]),
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

    runtime_sources = sorted((ASSETS_ROOT / "Scripts").rglob("*.cs"))
    aim_setters: list[str] = []
    aim_calls: list[str] = []
    shooting_scripts: list[str] = []
    for source in runtime_sources:
        source_text = source.read_text(encoding="utf-8")
        for line_number, line in enumerate(source_text.splitlines(), 1):
            if "SetBool" in line and "IsAiming" in line:
                aim_setters.append(f"{display(source)}:{line_number}")
            if "SetAiming(" in line and "public void SetAiming" not in line:
                aim_calls.append(f"{display(source)}:{line_number}")
        lowered = display(source).lower()
        if any(word in lowered for word in ("weapon", "shooting", "firearm", "gun")):
            shooting_scripts.append(display(source))

    report.check(
        "Aim Animator parameter is driven by runtime",
        bool(aim_setters),
        "found SetBool(IsAiming) in runtime source: " + ", ".join(aim_setters[:4]),
        "no SetBool(IsAiming) exists under Assets/Scripts; the generated Aim state uses IsAiming, while PlayerCharacterVisual.SetAiming only changes UpperBody layer weight",
    )
    report.check(
        "Aim gameplay hook is called",
        bool(aim_calls),
        "found runtime caller(s): " + ", ".join(aim_calls[:4]),
        "PlayerCharacterVisual.SetAiming(bool) has no runtime caller in Assets/Scripts; the Aim state cannot be entered from current gameplay input",
    )
    report.not_verified(
        "live shooting gameplay integration",
        "no live weapon/fire system exists under Assets/Scripts; only PlayerCharacterVisual.TriggerShoot/TriggerReload animation hooks and UI copy were found"
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
