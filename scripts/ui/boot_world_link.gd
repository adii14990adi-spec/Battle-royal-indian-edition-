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
##   ready:   world scene packed, baked terrain 64/64   (green)
##   broken:  world scene packed: false | baked chunks: 0/64   (amber)
##
## and every failure path puts plain text on screen instead of a silent `return`.

const WORLD_SCENE: String = "res://scenes/world/siran_island.tscn"
const TERRAIN_DIR: String = "res://scenes/world/terrain/"
## SiranMapConfig.chunks_per_side() for the 4 km / 500 m map. Duplicated on
## purpose: the boot screen must not break if the world scripts fail to load.
const CHUNKS_PER_SIDE: int = 8
const TOTAL_CHUNKS: int = CHUNKS_PER_SIDE * CHUNKS_PER_SIDE

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
	var world_packed: bool = FileAccess.file_exists(WORLD_SCENE)
	var chunks: int = count_baked_chunks()
	if world_packed and chunks == TOTAL_CHUNKS:
		text = "ENTER SIRAN ISLAND — PHASE 3 GREYBOX"
		tooltip_text = "Fly the baked 4 km map and watch chunks stream"
		_status.text = "Ready — world scene packed, baked terrain %d/%d" % [chunks, TOTAL_CHUNKS]
		_status.add_theme_color_override("font_color", STATUS_COLOR_OK)
	else:
		text = "ENTER SIRAN ISLAND — INCOMPLETE BUILD"
		tooltip_text = "The export is missing files; the status line names them"
		_status.text = "world scene packed: %s  |  baked chunks: %d/%d" % [
			str(world_packed), chunks, TOTAL_CHUNKS,
		]
		_status.add_theme_color_override("font_color", STATUS_COLOR_BAD)
	print("[boot] %s" % _status.text)


func count_baked_chunks() -> int:
	## Probed by path instead of listing the directory: `FileAccess.file_exists()`
	## behaves the same in the editor and inside an APK, where every exported file
	## is an individual `assets/` entry.
	var found: int = 0
	for cz in range(CHUNKS_PER_SIDE):
		for cx in range(CHUNKS_PER_SIDE):
			if FileAccess.file_exists("%schunk_%d_%d.tscn" % [TERRAIN_DIR, cx, cz]):
				found += 1
	return found


func _on_pressed() -> void:
	# Both checks exist because the export, not the code, is what usually breaks:
	# a file created after the editor's last import can be left out of the pack
	# and every loader below then fails with nothing visible on screen.
	if not FileAccess.file_exists(WORLD_SCENE):
		_show_error("Missing %s — the world scene is not in this build." % WORLD_SCENE)
		return
	var chunks: int = count_baked_chunks()
	if chunks < TOTAL_CHUNKS:
		var incomplete := "Baked terrain incomplete: %d/%d chunks in this build." % [
			chunks, TOTAL_CHUNKS,
		]
		_show_error(incomplete + " The map was not exported — check include_filter.")
		return
	print("[boot] change_scene_to_file(%s) — %d/%d chunks packed" % [
		WORLD_SCENE, chunks, TOTAL_CHUNKS,
	])
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
