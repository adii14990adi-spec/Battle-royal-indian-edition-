class_name ChunkManager
extends Node3D
## Streams Siran Island out of ONE committed baked scene.
##
##     res://scenes/world/siran_island_baked.tscn
##
## The scene is generated once by `TerrainGenerator` (editor: File > Run on
## tools/bake/terrain_generator_editor.gd) and committed with the rest of the
## project. This node never builds geometry: it loads that PackedScene on a
## worker thread, parks its 64 `Chunk_<x>_<z>` children, and puts the ones near
## the player back into the tree.
##
## Why this shape (and not 64 separate scene files):
##   * one file cannot be "half packed" — either the map is in the APK or the
##     boot button says so, which is the failure mode this project kept hitting;
##   * one threaded request instead of 64: the parse cost of the whole island is
##     paid once, off the main thread, instead of stalling per chunk on eMMC;
##   * streaming still decides what is *drawn and simulated*: a chunk outside the
##     radius leaves the tree (no draw call, no collider in the broadphase).
##
## Phase 3 rules this implements (docs/PHASE3_WORLD.md §5):
##   * the map is cut into 500 × 500 m chunks;
##   * a chunk is resident when it is within `stream_radius_m` of the player;
##   * chunks further away are released;
##   * nothing generates terrain at runtime.
##
## Radius note: the spec's 50 m radius is smaller than one chunk (500 m), so a
## literal implementation would keep a single chunk and show the void on all
## four sides. `keep_ring` keeps the ring of chunks around the focus chunk
## resident as a safety floor (1 → 3×3 chunks); the radius decides everything
## beyond that. Raise `stream_radius_m` to 600+ once the map has real landmarks
## to hide behind.

signal chunk_loaded(chunk: Vector2i)
signal chunk_unloaded(chunk: Vector2i)
## Emitted once if the baked scene is not in the build at all.
signal bake_missing

enum State {
	EMPTY = 0,
	LOADING = 1,
	LOADED = 2,
	MISSING = 3,
	FAILED = 4,
}

## Every path here comes from `TerrainGenerator`, so the generator and the
## streamer can never disagree about where the map lives.
const DEFAULT_BAKED_SCENE_PATH: String = TerrainGenerator.BAKED_SCENE_PATH
const CHUNK_NODE_PATTERN: String = TerrainGenerator.CHUNK_NODE_PATTERN

@export var config: SiranMapConfig
## Node the streaming follows — normally the local player.
@export var focus_path: NodePath
## Load anything whose chunk bounds are within this distance of the focus, in
## metres. Phase 3 default per spec.
@export var stream_radius_m: float = 50.0
## Always keep this many chunk rings around the focus chunk resident.
@export_range(0, 3) var keep_ring: int = 1
## Chunks that may enter the tree per tick. Entering the tree is cheap (the mesh
## is already parsed), so this only smooths the first fill; 2 matches the old
## budget and keeps a 3×3 block from appearing in one frame.
@export_range(1, 8) var max_loads_in_flight: int = 2
## Chunks released per tick. Freeing is cheap, but spreading it avoids
## allocation spikes on the main thread.
@export_range(1, 16) var unloads_per_tick: int = 2
## How often the streaming bookkeeping runs, in seconds. 5 Hz is plenty for a
## 40 m/s spectator and costs ~64 rectangle tests per tick.
@export var tick_interval_s: float = 0.2
## Activate every resident chunk on the first pass instead of two per tick, so
## the player never spawns into a world that fills in over a second.
@export var preload_focus_chunk: bool = true
## The committed baked scene. Overridable for tests.
@export var baked_scene_path: String = DEFAULT_BAKED_SCENE_PATH

