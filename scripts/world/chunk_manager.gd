class_name ChunkManager
extends Node3D
## Streams the baked Siran Island chunks in and out around a focus node.
##
## Phase 3 rules this implements (see docs PHASE3_WORLD.md §5):
##   * the map is cut into 500 × 500 m chunks, baked to disk as one `.tscn` each;
##   * a chunk is loaded when it is within `stream_radius_m` of the player;
##   * chunks further away are freed;
##   * loading goes through `ResourceLoader.load_threaded_request()` so the frame
##     never stalls on eMMC or on mesh upload — on a T606 a synchronous
##     `load()` of a 150 KB chunk mesh is a 40–80 ms hitch, which is 2–3 frames
##     of black at 30 FPS.
##
## Nothing here generates geometry. If the chunks are missing it says so, once,
## with the command that fixes it, instead of silently rendering nothing.
##
## Radius note: the spec's 50 m radius is smaller than one chunk (500 m), so a
## literal implementation would load a single chunk and show the void on all
## four sides. `keep_ring` keeps the ring of chunks around the focus chunk
## resident as a safety floor (1 → 3×3 chunks); the radius still decides
## everything beyond that. Raise `stream_radius_m` to 600+ once the map has
## real landmarks to hide behind.

signal chunk_loaded(chunk: Vector2i)
signal chunk_unloaded(chunk: Vector2i)
## Emitted once if the baked chunk files are not on disk.
signal bake_missing

enum State {
	EMPTY = 0,
	LOADING = 1,
	LOADED = 2,
	MISSING = 3,
	FAILED = 4,
}

const DEFAULT_SCENE_PATTERN: String = "res://scenes/world/terrain/chunk_%d_%d.tscn"

@export var config: SiranMapConfig
## Node the streaming follows — normally the local player.
@export var focus_path: NodePath
## Load anything whose chunk bounds are within this distance of the focus, in
## metres. Phase 3 default per spec.
@export var stream_radius_m: float = 50.0
## Always keep this many chunk rings around the focus chunk resident.
@export_range(0, 3) var keep_ring: int = 1
## Maximum concurrent threaded requests. Two is kind to eMMC and to the 4 GB
## RAM ceiling; more just moves the hitch.
@export_range(1, 8) var max_loads_in_flight: int = 2
## Chunks freed per tick. Freeing is cheap, but spreading it avoids allocation
## spikes on the main thread.
@export_range(1, 16) var unloads_per_tick: int = 2
## How often the streaming bookkeeping runs, in seconds. 5 Hz is plenty for a
## 40 m/s spectator and costs ~64 rectangle tests per tick.
@export var tick_interval_s: float = 0.2
## Load the focus chunk synchronously on the first tick so the player never
## spawns into an empty world.
@export var preload_focus_chunk: bool = true
@export var chunk_scene_pattern: String = DEFAULT_SCENE_PATTERN

var _states: Dictionary = {}
var _in_flight: Array[Vector2i] = []
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


func _ready() -> void:
	if config == null:
		config = SiranMapConfig.shared()
	if not focus_path.is_empty():
		var candidate: Node = get_node_or_null(focus_path)
		if candidate is Node3D:
			_focus = candidate as Node3D
	if _focus == null:
		push_warning("ChunkManager has no focus node; call set_focus_node() before use")
	print("[stream] %s | radius %.0f m, keep_ring %d, %d chunks available"
			% [config.describe(), stream_radius_m, keep_ring, config.chunk_count()])


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
	_poll_requests()
	if not _first_tick_done:
		_first_tick_done = true
		if preload_focus_chunk:
			_load_chunk_sync(_focus_chunk)
	_recompute_desired_if_needed()
	_start_requests()
	_unload_outside_radius()


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
	## Nearest first, so the streamer spends its two request slots on the chunks
	## the player is about to walk into.
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


# --- Loading ------------------------------------------------------------------

func _start_requests() -> void:
	var budget: int = max_loads_in_flight - _in_flight.size()
	if budget <= 0:
		return
	for chunk in _desired_order:
		if budget <= 0:
			return
		var state: int = _state_of(chunk)
		if state == State.LOADING or state == State.LOADED:
			continue
		if state == State.MISSING:
			continue
		if _request_chunk(chunk):
			budget -= 1


func _request_chunk(chunk: Vector2i) -> bool:
	var path: String = chunk_scene_pattern % [chunk.x, chunk.y]
	if not FileAccess.file_exists(path):
		_states[chunk] = {"state": State.MISSING, "node": null, "path": path}
		_warn_missing_bake()
		return false
	var error: int = ResourceLoader.load_threaded_request(path, "PackedScene", true)
	if error != OK:
		_states[chunk] = {"state": State.FAILED, "node": null, "path": path}
		_load_errors += 1
		push_error("[stream] threaded request failed for %s (error %d)" % [path, error])
		return false
	_states[chunk] = {"state": State.LOADING, "node": null, "path": path}
	_in_flight.append(chunk)
	return true


