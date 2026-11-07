@tool
class_name TerrainGenerator
extends RefCounted
## Generates the whole Siran Island and saves it as **one** baked scene:
##
##     res://scenes/world/siran_island_baked.tscn
##
## This replaces the old "64 chunk scenes written by a CI bake" pipeline. The
## map now lives in the project as a normal, committed Godot scene, so the APK
## build never has to generate anything: `ChunkManager` loads this one
## PackedScene and streams its chunk children in and out.
##
## How to run it (it is a `@tool` script, so the editor can run it):
##
##   * **Editor** — open `tools/bake/terrain_generator_editor.gd` and press
##     File > Run (Ctrl+Shift+X). It calls this class and prints the result.
##   * **Terminal** —
##     `godot --headless --path . --script res://tools/bake/generate_siran_island.gd`
##
## What the saved scene contains, and why:
##
##     SiranIslandBaked (Node3D)          root, no transform
##       Chunk_<x>_<z> (Node3D)           one per 500 m chunk, at the chunk centre
##         Terrain (MeshInstance3D)       SurfaceTool-built ArrayMesh + vertex colours
##         Roads (MeshInstance3D)         only where a road crosses the chunk
##         Collision (StaticBody3D)
##           Ground (CollisionShape3D)    HeightMapShape3D, never a trimesh
##
## The heightfield comes from `IslandHeightmap` (FastNoiseLite: island mask,
## hills, ridged east side, lake, river, zone pads, road grading) and the road
## ribbons from `SurfaceMeshBuilder`. Nothing here runs in a shipped build.
##
## The river surface and the summary are written next to the scene rather than
## inside it: `siran_island.tscn` loads the river through `BakedExtras`, and the
## summary needs to be readable by `verify_phase3.sh` without unpacking a scene.

## The one file the map lives in. `ChunkManager` loads exactly this path.
const BAKED_SCENE_PATH: String = "res://scenes/world/siran_island_baked.tscn"
const SUMMARY_PATH: String = "res://resources/world/baked/siran_island_summary.tres"
const RIVER_MESH_PATH: String = "res://resources/world/baked/siran_river_surface.res"
const PREVIEW_PATH: String = "res://docs/images/siran_island_preview.png"
const TERRAIN_MATERIAL_PATH: String = "res://resources/world/materials/terrain_greybox.tres"
const ROAD_MATERIAL_PATH: String = "res://resources/world/materials/road_greybox.tres"

## Node name of one chunk inside the baked scene. `ChunkManager` parses the two
## numbers back out of it, so the format is part of the contract between the
## generator and the streamer — there is no second list of chunk paths anywhere.
const CHUNK_NODE_PATTERN: String = "Chunk_%d_%d"

## Edge of the top-down preview image, in pixels.
const PREVIEW_SIZE: int = 512
## Print a progress line every N chunks.
const PROGRESS_EVERY: int = 8

var config: SiranMapConfig
var heightmap: IslandHeightmap
var summary: SiranBakeSummary

var _warnings: PackedStringArray = PackedStringArray()


func _init(map_config: SiranMapConfig = null) -> void:
	config = map_config if map_config != null else SiranMapConfig.default_siran_island()
	summary = SiranBakeSummary.new()


# --- Entry point --------------------------------------------------------------

func generate() -> SiranBakeSummary:
	## Generates everything, writes the scene + river + summary + preview.
	## Returns the summary, or null if the config is broken or a write failed —
	## a half-written scene must never be left on disk looking like a good map.
	var started: int = Time.get_ticks_msec()
	print("[gen] ---- Siran Island generation v%d ----" % config.generator_version)
	print("[gen] %s" % config.describe())

	var materials: Array = _load_materials()
	if materials.is_empty():
		return null

	heightmap = IslandHeightmap.new(config)
	heightmap.generate()

	print("[gen] building road ribbons for %d road(s)" % config.roads.size())
	var road_meshes: Dictionary = SurfaceMeshBuilder.build_road_meshes(config, heightmap)

	if not _write_baked_scene(materials[0], materials[1], road_meshes):
		return null

	_write_river()
	_write_preview()
	_fill_summary(started)
	if ResourceSaver.save(summary, SUMMARY_PATH) != OK:
		push_error("[gen] could not write %s" % SUMMARY_PATH)
		return null

	_report(started)
	return summary