## Vector2i -> Chunk_<x>_<z> node. Nodes are either in the tree (loaded) or
## parked (residents waiting for their turn).
var _chunks: Dictionary = {}
var _states: Dictionary = {}
var _active: Dictionary = {}
var _desired: Dictionary = {}
var _desired_order: Array[Vector2i] = []
var _focus: Node3D
var _focus_position: Vector3 = Vector3.ZERO
var _focus_chunk: Vector2i = Vector2i(-1, -1)
## The focus chunk the current `_desired` set was computed for. Comparing against
## the chunk itself (rather than "is the focus chunk in the set") is what makes a
## one-chunk step in a corner actually recompute and free the chunk behind us.
var _desired_for_focus: Vector2i = Vector2i(-2, -2)
var _tick_accumulator: float = 0.0
var _first_tick_done: bool = false
var _bake_warning_emitted: bool = false
var _load_errors: int = 0

## The one in-flight request for the baked scene (0 or 1).
var _requesting: bool = false
var _bake_root: Node3D
var _activated_once: bool = false


func _ready() -> void:
	if config == null:
		config = SiranMapConfig.shared()
	if not focus_path.is_empty():
		var candidate: Node = get_node_or_null(focus_path)
		if candidate is Node3D:
			_focus = candidate as Node3D
	if _focus == null:
		push_warning("ChunkManager has no focus node; call set_focus_node() before use")
	print("[stream] %s | radius %.0f m, keep_ring %d, map %s"
			% [config.describe(), stream_radius_m, keep_ring, baked_scene_path])


func set_focus_node(node: Node3D) -> void:
	_focus = node
	_focus_chunk = Vector2i(-1, -1)


func _process(delta: float) -> void:
	if _focus == null:
		return
	_tick_accumulator += delta
	if _tick_accumulator < tick_interval_s:
		return
	_tick_accumulator = 0.0
	_update_focus()
	_poll_baked_scene()
	if not _first_tick_done:
		_first_tick_done = true
		if preload_focus_chunk:
			_request_baked_scene()
	_recompute_desired_if_needed()
	_activate_desired()
	_release_outside_radius()


# --- Focus / desired set ------------------------------------------------------

func _update_focus() -> void:
	if not is_instance_valid(_focus):
		_focus = null
		return
	_focus_position = _focus.global_position
	var chunk: Vector2i = config.world_to_chunk(_focus_position.x, _focus_position.z)
	if chunk != _focus_chunk:
		_focus_chunk = chunk


func _recompute_desired_if_needed() -> void:
	## Rebuilds the desired set only when the focus crosses a chunk border, so a
	## walking player costs one rebuild per 500 m instead of one per tick.
	if _desired_for_focus == _focus_chunk and _desired.size() > 0:
		return
	_desired_for_focus = _focus_chunk
	_desired_order = compute_desired_for(_focus_position)
	_desired.clear()
	for chunk in _desired_order:
		_desired[chunk] = true


func compute_desired_for(position: Vector3) -> Array[Vector2i]:
	## Pure function of a world position: which chunks *should* be resident.
	## Nearest first, so the streamer spends its request budget on the chunks the
	## player is about to walk into.
	##
	## Kept free of state so the Phase 3 self-check can assert the 50 m radius
	## maths without waiting for real loads.
	var centre_chunk: Vector2i = config.world_to_chunk(position.x, position.z)
	var focus_xz := Vector2(position.x, position.z)
	var result: Array[Vector2i] = []
	var side: int = config.chunks_per_side()
	for cz in range(side):
		for cx in range(side):
			var chunk := Vector2i(cx, cz)
			if _is_within_ring(chunk, centre_chunk, keep_ring) \
					or config.chunk_rect(chunk).grow(stream_radius_m).has_point(focus_xz):
				result.append(chunk)
	result.sort_custom(func(a: Vector2i, b: Vector2i) -> bool:
		return _chunk_distance_squared(a, position) < _chunk_distance_squared(b, position))
	return result


func _is_within_ring(chunk: Vector2i, around: Vector2i, ring: int) -> bool:
	if ring <= 0:
		return false
	return absi(chunk.x - around.x) <= ring and absi(chunk.y - around.y) <= ring


