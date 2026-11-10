"""Generate the male and female base characters (Blender 4.2+).

    blender --background --python processing/blender/generators/generate_characters.py

Writes into `processing/blender/output/characters/`:

    bh_male_base.fbx    + .glb + bh_male_base.json
    bh_female_base.fbx  + .glb + bh_female_base.json

**Read this before using the output.** No shippable male or female source art is
committed under `unity-project/Assets/Art/Characters/`. This generator creates a
rig-test harness only; it is not a finished character replacement.

What this generator produces is a **rig-and-skinning test harness**: a properly
proportioned humanoid mesh (head, neck, chest, hips, two arms, two legs) skinned
to a 15-bone armature that follows the Mixamo/Unity humanoid naming convention,
with a small idle/walk/run pose set. It exists so that:

  * the armature hierarchy, bone names, skin weights (<= 4 influences/vertex) and
    export path can be validated end to end today,
  * dropping a real Mixamo (or Blender-authored) character into
    `unity-project/Assets/Art/Characters/` later is a file swap, not a pipeline rewrite.

It is deliberately **not** a finished art asset and must not be shipped as one.
`bh_*_base.json` records `"role": "rig_test_harness"` so downstream tooling can
tell the difference.
"""

from __future__ import annotations

import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))

import bh_blender_common as bh  # noqa: E402

OUTPUT = bh.DEFAULT_OUTPUT_DIR / "characters"

# Bone name -> (head, tail, parent). Names follow the Mixamo/Unity humanoid
# mapping ("mixamorig:Hips" etc. in Mixamo exports; Unity maps by these names).
BONES = [
    ("Hips", (0.0, 0.0, 0.95), (0.0, 0.0, 1.06), None),
    ("Spine", (0.0, 0.0, 1.06), (0.0, 0.0, 1.20), "Hips"),
    ("Spine1", (0.0, 0.0, 1.20), (0.0, 0.0, 1.34), "Spine"),
    ("Spine2", (0.0, 0.0, 1.34), (0.0, 0.0, 1.48), "Spine1"),
    ("Neck", (0.0, 0.0, 1.48), (0.0, 0.0, 1.58), "Spine2"),
    ("Head", (0.0, 0.0, 1.58), (0.0, 0.0, 1.76), "Neck"),
    ("LeftShoulder", (0.04, 0.0, 1.45), (0.17, 0.0, 1.45), "Spine2"),
    ("LeftArm", (0.17, 0.0, 1.45), (0.44, 0.0, 1.42), "LeftShoulder"),
    ("LeftForeArm", (0.44, 0.0, 1.42), (0.70, 0.0, 1.38), "LeftArm"),
    ("LeftHand", (0.70, 0.0, 1.38), (0.78, 0.0, 1.36), "LeftForeArm"),
    ("RightShoulder", (-0.04, 0.0, 1.45), (-0.17, 0.0, 1.45), "Spine2"),
    ("RightArm", (-0.17, 0.0, 1.45), (-0.44, 0.0, 1.42), "RightShoulder"),
    ("RightForeArm", (-0.44, 0.0, 1.42), (-0.70, 0.0, 1.38), "RightArm"),
    ("RightHand", (-0.70, 0.0, 1.38), (-0.78, 0.0, 1.36), "RightForeArm"),
    ("LeftUpLeg", (0.10, 0.0, 0.92), (0.11, 0.0, 0.50), "Hips"),
    ("LeftLeg", (0.11, 0.0, 0.50), (0.11, 0.0, 0.08), "LeftUpLeg"),
    ("LeftFoot", (0.11, 0.0, 0.08), (0.11, 0.15, 0.02), "LeftLeg"),
    ("RightUpLeg", (-0.10, 0.0, 0.92), (-0.11, 0.0, 0.50), "Hips"),
    ("RightLeg", (-0.11, 0.0, 0.50), (-0.11, 0.0, 0.08), "RightUpLeg"),
    ("RightFoot", (-0.11, 0.0, 0.08), (-0.11, 0.15, 0.02), "RightLeg"),
]

REQUIRED_HUMANOID_BONES = {
    "Hips", "Spine", "Spine1", "Spine2", "Neck", "Head",
    "LeftShoulder", "LeftArm", "LeftForeArm", "LeftHand",
    "RightShoulder", "RightArm", "RightForeArm", "RightHand",
    "LeftUpLeg", "LeftLeg", "LeftFoot", "RightUpLeg", "RightLeg", "RightFoot",
}