func _load_materials() -> Array:
	## Validation and material loading in one place, so `generate()` reads as the
	## pipeline it is.
	var problems: PackedStringArray = config.validate()
	if not problems.is_empty():
		for problem in problems:
			push_error("[gen] invalid config: %s" % problem)
		return []
	var terrain_material: Material = ResourceLoader.load(TERRAIN_MATERIAL_PATH)
	var road_material: Material = ResourceLoader.load(ROAD_MATERIAL_PATH)
	if terrain_material == null or road_material == null:
		push_error("[gen] missing material (%s / %s)"
				% [TERRAIN_MATERIAL_PATH, ROAD_MATERIAL_PATH])
		return []
	return [terrain_material, road_material]


func _write_baked_scene(terrain_material: Material, road_material: Material,
		road_meshes: Dictionary) -> bool:
	## Builds, packs and writes `siran_island_baked.tscn`, then re-reads it.
	var root: Node3D = _build_scene_root(terrain_material, road_material, road_meshes)
	if root == null:
		return false
	# `pack()` silently drops every node whose `owner` is not the scene root
	# (packed_scene.cpp: "discard nodes that do not belong to be processed"), so
	# the owners are set before packing, and the result is verified after.
	_set_owner_recursive(root, root)
	var scene := PackedScene.new()
	var pack_error: int = scene.pack(root)
	root.free()
	if pack_error != OK:
		push_error("[gen] could not pack the baked scene (error %d)" % pack_error)
		return false

	_ensure_dir(BAKED_SCENE_PATH.get_base_dir())
	if ResourceSaver.save(scene, BAKED_SCENE_PATH) != OK:
		push_error("[gen] could not write %s" % BAKED_SCENE_PATH)
		return false
	return _verify_saved_scene()


# --- Scene assembly -----------------------------------------------------------

func _build_scene_root(terrain_material: Material, road_material: Material,
		road_meshes: Dictionary) -> Node3D:
	var root := Node3D.new()
	root.name = "SiranIslandBaked"

	var side: int = config.chunks_per_side()
	var written: int = 0
	for cz in range(side):
		for cx in range(side):
			var chunk := Vector2i(cx, cz)
			var node: Node3D = _build_chunk_node(chunk, terrain_material, road_material,
					road_meshes)
			if node == null:
				root.free()
				return null
			root.add_child(node)
			written += 1
			summary.terrain_vertices_total += config.chunk_vertices_per_side() \
					* config.chunk_vertices_per_side()
			summary.terrain_triangles_total += (config.chunk_vertices_per_side() - 1) \
					* (config.chunk_vertices_per_side() - 1) * 2
			if written % PROGRESS_EVERY == 0 or written == config.chunk_count():
				print("[gen]   %d/%d chunks" % [written, config.chunk_count()])
	summary.chunks_written = written
	if written != config.chunk_count():
		push_error("[gen] built %d of %d chunks" % [written, config.chunk_count()])
		root.free()
		return null
	return root