func _chunk_distance_squared(chunk: Vector2i, from: Vector3) -> float:
	var centre: Vector3 = config.chunk_center(chunk)
	var dx: float = centre.x - from.x
	var dz: float = centre.z - from.z
	return dx * dx + dz * dz


# --- The baked scene ----------------------------------------------------------

func _request_baked_scene() -> void:
	## One threaded request for the whole map. `ResourceLoader.exists()` (not
	## `FileAccess.file_exists()`) because the export rewrites the scene into
	## `.godot/exported/…` and leaves a `<path>.remap` beside it: plain file
	## checks are blind to that, which is exactly how a fully packed build once
	## reported itself as unbaked on a phone.
	if _bake_root != null or _requesting:
		return
	if not ResourceLoader.exists(baked_scene_path):
		_mark_all_missing()
		_warn_missing_bake()
		return
	var error: int = ResourceLoader.load_threaded_request(baked_scene_path, "PackedScene", true)
	if error != OK:
		_load_errors += 1
		_mark_all_missing()
		push_error("[stream] threaded request failed for %s (error %d)"
				% [baked_scene_path, error])
		return
	_requesting = true


func _poll_baked_scene() -> void:
	if not _requesting:
		return
	var progress: Array = []
	var status: int = ResourceLoader.load_threaded_get_status(baked_scene_path, progress)
	match status:
		ResourceLoader.THREAD_LOAD_LOADED:
			_requesting = false
			_attach_baked_scene(ResourceLoader.load_threaded_get(baked_scene_path))
		ResourceLoader.THREAD_LOAD_IN_PROGRESS:
			pass
		_:
			_requesting = false
			_load_errors += 1
			_mark_all_missing()
			push_error("[stream] load failed for %s (status %d)"
					% [baked_scene_path, status])


func _attach_baked_scene(packed: Resource) -> void:
	## Splits the instantiated scene into parked chunk nodes. Nothing is added to
	## the tree here: the streaming pass decides what is resident.
	if packed == null or not (packed is PackedScene):
		_load_errors += 1
		_mark_all_missing()
		push_error("[stream] %s is not a PackedScene" % baked_scene_path)
		return
	_bake_root = (packed as PackedScene).instantiate() as Node3D
	if _bake_root == null:
		_load_errors += 1
		_mark_all_missing()
		push_error("[stream] %s did not instantiate" % baked_scene_path)
		return

	for child in _bake_root.get_children():
		var chunk: Vector2i = _parse_chunk_name(String(child.name))
		if chunk.x < 0 or not (child is Node3D):
			continue
		_bake_root.remove_child(child)
		_chunks[chunk] = child
		_states[chunk] = {"state": State.EMPTY, "node": null, "path": baked_scene_path}

	var expected: int = config.chunk_count()
	if _chunks.size() != expected:
		push_error("[stream] baked scene holds %d chunk(s), expected %d"
				% [_chunks.size(), expected])
		_load_errors += 1
	print("[stream] baked map loaded: %d/%d chunks parked (%s)"
			% [_chunks.size(), expected, baked_scene_path])


static func _parse_chunk_name(node_name: String) -> Vector2i:
	## "Chunk_3_7" -> Vector2i(3, 7). Returns (-1, -1) for anything else, so a
	## stray node in the baked scene is ignored instead of becoming a chunk.
	if not node_name.begins_with("Chunk_"):
		return Vector2i(-1, -1)
	var parts: PackedStringArray = node_name.trim_prefix("Chunk_").split("_")
	if parts.size() != 2:
		return Vector2i(-1, -1)
	if not parts[0].is_valid_int() or not parts[1].is_valid_int():
		return Vector2i(-1, -1)
	return Vector2i(parts[0].to_int(), parts[1].to_int())


# --- Streaming ----------------------------------------------------------------

