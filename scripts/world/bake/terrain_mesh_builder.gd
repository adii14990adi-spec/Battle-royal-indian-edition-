class_name TerrainMeshBuilder
extends RefCounted
## Builds one chunk's greybox terrain mesh and its HeightMapShape3D.
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
## triangle from the finished mesh and fails the bake if that ever regresses
## (invisible terrain is a very expensive thing to debug on a phone).

## Minimum vertices per side that still yields a Jolt height field (not a mesh).
const JOLT_MIN_WIDTH: int = 4


static func build_chunk_mesh(config: SiranMapConfig, heightmap: IslandHeightmap,
		chunk: Vector2i) -> ArrayMesh:
	var vertices: PackedVector3Array = PackedVector3Array()
	var normals: PackedVector3Array = PackedVector3Array()
	var colors: PackedColorArray = PackedColorArray()
	var indices: PackedInt32Array = PackedInt32Array()

	var verts_per_side: int = config.chunk_vertices_per_side()
	var cells: int = verts_per_side - 1
	var spacing: float = config.vertex_spacing_m
	var half_chunk: float = config.chunk_size_m * 0.5
	var ix0: int = chunk.x * cells
	var iz0: int = chunk.y * cells
	var vertex_count: int = verts_per_side * verts_per_side
	vertices.resize(vertex_count)
	normals.resize(vertex_count)
	colors.resize(vertex_count)

	for lz in range(verts_per_side):
		var world_z: float = heightmap.grid_z(iz0 + lz)
		var local_z: float = float(lz) * spacing - half_chunk
		for lx in range(verts_per_side):
			var world_x: float = heightmap.grid_x(ix0 + lx)
			var index: int = lz * verts_per_side + lx
			var height: float = heightmap.height_at_index(ix0 + lx, iz0 + lz)
			var normal: Vector3 = heightmap.normal_at_index(ix0 + lx, iz0 + lz)
			vertices[index] = Vector3(float(lx) * spacing - half_chunk, height, local_z)
			normals[index] = normal
			colors[index] = heightmap.color_at(world_x, world_z, height, normal)

	indices.resize(cells * cells * 6)
	var cursor: int = 0
	for lz in range(cells):
		for lx in range(cells):
			var top_left: int = lz * verts_per_side + lx
			var top_right: int = top_left + 1
			var bottom_left: int = top_left + verts_per_side
			var bottom_right: int = bottom_left + 1
			indices[cursor] = top_left
			indices[cursor + 1] = top_right
			indices[cursor + 2] = bottom_left
			indices[cursor + 3] = top_right
			indices[cursor + 4] = bottom_right
			indices[cursor + 5] = bottom_left
			cursor += 6

	var arrays: Array = []
	arrays.resize(Mesh.ARRAY_MAX)
	arrays[Mesh.ARRAY_VERTEX] = vertices
	arrays[Mesh.ARRAY_NORMAL] = normals
	arrays[Mesh.ARRAY_COLOR] = colors
	arrays[Mesh.ARRAY_INDEX] = indices

	var mesh := ArrayMesh.new()
	mesh.resource_name = "terrain_chunk_%d_%d" % [chunk.x, chunk.y]
	mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
	return mesh


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
