"""Shared helpers for the Battle Horizons Blender pipeline (Blender 4.2+).

Everything in `processing/blender/` is run with `blender --background --python`,
so it must not import anything outside the standard library + bpy, and must work
on a headless CI runner with no GPU.

Design rules (they are the contract the validators enforce):

* **Metres.** One Blender unit = one Unity unit = one metre. The Unity project
  imports models with `globalScale = 1`.
* **Z-up in Blender, Y-up in Unity.** Both glTF and FBX exporters default to
  "+Y up" which resolves this; the pipeline never applies a rotation by hand.
* **Transforms applied.** Objects leave the pipeline with scale (1,1,1) and an
  origin where a game object should rotate from (feet / pivot), not at the world
  origin. `apply_transforms()` is the only place allowed to touch them.
* **Deterministic.** No timestamps, no `random` without a seed, no dependence on
  the machine. Identical input must produce byte-identical output so CI can diff.
* **Collision is explicit.** Anything that needs collision gets a separate mesh
  object whose name ends in `_collision` (Unity importer convention used by
  `processing/unity/build_prefabs.py`), not a render mesh with a flag.
"""

from __future__ import annotations

import hashlib
import json
import math
import os
import sys
from pathlib import Path

try:
    import bpy  # type: ignore
    from mathutils import Euler, Matrix, Vector  # noqa: F401
except ImportError:  # pragma: no cover - allows the module to be imported by tests
    bpy = None

PIPELINE_ROOT = Path(__file__).resolve().parents[1]
REPO_ROOT = PIPELINE_ROOT.parents[1]
DEFAULT_OUTPUT_DIR = PIPELINE_ROOT / "output"
MANIFEST_NAME = "asset_manifest.json"

# Unity-friendly defaults, matching docs/ASSET_PIPELINE.md.
TEXTURE_MAX_SIZE = 512
PROP_VERTEX_BUDGET = 8000
CHARACTER_VERTEX_BUDGET = 40000
MAX_BONE_INFLUENCES = 4
COLLISION_SUFFIX = "_collision"


class PipelineError(RuntimeError):
    """Raised when an asset would violate the pipeline contract."""


def log(stage: str, message: str) -> None:
    print(f"[blender][{stage}] {message}", flush=True)


def require_blender() -> None:
    if bpy is None:
        raise PipelineError(
            "bpy is not available: run this script with `blender --background --python ...`"
        )


# --- Scene hygiene -----------------------------------------------------------

def reset_scene() -> None:
    """Delete everything, so a generator always starts from a known state."""
    require_blender()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    for block_collection in (
        bpy.data.meshes,
        bpy.data.materials,
        bpy.data.objects,
        bpy.data.armatures,
        bpy.data.actions,
        bpy.data.images,
    ):
        for block in list(block_collection):
            if block.users == 0:
                block_collection.remove(block)


def set_scene_units() -> None:
    """Metres, and a sane clip range for a 4 km map."""
    require_blender()
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.unit_settings.length_unit = "METERS"
    scene.render.fps = 30  # the Godot project's physics/render floor


def deselect_all() -> None:
    for obj in bpy.context.scene.objects:
        obj.select_set(False)


def activate(obj) -> None:
    if bpy is None:
        return
    deselect_all()
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


# --- Transform / origin ------------------------------------------------------

def apply_transforms(obj, location: bool = False) -> None:
    """Bake rotation and scale into the mesh; keep location unless asked."""
    require_blender()
    activate(obj)
    bpy.ops.object.transform_apply(location=location, rotation=True, scale=True)


def origin_to(obj, point: str = "BOTTOM") -> None:
    """Move the object origin to a predictable place.

    point: "BOTTOM" (feet / base), "CENTER", "CURSOR" (assumes the cursor is set),
    or a 3-tuple in world space.
    """
    require_blender()
    activate(obj)
    scene = bpy.context.scene
    if isinstance(point, str):
        if point == "CURSOR":
            bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
            return
        bpy.ops.object.origin_set(type="ORIGIN_GEOMETRY", center="BOUNDS")
        if point.upper() == "BOTTOM":
            # Drop the origin from the bounds centre to the lowest point.
            lowest = min((obj.matrix_world @ Vector(corner)).z for corner in obj.bound_box)
            scene.cursor.location = (obj.location.x, obj.location.y, lowest)
            bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
            scene.cursor.location = (0.0, 0.0, 0.0)
        return

    scene.cursor.location = tuple(point)
    bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
    scene.cursor.location = (0.0, 0.0, 0.0)


