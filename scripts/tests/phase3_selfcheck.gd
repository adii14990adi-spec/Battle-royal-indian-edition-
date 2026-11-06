extends Node
## Phase 3 self-check — the gates that can be proven on a machine.
##
##     godot --headless --path . scenes/tests/phase3_selfcheck.tscn
##     godot --headless --path . scenes/tests/phase3_selfcheck.tscn -- --require-bake
##
## It answers the two questions a greybox can silently get wrong:
##
##   1. **Is the layout coherent?** chunk grid maths, 50 m streaming radius,
##      zone footprints, river/road topology — all pure data, no assets needed.
##   2. **Does the collision match the picture?** It instantiates a baked chunk
##      and fires rays straight down at 25 known mesh vertices, then compares the
##      hit height with the vertex height. That is the test that catches a
##      HeightMapShape3D scale/offset mistake — the failure mode where you walk
##      2 m above or below the ground and only notice on a phone.
##
## Exit code: 0 all gates passed (or bake skipped), 1 failures.

const CHUNK_SCENE_PATTERN: String = "res://scenes/world/terrain/chunk_%d_%d.tscn"
## Collision may differ from the rendered vertex by this much (metres) and still
## be correct: a ray landing on a triangle edge hits the interpolated surface.
const COLLISION_TOLERANCE_M: float = 0.6
## Rays fired per chunk, per axis (5x5 = 25).
const RAY_SAMPLES_PER_AXIS: int = 5
## Flat zones must not vary by more than this (metres).
const FLATNESS_TOLERANCE_M: float = 0.05

var _config: SiranMapConfig
var _checks: int = 0
var _failures: int = 0
var _skips: int = 0
var _require_bake: bool = false


func _ready() -> void:
	_require_bake = OS.get_cmdline_user_args().has("--require-bake")
	_config = SiranMapConfig.default_siran_island()
	print("[check] ---- Phase 3 self-check: %s ----" % _config.display_name)

	_check_config()
	_check_grid_math()
	_check_streaming_radius()
	_check_bake_summary()

	var baked: bool = _count_baked_chunks() == _config.chunk_count()
	if baked:
		await _check_baked_chunks()
	else:
		_skip_line("baked chunks", "only %d/%d on disk; run tools/bake_siran_island.sh"
				% [_count_baked_chunks(), _config.chunk_count()])
		if _require_bake:
			_fail_line("--require-bake was passed but the bake is incomplete")

	print("[check] %d checks, %d failed, %d skipped" % [_checks, _failures, _skips])
	if _failures > 0:
		print("[check] PHASE 3 SELF-CHECK FAILED")
		get_tree().quit(1)
	else:
		print("[check] Phase 3 self-check passed")
		get_tree().quit(0)


# --- Group 1: data only -------------------------------------------------------

func _check_config() -> void:
	var problems: PackedStringArray = _config.validate()
	_expect(problems.is_empty(), "config.validate() reports no problems",
			", ".join(problems))
	_expect(_config.zones.size() == 5, "5 zones (factory, docks, central, graveyard, airfield)",
			"found %d" % _config.zones.size())
	for required in [&"factory_island", &"konkan_docks", &"central_state",
			&"graveyard_center", &"aircraft_industry"]:
		_expect(_config.get_zone_by_id(required) != null,
				"zone '%s' exists" % required, "missing")
	_expect(_config.lake != null and _config.lake.contains(-0.0, 0.0) == false,
			"lake is defined and does not cover the map centre", "lake missing")
	_expect(_config.river != null and _config.river.points.size() >= 2,
			"river has a centreline", "river missing")
	_expect(_config.roads.size() >= 4, "at least 4 roads connect the zones",
			"found %d" % _config.roads.size())

	var airfield: IslandZone = _config.get_zone_by_id(&"aircraft_industry")
	if airfield != null:
		_expect(is_equal_approx(airfield.flatness, 1.0),
				"aircraft industry asks for perfect flatness", "flatness %f" % airfield.flatness)
		_expect(airfield.kind == IslandZone.Kind.AIRFIELD, "aircraft industry is an AIRFIELD",
				"wrong kind")
		_expect(_config.get_zone_at(airfield.center.x, airfield.center.y) == airfield,
				"airfield centre resolves back to the airfield zone", "zone lookup mismatch")


