#!/usr/bin/env python3
"""Validate that this checkout is a Unity-only repository with a complete project.

Run with:
    python3 processing/unity/validate_structure.py

No Unity editor is required. The check protects the Unity project layout, the
GameCI workflow, the committed map resources, and repository hygiene while
rejecting the removed legacy Godot project tree and workflow.
"""

from __future__ import annotations

import json
import py_compile
import subprocess
import tempfile
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
UNITY = REPO_ROOT / "unity-project"
PROCESSING = REPO_ROOT / "processing"

UNITY_REQUIRED_DIRS = [
    "Assets/Editor",
    "Assets/Resources/Art/Textures",
    "Assets/Resources/Config",
    "Assets/Scripts/Camera",
    "Assets/Scripts/Core",
    "Assets/Scripts/Player",
    "Assets/Scripts/UI",
    "Assets/Scripts/World",
    "Assets/Shaders",
    "Assets/Tests/EditMode",
    "Assets/Tests/PlayMode",
    "Packages",
    "ProjectSettings",
]

UNITY_REQUIRED_FILES = [
    "Assets/Editor/AndroidBuild.cs",
    "Assets/Editor/BuildCommand.cs",
    "Assets/Editor/ProjectBootstrap.cs",
    "Assets/Editor/ProjectValidator.cs",
    "Assets/Resources/Config/siran_island_colors.bytes",
    "Assets/Resources/Config/siran_island_heights.bytes",
    "Assets/Resources/Config/siran_island_layout.json",
    "Assets/Resources/Config/siran_island_manifest.json",
    "Assets/Resources/Config/siran_island_river.bytes",
    "Assets/Resources/Config/siran_island_roads.bytes",
    "Assets/Resources/Config/siran_island_terrain.json",
    "Assets/Resources/Art/Textures/water_normal_256.png",
    "Assets/Scripts/Core/GameBootstrap.cs",
    "Assets/Scripts/Core/QualityProfile.cs",
    "Assets/Scripts/World/SiranChunkStreamer.cs",
    "Assets/Scripts/World/SiranIslandData.cs",
    "Assets/Scripts/World/SiranIslandTerrain.cs",
    "Assets/Scripts/World/SiranMapConfig.cs",
    "Assets/Scripts/World/SiranWorldBuilder.cs",
    "Assets/Shaders/SiranRadiusClip.shader",
    "Packages/manifest.json",
    "ProjectSettings/ProjectVersion.txt",
    "ProjectSettings/ProjectSettings.asset",
    "ProjectSettings/QualitySettings.asset",
    "ProjectSettings/TagManager.asset",
    "ProjectSettings/GraphicsSettings.asset",
    "README.md",
]

DOCS_REQUIRED = [
    "README.md",
    "CONTRIBUTING.md",
    "docs/ASSET_PIPELINE.md",
    "docs/UNISOC_BUDGET.md",
    "docs/architecture/UNITY_ARCHITECTURE.md",
    "docs/performance/ANDROID_PROFILES.md",
    "docs/asset-pipeline/BLENDER_PIPELINE.md",
    "docs/migration/BAKE_FORMAT.md",
]

LEGACY_ROOT_DIRS = (
    "addons", "assets", "export_presets", "resources", "scenes", "scripts", "tools",
)
LEGACY_ROOT_FILES = ("project.godot", "export_presets.cfg")
LEGACY_SUFFIXES = (".gd", ".gdshader", ".tscn", ".tres", ".res", ".import", ".scn")
FORBIDDEN_ARTIFACT_SUFFIXES = (".apk", ".aab", ".keystore", ".jks", ".ulf", ".alf", ".p12")


class Report:
    def __init__(self) -> None:
        self.failures: list[str] = []
        self.passes = 0

    def check(self, condition: bool, message: str) -> None:
        if condition:
            self.passes += 1
            print(f"  PASS  {message}")
        else:
            self.failures.append(message)
            print(f"  FAIL  {message}")
            print(f"::error::{message}")

    def summary(self) -> int:
        print(f"\n{self.passes} passed, {len(self.failures)} failed")
        return 1 if self.failures else 0