func _build_chunk_node(chunk: Vector2i, terrain_material: Material, road_material: Material,
		road_meshes: Dictionary) -> Node3D:
	## The exact node layout `ChunkManager` and the Phase 3 self-check expect.
	var mesh: ArrayMesh = _build_chunk_mesh(chunk)
	if mesh == null or not TerrainMeshBuilder.verify_winding(mesh):
		push_error("[gen] chunk %s: bad mesh or flipped winding (invisible with cull_back)"
				% str(chunk))
		return null

	var node := Node3D.new()
	node.name = CHUNK_NODE_PATTERN % [chunk.x, chunk.y]
	node.position = config.chunk_center(chunk)

	var terrain := MeshInstance3D.new()
	terrain.name = "Terrain"
	terrain.mesh = mesh
	terrain.material_override = terrain_material
	terrain.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	terrain.gi_mode = GeometryInstance3D.GI_MODE_DISABLED
	node.add_child(terrain)

	if road_meshes.has(chunk):
		var roads := MeshInstance3D.new()
		roads.name = "Roads"
		roads.mesh = road_meshes[chunk]
		roads.material_override = road_material
		roads.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		roads.gi_mode = GeometryInstance3D.GI_MODE_DISABLED
		node.add_child(roads)

	var shape: HeightMapShape3D = TerrainMeshBuilder.build_chunk_collision(config, heightmap,
			chunk)
	if shape == null:
		push_error("[gen] chunk %s: no collision shape" % str(chunk))
		node.free()
		return null

	var body := StaticBody3D.new()
	body.name = "Collision"
	body.collision_layer = 1
	body.collision_mask = 0
	var shape_node := CollisionShape3D.new()
	shape_node.name = "Ground"
	shape_node.shape = shape
	# HeightMapShape3D cells are 1 m no matter what; this is what turns them into
	# `vertex_spacing_m` cells. X and Z must stay equal (Jolt requirement).
	shape_node.scale = Vector3(config.vertex_spacing_m, 1.0, config.vertex_spacing_m)
	body.add_child(shape_node)
	node.add_child(body)
	return node


func _build_chunk_mesh(chunk: Vector2i) -> ArrayMesh:
	## SurfaceTool, so the committed scene is built exactly the way the editor
	## builds one: one triangle list per chunk, explicit normals from the
	## heightfield, one vertex colour per vertex (the whole greybox "material").
	var verts_per_side: int = config.chunk_vertices_per_side()
	var cells: int = verts_per_side - 1
	var spacing: float = config.vertex_spacing_m
	var half_chunk: float = config.chunk_size_m * 0.5
	var ix0: int = chunk.x * cells
	var iz0: int = chunk.y * cells

	var builder := SurfaceTool.new()
	builder.begin(Mesh.PRIMITIVE_TRIANGLES)
	for lz in range(verts_per_side):
		var world_z: float = heightmap.grid_z(iz0 + lz)
		var local_z: float = float(lz) * spacing - half_chunk
		for lx in range(verts_per_side):
			var height: float = heightmap.height_at_index(ix0 + lx, iz0 + lz)
			var normal: Vector3 = heightmap.normal_at_index(ix0 + lx, iz0 + lz)
			var world_x: float = heightmap.grid_x(ix0 + lx)
			builder.set_normal(normal)
			builder.set_color(heightmap.color_at(world_x, world_z, height, normal))
			builder.add_vertex(Vector3(float(lx) * spacing - half_chunk, height, local_z))

	for lz in range(cells):
		for lx in range(cells):
			var top_left: int = lz * verts_per_side + lx
			var top_right: int = top_left + 1
			var bottom_left: int = top_left + verts_per_side
			var bottom_right: int = bottom_left + 1
			builder.add_index(top_left)
			builder.add_index(top_right)
			builder.add_index(bottom_left)
			builder.add_index(top_right)
			builder.add_index(bottom_right)
			builder.add_index(bottom_left)

	var mesh: ArrayMesh = builder.commit()
	if mesh == null:
		return null
	mesh.resource_name = "terrain_chunk_%d_%d" % [chunk.x, chunk.y]
	return mesh


static func _set_owner_recursive(node: Node, scene_root: Node) -> void:
	for child in node.get_children():
		child.owner = scene_root
		_set_owner_recursive(child, scene_root)