func _check_grid_math() -> void:
	_expect(_config.chunks_per_side() == 8, "8 chunks per side (4000 m / 500 m)",
			"%d" % _config.chunks_per_side())
	_expect(_config.chunk_count() == 64, "64 chunks total", "%d" % _config.chunk_count())
	_expect(_config.chunk_vertices_per_side() == 51,
			"51 vertices per chunk side (500 m / 10 m + 1)",
			"%d" % _config.chunk_vertices_per_side())
	_expect(_config.grid_vertices_per_axis() == 401, "401 global samples per axis",
			"%d" % _config.grid_vertices_per_axis())

	# Neighbouring chunks must share their edge samples exactly, or the seams
	# show up as cracks in the terrain.
	var cells: int = _config.chunk_cells_per_side()
	_expect(cells * _config.chunks_per_side() * _config.vertex_spacing_m
			== _config.map_size_m, "chunk cells tile the map exactly", "grid does not tile")

	var centre_chunk: Vector2i = _config.world_to_chunk(0.0, 0.0)
	_expect(centre_chunk == Vector2i(4, 4), "map centre is chunk (4, 4)", str(centre_chunk))
	var rect: Rect2 = _config.chunk_rect(centre_chunk)
	_expect(is_equal_approx(rect.size.x, 500.0) and is_equal_approx(rect.size.y, 500.0),
			"chunk rect is 500 x 500 m", str(rect.size))
	# 8 chunks per side means the world origin sits on a chunk *corner*, not in a
	# chunk centre. Asserting it stops someone "fixing" the grid to 9x9 later and
	# silently shifting every baked chunk by 250 m.
	_expect(is_equal_approx(rect.position.x, 0.0) and is_equal_approx(rect.position.y, 0.0),
			"chunk (4, 4) spans 0 .. 500 m (the origin is a chunk corner)",
			str(rect.position))
	_expect(_config.chunk_center(Vector2i(0, 0)).is_equal_approx(
			Vector3(-1750.0, 0.0, -1750.0)), "chunk (0, 0) is centred at -1750, -1750",
			str(_config.chunk_center(Vector2i(0, 0))))
	_expect(_config.world_to_chunk(-1999.0, -1999.0) == Vector2i(0, 0),
			"map corner maps to chunk (0, 0)", str(_config.world_to_chunk(-1999.0, -1999.0)))
	_expect(_config.world_to_chunk(1999.0, 1999.0) == Vector2i(7, 7),
			"far corner maps to chunk (7, 7)", str(_config.world_to_chunk(1999.0, 1999.0)))


func _check_streaming_radius() -> void:
	var manager := ChunkManager.new()
	manager.config = _config

	var centre: Array[Vector2i] = manager.compute_desired_for(Vector3(0.0, 40.0, 0.0))
	_expect(centre.size() == 9, "50 m radius + keep_ring 1 keeps a 3x3 chunk block",
			"%d chunks at the map centre" % centre.size())
	_expect(centre.has(Vector2i(4, 4)), "the chunk under the focus is always included", "absent")

	var corner: Array[Vector2i] = manager.compute_desired_for(Vector3(-1990.0, 40.0, -1990.0))
	_expect(corner.size() == 4, "a map corner clamps the ring to 4 chunks",
			"%d chunks at the corner" % corner.size())
	_expect(corner.has(Vector2i(0, 0)), "corner chunk (0, 0) is resident", "absent")

	manager.keep_ring = 0
	var bare: Array[Vector2i] = manager.compute_desired_for(Vector3(0.0, 40.0, 0.0))
	_expect(bare.size() == 1, "with keep_ring 0 and a 50 m radius only one chunk survives",
			"%d chunks" % bare.size())
	_expect(bare.has(Vector2i(4, 4)), "and it is the chunk under the focus", "absent")

	manager.stream_radius_m = 1200.0
	var wide: Array[Vector2i] = manager.compute_desired_for(Vector3(0.0, 40.0, 0.0))
	_expect(wide.size() > 9 and wide.size() <= 64, "a 1200 m radius widens the set",
			"%d chunks" % wide.size())
	_expect(not wide.has(Vector2i(0, 0)),
			"a chunk 2.8 km away stays unloaded even at 1200 m", "far chunk leaked in")

	# Nearest-first ordering is what makes the two-request budget useful.
	if wide.size() >= 2:
		var first: Vector3 = _config.chunk_center(wide[0])
		var last: Vector3 = _config.chunk_center(wide[wide.size() - 1])
		_expect(first.length() <= last.length(), "desired chunks are sorted nearest first",
				"%s before %s" % [str(first), str(last)])
	manager.free()


