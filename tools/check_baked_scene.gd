extends SceneTree
## Structural verification of the committed baked map. **No physics, no frames.**
##
##     godot --headless --path . --script res://tools/check_baked_scene.gd
##
## Exit code 0 = the scene in Git is a real map, 1 = something is missing.
##
## This is the fast half of the Phase 3 self-check. `phase3_selfcheck.tscn` proves
## the *physics* matches the picture (rays against `HeightMapShape3D`), which
## needs a real scene tree and a Jolt space; this script proves the *content* —
## 64 chunk nodes, a mesh with a surface and the expected vertex count on every
## one, a square `HeightMapShape3D` per chunk, vertex colours present, mesh height
## equal to collision height, and no trimesh anywhere.
##
## CI runs it before the export so a broken map fails in seconds, with its own
## `FAIL` lines, instead of inside a four-minute APK build.

const BAKED_SCENE: String = TerrainGenerator.BAKED_SCENE_PATH
## Mesh vertex height vs collision height, metres. They are generated from the
## same samples, so anything above rounding is a real bug.
const HEIGHT_TOLERANCE_M: float = 0.01

var _checks: int = 0
var _failures: int = 0


func _init() -> void:
	var config: SiranMapConfig = SiranMapConfig.default_siran_island()
	print("[verify] ---- baked map structure: %s ----" % BAKED_SCENE)

	if not ResourceLoader.exists(BAKED_SCENE):
		_fail("%s is not in this build" % BAKED_SCENE)
		_verdict()
		return

	var packed: PackedScene = ResourceLoader.load(BAKED_SCENE, "PackedScene",
			ResourceLoader.CACHE_MODE_REUSE) as PackedScene
	if packed == null:
		_fail("%s does not load as a PackedScene" % BAKED_SCENE)
		_verdict()
		return

	var root: Node = packed.instantiate()
	if root == null:
		_fail("%s does not instantiate" % BAKED_SCENE)
		_verdict()
		return

	var verts_per_side: int = config.chunk_vertices_per_side()
	var expected_vertices: int = verts_per_side * verts_per_side
	var chunks: int = 0
	var bad_mesh: int = 0
	var bad_collision: int = 0
	var bad_heights: int = 0
	var no_colors: int = 0
	var trimeshes: int = 0
	var first_problem: String = ""

	for child in root.get_children():
		var chunk: Vector2i = _parse_chunk_name(String(child.name))
		if chunk.x < 0:
			continue
		chunks += 1
		var terrain: MeshInstance3D = child.get_node_or_null("Terrain") as MeshInstance3D
		if terrain == null or terrain.mesh == null or terrain.mesh.get_surface_count() != 1:
			bad_mesh += 1
			if first_problem.is_empty():
				first_problem = "%s has no single-surface Terrain mesh" % child.name
			continue
		var arrays: Array = terrain.mesh.surface_get_arrays(0)
		var vertices: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
		if vertices.size() != expected_vertices:
			bad_mesh += 1
			if first_problem.is_empty():
				first_problem = "%s has %d vertices, expected %d" % [
					child.name, vertices.size(), expected_vertices]
			continue
		var colors = arrays[Mesh.ARRAY_COLOR]
		if colors == null or (colors as PackedColorArray).size() != expected_vertices:
			no_colors += 1

		var shape: HeightMapShape3D = _heightmap_of(child)
		if shape == null:
			bad_collision += 1
			if first_problem.is_empty():
				first_problem = "%s has no HeightMapShape3D" % child.name
			continue
		if shape.map_width != verts_per_side or shape.map_depth != verts_per_side \
				or shape.map_data.size() != expected_vertices:
			bad_collision += 1
			if first_problem.is_empty():
				first_problem = "%s heightmap is %dx%d (%d samples)" % [
					child.name, shape.map_width, shape.map_depth, shape.map_data.size()]
			continue
		for i in range(vertices.size()):
			if absf(vertices[i].y - shape.map_data[i]) > HEIGHT_TOLERANCE_M:
				bad_heights += 1
				if first_problem.is_empty():
					first_problem = "%s mesh height differs from collision at vertex %d" % [
						child.name, i]
				break
		trimeshes += _count_trimeshes(child)

	_expect(chunks == config.chunk_count(), "baked scene holds %d chunk nodes"
			% config.chunk_count(), "found %d" % chunks)
	_expect(bad_mesh == 0, "every chunk has one Terrain mesh of %d vertices"
			% expected_vertices, "%d bad" % bad_mesh)
	_expect(bad_collision == 0, "every chunk has a %dx%d HeightMapShape3D"
			% [verts_per_side, verts_per_side], "%d bad" % bad_collision)
	_expect(bad_heights == 0, "mesh height matches collision height on every vertex",
			"%d chunk(s) differ" % bad_heights)
	_expect(no_colors == 0, "every vertex carries a colour (the greybox material)",
			"%d chunk(s) without" % no_colors)
	_expect(trimeshes == 0, "no ConcavePolygonShape3D anywhere", "found %d" % trimeshes)
	if not first_problem.is_empty():
		print("[verify]   first problem: %s" % first_problem)

	root.free()
	_verdict()


static func _parse_chunk_name(node_name: String) -> Vector2i:
	if not node_name.begins_with("Chunk_"):
		return Vector2i(-1, -1)
	var parts: PackedStringArray = node_name.trim_prefix("Chunk_").split("_")
	if parts.size() != 2 or not parts[0].is_valid_int() or not parts[1].is_valid_int():
		return Vector2i(-1, -1)
	return Vector2i(parts[0].to_int(), parts[1].to_int())


static func _heightmap_of(chunk: Node) -> HeightMapShape3D:
	var shape_node: CollisionShape3D = chunk.get_node_or_null("Collision/Ground") \
			as CollisionShape3D
	if shape_node == null:
		return null
	return shape_node.shape as HeightMapShape3D


static func _count_trimeshes(node: Node) -> int:
	var count: int = 0
	for child in node.get_children():
		count += _count_trimeshes(child)
	var shape_node := node as CollisionShape3D
	if shape_node != null and shape_node.shape is ConcavePolygonShape3D:
		count += 1
	return count


func _expect(condition: bool, label: String, detail: String = "") -> void:
	_checks += 1
	if condition:
		print("  ok    %s" % label)
		return
	_failures += 1
	print("  FAIL  %s%s" % [label, "" if detail.is_empty() else "  (%s)" % detail])


func _fail(label: String) -> void:
	_expect(false, label)


func _verdict() -> void:
	print("[verify] %d checks, %d failed" % [_checks, _failures])
	if _failures > 0:
		print("[verify] BAKED MAP IS BROKEN — the APK would ship an empty or unplayable world")
		quit(1)
	else:
		print("[verify] baked map is structurally sound")
		quit(0)
