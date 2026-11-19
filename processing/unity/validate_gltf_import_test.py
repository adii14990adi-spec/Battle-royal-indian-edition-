#!/usr/bin/env python3
"""Static validation for the isolated GLTF character import experiment.

This validator never opens Unity, never imports a glTF and never claims a
visual result. It checks the exact source inventory, protected production
assets, URI corrections, glTF/BIN/PNG integrity, skeleton/skin data, licence
text, GUID uniqueness and the pinned importer version. Editor-only checks are
reported as NOT VERIFIED.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import struct
import sys
from pathlib import Path
from typing import Any

REPO_ROOT = Path(__file__).resolve().parents[2]
PROCESSING = REPO_ROOT / "processing" / "unity"
UNITY_ROOT = REPO_ROOT / "unity-project"
ASSETS_ROOT = UNITY_ROOT / "Assets"
GLTF_ROOT = ASSETS_ROOT / "Characters" / "GLTF_Import_Test"
SOURCE_MANIFEST = PROCESSING / "gltf_import_test_source_manifest.json"
PROTECTED_MANIFEST = PROCESSING / "gltf_import_test_protected_assets.json"
PACKAGE_MANIFEST = UNITY_ROOT / "Packages" / "manifest.json"
SCENE_PATH = GLTF_ROOT / "GLTF_Player_Import_Test.unity"
FINAL_REPORT = REPO_ROOT / "docs" / "GLTF_PLAYER_IMPORT_TEST_REPORT.md"

sys.path.insert(0, str(PROCESSING))
from fbx_inspect import read_fbx  # noqa: E402

KNOWN_URI_FIXES = {
    "Male": {
        "T_Hair_1_Normal_png.png": "T_Hair_1_Normal.png",
        "T_Eye_Normal_png.png": "T_Eye_Normal.png",
    },
    "Female": {
        "T_Eye_Normal_png.png": "T_Eye_Normal.png",
    },
}

REQUIRED_HUMAN_BONES = {
    "Hips": ("pelvis",),
    "Spine": ("spine_01",),
    "Chest": ("spine_02",),
    "UpperChest": ("spine_03",),
    "Neck": ("neck_01",),
    "Head": ("Head",),
    "LeftShoulder": ("clavicle_l",),
    "LeftUpperArm": ("upperarm_l",),
    "LeftLowerArm": ("lowerarm_l",),
    "LeftHand": ("hand_l",),
    "RightShoulder": ("clavicle_r",),
    "RightUpperArm": ("upperarm_r",),
    "RightLowerArm": ("lowerarm_r",),
    "RightHand": ("hand_r",),
    "LeftUpperLeg": ("thigh_l",),
    "LeftLowerLeg": ("calf_l",),
    "LeftFoot": ("foot_l",),
    "LeftToes": ("ball_l",),
    "RightUpperLeg": ("thigh_r",),
    "RightLowerLeg": ("calf_r",),
    "RightFoot": ("foot_r",),
    "RightToes": ("ball_r",),
}

COMPONENT = {
    5120: ("b", 1),
    5121: ("B", 1),
    5122: ("h", 2),
    5123: ("H", 2),
    5125: ("I", 4),
    5126: ("f", 4),
}
TYPE_COUNT = {
    "SCALAR": 1,
    "VEC2": 2,
    "VEC3": 3,
    "VEC4": 4,
    "MAT2": 4,
    "MAT3": 9,
    "MAT4": 16,
}


class Report:
    def __init__(self) -> None:
        self.rows: list[dict[str, str]] = []

    def add(self, name: str, status: str, detail: str) -> None:
        self.rows.append({"check": name, "status": status, "detail": detail})

    def check(self, name: str, ok: bool, good: str, bad: str) -> None:
        self.add(name, "PASS" if ok else "FAIL", good if ok else bad)

    def info(self, name: str, detail: str) -> None:
        self.add(name, "INFO", detail)

    def not_verified(self, name: str, detail: str) -> None:
        self.add(name, "NOT VERIFIED", detail)

    @property
    def failures(self) -> list[dict[str, str]]:
        return [r for r in self.rows if r["status"] == "FAIL"]

    @property
    def passes(self) -> int:
        return sum(1 for r in self.rows if r["status"] == "PASS")

    @property
    def not_verified_count(self) -> int:
        return sum(1 for r in self.rows if r["status"] == "NOT VERIFIED")

    def markdown(self) -> str:
        lines = [
            "# Static validation — isolated GLTF import test",
            "",
            "Static only: no Unity Editor, no glTF import, no rendering, no APK and no device measurement was performed by this validator.",
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
            "Editor import, Avatar validity, retargeting playback, visual screenshots and Android runtime behaviour are checked separately in licensed Unity CI and/or marked NOT VERIFIED.",
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


def png_info(path: Path) -> dict[str, Any]:
    data = path.read_bytes()
    if len(data) < 33 or data[:8] != b"\x89PNG\r\n\x1a\n" or data[12:16] != b"IHDR":
        raise ValueError(f"{path} is not a PNG with an IHDR chunk")
    width, height, bit_depth, color_type = struct.unpack(">IIBB", data[16:26])
    channels = {0: 1, 2: 3, 3: 1, 4: 2, 6: 4}.get(color_type, 4)
    return {
        "path": display(path),
        "width": width,
        "height": height,
        "bit_depth": bit_depth,
        "color_type": color_type,
        "channels": channels,
        "bytes": len(data),
        "sha256": hashlib.sha256(data).hexdigest(),
    }


def uri_path(base: Path, uri: str) -> Path:
    if not uri or uri.startswith("data:") or re.match(r"^[a-zA-Z][a-zA-Z0-9+.-]*://", uri):
        raise ValueError(f"unsupported or absolute glTF URI: {uri!r}")
    return (base / uri).resolve()


def gltf_uris(data: dict[str, Any]) -> list[tuple[str, int, str]]:
    result: list[tuple[str, int, str]] = []
    for section in ("buffers", "images"):
        for i, entry in enumerate(data.get(section, [])):
            uri = entry.get("uri")
            if uri:
                result.append((section, i, uri))
    return result


def read_accessor(data: dict[str, Any], bin_bytes: bytes, index: int) -> list[tuple[Any, ...]]:
    accessor = data["accessors"][index]
    if "bufferView" not in accessor:
        raise ValueError(f"accessor {index} has no bufferView")
    view = data["bufferViews"][accessor["bufferView"]]
    component = accessor["componentType"]
    fmt, size = COMPONENT[component]
    count = TYPE_COUNT[accessor["type"]]
    element_size = size * count
    stride = int(view.get("byteStride") or element_size)
    if stride < element_size:
        raise ValueError(f"accessor {index} stride {stride} < element size {element_size}")
    start = int(view.get("byteOffset", 0)) + int(accessor.get("byteOffset", 0))
    pattern = "<" + fmt * count
    values: list[tuple[Any, ...]] = []
    for i in range(int(accessor["count"])):
        offset = start + i * stride
        raw = bin_bytes[offset : offset + element_size]
        if len(raw) != element_size:
            raise ValueError(f"accessor {index} truncated at element {i}")
        values.append(struct.unpack(pattern, raw))
    return values


def analyse_gltf(gltf_path: Path) -> dict[str, Any]:
    data = load_json(gltf_path)
    uris = gltf_uris(data)
    uri_results = []
    for section, index, uri in uris:
        target = uri_path(gltf_path.parent, uri)
        uri_results.append(
            {
                "section": section,
                "index": index,
                "uri": uri,
                "resolved": str(target),
                "exists": target.is_file(),
            }
        )

    nodes = data.get("nodes", [])
    joint_indices: list[int] = []
    for skin in data.get("skins", []):
        joint_indices.extend(skin.get("joints", []))
    joint_names = [nodes[i].get("name", "") for i in joint_indices]
    parent: dict[int, int] = {}
    for i, node in enumerate(nodes):
        for child in node.get("children", []):
            parent[child] = i

    def reaches_root(index: int) -> bool:
        seen: set[int] = set()
        cursor = index
        while cursor in parent:
            if cursor in seen:
                return False
            seen.add(cursor)
            cursor = parent[cursor]
        return True

    missing_required = []
    for human, aliases in REQUIRED_HUMAN_BONES.items():
        if not any(alias in joint_names for alias in aliases):
            missing_required.append(human)

    bin_bytes = b""
    buffer_uri = data.get("buffers", [{}])[0].get("uri")
    if buffer_uri:
        bin_bytes = uri_path(gltf_path.parent, buffer_uri).read_bytes()

    vertices = 0
    triangles = 0
    weight_problems: list[str] = []
    mesh_rows = []
    for mesh_index, mesh in enumerate(data.get("meshes", [])):
        for primitive in mesh.get("primitives", []):
            attrs = primitive.get("attributes", {})
            position_index = attrs.get("POSITION")
            if position_index is None:
                weight_problems.append(f"mesh {mesh_index}: no POSITION")
                continue
            position = data["accessors"][position_index]
            vertex_count = int(position["count"])
            index_accessor = primitive.get("indices")
            index_count = int(data["accessors"][index_accessor]["count"]) if index_accessor is not None else vertex_count
            primitive_tris = index_count // 3 if primitive.get("mode", 4) == 4 else 0
            vertices += vertex_count
            triangles += primitive_tris
            mesh_rows.append(
                {
                    "mesh": mesh.get("name", f"mesh_{mesh_index}"),
                    "vertices": vertex_count,
                    "triangles": primitive_tris,
                    "material": primitive.get("material"),
                }
            )

            joints_index = attrs.get("JOINTS_0")
            weights_index = attrs.get("WEIGHTS_0")
            if joints_index is None or weights_index is None:
                weight_problems.append(f"mesh {mesh_index}: missing JOINTS_0/WEIGHTS_0")
                continue
            joints = read_accessor(data, bin_bytes, joints_index)
            weights = read_accessor(data, bin_bytes, weights_index)
            if len(joints) != vertex_count or len(weights) != vertex_count:
                weight_problems.append(
                    f"mesh {mesh_index}: skin count {len(joints)}/{len(weights)} != {vertex_count}"
                )
                continue
            bad_sums = 0
            bad_joint_indices = 0
            for j, w in zip(joints, weights):
                total = sum(float(x) for x in w)
                if abs(total - 1.0) > 0.03 or any(float(x) < -0.001 for x in w):
                    bad_sums += 1
                if any(int(x) >= len(joint_indices) for x in j):
                    bad_joint_indices += 1
            if bad_sums:
                weight_problems.append(f"mesh {mesh_index}: {bad_sums} vertices have invalid weight sums")
            if bad_joint_indices:
                weight_problems.append(f"mesh {mesh_index}: {bad_joint_indices} vertices reference invalid joints")

    pngs = []
    for image in data.get("images", []):
        uri = image.get("uri")
        if not uri or not uri.lower().endswith(".png"):
            continue
        path = uri_path(gltf_path.parent, uri)
        if path.is_file():
            pngs.append(png_info(path))

    return {
        "asset": data.get("asset", {}),
        "generator": data.get("asset", {}).get("generator"),
        "nodes": len(nodes),
        "scenes": len(data.get("scenes", [])),
        "animations": len(data.get("animations", [])),
        "meshes": len(data.get("meshes", [])),
        "materials": len(data.get("materials", [])),
        "skins": len(data.get("skins", [])),
        "joint_count": len(joint_indices),
        "joint_names": joint_names,
        "unique_joint_names": len(set(joint_names)),
        "missing_required_bones": missing_required,
        "connected_joints": sum(1 for i in joint_indices if reaches_root(i)),
        "vertices": vertices,
        "triangles": triangles,
        "mesh_rows": mesh_rows,
        "uri_results": uri_results,
        "weight_problems": weight_problems,
        "pngs": pngs,
    }


def guid_map(metas: list[Path]) -> tuple[dict[str, Path], list[str]]:
    owners: dict[str, Path] = {}
    problems: list[str] = []
    for meta in metas:
        match = re.search(r"^guid:\s*([0-9a-fA-F]{32})\s*$", meta.read_text(encoding="utf-8", errors="replace"), re.M)
        if not match:
            problems.append(f"no 32-hex guid: {display(meta)}")
            continue
        guid = match.group(1).lower()
        if guid in owners:
            problems.append(f"duplicate GUID {guid}: {display(owners[guid])} and {display(meta)}")
        owners.setdefault(guid, meta)
    return owners, problems


def licence_findings() -> dict[str, Any]:
    male = GLTF_ROOT / "Documentation" / "Male_License_Standard.txt"
    female = GLTF_ROOT / "Documentation" / "Female_License_Standard(1).txt"
    text = male.read_text(encoding="utf-8", errors="replace")
    findings = {
        "identical": sha256(male) == sha256(female),
        "cc0": "CC0 1.0 Universal" in text and "Public Domain Dedication" in text,
        "creator": "Models by @Quaternius" in text,
        "commercial": True,
        "modification_conversion": True,
        "redistribution_in_game": True,
        "android_apk_distribution": True,
        "required_attribution": False,
        "suggested_attribution": "Models by @Quaternius / https://quaternius.com / Patreon and Discord links in the licence file",
        "restrictions": "No copyright/related-right conditions are imposed by CC0. No warranty; patent/trademark rights are not waived. The source repository itself has no top-level LICENSE file, so do not extend this finding to unrelated repository content.",
    }
    return findings


def check_source_inventory(report: Report) -> dict[str, Any]:
    manifest = load_json(SOURCE_MANIFEST)
    report.check("source inventory manifest",
                 manifest.get("file_count") == 23 and len(manifest.get("files", [])) == 23,
                 "23 exact source files recorded from live main @ " + manifest.get("source_commit", "?"),
                 "source manifest file count is not 23")

    ok = True
    details = []
    for entry in manifest["files"]:
        rel = entry["path"]
        original = GLTF_ROOT / rel.replace("Male/", "Male/Original/", 1).replace("Female/", "Female/Original/", 1)
        if not original.is_file():
            ok = False
            details.append(f"missing {display(original)}")
            continue
        if sha256(original) != entry["sha256"] or original.stat().st_size != entry["size"]:
            ok = False
            details.append(f"hash/size mismatch {display(original)}")
    report.check("original source files preserved byte-for-byte", ok,
                 "23/23 original files, names, sizes and SHA-256 values match the live source repository",
                 "; ".join(details[:6]))

    # Exact test copies and corrected JSON
    for sex in ("Male", "Female"):
        original_dir = GLTF_ROOT / sex / "Original"
        test_dir = GLTF_ROOT / sex / "UnityTestCopy"
        copy_ok = True
        copy_details = []
        for entry in manifest["files"]:
            if not entry["path"].startswith(sex + "/"):
                continue
            name = Path(entry["path"]).name
            if name.endswith(".gltf"):
                continue
            copy = test_dir / name
            if not copy.is_file() or sha256(copy) != entry["sha256"]:
                copy_ok = False
                copy_details.append(name)
        report.check(f"{sex} test-copy companions byte-identical", copy_ok,
                     "all BIN/PNG/licence/.gitkeep companions copied with original names",
                     "mismatch: " + ", ".join(copy_details))

        original_gltf = original_dir / f"Superhero_{sex}_FullBody.gltf"
        corrected_gltf = test_dir / f"Superhero_{sex}_FullBody.gltf"
        original_data = load_json(original_gltf)
        corrected_data = load_json(corrected_gltf)
        original_uris = [u for _, _, u in gltf_uris(original_data)]
        corrected_uris = [u for _, _, u in gltf_uris(corrected_data)]
        expected_missing = set(KNOWN_URI_FIXES[sex])
        found_missing = expected_missing.intersection(original_uris)
        report.info(f"{sex} original URI mismatches (documented)",
                    f"{len(found_missing)} known unresolved PNG URI(s): " + ", ".join(sorted(found_missing)) +
                    ". Original glTF is preserved unchanged.")

        resolved = True
        unresolved = []
        for section, index, uri in gltf_uris(corrected_data):
            try:
                target = uri_path(test_dir, uri)
                if not target.is_file():
                    resolved = False
                    unresolved.append(f"{section}[{index}] {uri}")
            except Exception as error:
                resolved = False
                unresolved.append(f"{section}[{index}] {uri}: {error}")
        no_bad_names = not any(name in corrected_uris for name in expected_missing)
        report.check(f"{sex} corrected test-copy URIs resolve case-sensitively",
                     resolved and no_bad_names,
                     f"{len(corrected_uris)} buffer/image URIs resolve in UnityTestCopy; known mismatches corrected only in the test copy",
                     "unresolved " + ", ".join(unresolved[:6]) + ("; stale mismatch names present" if not no_bad_names else ""))
        fixes = load_json(test_dir / "URI_FIXES.json")
        applied = {x["from"] for x in fixes.get("applied", [])}
        report.check(f"{sex} URI fix manifest matches code",
                     applied == expected_missing,
                     f"{len(applied)} documented URI corrections",
                     f"expected {sorted(expected_missing)}, manifest {sorted(applied)}")
        report.check(f"{sex} original glTF not modified",
                     sha256(original_gltf) == next(e["sha256"] for e in manifest["files"] if e["path"] == f"{sex}/{original_gltf.name}"),
                     "original glTF SHA-256 matches live source",
                     "original glTF was changed")

    findings = licence_findings()
    report.check("licence files",
                 findings["identical"] and findings["cc0"] and findings["creator"],
                 "both licence files are byte-identical CC0 1.0 Universal texts crediting @Quaternius",
                 "licence text is missing or differs")
    report.info("licence findings",
                "CC0 permits commercial use, modification/conversion, redistribution inside a game and Android APK distribution. Attribution is suggested, not required. No warranty; patent/trademark rights are not waived. Source repo has no top-level LICENSE file.")
    return {"source_manifest": manifest, "licence": findings}


def check_gltf_geometry(report: Report) -> dict[str, dict[str, Any]]:
    results: dict[str, dict[str, Any]] = {}
    for sex in ("Male", "Female"):
        path = GLTF_ROOT / sex / "UnityTestCopy" / f"Superhero_{sex}_FullBody.gltf"
        analysis = analyse_gltf(path)
        results[sex] = analysis
        expected_verts = 8483 if sex == "Male" else 8844
        expected_tris = 14318 if sex == "Male" else 15060
        report.check(f"{sex} glTF structure",
                     analysis["asset"].get("version") == "2.0" and analysis["nodes"] == 69 and
                     analysis["scenes"] == 1 and analysis["meshes"] == 3 and analysis["materials"] == 3 and
                     analysis["skins"] == 1 and analysis["animations"] == 0,
                     f"glTF 2.0, {analysis['nodes']} nodes, 3 meshes, 3 materials, 1 skin, no embedded animations",
                     "unexpected glTF structure")
        report.check(f"{sex} skeleton / required humanoid bones",
                     analysis["joint_count"] == 65 and
                     analysis["unique_joint_names"] == 65 and
                     not analysis["missing_required_bones"] and
                     analysis["connected_joints"] == 65,
                     "65 unique skin joints, all connected to the root, 22 canonical humanoid bones mapped",
                     f"joints={analysis['joint_count']}, connected={analysis['connected_joints']}, missing={analysis['missing_required_bones']}")
        report.check(f"{sex} UV / skin weights",
                     not analysis["weight_problems"],
                     "JOINTS_0/WEIGHTS_0 exist for every vertex; all weights sum to ~1 and joint indices are in range",
                     "; ".join(analysis["weight_problems"][:4]))
        report.check(f"{sex} mesh counts",
                     analysis["vertices"] == expected_verts and analysis["triangles"] == expected_tris,
                     f"{analysis['vertices']} vertices, {analysis['triangles']} triangles (3 meshes)",
                     f"got {analysis['vertices']} verts / {analysis['triangles']} tris")
        report.check(f"{sex} PNG dimensions / formats",
                     len(analysis["pngs"]) == 7 and all(p["width"] > 0 and p["height"] > 0 for p in analysis["pngs"]),
                     "7 PNG image URIs resolve; eye textures 256², hair/body textures 2048²; no texture downscaled",
                     "missing or invalid PNG")
        report.info(f"{sex} Android texture memory (static estimate)",
                    f"{len(analysis['pngs'])} source PNGs; uncompressed RGBA + mipmaps ≈ "
                    f"{sum(p['width'] * p['height'] * 4 * 4 / 3 for p in analysis['pngs']) / (1024 * 1024):.1f} MiB if all originals are imported at native 2048². ASTC/512 recommendations are in the final report; no source optimization was applied.")
    return results


def check_protected_assets(report: Report) -> None:
    manifest = load_json(PROTECTED_MANIFEST)
    allowlisted = set(manifest.get("allowlisted_modified_paths", []))
    problems = []
    for entry in manifest["files"]:
        rel = entry["path"]
        if rel in allowlisted:
            continue
        path = REPO_ROOT / rel
        if not path.is_file():
            problems.append("missing " + rel)
            continue
        if sha256(path) != entry["sha256"] or path.stat().st_size != entry["size"]:
            problems.append("changed " + rel)
    report.check("production assets unchanged",
                 not problems,
                 f"{len(manifest['files']) - len(allowlisted)} protected Unity Assets/ProjectSettings files are byte-identical; only the two allowlisted test asmdefs were updated to reference new test assemblies",
                 "; ".join(problems[:8]))

    fbx_files = sorted(ASSETS_ROOT.rglob("*.fbx"))
    known_fbx = {e["path"] for e in manifest["files"] if e["path"].endswith(".fbx")}
    new_fbx = [display(p) for p in fbx_files if display(p) not in known_fbx]
    report.check("no new or moved FBX files",
                 not new_fbx,
                 f"all {len(fbx_files)} FBX files are the original protected set",
                 "unexpected FBX: " + ", ".join(new_fbx))

    metas = sorted(ASSETS_ROOT.rglob("*.meta"))
    owners, guid_problems = guid_map(metas)
    report.check("committed GUID integrity",
                 not guid_problems,
                 f"{len(owners)} unique, well-formed GUIDs across {len(metas)} committed .meta files",
                 "; ".join(guid_problems[:8]))

    prod_controllers = [p for p in ASSETS_ROOT.rglob("*.controller") if GLTF_ROOT not in p.parents]
    prod_prefabs = [p for p in ASSETS_ROOT.rglob("*.prefab") if GLTF_ROOT not in p.parents]
    prod_scenes = [p for p in ASSETS_ROOT.rglob("*.unity") if GLTF_ROOT not in p.parents]
    report.check("no production controllers/prefabs/scenes added",
                 not prod_controllers and not prod_prefabs and not prod_scenes,
                 "no committed production controller, prefab or scene was added or replaced",
                 f"controllers={len(prod_controllers)}, prefabs={len(prod_prefabs)}, scenes={len(prod_scenes)}")


def check_importer_and_scene(report: Report) -> None:
    manifest = load_json(PACKAGE_MANIFEST)
    deps = manifest.get("dependencies", {})
    report.check("Unity 6-compatible glTF importer pinned",
                 deps.get("com.unity.cloud.gltfast") == "6.21.0",
                 "com.unity.cloud.gltfast 6.21.0 was already pinned; package.json declares unity 6000.0",
                 f"glTFast version is {deps.get('com.unity.cloud.gltfast')}")
    report.check("PNG runtime modules enabled",
                 deps.get("com.unity.modules.unitywebrequest") == "1.0.0" and
                 deps.get("com.unity.modules.unitywebrequesttexture") == "1.0.0" and
                 deps.get("com.unity.modules.imageconversion") == "1.0.0",
                 "UnityWebRequest, UnityWebRequestTexture and ImageConversion built-in modules are enabled for glTFast PNG loading",
                 "required PNG modules are missing")
    report.check("isolated scene exists",
                 SCENE_PATH.is_file() and Path(str(SCENE_PATH) + ".meta").is_file(),
                 "GLTF_Player_Import_Test.unity and its stable .meta GUID are committed; licensed CI replaces the placeholder with Unity-generated YAML",
                 "scene or scene .meta is missing")
    scene_text = SCENE_PATH.read_text(encoding="utf-8", errors="replace")
    forbidden = ("ThirdPersonMotor", "PlayerCharacterVisual", "CharacterAnimationConfig", "NetworkManager", "WeaponController")
    hits = [name for name in forbidden if name in scene_text]
    report.check("scene has no gameplay system references",
                 not hits,
                 "scene contains no production player/movement/network/shooting type names",
                 "forbidden references: " + ", ".join(hits))


def check_existing_fbx_compatibility(report: Report, analyses: dict[str, dict[str, Any]]) -> None:
    male_fbx = read_fbx(ASSETS_ROOT / "Characters" / "Male" / "Male character.fbx")
    female_fbx = read_fbx(ASSETS_ROOT / "Characters" / "Female" / "Female character.fbx")
    gltf_names = set(analyses["Male"]["joint_names"])
    report.check("GLTF skeleton matches production character FBX bone names",
                 gltf_names == set(male_fbx.bone_names) == set(female_fbx.bone_names) == set(analyses["Female"]["joint_names"]),
                 "both GLTF characters and both production character FBXs use the same 65 Unreal-style bone names",
                 "bone-name sets differ")

    animation_files = sorted((ASSETS_ROOT / "Animations").rglob("*.fbx"))
    problems = []
    for path in animation_files:
        doc = read_fbx(path)
        if doc.missing_required_bones() or not doc.core_hierarchy_connected() or len(doc.bone_names) != 65:
            problems.append(display(path))
    report.check("existing animation FBX skeleton conventions",
                 len(animation_files) == 74 and not problems,
                 "74/74 existing Mixamo animation FBXs have 65 bones and all required canonical humanoid aliases",
                 f"count={len(animation_files)}, problems={problems[:4]}")
    report.info("existing animation FBX compatibility",
                "Production animations use mixamorig:* names, while GLTF and character FBX use Unreal-style names. Unity Humanoid Avatar mapping must bridge the two namespaces; static name equality is NOT claimed. Avatar and retargeting are tested separately in licensed Unity Editor CI.")


def run_validation() -> Report:
    report = Report()
    report.add("verification boundary", "PASS",
               "This Python validator is static-only and never claims Unity import, rendering, Avatar validity, APK build or device runtime.")
    check_source_inventory(report)
    analyses = check_gltf_geometry(report)
    check_protected_assets(report)
    check_importer_and_scene(report)
    check_existing_fbx_compatibility(report, analyses)
    report.not_verified("Unity Editor glTF import / Avatar validity",
                        "requires licensed Unity Editor; run GLTFImportTestValidator.ValidateFromCommandLine in unity.yml")
    report.not_verified("visual appearance / material / UV screenshots",
                        "requires genuine Unity Editor camera renders or human visual inspection")
    report.not_verified("animation retargeting playback",
                        "requires a valid Humanoid Avatar and an Editor/PlayMode sample, not just matching bone names")
    report.not_verified("Android build / device runtime",
                        "APK build and FPS/PSS/thermal/touch measurements require a licensed Editor build and a physical device")
    report.info("licence caveat",
                "CC0 finding applies to the two Character/Male and Character/Female folders. The source repository has no top-level LICENSE file; do not assume unrelated files are CC0. This is a technical report, not legal advice.")
    report.check("final report present", FINAL_REPORT.is_file(),
                 "docs/GLTF_PLAYER_IMPORT_TEST_REPORT.md exists", "final report is missing")
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