func _check_bake_summary() -> void:
	var path: String = IslandBaker.SUMMARY_PATH
	if not ResourceLoader.exists(path):
		_skip_line("bake summary", "not baked yet")
		return
	var summary: SiranBakeSummary = ResourceLoader.load(path) as SiranBakeSummary
	if summary == null:
		_fail_line("bake summary loads as SiranBakeSummary")
		return
	_expect(not summary.is_stale(_config), "bake is not stale",
			"bake v%d vs config v%d" % [summary.generator_version, _config.generator_version])
	_expect(summary.chunks_written == _config.chunk_count(), "summary counts 64 chunks",
			"%d" % summary.chunks_written)
	_expect(summary.min_height_m < _config.sea_level + 1.0, "terrain has sea floor below sea",
			"min %.1f" % summary.min_height_m)
	_expect(summary.max_height_m - summary.min_height_m > 60.0,
			"terrain has real relief, not a pancake (%.0f m)"
			% (summary.max_height_m - summary.min_height_m),
			"%.1f .. %.1f m" % [summary.min_height_m, summary.max_height_m])


# --- Group 2: baked chunk structure + physics --------------------------------

func _check_baked_chunks() -> void:
	var airfield: IslandZone = _config.get_zone_by_id(&"aircraft_industry")
	var chunk: Vector2i = _config.world_to_chunk(airfield.center.x, airfield.center.y)
	var path: String = CHUNK_SCENE_PATTERN % [chunk.x, chunk.y]
	_expect(FileAccess.file_exists(path), "runway chunk %s exists on disk" % str(chunk), path)

	var packed: PackedScene = ResourceLoader.load(path, "PackedScene") as PackedScene
	if packed == null:
		_fail_line("chunk scene loads")
		return
	var instance: Node3D = packed.instantiate() as Node3D
	add_child(instance)
	await get_tree().physics_frame
	await get_tree().physics_frame

	var terrain: MeshInstance3D = instance.get_node_or_null("Terrain") as MeshInstance3D
	_expect(terrain != null, "chunk has a 'Terrain' MeshInstance3D", "missing")
	var body: StaticBody3D = instance.get_node_or_null("Collision") as StaticBody3D
	_expect(body != null, "chunk has a 'Collision' StaticBody3D", "missing")
	if terrain == null or body == null:
		instance.queue_free()
		return

	_expect(terrain.mesh != null and terrain.mesh.get_surface_count() == 1,
			"terrain mesh has exactly one surface (one draw call per chunk)",
			"%d surfaces" % (0 if terrain.mesh == null else terrain.mesh.get_surface_count()))
	_expect(terrain.material_override != null, "terrain mesh has a material override", "none")
	_expect(TerrainMeshBuilder.verify_winding(terrain.mesh),
			"terrain triangles wind clockwise (visible with cull_back)", "flipped winding")
	_expect(_count_trimeshes(instance) == 0,
			"no ConcavePolygonShape3D anywhere in the chunk (HeightMapShape3D only)",
			"found %d" % _count_trimeshes(instance))

	var shape_node: CollisionShape3D = body.get_node_or_null("Ground") as CollisionShape3D
	_expect(shape_node != null, "collision body has a 'Ground' CollisionShape3D", "missing")
	if shape_node == null:
		instance.queue_free()
		return
	var shape: HeightMapShape3D = shape_node.shape as HeightMapShape3D
	_expect(shape != null, "ground collision uses HeightMapShape3D", "wrong shape type")
	if shape == null:
		instance.queue_free()
		return
	var wanted: int = _config.chunk_vertices_per_side()
	_expect(shape.map_width == wanted and shape.map_depth == wanted,
			"heightmap is %dx%d (square, Jolt keeps it a height field)"
			% [wanted, wanted], "%dx%d" % [shape.map_width, shape.map_depth])
	_expect(shape.map_data.size() == wanted * wanted, "heightmap data length matches",
			"%d" % shape.map_data.size())
	_expect(shape_node.scale.is_equal_approx(Vector3(_config.vertex_spacing_m, 1.0,
			_config.vertex_spacing_m)), "collision shape scales cells to 10 m",
			str(shape_node.scale))
	_expect(is_equal_approx(instance.global_position.x,
			_config.chunk_center(chunk).x), "chunk root sits at the chunk centre",
			str(instance.global_position))

	_check_collision_alignment(instance, terrain)
	_check_zone_flatness(terrain, airfield)

	instance.queue_free()
	await get_tree().process_frame


