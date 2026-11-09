#!/usr/bin/env python3
"""Validate the repository layout after the migration (no tools required).

    python3 processing/unity/validate_structure.py

This is the first gate in CI because it is the one that catches the destructive
mistake: a migration that quietly moved, deleted or rewrote the Godot project.

Checks:
  1. the Godot project is untouched: `project.godot`, the baked scene, the
     summary, both shaders and all its scripts are still where they were,
  2. the Godot project still parses as a project (required keys present) and the
     baked scene still contains 64 chunk nodes (the map was not damaged),
  3. the committed Godot workflow still exists and still exports the APK,
  4. the Unity layout matches the agreed tree (Assets/Art, Scripts/*, Settings,
     Scenes/MainMenu|Gameplay|Test, Packages, ProjectSettings),
  5. the docs tree exists (migration / architecture / performance / asset-pipeline),
  6. the Blender pipeline folders exist and every generator is importable Python,
  7. no build artefact (APK/AAB/keystore/licence) is committed anywhere,
  8. the Unity project contains no `Assets/Scenes/*.unity` that the setup tool
     would not regenerate (so a stale handmade scene cannot sneak in).
"""

from __future__ import annotations

import json
import py_compile
import re
import sys
import tempfile
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
GODOT = REPO_ROOT
UNITY = REPO_ROOT / "unity-project"
PROCESSING = REPO_ROOT / "processing"
DOCS = REPO_ROOT / "docs"


class Report:
    def __init__(self) -> None:
        self.failures: list[str] = []
        self.passes = 0

    def ok(self, message: str) -> None:
        self.passes += 1
        print(f"  PASS  {message}")

    def fail(self, message: str) -> None:
        self.failures.append(message)
        print(f"  FAIL  {message}")
        print(f"::error::{message}")

    def summary(self) -> int:
        print()
        print(f"{self.passes} passed, {len(self.failures)} failed")
        return 1 if self.failures else 0


GODOT_REQUIRED = [
    "project.godot",
    "export_presets.cfg",
    "scenes/core/boot.tscn",
    "scenes/world/siran_island.tscn",
    "scenes/world/siran_island_baked.tscn",
    "resources/world/baked/siran_island_summary.tres",
    "resources/world/baked/siran_river_surface.res",
    "resources/world/materials/terrain_greybox.tres",
    "resources/world/materials/road_greybox.tres",
    "assets/shaders/terrain_greybox.gdshader",
    "assets/shaders/water_surface.gdshader",
    "assets/textures/water_normal_256.png",
    "scripts/world/siran_map_config.gd",
    "scripts/world/bake/island_heightmap.gd",
    "scripts/world/bake/terrain_generator.gd",
    "scripts/world/bake/surface_mesh_builder.gd",
    "scripts/world/bake/terrain_mesh_builder.gd",
    "scripts/world/chunk_manager.gd",
    "scripts/autoload/perf_manager.gd",
    "tools/verify_phase1.sh",
    "tools/verify_phase3.sh",
    ".github/workflows/build.yml",
    ".github/actions/setup-godot-android/action.yml",
]

UNITY_REQUIRED_DIRS = [
    "Assets/Art/Characters", "Assets/Art/Environment", "Assets/Art/Buildings",
    "Assets/Art/Vegetation", "Assets/Art/Weapons", "Assets/Animations", "Assets/Audio",
    "Assets/Materials", "Assets/Prefabs", "Assets/Scenes/MainMenu",
    "Assets/Scenes/Gameplay", "Assets/Scenes/Test", "Assets/Scripts/Core",
    "Assets/Scripts/Player", "Assets/Scripts/Camera", "Assets/Scripts/Combat",
    "Assets/Scripts/Weapons", "Assets/Scripts/Inventory", "Assets/Scripts/Vehicles",
    "Assets/Scripts/World", "Assets/Scripts/UI", "Assets/Settings", "Assets/Resources",
    "Packages", "ProjectSettings",
]

