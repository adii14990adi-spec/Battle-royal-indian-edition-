"""Generate the modular environment kit for Siran Island (Blender 4.2+).

    blender --background --python processing/blender/generators/generate_environment_kit.py

Writes into `processing/blender/output/environment/`:

    wall_4m.glb / wall_4m.fbx           modular concrete wall, 4 x 3 x 0.3 m
    floor_4m.glb / floor_4m.fbx         modular floor tile, 4 x 4 m
    doorway_4m.glb / doorway_4m.fbx     wall segment with a 1.1 x 2.1 m opening
    crate_1m.glb / crate_1m.fbx         loot crate, 1 m cube
    container_6m.glb / container_6m.fbx shipping container, 6 x 2.6 x 2.6 m
    factory_chimney.glb                 industrial stack, 3 m base, 18 m tall
    runway_light.glb                    runway edge light, 0.3 m
    graveyard_headstone.glb             headstone, 0.8 m
    dock_plank.glb                      dock decking panel, 4 x 2 m
    + one `*_collision` mesh per asset that needs collision
    + asset_manifest.json (sizes, triangle counts, hashes)

Why these assets: they are the modular pieces the five Siran Island zones need
(industrial north-west, urban centre, graveyard east, airfield south, docks
south-west — see docs/PHASE3_WORLD.md §2). Nothing here pretends to be a finished
art pass: they are greybox kit pieces with correct scale, origins, UVs, PBR
material parameters and collision, which is what the map needs next.

The generator is deterministic (no randomness), so a re-run produces identical
files and the manifest hashes are stable.
"""

from __future__ import annotations

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))

import bh_blender_common as bh  # noqa: E402

OUTPUT = bh.DEFAULT_OUTPUT_DIR / "environment"


def box(name: str, size, location=(0.0, 0.0, 0.0), material=None, origin="CENTER"):
    """A unit cube scaled to `size`, transforms applied, origin fixed."""
    bh.bpy.ops.mesh.primitive_cube_add(size=1.0, location=location)
    obj = bh.bpy.context.active_object
    obj.name = name
    obj.scale = size
    bh.apply_transforms(obj)
    bh.origin_to(obj, origin)
    if material is not None:
        bh.assign_material(obj, material)
    bh.unwrap_uvs(obj)
    return obj


def cylinder(name: str, radius: float, depth: float, location=(0.0, 0.0, 0.0), vertices: int = 12,
             material=None, origin="BOTTOM"):
    bh.bpy.ops.mesh.primitive_cylinder_add(radius=radius, depth=depth, vertices=vertices, location=location)
    obj = bh.bpy.context.active_object
    obj.name = name
    bh.apply_transforms(obj)
    bh.origin_to(obj, origin)
    if material is not None:
        bh.assign_material(obj, material)
    bh.unwrap_uvs(obj)
    return obj


def join(objects, name: str):
    bh.deselect_all()
    for obj in objects:
        obj.select_set(True)
    bh.bpy.context.view_layer.objects.active = objects[0]
    bh.bpy.ops.object.join()
    joined = bh.bpy.context.active_object
    joined.name = name
    return joined