# Body-part capsules: (bone, radius, start_fraction, end_fraction, offset x)
SEGMENTS = [
    ("Head", 0.105, 0.0, 1.0, 0.0),
    ("Neck", 0.055, 0.0, 1.0, 0.0),
    ("Spine2", 0.145, 0.0, 1.0, 0.0),
    ("Spine1", 0.135, 0.0, 1.0, 0.0),
    ("Spine", 0.125, 0.0, 1.0, 0.0),
    ("Hips", 0.135, 0.0, 0.9, 0.0),
    ("LeftArm", 0.050, 0.0, 1.0, 0.0),
    ("LeftForeArm", 0.042, 0.0, 1.0, 0.0),
    ("LeftHand", 0.045, 0.0, 1.0, 0.0),
    ("RightArm", 0.050, 0.0, 1.0, 0.0),
    ("RightForeArm", 0.042, 0.0, 1.0, 0.0),
    ("RightHand", 0.045, 0.0, 1.0, 0.0),
    ("LeftUpLeg", 0.072, 0.0, 1.0, 0.0),
    ("LeftLeg", 0.058, 0.0, 1.0, 0.0),
    ("LeftFoot", 0.050, 0.0, 1.0, 0.0),
    ("RightUpLeg", 0.072, 0.0, 1.0, 0.0),
    ("RightLeg", 0.058, 0.0, 1.0, 0.0),
    ("RightFoot", 0.050, 0.0, 1.0, 0.0),
]

# Female proportions: same skeleton, slightly smaller shoulders/hips and a
# marginally shorter torso. Kept as a pure scale table so both characters share
# one skeleton definition (which is what makes animation retargeting sane).
FEMALE_SCALE = {
    "Hips": 0.94,
    "Spine2": 0.92,
    "LeftShoulder": 0.86,
    "RightShoulder": 0.86,
    "LeftArm": 0.95,
    "RightArm": 0.95,
    "LeftUpLeg": 0.97,
    "RightUpLeg": 0.97,
    "Head": 0.97,
}


def build_armature(name: str):
    bpy = bh.bpy
    armature_data = bpy.data.armatures.new(name + "Armature")
    armature_object = bpy.data.objects.new(name + "Armature", armature_data)
    bpy.context.collection.objects.link(armature_object)
    bh.activate(armature_object)
    bpy.ops.object.mode_set(mode="EDIT")

    created = {}
    for bone_name, head, tail, parent in BONES:
        bone = armature_data.edit_bones.new(bone_name)
        bone.head = head
        bone.tail = tail
        bone.use_deform = True
        if parent:
            bone.parent = created[parent]
            bone.use_connect = bone_name not in ("LeftShoulder", "RightShoulder",
                                                 "LeftUpLeg", "RightUpLeg")
        created[bone_name] = bone

    bpy.ops.object.mode_set(mode="OBJECT")
    bh.log("character", f"{name}: {len(created)} bones, hierarchy built")
    return armature_object


def build_body(name: str, scale_table: dict):
    """One capsule per bone, joined into a single skinned mesh."""
    bpy = bh.bpy
    skin = bh.make_pbr_material(f"bh_{name}_skin", (0.62, 0.47, 0.38, 1.0), roughness=0.65)
    cloth = bh.make_pbr_material(f"bh_{name}_cloth", (0.22, 0.27, 0.30, 1.0), roughness=0.85)
    parts = []

    for bone_name, radius, start, end, _ in SEGMENTS:
        head = next(b[1] for b in BONES if b[0] == bone_name)
        tail = next(b[2] for b in BONES if b[0] == bone_name)
        factor = scale_table.get(bone_name, 1.0)
        length = ((tail[0] - head[0]) ** 2 + (tail[1] - head[1]) ** 2 + (tail[2] - head[2]) ** 2) ** 0.5
        centre = tuple((head[i] + tail[i]) * 0.5 for i in range(3))
        bpy.ops.mesh.primitive_uv_sphere_add(segments=14, ring_count=8, radius=radius * factor,
                                             location=centre)
        part = bpy.context.active_object
        part.name = f"{name}_{bone_name}"
        # Stretch the sphere along the bone so it reads as a limb.
        part.scale = (1.0, 1.0, max(length * 0.72, 0.05))
        bh.apply_transforms(part)
        bh.assign_material(part, cloth if bone_name in ("Spine", "Spine1", "Spine2", "Hips") else skin)
        parts.append(part)

    bh.deselect_all()
    for part in parts:
        part.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    body = bpy.context.active_object
    body.name = name
    bh.triangulate(body)
    bh.unwrap_uvs(body)
    bh.origin_to(body, "BOTTOM")
    return body


def skin_to_armature(body, armature_object):
    """Automatic weights, then clamp to 4 influences and verify."""
    bpy = bh.bpy
    bh.deselect_all()
    body.select_set(True)
    armature_object.select_set(True)
    bpy.context.view_layer.objects.active = armature_object
    bpy.ops.object.parent_set(type="ARMATURE_AUTO")

    bh.activate(body)
    try:
        bpy.ops.object.vertex_group_limit_total(limit=bh.MAX_BONE_INFLUENCES)
    except RuntimeError:
        bh.log("character", "vertex_group_limit_total unavailable; weights left as generated")

    influences = bh.vertex_influences(body)
    worst = max((int(key) for key in influences), default=0)
    bh.log("character", f"{body.name}: weight histogram {influences} (max {worst} influences/vertex)")
    if worst > bh.MAX_BONE_INFLUENCES:
        raise bh.PipelineError(
            f"{body.name} has vertices with {worst} bone influences; the budget is {bh.MAX_BONE_INFLUENCES}"
        )
    return influences