def world_bounds(obj) -> tuple:
    """(min, max) corners of an object's world-space bounds."""
    require_blender()
    corners = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    xs = [c.x for c in corners]
    ys = [c.y for c in corners]
    zs = [c.z for c in corners]
    return (min(xs), min(ys), min(zs)), (max(xs), max(ys), max(zs))


def drop_to_ground(obj) -> None:
    """Place the object so its lowest point is at z = 0."""
    (_, _, lowest), _ = world_bounds(obj)
    obj.location.z -= lowest
    bpy.context.view_layer.update()


# --- Geometry ----------------------------------------------------------------

def triangulate(obj) -> None:
    require_blender()
    activate(obj)
    modifier = obj.modifiers.new("Triangulate", "TRIANGULATE")
    modifier.quad_method = "BEAUTY"
    modifier.min_quad_angle = math.radians(1.0)
    bpy.ops.object.modifier_apply(modifier=modifier.name)


def unwrap_uvs(obj, angle_limit_degrees: float = 66.0, margin: float = 0.02) -> None:
    """Smart-project UVs. Preserves an existing UV map if the object has one with faces."""
    require_blender()
    mesh = obj.data
    if mesh.uv_layers and any(poly.loop_total for poly in mesh.polygons):
        existing = mesh.uv_layers.active
        if existing is not None and existing.name not in ("UVMap", ""):
            return
    activate(obj)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(angle_limit_degrees), island_margin=margin)
    bpy.ops.object.mode_set(mode="OBJECT")


def shade_smooth(obj, angle_degrees: float = 40.0) -> None:
    """Smooth shading with an auto-smooth split-normal modifier (Blender 4.2 API)."""
    require_blender()
    activate(obj)
    bpy.ops.object.shade_smooth()
    modifier = obj.modifiers.new("SmoothByAngle", "NODES")
    # 4.2 ships "Smooth by Angle" as a geometry-nodes asset; if it is not
    # available the object still renders correctly, just faceted.
    try:
        node_group = bpy.data.node_groups.get("Smooth by Angle")
        if node_group is not None:
            modifier.node_group = node_group
    except Exception:  # pragma: no cover - never fatal
        pass


def mesh_stats(obj) -> dict:
    require_blender()
    mesh = obj.data
    return {
        "vertices": len(mesh.vertices),
        "polygons": len(mesh.polygons),
        "triangles": sum(max(len(poly.vertices) - 2, 0) for poly in mesh.polygons),
        "uv_layers": [layer.name for layer in mesh.uv_layers],
        "materials": [slot.material.name if slot.material else None for slot in obj.material_slots],
    }


def vertex_influences(obj) -> dict:
    """Skinning weight statistics: how many bones actually influence each vertex."""
    require_blender()
    counts = {}
    for vertex in obj.data.vertices:
        influential = sum(1 for group in vertex.groups if group.weight > 1e-4)
        counts[influential] = counts.get(influential, 0) + 1
    return {str(key): value for key, value in sorted(counts.items())}


# --- Materials ---------------------------------------------------------------

def make_pbr_material(name: str, base_color=(0.8, 0.8, 0.8, 1.0), roughness: float = 0.7,
                     metallic: float = 0.0, texture_path: Path | None = None):
    """A Principled BSDF material — the node graph both glTF and FBX understand."""
    require_blender()
    material = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    for node in list(nodes):
        nodes.remove(node)

    output = nodes.new("ShaderNodeOutputMaterial")
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    shader.inputs["Base Color"].default_value = base_color
    shader.inputs["Roughness"].default_value = roughness
    shader.inputs["Metallic"].default_value = metallic
    material.node_tree.links.new(shader.outputs["BSDF"], output.inputs["Surface"])

    if texture_path is not None and Path(texture_path).exists():
        image = bpy.data.images.load(str(texture_path), check_existing=True)
        tex = nodes.new("ShaderNodeTexImage")
        tex.image = image
        tex.interpolation = "Linear"
        material.node_tree.links.new(tex.outputs["Color"], shader.inputs["Base Color"])

    material["bh_pbr"] = True
    return material


