#!/usr/bin/env python3
"""Static checks for the Unity project that do not need Unity installed.

    python3 processing/unity/validate_csharp.py

Unity is not available in every environment (it needs a licence and a ~10 GB
install), so this script does the part of the job that does not need the editor:
it parses every C# file with tree-sitter, then checks the *cross-file* contracts a
compiler would catch and a reviewer would not:

  1. every `.cs` file parses (syntax),
  2. every `using BattleHorizons.X` resolves to a namespace the project declares,
  3. every `FindProperty("name")` in editor code names a field that some
     `[SerializeField]`/public field actually declares — this is the classic
     "silently null scene reference" bug,
  4. scene names used in code match the scene paths the setup tool creates,
  5. TagManager has the layers/tags the code asks for, and
     QualitySettings has the two profiles the code selects,
  6. every assembly definition is valid JSON and references only assemblies the
     project (or Unity) provides,
  7. every `.bytes` asset has a `.meta` with a unique GUID (missing metas are the
     usual cause of "Resources.Load returned null" after a clone),
  8. no `UnityEngine.Random` without a seed and no `System.Random` in gameplay
     code (determinism), no `FindObjectOfType` in per-frame code paths,
  9. the Android/URP settings the validator asserts in Unity are present in the
     committed project settings.

Exit code is the number of failures (capped at 1 for CI convenience).
"""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path

try:
    import tree_sitter  # noqa: F401
    import tree_sitter_c_sharp
    from tree_sitter import Language, Parser
except ImportError:  # pragma: no cover
    print("::error::tree-sitter/tree-sitter-c-sharp are not installed: "
          "pip install tree-sitter tree-sitter-c-sharp", file=sys.stderr)
    raise SystemExit(2)

REPO_ROOT = Path(__file__).resolve().parents[2]
UNITY = REPO_ROOT / "unity-project"
ASSETS = UNITY / "Assets"

LANGUAGE = Language(tree_sitter_c_sharp.language())


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


def parse(path: Path):
    return Parser(LANGUAGE).parse(path.read_bytes())


def walk(node):
    stack = [node]
    while stack:
        current = stack.pop()
        yield current
        stack.extend(current.children)


def text_of(path: Path, node) -> str:
    return path.read_bytes()[node.start_byte:node.end_byte].decode("utf-8", "replace")


def collect_namespaces_and_types(files: dict[Path, object]):
    namespaces: set[str] = set()
    types: dict[str, Path] = {}
    fields: dict[str, set[str]] = {}

    for path, tree in files.items():
        source = path.read_text(encoding="utf-8")
        for match in re.finditer(r"^namespace\s+([\w.]+)", source, re.M):
            namespaces.add(match.group(1))
        for match in re.finditer(r"\b(?:class|struct|interface|enum)\s+(\w+)", source):
            types[match.group(1)] = path

        for node in walk(tree.root_node):
            if node.type != "class_declaration":
                continue
            name_node = node.child_by_field_name("name")
            if name_node is None:
                continue
            class_name = text_of(path, name_node)
            body = node.child_by_field_name("body")
            if body is None:
                continue
            for member in body.children:
                if member.type != "field_declaration":
                    continue
                declaration = text_of(path, member)
                for identifier in re.finditer(r"(\w+)\s*(?:=|;)", declaration):
                    candidate = identifier.group(1)
                    fields.setdefault(class_name, set()).add(candidate)
                # Also record `public Vector2 Center;` style single names.
                names = re.findall(r"(\w+)\s*;", declaration)
                for candidate in names:
                    fields.setdefault(class_name, set()).add(candidate)
    return namespaces, types, fields