UNITY_REQUIRED_FILES = [
    "Packages/manifest.json",
    "ProjectSettings/ProjectVersion.txt",
    "ProjectSettings/ProjectSettings.asset",
    "ProjectSettings/QualitySettings.asset",
    "ProjectSettings/TagManager.asset",
    "ProjectSettings/GraphicsSettings.asset",
    "Assets/Scripts/Core/GameBootstrap.cs",
    "Assets/Scripts/Core/QualityProfile.cs",
    "Assets/Scripts/World/SiranMapConfig.cs",
    "Assets/Scripts/World/SiranIslandData.cs",
    "Assets/Scripts/World/SiranIslandTerrain.cs",
    "Assets/Scripts/World/SiranIslandWater.cs",
    "Assets/Scripts/World/SiranChunkStreamer.cs",
    "Assets/Scripts/World/SiranWorldBuilder.cs",
    "Assets/Scripts/Player/ThirdPersonMotor.cs",
    "Assets/Scripts/Player/PlayerInputRouter.cs",
    "Assets/Scripts/Player/VirtualJoystick.cs",
    "Assets/Scripts/Camera/ThirdPersonCameraController.cs",
    "Assets/Editor/ProjectBootstrap.cs",
    "Assets/Editor/ProjectValidator.cs",
    "Assets/Resources/Config/siran_island_terrain.json",
    "Assets/Resources/Config/siran_island_heights.bytes",
    "Assets/Resources/Config/siran_island_colors.bytes",
    "Assets/Resources/Config/siran_island_roads.bytes",
    "Assets/Resources/Config/siran_island_layout.json",
    "README.md",
]

DOCS_REQUIRED = [
    "docs/migration/REPORT.md",
    "docs/migration/ASSET_AUDIT.md",
    "docs/migration/BAKE_FORMAT.md",
    "docs/architecture/UNITY_ARCHITECTURE.md",
    "docs/performance/ANDROID_PROFILES.md",
    "docs/asset-pipeline/BLENDER_PIPELINE.md",
]

FORBIDDEN_ARTEFACTS = ("*.apk", "*.aab", "*.keystore", "*.jks", "*.ulf", "*.alf", "*.p12")