def build_poses(body, armature_object):
    """A minimal idle/walk/run pose set, exported as NLA strips.

    Deliberately tiny: it proves the action -> NLA -> FBX path works so that real
    Mixamo clips can replace it without touching the exporter.
    """
    bpy = bh.bpy
    bh.activate(armature_object)
    bpy.ops.object.mode_set(mode="POSE")

    poses = {
        "bh_idle": {"LeftArm": 0.0, "RightArm": 0.0, "Spine1": 0.0},
        "bh_walk": {"LeftArm": 18.0, "RightArm": -18.0, "Spine1": 3.0},
        "bh_run": {"LeftArm": 42.0, "RightArm": -42.0, "Spine1": 8.0},
    }

    created = []
    for action_name, rotations in poses.items():
        action = bpy.data.actions.new(action_name)
        armature_object.animation_data_create()
        armature_object.animation_data.action = action
        for bone_name, degrees in rotations.items():
            pose_bone = armature_object.pose.bones.get(bone_name)
            if pose_bone is None:
                continue
            pose_bone.rotation_mode = "XYZ"
            pose_bone.rotation_euler = (0.0, 0.0, degrees * 0.017453292519943295)
            pose_bone.keyframe_insert(data_path="rotation_euler", frame=1)
            pose_bone.keyframe_insert(data_path="rotation_euler", frame=20)
        created.append(action)

    bpy.ops.object.mode_set(mode="OBJECT")
    bh.log("character", f"{body.name}: {len(created)} pose actions ({', '.join(a.name for a in created)})")
    return created


def build_character(name: str, scale_table: dict):
    body = build_body(name, scale_table)
    armature_object = build_armature(name)
    influences = skin_to_armature(body, armature_object)
    build_poses(body, armature_object)

    glb = bh.export_glb([body, armature_object], OUTPUT / f"{name}.glb")
    fbx = bh.export_fbx([body, armature_object], OUTPUT / f"{name}.fbx", include_animation=True)

    stats = bh.mesh_stats(body)
    if stats["triangles"] > bh.CHARACTER_VERTEX_BUDGET:
        raise bh.PipelineError(f"{name} is {stats['triangles']} triangles; budget {bh.CHARACTER_VERTEX_BUDGET}")
    if not stats["uv_layers"]:
        raise bh.PipelineError(f"{name} has no UV map")

    sidecar = {
        "name": name,
        "role": "rig_test_harness",
        "note": (
            "Procedural humanoid rig used to validate the armature/skinning/export "
            "path. NOT a finished character: no source character art is committed. "
            "Replace with a real Mixamo or Blender-authored FBX in "
            "unity-project/Assets/Art/Characters/ before shipping."
        ),
        "bones": [bone[0] for bone in BONES],
        "missing_required_bones": sorted(REQUIRED_HUMANOID_BONES - {bone[0] for bone in BONES}),
        "influences_per_vertex": influences,
        "max_influences": bh.MAX_BONE_INFLUENCES,
        "mesh": stats,
        "units": "metres",
        "height_m": round(max(b[2][2] for b in BONES) - min(b[1][2] for b in BONES), 3),
        "blender_version": bh.blender_version(),
        "files": {
            "glb": {"path": str(glb.relative_to(bh.REPO_ROOT)), "sha256": bh.sha256_file(glb)},
            "fbx": {"path": str(fbx.relative_to(bh.REPO_ROOT)), "sha256": bh.sha256_file(fbx)},
        },
    }

    sidecar_path = OUTPUT / f"{name}.json"
    sidecar_path.write_text(json.dumps(sidecar, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    bh.write_manifest(OUTPUT, {
        "name": name,
        "category": "characters",
        "role": "rig_test_harness",
        "files": {
            "glb": sidecar["files"]["glb"],
            "fbx": sidecar["files"]["fbx"],
            "sidecar": {"path": str(sidecar_path.relative_to(bh.REPO_ROOT))},
        },
        "mesh": stats,
        "bone_count": len(BONES),
        "units": "metres",
        "blender_version": bh.blender_version(),
    })
    return sidecar


def build_all():
    bh.reset_scene()
    bh.set_scene_units()

    OUTPUT.mkdir(parents=True, exist_ok=True)
    male = build_character("bh_male_base", {})
    bh.reset_scene()
    bh.set_scene_units()
    female = build_character("bh_female_base", FEMALE_SCALE)

    bh.log("character", f"wrote {male['name']} and {female['name']} to {OUTPUT}")
    return male, female


if __name__ == "__main__":
    bh.main_guard(build_all)