def main() -> int:
    report = Report()
    print("=" * 62)
    print(" Unity project static validation (no Unity required)")
    print("=" * 62)

    cs_files = sorted(ASSETS.rglob("*.cs"))
    if not cs_files:
        report.fail("no C# files found under unity-project/Assets")
        return report.summary()

    files: dict[Path, object] = {}
    syntax_errors = []
    for path in cs_files:
        tree = parse(path)
        files[path] = tree
        if tree.root_node.has_error:
            syntax_errors.append(str(path.relative_to(UNITY)))
    if syntax_errors:
        report.fail(f"{len(syntax_errors)} C# file(s) do not parse: {', '.join(syntax_errors)}")
    else:
        report.ok(f"{len(cs_files)} C# files parse cleanly")

    namespaces, types, fields = collect_namespaces_and_types(files)
    report.ok(f"{len(namespaces)} namespaces, {len(types)} types, {len(fields)} classes with fields")

    # --- 2: using directives resolve ---------------------------------------
    unresolved = []
    for path, _tree in files.items():
        source = path.read_text(encoding="utf-8")
        for match in re.finditer(r"^using\s+(BattleHorizons[\w.]*)\s*;", source, re.M):
            used = match.group(1)
            if not any(ns == used or ns.startswith(used + ".") or used.startswith(ns + ".")
                       for ns in namespaces):
                unresolved.append(f"{path.name}: using {used}")
    if unresolved:
        report.fail(f"{len(unresolved)} unresolved project namespace(s): " + "; ".join(unresolved[:5]))
    else:
        report.ok("every BattleHorizons.* using resolves to a declared namespace")

    # --- 3: FindProperty targets exist -------------------------------------
    all_field_names = set()
    for names in fields.values():
        all_field_names |= names
    missing_properties = []
    property_count = 0
    for path, _tree in files.items():
        if "/Editor/" not in str(path):
            continue
        source = path.read_text(encoding="utf-8")
        for match in re.finditer(r'FindProperty\("([^"]+)"\)', source):
            name = match.group(1)
            if name.startswith("m_"):
                # Unity's own SerializedObject property names on package assets
                # (URP pipeline asset, TerrainData, ...) are not C# fields.
                continue
            property_count += 1
            if name not in all_field_names:
                missing_properties.append(f"{path.name}: FindProperty(\"{name}\")")
    if missing_properties:
        report.fail("serialized field references that do not exist: " + "; ".join(missing_properties))
    else:
        report.ok(f"all {property_count} FindProperty() targets are real serialized fields")

    # --- 4: scene names match the setup tool -------------------------------
    bootstrap = (ASSETS / "Scripts" / "Core" / "GameBootstrap.cs").read_text(encoding="utf-8")
    declared_scenes = set(re.findall(r'public const string \w+ = "([^"]+)"', bootstrap))
    setup = (ASSETS / "Editor" / "ProjectBootstrap.cs").read_text(encoding="utf-8")
    # ScenePaths is built as `ScenesFolder + "/Gameplay/SiranIsland.unity"`, so both
    # the literal form and the concatenated form must be recognised.
    scene_files = set(re.findall(r'Scenes/(?:\S*?/)?(\w+)\.unity', setup))
    scene_files |= set(re.findall(r'\+\s*"/(?:\w+/)?(\w+)\.unity"', setup))
    missing_scene_names = {name for name in declared_scenes if name not in scene_files and name != "Boot"}
    if not scene_files:
        report.fail("ProjectBootstrap declares no scene paths")
    elif missing_scene_names:
        report.fail(f"GameBootstrap scene names without a build file: {sorted(missing_scene_names)}")
    else:
        report.ok(f"scene names {sorted(declared_scenes)} all exist in ProjectBootstrap ({len(scene_files)} files)")

    # --- 5: tags, layers, quality profiles ---------------------------------
    tag_manager = (UNITY / "ProjectSettings" / "TagManager.asset").read_text(encoding="utf-8")
    layers_block = tag_manager.split("layers:", 1)[1].split("m_SortingLayers:", 1)[0]
    layers = [line.strip()[2:] if line.strip().startswith("- ") else line.strip()
              for line in layers_block.strip().splitlines()]
    terrain_layer_index = 8
    if len(layers) > terrain_layer_index and layers[terrain_layer_index].strip() == "Terrain":
        report.ok(f"layer {terrain_layer_index} is 'Terrain' (matches SiranIslandTerrainBuilder)")
    else:
        report.fail(f"layer {terrain_layer_index} is not 'Terrain' (found "
                    f"{(layers[terrain_layer_index] if len(layers) > terrain_layer_index else 'nothing')!r})")

    for tag in ("Player", "Terrain"):
        if re.search(r"^  - %s$" % tag, tag_manager.split("tags:", 1)[1].split("layers:", 1)[0], re.M):
            report.ok(f"tag '{tag}' is defined")
        else:
            report.fail(f"tag '{tag}' is used in code but missing from TagManager.asset")

    quality = (UNITY / "ProjectSettings" / "QualitySettings.asset").read_text(encoding="utf-8")
    for profile in ("BattleHorizons_Low", "BattleHorizons_High"):
        if f"name: {profile}" in quality:
            report.ok(f"quality profile '{profile}' exists")
        else:
            report.fail(f"quality profile '{profile}' is missing from QualitySettings.asset")

    if "m_PerPlatformDefaultQuality:" in quality and "Android: 0" in quality:
        report.ok("Android defaults to the Low profile index")
    else:
        report.fail("Android does not default to quality index 0")

    # --- 6: assembly definitions ------------------------------------------
    unity_assemblies = {
        "Unity.InputSystem", "UnityEngine.TestRunner", "UnityEditor.TestRunner",
        "Unity.RenderPipelines.Universal.Runtime", "Unity.RenderPipelines.Core.Runtime",
        "Unity.RenderPipelines.Universal.Editor", "UnityEngine.UI",
    }
    project_assemblies = set()
    asmdefs = {}
    for path in ASSETS.rglob("*.asmdef"):
        try:
            data = json.loads(path.read_text(encoding="utf-8"))
        except json.JSONDecodeError as error:
            report.fail(f"{path.name} is not valid JSON ({error})")
            continue
        asmdefs[path.name] = data
        project_assemblies.add(data["name"])

    bad_references = []
    for name, data in asmdefs.items():
        for reference in data.get("references", []):
            if reference in project_assemblies or reference in unity_assemblies:
                continue
            bad_references.append(f"{name} -> {reference}")
    if bad_references:
        report.fail("assembly references that do not resolve: " + "; ".join(bad_references))
    else:
        report.ok(f"{len(asmdefs)} assembly definitions, all references resolve")

    # The editor assembly must be Editor-only and the runtime one must not be.
    editor_asm = asmdefs.get("BattleHorizons.Editor.asmdef", {})
    if editor_asm.get("includePlatforms") == ["Editor"]:
        report.ok("editor assembly is Editor-only")
    else:
        report.fail("BattleHorizons.Editor.asmdef must set includePlatforms to [\"Editor\"]")
    runtime_asm = asmdefs.get("BattleHorizons.Runtime.asmdef", {})
    if runtime_asm.get("includePlatforms") == []:
        report.ok("runtime assembly targets all platforms")
    else:
        report.fail("BattleHorizons.Runtime.asmdef must include all platforms")

    # --- 7: metas for binary resources ------------------------------------
    missing_meta = []
    guids: dict[str, Path] = {}
    duplicate_guids = []
    for path in sorted(ASSETS.rglob("*")):
        if path.is_dir() or path.suffix == ".meta":
            continue
        meta = path.with_suffix(path.suffix + ".meta")
        if path.suffix == ".bytes" and not meta.exists():
            missing_meta.append(path.name)
            continue
        if meta.exists():
            match = re.search(r"^guid: (\w+)", meta.read_text(encoding="utf-8"), re.M)
            if match:
                if match.group(1) in guids:
                    duplicate_guids.append(f"{path.name} vs {guids[match.group(1)].name}")
                guids[match.group(1)] = path
    if missing_meta:
        report.fail("binary resources without a .meta: " + ", ".join(missing_meta))
    else:
        report.ok("every .bytes resource has a .meta (Resources.Load will find them)")
    if duplicate_guids:
        report.fail("duplicate asset GUIDs: " + ", ".join(duplicate_guids))
    else:
        report.ok(f"{len(guids)} asset GUID(s), all unique")

    # --- 8: determinism / per-frame hygiene -------------------------------
    suspicious = []
    for path, _tree in files.items():
        source = path.read_text(encoding="utf-8")
        if path.name.endswith("Tests.cs") or "/Tests/" in str(path):
            continue
        if re.search(r"new System\.Random\(\)", source):
            suspicious.append(f"{path.name}: unseeded System.Random")
        if re.search(r"UnityEngine\.Random\.(value|insideUnitSphere|Range)\b", source) and "InitState" not in source:
            suspicious.append(f"{path.name}: UnityEngine.Random without InitState")
        # FindObjectOfType inside Update() is a per-frame scene scan.
        if "void Update()" in source and re.search(r"Find(First)?ObjectOfType<", source):
            update_body = source.split("void Update()", 1)[1][:1200]
            if "Find" in update_body and "ObjectOfType" in update_body:
                suspicious.append(f"{path.name}: FindObjectOfType inside Update()")
    if suspicious:
        report.fail("hygiene: " + "; ".join(suspicious))
    else:
        report.ok("no unseeded randomness and no per-frame FindObjectOfType")

    # --- 9: committed project settings -----------------------------------
    project_settings = (UNITY / "ProjectSettings" / "ProjectSettings.asset").read_text(encoding="utf-8")
    expectations = {
        "AndroidTargetArchitectures: 3": "ARM64-only Android build",
        "AndroidMinSdkVersion: 26": "Android 8.0 floor",
        "ForceInternetPermission: 0": "offline game (no INTERNET permission)",
        "activeInputHandler: 1": "Input System package (not the legacy manager)",
        "m_ActiveColorSpace: 1": "linear colour space",
        "productName: Battle Horizons": "product name",
        "com.aurastudios.battlehorizons": "Android application id",
        "allowedAutorotateToPortrait: 0": "landscape-only",
    }
    for needle, description in expectations.items():
        if needle in project_settings:
            report.ok(f"project setting: {description}")
        else:
            report.fail(f"project setting missing: {description} (expected '{needle}')")

    graphics = (UNITY / "ProjectSettings" / "GraphicsSettings.asset").read_text(encoding="utf-8")
    if "m_LightsUseLinearIntensity: 1" in graphics:
        report.ok("linear light intensity (matches the baked greybox palette)")
    else:
        report.fail("GraphicsSettings does not enable linear light intensity")

    time_settings = (UNITY / "ProjectSettings" / "TimeManager.asset").read_text(encoding="utf-8")
    if "Fixed Timestep: 0.033333335" in time_settings:
        report.ok("physics at 30 Hz (port of physics_ticks_per_second=30)")
    else:
        report.fail("physics timestep is not 30 Hz")

    manifest = json.loads((UNITY / "Packages" / "manifest.json").read_text(encoding="utf-8"))
    if "com.unity.render-pipelines.high-definition" in manifest["dependencies"]:
        report.fail("HDRP must not be a dependency (Android-first URP project)")
    else:
        report.ok("no HDRP dependency")

    version = (UNITY / "ProjectSettings" / "ProjectVersion.txt").read_text(encoding="utf-8")
    if re.search(r"m_EditorVersion: 6000\.\d+\.\d+f\d+", version):
        report.ok("project version is Unity 6 (6000.x)")
    else:
        report.fail("ProjectVersion.txt is not a Unity 6 version: " + version.strip())

    return report.summary()


if __name__ == "__main__":
    raise SystemExit(main())
