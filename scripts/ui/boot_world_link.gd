extends Button
## "Enter Siran Island" button on the Phase 1 boot screen.
##
## Phase 3 needs a way onto the map that does not involve plugging in a keyboard:
## the tester taps this, the greybox world loads, and the HUD does the rest. The
## boot scene stays the main scene (Phase 1 gate 4 / verify_phase1.sh depends on
## it), so this is an entry point rather than a replacement.
##
## A tap that does nothing is the worst possible failure mode — the screen looks
## healthy, the button looks healthy, and there is no logcat on a phone in a
## tester's hand. So the button reports what it found before it is even pressed:
##
##   ready:   world scene packed, baked map packed     (green)
##   broken:  world scene packed: false | baked map: false   (amber)
##
## and every failure path puts plain text on screen instead of a silent `return`.
##
## Both checks must go through `ResourceLoader.exists()`, never plain
## `FileAccess.file_exists()`. At export time Godot converts text resources to
## binary and writes `<path>.remap` beside them (`_export_customize` in
## editor_export_platform.cpp), so inside an APK `siran_island_baked.tscn` does
## not exist as a file at all — it is `siran_island_baked.tscn.remap` plus a
## `.scn` under `.godot/exported/`. `ResourceLoader.exists()` follows that remap
## (`ResourceLoader::_path_remap`); `FileAccess` does not, which is precisely how
## a fully packed build can report itself as unbaked on a phone.

const WORLD_SCENE: String = "res://scenes/world/siran_island.tscn"
## The one file the whole 4 km map is baked into. Duplicated as a literal on
## purpose: the boot screen must not break if the world scripts fail to load.
const BAKED_MAP_SCENE: String = "res://scenes/world/siran_island_baked.tscn"

const STATUS_FONT_SIZE: int = 15
const ERROR_FONT_SIZE: int = 16
const STATUS_COLOR_OK: Color = Color(0.6, 0.95, 0.65)
const STATUS_COLOR_BAD: Color = Color(1.0, 0.78, 0.35)
const ERROR_COLOR: Color = Color(1.0, 0.45, 0.4)

var _status: Label
var _error_label: Label


func _ready() -> void:
	action_mode = BaseButton.ACTION_MODE_BUTTON_RELEASE
	_build_labels()
	pressed.connect(_on_pressed)
	_refresh_status()


func _build_labels() -> void:
	## Created here rather than in boot.tscn so the Phase 1 baseline scene stays
	## a plain scene; the button owns its own diagnostics.
	var parent: Node = get_parent()
	_status = _make_label(STATUS_FONT_SIZE, STATUS_COLOR_OK)
	_status.name = "BakeStatus"
	_status.position = Vector2(offset_left, offset_bottom + 10.0)
	parent.add_child(_status)

	_error_label = _make_label(ERROR_FONT_SIZE, ERROR_COLOR)
	_error_label.name = "LoadError"
	_error_label.position = Vector2(offset_left, offset_bottom + 36.0)
	_error_label.size = Vector2(760.0, 130.0)
	_error_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_error_label.visible = false
	parent.add_child(_error_label)


func _make_label(font_size: int, colour: Color) -> Label:
	var label := Label.new()
	label.add_theme_font_size_override("font_size", font_size)
	label.add_theme_color_override("font_color", colour)
	label.add_theme_color_override("font_outline_color", Color(0.03, 0.03, 0.05))
	label.add_theme_constant_override("outline_size", 4)
	return label


func _refresh_status() -> void:
	var world_packed: bool = resource_in_build(WORLD_SCENE)
	var map_packed: bool = resource_in_build(BAKED_MAP_SCENE)
	if world_packed and map_packed:
		text = "ENTER SIRAN ISLAND — PHASE 3 GREYBOX"
		tooltip_text = "Fly the baked 4 km map and watch chunks stream"
		_status.text = "Ready — world scene packed, baked map packed"
		_status.add_theme_color_override("font_color", STATUS_COLOR_OK)
	else:
		text = "ENTER SIRAN ISLAND — INCOMPLETE BUILD"
		tooltip_text = "The export is missing files; the status line names them"
		_status.text = "world scene packed: %s  |  baked map: %s" % [
			str(world_packed), str(map_packed),
		]
		_status.add_theme_color_override("font_color", STATUS_COLOR_BAD)
	print("[boot] %s" % _status.text)


func resource_in_build(path: String) -> bool:
	## Raw file first (editor, or a build with conversion disabled), then the
	## loader, which is the only check that follows `<path>.remap`.
	if FileAccess.file_exists(path):
		return true
	return ResourceLoader.exists(path)


func _on_pressed() -> void:
	# Both checks exist because the export, not the code, is what usually breaks:
	# a file left out of the pack makes every loader below fail with nothing
	# visible on screen.
	if not resource_in_build(WORLD_SCENE):
		_show_error("Missing %s — the world scene is not in this build." % WORLD_SCENE)
		return
	if not resource_in_build(BAKED_MAP_SCENE):
		_show_error("Missing %s — the baked map is not in this build. "
				% BAKED_MAP_SCENE
				+ "Run TerrainGenerator in the editor, then check include_filter.")
		return
	print("[boot] change_scene_to_file(%s) — baked map packed" % WORLD_SCENE)
	var error: int = get_tree().change_scene_to_file(WORLD_SCENE)
	if error != OK:
		_show_error("change_scene_to_file failed for %s: %s (error %d)" % [
			WORLD_SCENE, error_string(error), error,
		])


func _show_error(message: String) -> void:
	push_error("[boot] %s" % message)
	print("[boot] ERROR %s" % message)
	if _error_label != null:
		_error_label.text = message
		_error_label.visible = true
