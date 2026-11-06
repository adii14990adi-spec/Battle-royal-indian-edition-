class_name DebugSpectator
extends Camera3D
## Phase 3 flight camera. Test harness, not gameplay — Phase 4 replaces it with
## the real character controller.
##
## Why it exists: chunk streaming can only be judged while moving, and the one
## thing a phone cannot give us is a WASD keyboard. So it has three ways to move:
##
##   * desktop: WASD + Q/E, hold Shift to sprint, drag with the left button to
##     look around;
##   * touch: drag to look, then use the HUD buttons to teleport between zones;
##   * hands-free: `start_auto_tour()` flies a slow circle around the island so a
##     tester can watch chunks load and unload (and logcat stay quiet) without
##     touching the screen.
##
## Movement is deliberately fast (40 m/s, 160 m/s sprinting): it stresses the
## streamer far harder than a player ever will.

## Base fly speed, metres per second.
const FLY_SPEED: float = 40.0
const SPRINT_MULTIPLIER: float = 4.0
const LOOK_DEGREES_PER_PIXEL: float = 0.18
const MIN_ALTITUDE_M: float = 4.0

## Auto tour: circle radius, altitude and angular speed.
const TOUR_RADIUS_M: float = 1100.0
const TOUR_ALTITUDE_M: float = 55.0
const TOUR_DEGREES_PER_SECOND: float = 2.6

@export var config: SiranMapConfig
## Extra height above the spawn point when the scene starts, metres.
@export var spawn_hover_m: float = 45.0

var _yaw_degrees: float = -35.0
var _pitch_degrees: float = -22.0
var _dragging: bool = false
var _touring: bool = false
var _tour_angle_degrees: float = 0.0
var _zone_index: int = 0


func _ready() -> void:
	if config == null:
		config = SiranMapConfig.shared()
	reset_to_spawn()
	# The HUD buttons must stay clickable, so this node never blocks input.
	set_process_input(true)


func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventMouseButton:
		var button: InputEventMouseButton = event
		if button.button_index == MOUSE_BUTTON_LEFT:
			_dragging = button.pressed
	elif event is InputEventMouseMotion and _dragging:
		var motion: InputEventMouseMotion = event
		_look(motion.relative)
	elif event is InputEventScreenDrag:
		var drag: InputEventScreenDrag = event
		_look(drag.relative)
	elif event is InputEventKey and event.pressed and not event.echo:
		_handle_key((event as InputEventKey).keycode)


func _look(relative: Vector2) -> void:
	_yaw_degrees -= relative.x * LOOK_DEGREES_PER_PIXEL
	_pitch_degrees = clampf(_pitch_degrees - relative.y * LOOK_DEGREES_PER_PIXEL, -89.0, 89.0)
	_touring = false
	_apply_rotation()


func _handle_key(keycode: int) -> void:
	match keycode:
		KEY_R:
			reset_to_spawn()
		KEY_F:
			start_auto_tour()
		KEY_TAB, KEY_N:
			cycle_zone()
		KEY_ESCAPE:
			# Back to the boot screen instead of killing the process, so the
			# device test can go back to the perf label.
			get_tree().change_scene_to_file("res://scenes/core/boot.tscn")


func _physics_process(delta: float) -> void:
	if _touring:
		_advance_tour(delta)
		return
	var direction := Vector3.ZERO
	if Input.is_key_pressed(KEY_W) or Input.is_key_pressed(KEY_UP):
		direction -= global_transform.basis.z
	if Input.is_key_pressed(KEY_S) or Input.is_key_pressed(KEY_DOWN):
		direction += global_transform.basis.z
	if Input.is_key_pressed(KEY_A) or Input.is_key_pressed(KEY_LEFT):
		direction -= global_transform.basis.x
	if Input.is_key_pressed(KEY_D) or Input.is_key_pressed(KEY_RIGHT):
		direction += global_transform.basis.x
	if Input.is_key_pressed(KEY_E) or Input.is_key_pressed(KEY_SPACE):
		direction += Vector3.UP
	if Input.is_key_pressed(KEY_Q) or Input.is_key_pressed(KEY_CTRL):
		direction -= Vector3.UP
	if direction == Vector3.ZERO:
		return
	var speed: float = FLY_SPEED
	if Input.is_key_pressed(KEY_SHIFT):
		speed *= SPRINT_MULTIPLIER
	var position_now: Vector3 = global_position + direction.normalized() * speed * delta
	position_now.y = maxf(position_now.y, MIN_ALTITUDE_M)
	global_position = position_now


# --- Public API used by the HUD buttons ---------------------------------------

func reset_to_spawn() -> void:
	_touring = false
	var spawn: Vector3 = config.spawn_point if config != null else Vector3.ZERO
	global_position = Vector3(spawn.x, maxf(spawn.y, MIN_ALTITUDE_M) + spawn_hover_m, spawn.z)
	_yaw_degrees = -35.0
	_pitch_degrees = -22.0
	_apply_rotation()


func start_auto_tour() -> void:
	_touring = true
	_tour_angle_degrees = rad_to_deg(atan2(global_position.z, global_position.x))
	_advance_tour(0.0)


func stop_auto_tour() -> void:
	_touring = false


func is_touring() -> bool:
	return _touring


func cycle_zone() -> bool:
	## Teleports to the next zone's centre. Returns false when there is nothing
	## to cycle through (no zones baked into the config).
	if config == null or config.zones.is_empty():
		return false
	_zone_index = (_zone_index + 1) % config.zones.size()
	var zone: IslandZone = config.zones[_zone_index]
	_touring = false
	global_position = Vector3(zone.center.x, zone.flat_height + 40.0, zone.center.y)
	_yaw_degrees = zone.rotation_deg
	_pitch_degrees = -30.0
	_apply_rotation()
	return true


func get_current_zone_name() -> String:
	if config == null:
		return "-"
	var zone: IslandZone = config.get_zone_at(global_position.x, global_position.z)
	return "-" if zone == null else zone.display_name


func _advance_tour(delta: float) -> void:
	_tour_angle_degrees = fmod(_tour_angle_degrees + TOUR_DEGREES_PER_SECOND * delta, 360.0)
	var radians: float = deg_to_rad(_tour_angle_degrees)
	global_position = Vector3(cos(radians) * TOUR_RADIUS_M, TOUR_ALTITUDE_M,
			sin(radians) * TOUR_RADIUS_M)
	# Look along the tangent (not across the circle, or the tour would be staring
	# at the ground it has already passed) and tilt down towards the island.
	# With Godot's -Z forward, a tangent of (-sin, cos) at angle θ needs yaw = 180° - θ.
	_yaw_degrees = 180.0 - _tour_angle_degrees
	_pitch_degrees = -14.0
	_apply_rotation()


func _apply_rotation() -> void:
	rotation_degrees = Vector3(_pitch_degrees, _yaw_degrees, 0.0)