def assign_material(obj, material) -> None:
    require_blender()
    obj.data.materials.clear()
    obj.data.materials.append(material)


# --- Collision ---------------------------------------------------------------

def build_box_collision(obj, padding: float = 0.0, suffix: str = COLLISION_SUFFIX):
    """A 12-triangle box hull around `obj`, named `<obj>_collision`.

    Convex hulls and boxes are the only collision shapes that stay cheap on a
    4 GB device (`docs/UNISOC_BUDGET.md` physics rule), so this deliberately does
    not emit a trimesh.
    """
    require_blender()
    (min_x, min_y, min_z), (max_x, max_y, max_z) = world_bounds(obj)
    bpy.ops.mesh.primitive_cube_add(
        size=1.0,
        location=((min_x + max_x) * 0.5, (min_y + max_y) * 0.5, (min_z + max_z) * 0.5),
    )
    collision = bpy.context.active_object
    collision.name = obj.name + suffix
    collision.scale = (
        max(max_x - min_x + padding * 2.0, 0.01),
        max(max_y - min_y + padding * 2.0, 0.01),
        max(max_z - min_z + padding * 2.0, 0.01),
    )
    apply_transforms(collision, location=False)
    collision["bh_collision"] = True
    collision.hide_render = True
    return collision


# --- Export ------------------------------------------------------------------

def export_glb(objects, output_path: Path, *, apply_modifiers: bool = True) -> Path:
    require_blender()
    output_path = Path(output_path)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    deselect_all()
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0] if objects else None
    bpy.ops.export_scene.gltf(
        filepath=str(output_path),
        export_format="GLB",
        use_selection=True,
        export_apply=apply_modifiers,
        export_yup=True,
        export_texcoords=True,
        export_normals=True,
        export_materials="EXPORT",
        export_cameras=False,
        export_lights=False,
        export_animations=False,
    )
    return output_path


def export_fbx(objects, output_path: Path, *, include_animation: bool = False,
               mesh_only: bool = False) -> Path:
    require_blender()
    output_path = Path(output_path)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    deselect_all()
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0] if objects else None
    bpy.ops.export_scene.fbx(
        filepath=str(output_path),
        use_selection=True,
        object_types={"MESH", "ARMATURE"} if not mesh_only else {"MESH"},
        global_scale=1.0,
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z",
        axis_up="Y",
        bake_anim=include_animation,
        bake_anim_use_all_actions=False,
        bake_anim_use_nla_strips=True,
        add_leaf_bones=False,
        mesh_smooth_type="FACE",
        use_mesh_modifiers=True,
        use_tspace=False,
        path_mode="COPY",
        embed_textures=False,
    )
    return output_path


# --- Manifest ----------------------------------------------------------------

def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with Path(path).open("rb") as handle:
        for chunk in iter(lambda: handle.read(1 << 20), b""):
            digest.update(chunk)
    return digest.hexdigest()


def write_manifest(output_dir: Path, entry: dict) -> Path:
    """Merge one asset's entry into `output/asset_manifest.json`."""
    output_dir = Path(output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)
    manifest_path = output_dir / MANIFEST_NAME
    manifest = {"assets": []}
    if manifest_path.exists():
        try:
            manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        except json.JSONDecodeError:
            log("manifest", "existing manifest was unreadable; starting a new one")

    assets = [asset for asset in manifest.get("assets", []) if asset.get("name") != entry.get("name")]
    assets.append(entry)
    assets.sort(key=lambda asset: asset.get("name", ""))
    manifest["assets"] = assets
    manifest["generator"] = "processing/blender/scripts/bh_blender_common.py"
    manifest_path.write_text(json.dumps(manifest, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    return manifest_path


def blender_version() -> str:
    if bpy is None:
        return "unavailable"
    return ".".join(str(part) for part in bpy.app.version)


def main_guard(function) -> None:
    """Run a generator function, turning PipelineError into a non-zero exit."""
    try:
        function()
    except PipelineError as error:
        log("error", str(error))
        sys.exit(1)