func _activate_desired() -> void:
	if _chunks.is_empty():
		return
	var budget: int = max_loads_in_flight
	if not _activated_once:
		# First fill: everything resident on the same tick the map arrives, so
		# the spawn point is never surrounded by holes for a second.
		budget = _desired_order.size()
	_activated_once = true
	for chunk in _desired_order:
		if budget <= 0:
			return
		if _active.has(chunk) or not _chunks.has(chunk):
			continue
		var node: Node3D = _chunks[chunk]
		add_child(node)
		_active[chunk] = true
		_states[chunk] = {"state": State.LOADED, "node": node, "path": baked_scene_path}
		budget -= 1
		chunk_loaded.emit(chunk)


func _release_outside_radius() -> void:
	var freed: int = 0
	for chunk in _active.keys():
		if freed >= unloads_per_tick:
			return
		if _desired.has(chunk):
			continue
		var node: Node3D = _chunks.get(chunk, null)
		if node == null or not is_instance_valid(node):
			_active.erase(chunk)
			continue
		remove_child(node)
		_active.erase(chunk)
		_states[chunk] = {"state": State.EMPTY, "node": null, "path": baked_scene_path}
		freed += 1
		chunk_unloaded.emit(chunk)


func free_all_chunks() -> void:
	## Releases the whole map. The parked nodes go with it; a later
	## `_request_baked_scene()` reloads the committed scene from disk.
	# Parked nodes are parentless (they were detached from the baked root when it
	# arrived), so freeing the root alone would leak all 64 chunk meshes.
	for chunk in _chunks.keys():
		var node: Node3D = _chunks[chunk]
		if node == null or not is_instance_valid(node):
			continue
		if _active.has(chunk):
			remove_child(node)
		node.queue_free()
	if _bake_root != null and is_instance_valid(_bake_root):
		_bake_root.queue_free()
	_bake_root = null
	_chunks.clear()
	_states.clear()
	_active.clear()
	_desired.clear()
	_desired_order.clear()
	_requesting = false
	_activated_once = false
	_focus_chunk = Vector2i(-1, -1)
	_desired_for_focus = Vector2i(-2, -2)


func _mark_all_missing() -> void:
	var side: int = config.chunks_per_side()
	for cz in range(side):
		for cx in range(side):
			_states[Vector2i(cx, cz)] = {"state": State.MISSING, "node": null,
					"path": baked_scene_path}


# --- Introspection (debug HUD, self-check) -----------------------------------

func _state_of(chunk: Vector2i) -> int:
	if not _states.has(chunk):
		return State.EMPTY
	return _states[chunk]["state"]


func get_loaded_chunks() -> Array[Vector2i]:
	var loaded: Array[Vector2i] = []
	for chunk in _active.keys():
		loaded.append(chunk)
	return loaded


func get_parked_count() -> int:
	return _chunks.size() - _active.size()


func get_in_flight_count() -> int:
	## 0 or 1: there is only ever one file to load.
	return 1 if _requesting else 0


func get_missing_count() -> int:
	var missing: int = 0
	for chunk in _states.keys():
		if _states[chunk]["state"] == State.MISSING:
			missing += 1
	return missing


func is_map_loaded() -> bool:
	return _bake_root != null


func get_desired_chunks() -> Array[Vector2i]:
	## Returns the set the streamer is aiming for. Used by the self-check to
	## assert the 50 m radius maths without waiting for real loads.
	return _desired_order.duplicate()


func get_load_error_count() -> int:
	return _load_errors


func get_loaded_node(chunk: Vector2i) -> Node:
	if not _active.has(chunk):
		return null
	return _chunks.get(chunk, null)


func get_focus_chunk() -> Vector2i:
	return _focus_chunk


func describe() -> String:
	return "chunks loaded %d | parked %d | desired %d%s" % [
		_active.size(),
		maxi(_chunks.size() - _active.size(), 0),
		_desired_order.size(),
		" | MISSING BAKE" if get_missing_count() > 0 else "",
	]


func _warn_missing_bake() -> void:
	if _bake_warning_emitted:
		return
	_bake_warning_emitted = true
	bake_missing.emit()
	push_warning("[stream] %s is not in this build. Generate it once in the editor "
			% baked_scene_path
			+ "(File > Run on tools/bake/terrain_generator_editor.gd) and commit it.")