func _verify_saved_scene() -> bool:
	## Re-reads the file that was just written and counts what is actually in it.
	## A bake that "succeeded" but packed empty chunk nodes is the failure mode
	## this whole file exists to make impossible — and it is invisible until the
	## APK is on a phone.
	var packed: PackedScene = ResourceLoader.load(BAKED_SCENE_PATH, "PackedScene",
			ResourceLoader.CACHE_MODE_IGNORE) as PackedScene
	if packed == null:
		push_error("[gen] %s does not load back as a PackedScene" % BAKED_SCENE_PATH)
		return false
	var state: SceneState = packed.get_state()
	var chunks: int = 0
	var meshes: int = 0
	var shapes: int = 0
	for i in range(state.get_node_count()):
		var node_name: String = String(state.get_node_name(i))
		if node_name.begins_with("Chunk_"):
			chunks += 1
		if node_name == "Terrain" or node_name == "Roads":
			meshes += 1
		if node_name == "Ground":
			shapes += 1
	var ok: bool = chunks == config.chunk_count() and meshes >= config.chunk_count() \
			and shapes == config.chunk_count()
	if not ok:
		push_error("[gen] packed scene has %d chunks, %d meshes, %d collision shapes (expected %d/%d/%d)"
				% [chunks, meshes, shapes, config.chunk_count(), config.chunk_count(),
				config.chunk_count()])
	return ok


# --- River, preview, summary ---------------------------------------------------

func _write_river() -> void:
	var mesh: ArrayMesh = SurfaceMeshBuilder.build_river_mesh(config, heightmap)
	if mesh == null:
		_warn("no river mesh generated (no river in config)")
		return
	_ensure_dir(RIVER_MESH_PATH.get_base_dir())
	if ResourceSaver.save(mesh, RIVER_MESH_PATH) != OK:
		_warn("could not write %s" % RIVER_MESH_PATH)
		return
	summary.river_mesh_bytes = _file_size(RIVER_MESH_PATH)


func _write_preview() -> void:
	## Top-down picture of the layout. Handy in a PR diff: it is the only way to
	## review "did the island move?" without opening Godot.
	var image := Image.create(PREVIEW_SIZE, PREVIEW_SIZE, false, Image.FORMAT_RGB8)
	var half: float = config.half_extent()
	var sun := Vector3(0.45, 0.78, 0.44).normalized()
	var water_color := Color(0.16, 0.30, 0.42)
	for py in range(PREVIEW_SIZE):
		var z: float = -half + (float(py) + 0.5) / float(PREVIEW_SIZE) * config.map_size_m
		for px in range(PREVIEW_SIZE):
			var x: float = -half + (float(px) + 0.5) / float(PREVIEW_SIZE) * config.map_size_m
			var height: float = heightmap.sample_height(x, z)
			var normal: Vector3 = heightmap.sample_normal(x, z)
			var color: Color = heightmap.color_at(x, z, height, normal)
			var water: float = heightmap.water_surface_at(x, z)
			if height < water - 0.2:
				var depth: float = clampf((water - height) / 14.0, 0.0, 1.0)
				color = water_color.lerp(Color(0.06, 0.12, 0.20), depth)
			else:
				var light: float = clampf(normal.dot(sun) * 1.25, 0.25, 1.0)
				color = color * light
			image.set_pixel(px, py, color)
	_draw_chunk_grid(image)
	_ensure_dir(PREVIEW_PATH.get_base_dir())
	if image.save_png(PREVIEW_PATH) != OK:
		_warn("could not write %s" % PREVIEW_PATH)


func _draw_chunk_grid(image: Image) -> void:
	## Chunk borders in white: the streaming debug view in one picture.
	var side: int = config.chunks_per_side()
	var step: int = PREVIEW_SIZE / side
	for i in range(side + 1):
		var pixel: int = mini(i * step, PREVIEW_SIZE - 1)
		for k in range(PREVIEW_SIZE):
			image.set_pixel(pixel, k, Color(0.95, 0.95, 0.95))
			image.set_pixel(k, pixel, Color(0.95, 0.95, 0.95))