def main() -> int:
    report = Report()
    print("=" * 62)
    print(" Repository structure validation (Godot preserved + Unity added)")
    print("=" * 62)

    # --- 1: the Godot project is untouched --------------------------------
    missing = [path for path in GODOT_REQUIRED if not (GODOT / path).exists()]
    if missing:
        report.fail("Godot project files are missing: " + ", ".join(missing))
    else:
        report.ok(f"all {len(GODOT_REQUIRED)} Godot project files are in place")

    godot_project = (GODOT / "project.godot").read_text(encoding="utf-8")
    for needle, description in {
        'config/name="Battle Horizons: Last Survival"': "project name",
        "renderer/rendering_method=\"gl_compatibility\"": "GLES3 compatibility renderer",
        'run/main_scene="res://scenes/core/boot.tscn"': "boot scene",
        "physics_ticks_per_second=30": "30 Hz physics",
    }.items():
        if needle in godot_project:
            report.ok(f"Godot setting intact: {description}")
        else:
            report.fail(f"Godot project.godot changed: {description} missing")

    baked = (GODOT / "scenes/world/siran_island_baked.tscn").read_text(encoding="utf-8")
    chunk_nodes = len(re.findall(r'\[node name="Chunk_\d+_\d+"', baked))
    if chunk_nodes == 64:
        report.ok("baked island scene still holds 64 chunk nodes")
    else:
        report.fail(f"baked island scene has {chunk_nodes} chunk nodes, expected 64")

    # --- 2: the Godot CI is still there -----------------------------------
    godot_workflow = (GODOT / ".github/workflows/build.yml").read_text(encoding="utf-8")
    for needle, description in {
        "Android_APK_Unisoc": "APK export preset",
        "tools/verify_phase1.sh": "Phase 1 gates",
        "tools/verify_phase3.sh": "Phase 3 gates",
        "--export-release": "release export",
    }.items():
        if needle in godot_workflow:
            report.ok(f"Godot workflow intact: {description}")
        else:
            report.fail(f"Godot workflow changed: {description} missing")

    # --- 3: Unity layout ---------------------------------------------------
    missing_dirs = [path for path in UNITY_REQUIRED_DIRS if not (UNITY / path).is_dir()]
    if missing_dirs:
        report.fail("Unity folders missing: " + ", ".join(missing_dirs))
    else:
        report.ok(f"all {len(UNITY_REQUIRED_DIRS)} Unity folders exist")

    missing_files = [path for path in UNITY_REQUIRED_FILES if not (UNITY / path).is_file()]
    if missing_files:
        report.fail("Unity files missing: " + ", ".join(missing_files))
    else:
        report.ok(f"all {len(UNITY_REQUIRED_FILES)} Unity files exist")

    manifest = json.loads((UNITY / "Packages/manifest.json").read_text(encoding="utf-8"))
    dependencies = manifest.get("dependencies", {})
    if "com.unity.render-pipelines.universal" in dependencies:
        report.ok("URP package pinned: " + dependencies["com.unity.render-pipelines.universal"])
    else:
        report.fail("Packages/manifest.json does not require URP")
    if any(key.startswith("com.unity.render-pipelines.high-definition") for key in dependencies):
        report.fail("HDRP must not be a dependency")
    else:
        report.ok("no HDRP dependency")

    terrain_json = json.loads(
        (UNITY / "Assets/Resources/Config/siran_island_terrain.json").read_text(encoding="utf-8"))
    if terrain_json.get("grid", {}).get("size") == 401:
        report.ok("converted terrain grid is 401 x 401 (matches the Godot bake)")
    else:
        report.fail("converted terrain grid is not 401 x 401: " + str(terrain_json.get("grid")))

    # --- 4: docs -----------------------------------------------------------
    missing_docs = [path for path in DOCS_REQUIRED if not (REPO_ROOT / path).exists()]
    if missing_docs:
        report.fail("documentation missing: " + ", ".join(missing_docs))
    else:
        report.ok(f"all {len(DOCS_REQUIRED)} required documents exist")

    # --- 5: Blender pipeline ----------------------------------------------
    for folder in ("scripts", "generators", "validators", "tests"):
        if (PROCESSING / "blender" / folder).is_dir():
            report.ok(f"blender pipeline folder '{folder}' exists")
        else:
            report.fail(f"blender pipeline folder '{folder}' is missing")

    python_files = sorted(PROCESSING.rglob("*.py"))
    broken = []
    for path in python_files:
        try:
            with tempfile.NamedTemporaryFile(suffix=".pyc", delete=True) as handle:
                py_compile.compile(str(path), cfile=handle.name, doraise=True)
        except py_compile.PyCompileError as error:
            broken.append(f"{path.name}: {error.msg.splitlines()[0] if error.msg else 'compile error'}")
    if broken:
        report.fail("python compile errors: " + "; ".join(broken))
    else:
        report.ok(f"{len(python_files)} pipeline scripts compile")

    # --- 6: no committed secrets or build artefacts -----------------------
    offenders = []
    for pattern in FORBIDDEN_ARTEFACTS:
        offenders.extend(str(path.relative_to(REPO_ROOT)) for path in REPO_ROOT.rglob(pattern))
    if offenders:
        report.fail("build artefacts/secrets committed: " + ", ".join(offenders[:10]))
    else:
        report.ok("no APK/AAB/keystore/licence committed")

    gitignore = (REPO_ROOT / ".gitignore").read_text(encoding="utf-8")
    # The ignore file uses bracket globs for case-insensitive folders, so the
    # check looks for the directory name rather than one exact line.
    for entry, description in {
        r"unity-project/[Ll]ibrary/": "Unity Library cache",
        r"unity-project/[Tt]emp/": "Unity Temp",
        r"unity-project/[Ll]ogs/": "Unity logs",
        "processing/blender/output/": "Blender output",
    }.items():
        if entry in gitignore:
            report.ok(f".gitignore covers {description}")
        else:
            report.fail(f".gitignore does not cover {description} ({entry})")

    # --- 7: no stale handmade scenes --------------------------------------
    expected_scenes = {"Boot", "MainMenu", "SiranIsland", "PlayerTest"}
    present = {path.stem for path in (UNITY / "Assets/Scenes").rglob("*.unity")}
    unexpected = present - expected_scenes
    if unexpected:
        report.fail("unexpected scene files in Assets/Scenes: " + ", ".join(sorted(unexpected)))
    else:
        report.ok(f"scene folder matches the setup tool ({len(present)} scene(s) present)")

    return report.summary()


if __name__ == "__main__":
    raise SystemExit(main())
