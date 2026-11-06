class_name SurfaceMeshBuilder
extends RefCounted
## Bake-time builders for the two "flat" surfaces on top of the terrain:
## road ribbons (16 m of asphalt with painted edges, split per chunk) and the
## river's water ribbon (one mesh, descending surface).
##
## Both hug the *final* heightfield — they sample `IslandHeightmap.sample_height()`
## after every flattening pass has run — so asphalt and water can never float
## above or sink into the ground, which is the classic way a greybox ends up
## looking broken on a phone.
##
## Winding note: Godot front faces are clockwise, so a top-facing quad is
## emitted as (a, c, b) + (b, c, d) with `c` one step along the ribbon and `b`
## one step across it. `TerrainMeshBuilder.verify_winding()` checks the same
## convention on the terrain mesh.

## Columns across a road cross-section, as an offset from the asphalt centre in
## metres plus the colour for that column. Outer columns are the painted edge.
const ROAD_EDGE_WIDTH_M: float = 1.6

## Horizontal step between river ribbon cross-sections, in metres.
const RIVER_SAMPLE_STEP_M: float = 18.0


static func build_road_meshes(config: SiranMapConfig, heightmap: IslandHeightmap) -> Dictionary:
	## Returns `{ Vector2i chunk: ArrayMesh }` with one road mesh per chunk that
	## any road passes through. Each road segment belongs to exactly one chunk
	## (the one containing its midpoint), so no asphalt is duplicated.
	var per_chunk: Dictionary = {}
	for road in config.roads:
		if road == null or road.baked_points.size() < 2:
			continue
		var cross_sections: Array[PackedVector3Array] = _road_cross_sections(road, heightmap)
		if cross_sections.size() < 2:
			continue
		for i in range(cross_sections.size() - 1):
			var current: PackedVector3Array = cross_sections[i]
			var next: PackedVector3Array = cross_sections[i + 1]
			var midpoint := Vector2(
					(current[0].x + current[current.size() - 1].x) * 0.5,
					(current[0].z + current[current.size() - 1].z) * 0.5)
			var chunk: Vector2i = config.world_to_chunk(midpoint.x, midpoint.y)
			var builder: Dictionary = per_chunk.get(chunk, {})
			per_chunk[chunk] = _append_road_quad(builder, current, next)
	for chunk in per_chunk:
		per_chunk[chunk] = _finish(per_chunk[chunk], "road_chunk_%d_%d" % [chunk.x, chunk.y])
	return per_chunk


static func _road_cross_sections(road: IslandRoad,
		heightmap: IslandHeightmap) -> Array[PackedVector3Array]:
	## One cross-section per baked centreline point: `[left_edge, left_lane,
	## right_lane, right_edge]`, all at the same height so the asphalt reads flat.
	var sections: Array[PackedVector3Array] = []
	var half_width: float = road.width * 0.5
	var offsets: PackedFloat32Array = PackedFloat32Array([
		-half_width, -half_width + ROAD_EDGE_WIDTH_M,
		half_width - ROAD_EDGE_WIDTH_M, half_width,
	])
	for i in range(road.baked_points.size()):
		var point: Vector2 = road.baked_points[i]
		var direction: Vector2 = _direction_at(road.baked_points, i)
		var across: Vector2 = Vector2(-direction.y, direction.x)
		var ground: float = heightmap.sample_height(point.x, point.y) + road.surface_offset
		var section := PackedVector3Array()
		section.resize(offsets.size())
		for k in range(offsets.size()):
			var offset_point: Vector2 = point + across * offsets[k]
			section[k] = Vector3(offset_point.x, ground, offset_point.y)
		sections.append(section)
	return sections


static func _direction_at(points: PackedVector2Array, index: int) -> Vector2:
	var previous: Vector2 = points[maxi(index - 1, 0)]
	var following: Vector2 = points[mini(index + 1, points.size() - 1)]
	var direction: Vector2 = following - previous
	if direction.length_squared() < 0.0001:
		return Vector2.RIGHT
	return direction.normalized()