func _fill_summary(started: int) -> void:
	var range_m: Vector2 = heightmap.height_range()
	summary.display_name = config.display_name
	summary.generator_version = config.generator_version
	summary.noise_seed = config.noise_seed
	summary.baked_at_utc = Time.get_datetime_string_from_system(true)
	summary.bake_duration_ms = Time.get_ticks_msec() - started
	summary.map_size_m = config.map_size_m
	summary.chunk_size_m = config.chunk_size_m
	summary.vertex_spacing_m = config.vertex_spacing_m
	summary.chunk_count = config.chunk_count()
	summary.grid_vertices_per_axis = config.grid_vertices_per_axis()
	summary.min_height_m = range_m.x
	summary.max_height_m = range_m.y
	summary.sea_level = config.sea_level
	summary.spawn_ground_height_m = heightmap.sample_height(
			config.spawn_point.x, config.spawn_point.z)
	summary.zone_stats = _collect_zone_stats()
	summary.warnings = PackedStringArray(_warnings)
	summary.chunk_scene_bytes = _file_size(BAKED_SCENE_PATH)


func _collect_zone_stats() -> Dictionary:
	var stats: Dictionary = {}
	var spacing: float = config.vertex_spacing_m
	for zone in config.zones:
		if zone == null:
			continue
		var lowest: float = INF
		var highest: float = -INF
		var worst_deviation: float = 0.0
		var samples: int = 0
		var flooded: int = 0
		var x: float = zone.center.x - zone.size.x * 0.5
		while x <= zone.center.x + zone.size.x * 0.5:
			var z: float = zone.center.y - zone.size.y * 0.5
			while z <= zone.center.y + zone.size.y * 0.5:
				if zone.signed_distance(x, z) <= 0.0:
					var height: float = heightmap.sample_height(x, z)
					lowest = minf(lowest, height)
					highest = maxf(highest, height)
					worst_deviation = maxf(worst_deviation, absf(height - zone.flat_height))
					samples += 1
					if height < config.sea_level + 0.4:
						flooded += 1
				z += spacing
			x += spacing
		stats[String(zone.id)] = {
			"min_height": lowest if samples > 0 else 0.0,
			"max_height": highest if samples > 0 else 0.0,
			"flat_deviation": worst_deviation,
			"samples": samples,
			"flooded_samples": flooded,
		}
		if flooded > 0:
			_warn("zone '%s' has %d flooded sample(s) — is it supposed to be underwater?"
					% [zone.id, flooded])
		if zone.flatness >= 0.999 and worst_deviation > 0.05:
			_warn("zone '%s' asked for perfect flatness but varies by %.2f m"
					% [zone.id, worst_deviation])
	return stats


func _report(started: int) -> void:
	print("[gen] terrain %.1f .. %.1f m above sea level"
			% [summary.min_height_m, summary.max_height_m])
	for zone_id in summary.zone_stats:
		var stats: Dictionary = summary.zone_stats[zone_id]
		print("[gen]   zone %-20s %6.1f .. %6.1f m, flat deviation %.3f m"
				% [zone_id, stats["min_height"], stats["max_height"], stats["flat_deviation"]])
	for warning in _warnings:
		print("[gen]   warning: %s" % warning)
	print("[gen] %s" % summary.human_size())
	print("[gen] wrote %s (%d chunks) in %.1f s"
			% [BAKED_SCENE_PATH, summary.chunks_written,
			float(Time.get_ticks_msec() - started) / 1000.0])
	print("[gen] commit scenes/world/siran_island_baked.tscn and resources/world/baked/")


# --- Helpers -------------------------------------------------------------------

func _warn(message: String) -> void:
	push_warning("[gen] %s" % message)
	_warnings.append(message)


static func _ensure_dir(path: String) -> void:
	if DirAccess.dir_exists_absolute(path):
		return
	var error: int = DirAccess.make_dir_recursive_absolute(path)
	if error != OK:
		push_error("[gen] cannot create %s (error %d)" % [path, error])


static func _file_size(path: String) -> int:
	var file := FileAccess.open(path, FileAccess.READ)
	if file == null:
		return 0
	var size: int = file.get_length()
	file.close()
	return size