def repository_files() -> list[str]:
    """Return files present in the worktree, including untracked additions.

    `git ls-files` alone also returns unstaged deletions. Filtering those out
    keeps this local validator aligned with the filesystem the Unity project
    will actually use, before or after changes have been staged.
    """
    result = subprocess.run(
        ["git", "ls-files", "--cached", "--others", "--exclude-standard", "-z"],
        cwd=REPO_ROOT,
        check=True,
        capture_output=True,
    )
    return [
        item.decode("utf-8")
        for item in result.stdout.split(b"\0")
        if item and (REPO_ROOT / item.decode("utf-8")).is_file()
    ]


def main() -> int:
    report = Report()
    print("=" * 62)
    print(" Unity-only repository structure validation")
    print("=" * 62)

    files = repository_files()
    legacy_dirs = [name for name in LEGACY_ROOT_DIRS if (REPO_ROOT / name).exists()]
    legacy_files = [name for name in LEGACY_ROOT_FILES if (REPO_ROOT / name).exists()]
    legacy_tracked = [path for path in files if Path(path).suffix.lower() in LEGACY_SUFFIXES]
    report.check(not legacy_dirs and not legacy_files and not legacy_tracked,
                 "legacy engine project files are removed" +
                 (": " + ", ".join(legacy_dirs + legacy_files + legacy_tracked[:8]) if legacy_dirs or legacy_files or legacy_tracked else ""))

    legacy_ci = [".github/actions/setup-godot-android/action.yml"]
    legacy_ci_present = [path for path in legacy_ci if (REPO_ROOT / path).exists()]
    unity_workflow = REPO_ROOT / ".github/workflows/unity.yml"
    workflow_text = unity_workflow.read_text(encoding="utf-8") if unity_workflow.is_file() else ""
    gameci_ok = (
        "game-ci/unity-builder@v4" in workflow_text
        and "game-ci/unity-test-runner@v4" in workflow_text
        and "Build the Android APK" in workflow_text
        and "BH_BUILD_OUTPUT" in workflow_text
    )
    android_workflow = REPO_ROOT / ".github/workflows/build.yml"
    android_workflow_text = android_workflow.read_text(encoding="utf-8") if android_workflow.is_file() else ""
    build_command = UNITY / "Assets/Editor/BuildCommand.cs"
    build_command_text = build_command.read_text(encoding="utf-8") if build_command.is_file() else ""
    online_gameci_build_ok = (
        "buildalon/activate-unity-license@" in android_workflow_text
        and "game-ci/unity-builder@" in android_workflow_text
        and "projectPath: ${{ env.UNITY_PROJECT_PATH }}" in android_workflow_text
        and "UNITY_PROJECT_PATH: unity-project" in android_workflow_text
        and "buildMethod: BuildCommand.PerformBuild" in android_workflow_text
        and "buildName: BattleHorizons" in android_workflow_text
        and "name: BattleHorizons-Android-APK-${{ github.run_number }}" in android_workflow_text
        and "Battle Horizons: Last Survive (Build #${GITHUB_RUN_NUMBER})" in android_workflow_text
        and "publish-release:" in android_workflow_text
        and "needs.build-android.outputs.signing_configured == 'true'" in android_workflow_text
        and "public static void PerformBuild()" in build_command_text
        and "AndroidBuild.ConfigureValidateAndBuildApk()" in build_command_text
    )
    report.check(not legacy_ci_present and gameci_ok and online_gameci_build_ok,
                 "obsolete engine CI is removed and both Unity/GameCI workflows plus the Android build entry point are configured")

    missing_dirs = [path for path in UNITY_REQUIRED_DIRS if not (UNITY / path).is_dir()]
    report.check(not missing_dirs,
                 "required Unity project directories exist" + (": " + ", ".join(missing_dirs) if missing_dirs else ""))
    missing_files = [path for path in UNITY_REQUIRED_FILES if not (UNITY / path).is_file()]
    report.check(not missing_files,
                 "required Unity project files and island resources exist" + (": " + ", ".join(missing_files) if missing_files else ""))

    package_path = UNITY / "Packages/manifest.json"
    if package_path.is_file():
        dependencies = json.loads(package_path.read_text(encoding="utf-8")).get("dependencies", {})
        report.check("com.unity.render-pipelines.universal" in dependencies,
                     "URP is declared in the Unity package manifest")
        report.check(not any(key.startswith("com.unity.render-pipelines.high-definition") for key in dependencies),
                     "HDRP is not a project dependency")
    else:
        report.check(False, "package manifest is present for URP validation")
        report.check(False, "HDRP is not a project dependency")

    version_path = UNITY / "ProjectSettings/ProjectVersion.txt"
    version = version_path.read_text(encoding="utf-8") if version_path.is_file() else ""
    report.check("6000.0.32f1" in version, "Unity editor version is pinned to 6000.0.32f1")

    stream_source = (UNITY / "Assets/Scripts/World/SiranChunkStreamer.cs").read_text(encoding="utf-8") if (UNITY / "Assets/Scripts/World/SiranChunkStreamer.cs").is_file() else ""
    quality_source = (UNITY / "Assets/Scripts/Core/QualityProfile.cs").read_text(encoding="utf-8") if (UNITY / "Assets/Scripts/Core/QualityProfile.cs").is_file() else ""
    shader_source = (UNITY / "Assets/Shaders/SiranRadiusClip.shader").read_text(encoding="utf-8") if (UNITY / "Assets/Shaders/SiranRadiusClip.shader").is_file() else ""
    world_source = (UNITY / "Assets/Scripts/World/SiranWorldBuilder.cs").read_text(encoding="utf-8") if (UNITY / "Assets/Scripts/World/SiranWorldBuilder.cs").is_file() else ""
    report.check("DefaultStreamRadiusM = 50f" in stream_source and
                 "StreamRadius(QualityProfile profile) => 50f;" in quality_source,
                 "both streaming entry points enforce a fixed 50 m radius")
    report.check("_BHStreamRadius" in shader_source and "clip(" in shader_source and
                 "Terrain.drawHeightmap = false" in world_source and "MapSizeM * 2f" not in stream_source,
                 "world shader clips the 50 m circle and no un-clipped full-map Terrain fallback is enabled")

    missing_docs = [path for path in DOCS_REQUIRED if not (REPO_ROOT / path).is_file()]
    report.check(not missing_docs,
                 "Unity setup, architecture, performance and asset docs exist" + (": " + ", ".join(missing_docs) if missing_docs else ""))

    for folder in ("scripts", "generators", "validators", "tests"):
        path = PROCESSING / "blender" / folder
        report.check(path.is_dir(), f"Blender pipeline folder '{folder}' exists")

    python_files = sorted(PROCESSING.rglob("*.py"))
    syntax_errors: list[str] = []
    for path in python_files:
        try:
            with tempfile.NamedTemporaryFile(suffix=".pyc", delete=True) as handle:
                py_compile.compile(str(path), cfile=handle.name, doraise=True)
        except py_compile.PyCompileError as error:
            syntax_errors.append(f"{path.relative_to(REPO_ROOT)}: {error.msg.splitlines()[0]}")
    report.check(not syntax_errors,
                 f"all {len(python_files)} processing Python files compile" +
                 (": " + "; ".join(syntax_errors[:4]) if syntax_errors else ""))

    artifacts = [path for path in files if Path(path).suffix.lower() in FORBIDDEN_ARTIFACT_SUFFIXES]
    report.check(not artifacts,
                 "no APK, AAB, keystore or Unity license artifact is committed" +
                 (": " + ", ".join(artifacts) if artifacts else ""))

    return report.summary()


if __name__ == "__main__":
    raise SystemExit(main())
