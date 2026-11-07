class_name TerrainMeshBuilder
extends RefCounted
## Builds one chunk's `HeightMapShape3D` collision, and re-checks the mesh
## winding that `TerrainGenerator` produced.
##
## The terrain *mesh* is built by `TerrainGenerator` with `SurfaceTool`; only the
## collision and the winding assertion live here, so there is exactly one place
## per concern.
##
## Why HeightMapShape3D and not a trimesh: a `ConcavePolygonShape3D` built from
## the same ~5 000 triangles would cost a BVH build per chunk, ~90 KB of physics
## memory per chunk and a broadphase entry per triangle. `HeightMapShape3D`
## compiles down to Jolt's `HeightFieldShape` (one AABB tree, no triangle soup)
## which is the cheapest primitive the engine has for ground.
##
## Jolt constraint that shapes this file: Jolt only builds a true height field
## when `map_width == map_depth` and the width is at least 4 — otherwise it
## silently falls back to a triangle mesh. Both are asserted here.
##
## Winding: Godot treats clockwise-when-seen-from-the-front as front-facing, so a
## top face needs (a, a+1, a+w) in that order. `verify_winding()` reconstructs a
## triangle from the generated mesh and fails the generation if that ever
## regresses (invisible terrain is a very expensive thing to debug on a phone).

## Minimum vertices per side that still yields a Jolt height field (not a mesh).
const JOLT_MIN_WIDTH: int = 4


static func build_chunk_collision(config: SiranMapConfig,
		heightmap: IslandHeightmap, chunk: Vector2i) -> HeightMapShape3D:
	## Height data in the order HeightMapShape3D expects: `map_data[z * width + x]`,
	## one unit per cell, centred on the shape origin. The chunk node's
	## CollisionShape3D is scaled by `vertex_spacing_m` on X and Z to turn those
	## unit cells into the chunk's real footprint.
	var verts_per_side: int = config.chunk_vertices_per_side()
	var cells: int = verts_per_side - 1
	var ix0: int = chunk.x * cells
	var iz0: int = chunk.y * cells

	if verts_per_side < JOLT_MIN_WIDTH:
		push_error("chunk heightmap is %d wide; Jolt needs >= %d to build a height field"
				% [verts_per_side, JOLT_MIN_WIDTH])
	if verts_per_side != config.chunk_vertices_per_side():
		push_error("heightmap must be square for Jolt to use HeightFieldShape")

	var data := PackedFloat32Array()
	data.resize(verts_per_side * verts_per_side)
	for lz in range(verts_per_side):
		for lx in range(verts_per_side):
			data[lz * verts_per_side + lx] = heightmap.height_at_index(ix0 + lx, iz0 + lz)

	var shape := HeightMapShape3D.new()
	shape.map_width = verts_per_side
	shape.map_depth = verts_per_side
	shape.map_data = data
	shape.resource_name = "terrain_collision_%d_%d" % [chunk.x, chunk.y]
	return shape


static func verify_winding(mesh: ArrayMesh) -> bool:
	## Rebuilds the first quad of a finished chunk mesh and checks that its
	## normal points up. Cheap insurance: a flipped winding makes the whole
	## island invisible with `cull_back`, and only on the export build.
	if mesh == null or mesh.get_surface_count() == 0:
		return false
	var arrays: Array = mesh.surface_get_arrays(0)
	var vertices: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
	var indices: PackedInt32Array = arrays[Mesh.ARRAY_INDEX]
	if vertices.size() < 3 or indices.size() < 3:
		return false
	var a: Vector3 = vertices[indices[0]]
	var b: Vector3 = vertices[indices[1]]
	var c: Vector3 = vertices[indices[2]]
	var normal: Vector3 = (b - a).cross(c - a).normalized()
	# Godot front faces are clockwise, so the right-hand-rule normal points away
	# from the camera: an upward-facing top surface has a downward RHR normal.
	return normal.y < -0.5