func _poll_requests() -> void:
	if _in_flight.is_empty():
		return
	var still_loading: Array[Vector2i] = []
	var progress: Array = []
	for chunk in _in_flight:
		var path: String = _states[chunk]["path"]
		var status: int = ResourceLoader.load_threaded_get_status(path, progress)
		match status:
			ResourceLoader.THREAD_LOAD_LOADED:
				_attach_loaded_chunk(chunk, path)
			ResourceLoader.THREAD_LOAD_IN_PROGRESS:
				still_loading.append(chunk)
			_:
				_states[chunk] = {"state": State.FAILED, "node": null, "path": path}
				_load_errors += 1
				push_error("[stream] load failed for %s (status %d)" % [path, status])
	_in_flight = still_loading


func _attach_loaded_chunk(chunk: Vector2i, path: String) -> void:
	var packed: Resource = ResourceLoader.load_threaded_get(path)
	if packed == null:
		_states[chunk] = {"state": State.FAILED, "node": null, "path": path}
		_load_errors += 1
		return
	var node: Node = (packed as PackedScene).instantiate()
	if node == null:
		_states[chunk] = {"state": State.FAILED, "node": null, "path": path}
		_load_errors += 1
		return
	add_child(node)
	_states[chunk] = {"state": State.LOADED, "node": node, "path": path}
	chunk_loaded.emit(chunk)


func _load_chunk_sync(chunk: Vector2i) -> void:
	## First-chunk only: the world must not open on a hole under the player.
	if _state_of(chunk) == State.LOADED:
		return
	var path: String = chunk_scene_pattern % [chunk.x, chunk.y]
	if not FileAccess.file_exists(path):
		_states[chunk] = {"state": State.MISSING, "node": null, "path": path}
		_warn_missing_bake()
		return
	var packed: Resource = ResourceLoader.load(path, "PackedScene")
	if packed == null:
		_states[chunk] = {"state": State.FAILED, "node": null, "path": path}
		return
	var node: Node = (packed as PackedScene).instantiate()
	add_child(node)
	_states[chunk] = {"state": State.LOADED, "node": node, "path": path}
	chunk_loaded.emit(chunk)


# --- Unloading ----------------------------------------------------------------

func _unload_outside_radius() -> void:
	var freed: int = 0
	for chunk in _states.keys():
		if freed >= unloads_per_tick:
			return
		if _desired.has(chunk):
			continue
		var state: Dictionary = _states[chunk]
		if state["state"] == State.LOADED:
			var node: Node = state["node"]
			if node != null and is_instance_valid(node):
				remove_child(node)
				node.queue_free()
			chunk_unloaded.emit(chunk)
			freed += 1
		if state["state"] == State.LOADING:
			# Cannot cancel a threaded request in Godot 4; let it finish and
			# drop it on the next pass.
			continue
		_states.erase(chunk)


func free_all_chunks() -> void:
	for chunk in _states.keys():
		var state: Dictionary = _states[chunk]
		var node: Node = state["node"]
		if node != null and is_instance_valid(node):
			remove_child(node)
			node.queue_free()
	_states.clear()
	_in_flight.clear()
	_desired.clear()
	_desired_order.clear()
	_focus_chunk = Vector2i(-1, -1)
	_desired_for_focus = Vector2i(-2, -2)


# --- Introspection (debug HUD, self-check) -----------------------------------

func _state_of(chunk: Vector2i) -> int:
	if not _states.has(chunk):
		return State.EMPTY
	return _states[chunk]["state"]


func get_loaded_chunks() -> Array[Vector2i]:
	var loaded: Array[Vector2i] = []
	for chunk in _states.keys():
		if _states[chunk]["state"] == State.LOADED:
			loaded.append(chunk)
	return loaded


func get_in_flight_count() -> int:
	return _in_flight.size()


func get_missing_count() -> int:
	var missing: int = 0
	for chunk in _states.keys():
		if _states[chunk]["state"] == State.MISSING:
			missing += 1
	return missing


func get_desired_chunks() -> Array[Vector2i]:
	## Returns the set the streamer is aiming for. Used by the self-check to
	## assert the 50 m radius maths without waiting for real loads.
	return _desired_order.duplicate()


func get_load_error_count() -> int:
	return _load_errors


func get_loaded_node(chunk: Vector2i) -> Node:
	if not _states.has(chunk):
		return null
	return _states[chunk]["node"]


func get_focus_chunk() -> Vector2i:
	return _focus_chunk


func describe() -> String:
	return "chunks loaded %d | in flight %d | desired %d%s" % [
		get_loaded_chunks().size(),
		_in_flight.size(),
		_desired_order.size(),
		" | MISSING BAKE" if get_missing_count() > 0 else "",
	]


func _warn_missing_bake() -> void:
	if _bake_warning_emitted:
		return
	_bake_warning_emitted = true
	bake_missing.emit()
	var example_path: String = chunk_scene_pattern % [0, 0]
	push_warning("[stream] no baked chunks on disk (expected e.g. %s). "
			% example_path
			+ "Run `bash tools/bake_siran_island.sh` with a Godot 4.4 binary.")