def build_kit():
    bh.reset_scene()
    bh.set_scene_units()

    concrete = bh.make_pbr_material("bh_concrete", (0.42, 0.41, 0.39, 1.0), roughness=0.85)
    steel = bh.make_pbr_material("bh_steel", (0.35, 0.36, 0.38, 1.0), roughness=0.55, metallic=0.7)
    wood = bh.make_pbr_material("bh_wood", (0.36, 0.26, 0.17, 1.0), roughness=0.75)
    stone = bh.make_pbr_material("bh_stone", (0.33, 0.33, 0.31, 1.0), roughness=0.9)
    light_glass = bh.make_pbr_material("bh_lamp", (0.95, 0.85, 0.55, 1.0), roughness=0.3, metallic=0.2)

    assets = {}

    # --- Modular shell -------------------------------------------------------
    wall = box("wall_4m", (4.0, 0.3, 3.0), location=(0.0, 0.0, 1.5), material=concrete, origin="BOTTOM")
    assets["wall_4m"] = ([wall], True)

    floor = box("floor_4m", (4.0, 4.0, 0.2), location=(0.0, 0.0, -0.1), material=concrete)
    assets["floor_4m"] = ([floor], False)  # floors are part of the terrain collision

    # Doorway: two piers + a lintel, joined into one mesh so it is one draw call.
    left = box("door_pier_l", (1.45, 0.3, 3.0), location=(-1.275, 0.0, 1.5), material=concrete, origin="BOTTOM")
    right = box("door_pier_r", (1.45, 0.3, 3.0), location=(1.275, 0.0, 1.5), material=concrete, origin="BOTTOM")
    lintel = box("door_lintel", (4.0, 0.3, 0.9), location=(0.0, 0.0, 2.55), material=concrete, origin="BOTTOM")
    doorway = join([left, right, lintel], "doorway_4m")
    bh.origin_to(doorway, "CENTER")
    assets["doorway_4m"] = ([doorway], True)

    # --- Props ---------------------------------------------------------------
    crate = box("crate_1m", (1.0, 1.0, 1.0), location=(0.0, 0.0, 0.5), material=wood, origin="BOTTOM")
    assets["crate_1m"] = ([crate], True)

    container_body = box("container_6m", (6.0, 2.44, 2.59), location=(0.0, 0.0, 1.295),
                         material=steel, origin="BOTTOM")
    assets["container_6m"] = ([container_body], True)

    chimney = cylinder("factory_chimney", radius=1.5, depth=18.0, location=(0.0, 0.0, 9.0),
                       vertices=16, material=concrete)
    assets["factory_chimney"] = ([chimney], True)

    # --- Airfield / graveyard / docks ---------------------------------------
    base = cylinder("runway_light_base", radius=0.14, depth=0.12, location=(0.0, 0.0, 0.06),
                    vertices=8, material=steel, origin="CENTER")
    lamp = cylinder("runway_light_lamp", radius=0.09, depth=0.16, location=(0.0, 0.0, 0.2),
                    vertices=8, material=light_glass, origin="CENTER")
    runway_light = join([base, lamp], "runway_light")
    bh.origin_to(runway_light, "BOTTOM")
    assets["runway_light"] = ([runway_light], False)

    headstone_body = box("headstone_body", (0.12, 0.62, 0.78), location=(0.0, 0.0, 0.39),
                         material=stone, origin="BOTTOM")
    assets["graveyard_headstone"] = ([headstone_body], True)

    deck = box("dock_plank", (4.0, 2.0, 0.14), location=(0.0, 0.0, -0.07), material=wood)
    post_a = cylinder("dock_post_a", radius=0.12, depth=2.4, location=(-1.8, 0.9, -1.2),
                      vertices=8, material=wood)
    post_b = cylinder("dock_post_b", radius=0.12, depth=2.4, location=(1.8, 0.9, -1.2),
                      vertices=8, material=wood)
    post_c = cylinder("dock_post_c", radius=0.12, depth=2.4, location=(-1.8, -0.9, -1.2),
                      vertices=8, material=wood)
    post_d = cylinder("dock_post_d", radius=0.12, depth=2.4, location=(1.8, -0.9, -1.2),
                      vertices=8, material=wood)
    dock = join([deck, post_a, post_b, post_c, post_d], "dock_plank")
    bh.origin_to(dock, "CENTER")
    assets["dock_plank"] = ([dock], True)

    # --- Export + validate + manifest ---------------------------------------
    manifest_entries = []
    for name, (objects, needs_collision) in sorted(assets.items()):
        exported = list(objects)
        if needs_collision:
            collision = bh.build_box_collision(objects[0])
            exported.append(collision)

        glb = bh.export_glb(exported, OUTPUT / f"{name}.glb")
        fbx_objects = [obj for obj in exported if not obj.get("bh_collision")]
        fbx = bh.export_fbx(fbx_objects, OUTPUT / f"{name}.fbx")

        stats = bh.mesh_stats(objects[0])
        if stats["triangles"] > bh.PROP_VERTEX_BUDGET:
            raise bh.PipelineError(
                f"{name} is {stats['triangles']} triangles; the prop budget is {bh.PROP_VERTEX_BUDGET}"
            )
        if not stats["uv_layers"]:
            raise bh.PipelineError(f"{name} has no UV map; the asset pipeline requires one")

        entry = {
            "name": name,
            "category": "environment",
            "files": {
                "glb": {"path": str(glb.relative_to(bh.REPO_ROOT)), "sha256": bh.sha256_file(glb),
                        "bytes": glb.stat().st_size},
                "fbx": {"path": str(fbx.relative_to(bh.REPO_ROOT)), "sha256": bh.sha256_file(fbx),
                        "bytes": fbx.stat().st_size},
            },
            "mesh": stats,
            "collision": needs_collision,
            "materials": [slot.material.name for slot in objects[0].material_slots if slot.material],
            "units": "metres",
            "blender_version": bh.blender_version(),
        }
        manifest_entries.append(entry)
        bh.log("kit", f"{name}: {stats['triangles']} tris, {len(stats['materials'])} material(s), "
                      f"collision={'yes' if needs_collision else 'no'}")

    for entry in manifest_entries:
        bh.write_manifest(OUTPUT, entry)

    bh.log("kit", f"wrote {len(manifest_entries)} assets to {OUTPUT}")
    return manifest_entries


if __name__ == "__main__":
    bh.main_guard(build_kit)