static func _append_road_quad(builder: Dictionary, current: PackedVector3Array,
		next: PackedVector3Array) -> Dictionary:
	var vertices: PackedVector3Array = builder.get("v", PackedVector3Array())
	var normals: PackedVector3Array = builder.get("n", PackedVector3Array())
	var colors: PackedColorArray = builder.get("c", PackedColorArray())
	var indices: PackedInt32Array = builder.get("i", PackedInt32Array())
	var base: int = vertices.size()
	for k in range(current.size()):
		vertices.append(current[k])
		normals.append(Vector3.UP)
		colors.append(_road_column_color(k, current.size()))
	for k in range(next.size()):
		vertices.append(next[k])
		normals.append(Vector3.UP)
		colors.append(_road_column_color(k, next.size()))
	var columns: int = current.size()
	for k in range(columns - 1):
		var a: int = base + k
		var b: int = base + k + 1
		var c: int = base + columns + k
		var d: int = base + columns + k + 1
		indices.append(a)
		indices.append(c)
		indices.append(b)
		indices.append(b)
		indices.append(c)
		indices.append(d)
	builder["v"] = vertices
	builder["n"] = normals
	builder["c"] = colors
	builder["i"] = indices
	return builder


static func _road_column_color(column: int, columns: int) -> Color:
	if column == 0 or column == columns - 1:
		return Color(0.78, 0.78, 0.74)
	return Color(0.22, 0.22, 0.24)


static func _finish(builder: Dictionary, mesh_name: String) -> ArrayMesh:
	var arrays: Array = []
	arrays.resize(Mesh.ARRAY_MAX)
	arrays[Mesh.ARRAY_VERTEX] = builder.get("v", PackedVector3Array())
	arrays[Mesh.ARRAY_NORMAL] = builder.get("n", PackedVector3Array())
	arrays[Mesh.ARRAY_COLOR] = builder.get("c", PackedColorArray())
	arrays[Mesh.ARRAY_INDEX] = builder.get("i", PackedInt32Array())
	var mesh := ArrayMesh.new()
	mesh.resource_name = mesh_name
	mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
	return mesh


# --- River surface ------------------------------------------------------------


static func build_river_mesh(config: SiranMapConfig,
		heightmap: IslandHeightmap) -> ArrayMesh:
	## One ribbon following the river's descending water surface. Deliberately
	## not split per chunk: it is a few hundred triangles and the streamer would
	## otherwise have to know about a sixth "chunk" type.
	var river: IslandRiver = config.river
	if river == null or river.points.size() < 2 or heightmap == null:
		return null
	var length: float = IslandShapeMath.polyline_length(river.points)
	var count: int = maxi(2, int(ceil(length / RIVER_SAMPLE_STEP_M)) + 1)
	var vertices := PackedVector3Array()
	var normals := PackedVector3Array()
	var indices := PackedInt32Array()
	vertices.resize(count * 2)
	normals.resize(count * 2)

	for i in range(count):
		var t: float = float(i) / float(count - 1)
		var centre: Vector2 = IslandShapeMath.point_at_distance(river.points, length * t)
		var tangent: Vector2 = _direction_at_distance(river.points, length, t)
		var across: Vector2 = Vector2(-tangent.y, tangent.x)
		var half_width: float = _river_half_width(river, t)
		var surface: float = river.surface_at(t)
		vertices[i * 2] = Vector3(centre.x - across.x * half_width, surface,
				centre.y - across.y * half_width)
		vertices[i * 2 + 1] = Vector3(centre.x + across.x * half_width, surface,
				centre.y + across.y * half_width)
		normals[i * 2] = Vector3.UP
		normals[i * 2 + 1] = Vector3.UP

	for i in range(count - 1):
		var a: int = i * 2
		var b: int = a + 1
		var c: int = a + 2
		var d: int = a + 3
		indices.append(a)
		indices.append(c)
		indices.append(b)
		indices.append(b)
		indices.append(c)
		indices.append(d)

	var arrays: Array = []
	arrays.resize(Mesh.ARRAY_MAX)
	arrays[Mesh.ARRAY_VERTEX] = vertices
	arrays[Mesh.ARRAY_NORMAL] = normals
	arrays[Mesh.ARRAY_INDEX] = indices
	var mesh := ArrayMesh.new()
	mesh.resource_name = "siran_river_surface"
	mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
	return mesh


static func _river_half_width(river: IslandRiver, t: float) -> float:
	if river.widths.is_empty():
		return 35.0
	var position: float = t * float(river.widths.size() - 1)
	return IslandShapeMath.sample_packed_float(river.widths, position) * 0.5


static func _direction_at_distance(points: PackedVector2Array, length: float,
		t: float) -> Vector2:
	var step: float = 6.0 / maxf(length, 1.0)
	var before: Vector2 = IslandShapeMath.point_at_distance(points, length * maxf(t - step, 0.0))
	var after: Vector2 = IslandShapeMath.point_at_distance(points, length * minf(t + step, 1.0))
	var direction: Vector2 = after - before
	if direction.length_squared() < 0.0001:
		return Vector2.RIGHT
	return direction.normalized()