func _check_collision_alignment(instance: Node3D, terrain: MeshInstance3D) -> void:
	## The one test that would have caught a wrong HeightMapShape3D transform.
	var space: PhysicsDirectSpaceState3D = instance.get_world_3d().direct_space_state
	if space == null:
		_fail_line("physics space is available for raycasts")
		return
	var arrays: Array = terrain.mesh.surface_get_arrays(0)
	var vertices: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
	var per_side: int = _config.chunk_vertices_per_side()
	var worst_error: float = 0.0
	var hits: int = 0
	var expected_hits: int = RAY_SAMPLES_PER_AXIS * RAY_SAMPLES_PER_AXIS
	var first: int = per_side / 6
	var step: int = maxi(1, (per_side - 2 * first) / (RAY_SAMPLES_PER_AXIS - 1))
	for iy in range(RAY_SAMPLES_PER_AXIS):
		for ix in range(RAY_SAMPLES_PER_AXIS):
			var lx: int = mini(first + ix * step, per_side - 1)
			var lz: int = mini(first + iy * step, per_side - 1)
			var vertex: Vector3 = vertices[lz * per_side + lx]
			var world_vertex: Vector3 = terrain.global_transform * vertex
			var from: Vector3 = world_vertex + Vector3(0.0, 120.0, 0.0)
			var to: Vector3 = world_vertex - Vector3(0.0, 120.0, 0.0)
			var query := PhysicsRayQueryParameters3D.create(from, to, 1)
			query.collide_with_areas = false
			var hit: Dictionary = space.intersect_ray(query)
			if hit.is_empty():
				continue
			hits += 1
			worst_error = maxf(worst_error, absf((hit["position"] as Vector3).y - world_vertex.y))
	_expect(hits == expected_hits, "all %d rays hit the chunk collider" % expected_hits,
			"%d hits" % hits)
	_expect(worst_error <= COLLISION_TOLERANCE_M,
			"collision height matches the rendered vertex (worst %.2f m)" % worst_error,
			"worst error %.2f m" % worst_error)


func _check_zone_flatness(terrain: MeshInstance3D, zone: IslandZone) -> void:
	## Vertex colours are the zone markers, so the *mesh* must be flat where the
	## runway is — this catches a road or a noise pass flattening the wrong way.
	var arrays: Array = terrain.mesh.surface_get_arrays(0)
	var vertices: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
	var lowest: float = INF
	var highest: float = -INF
	var samples: int = 0
	for vertex in vertices:
		var world: Vector3 = terrain.global_transform * vertex
		if zone.signed_distance(world.x, world.z) > -30.0:
			continue
		lowest = minf(lowest, world.y)
		highest = maxf(highest, world.y)
		samples += 1
	_expect(samples > 0, "the airfield chunk contains airfield vertices",
			"no vertices inside the runway pad")
	if samples == 0:
		return
	_expect(highest - lowest <= FLATNESS_TOLERANCE_M,
			"aircraft industry is perfectly flat (spread %.3f m)" % (highest - lowest),
			"spread %.3f m over %d vertices" % [highest - lowest, samples])
	_expect(absf(highest - zone.flat_height) <= 0.2,
			"aircraft industry sits at its bake height (%.1f m)" % zone.flat_height,
			"%.1f m" % highest)


func _count_trimeshes(root: Node) -> int:
	var count: int = 0
	for child in root.get_children():
		count += _count_trimeshes(child)
	var shape_node: CollisionShape3D = root as CollisionShape3D
	if shape_node != null and shape_node.shape is ConcavePolygonShape3D:
		count += 1
	return count


func _count_baked_chunks() -> int:
	var found: int = 0
	var side: int = _config.chunks_per_side()
	for cz in range(side):
		for cx in range(side):
			if FileAccess.file_exists(CHUNK_SCENE_PATTERN % [cx, cz]):
				found += 1
	return found


# --- Assertions ---------------------------------------------------------------

func _expect(condition: bool, label: String, detail: String = "") -> void:
	_checks += 1
	if condition:
		print("  PASS  %s" % label)
	else:
		_failures += 1
		print("  FAIL  %s%s" % [label, "" if detail.is_empty() else "  (%s)" % detail])


func _fail_line(label: String) -> void:
	_checks += 1
	_failures += 1
	print("  FAIL  %s" % label)


func _skip_line(label: String, why: String) -> void:
	_skips += 1
	print("  SKIP  %s  (%s)" % [label, why])
