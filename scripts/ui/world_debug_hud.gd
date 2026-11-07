class_name WorldDebugHud
extends CanvasLayer
## Phase 3 greybox HUD: the numbers that tell you whether streaming works, plus
## four buttons so the whole thing is testable on a phone with no keyboard.
##
## The UI is built in code on purpose — a hand-written .tscn of anchored
## containers is 60 lines of offsets that nobody can review, and this is a debug
## overlay, not shipping UI. Cost on device: one Label and four Buttons, i.e. a
## handful of 2D draw calls that the 60-draw-call budget barely notices.
##
## Refresh at 4 Hz: a Label that rewrites itself every frame is a string
## allocation per frame, which is exactly what the Unisoc budget forbids.

const REFRESH_INTERVAL_S: float = 0.25
const BYTES_PER_MB: float = 1048576.0
const BUTTON_MIN_SIZE: Vector2 = Vector2(112.0, 48.0)

@export var chunk_manager_path: NodePath = ^"../ChunkManager"
@export var spectator_path: NodePath = ^"../Spectator"
@export var extras_path: NodePath = ^"../RiverSurface"

var _label: Label
var _manager: ChunkManager
var _spectator: DebugSpectator
var _extras: BakedExtras
var _config: SiranMapConfig
var _accumulator: float = 0.0


func _ready() -> void:
	_config = SiranMapConfig.shared()
	_manager = get_node_or_null(chunk_manager_path) as ChunkManager
	_spectator = get_node_or_null(spectator_path) as DebugSpectator
	_extras = get_node_or_null(extras_path) as BakedExtras
	_build_ui()
	_refresh()


func _process(delta: float) -> void:
	_accumulator += delta
	if _accumulator < REFRESH_INTERVAL_S:
		return
	_accumulator = 0.0
	_refresh()


func _build_ui() -> void:
	var root := Control.new()
	root.name = "Root"
	root.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	root.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(root)

	_label = Label.new()
	_label.name = "Stats"
	_label.set_anchors_and_offsets_preset(Control.PRESET_TOP_LEFT)
	_label.offset_left = 24.0
	_label.offset_top = 16.0
	_label.offset_right = 1180.0
	_label.offset_bottom = 220.0
	_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_label.add_theme_font_size_override("font_size", 18)
	_label.add_theme_color_override("font_color", Color(1.0, 1.0, 1.0))
	_label.add_theme_color_override("font_outline_color", Color(0.03, 0.03, 0.05))
	_label.add_theme_constant_override("outline_size", 6)
	_label.add_theme_constant_override("line_spacing", 4)
	root.add_child(_label)

	var bar := HBoxContainer.new()
	bar.name = "Buttons"
	bar.set_anchors_and_offsets_preset(Control.PRESET_BOTTOM_RIGHT)
	bar.offset_left = -640.0
	bar.offset_top = -72.0
	bar.offset_right = -16.0
	bar.offset_bottom = -16.0
	bar.add_theme_constant_override("separation", 10)
	root.add_child(bar)

	_add_button(bar, "TOUR", _on_tour_pressed)
	_add_button(bar, "STOP", _on_stop_pressed)
	_add_button(bar, "ZONE \u25B6", _on_zone_pressed)
	_add_button(bar, "RESET", _on_reset_pressed)
	_add_button(bar, "EXIT", _on_exit_pressed)


func _add_button(parent: HBoxContainer, text: String, handler: Callable) -> void:
	var button := Button.new()
	button.text = text
	button.custom_minimum_size = BUTTON_MIN_SIZE
	button.focus_mode = Control.FOCUS_NONE
	button.pressed.connect(handler)
	parent.add_child(button)


# --- Button handlers ----------------------------------------------------------

func _on_tour_pressed() -> void:
	if _spectator != null:
		_spectator.start_auto_tour()


func _on_stop_pressed() -> void:
	if _spectator != null:
		_spectator.stop_auto_tour()


func _on_zone_pressed() -> void:
	if _spectator != null:
		_spectator.cycle_zone()


func _on_reset_pressed() -> void:
	if _spectator != null:
		_spectator.reset_to_spawn()


func _on_exit_pressed() -> void:
	get_tree().change_scene_to_file("res://scenes/core/boot.tscn")


# --- Text ---------------------------------------------------------------------

func _refresh() -> void:
	if _label == null:
		return
	_label.text = "\n".join(PackedStringArray(_build_lines()))


func _build_lines() -> Array[String]:
	var lines: Array[String] = []
	var fps: int = int(round(Performance.get_monitor(Performance.TIME_FPS)))
	var draws: int = int(Performance.get_monitor(
			Performance.RENDER_TOTAL_DRAW_CALLS_IN_FRAME))
	var static_mb: float = Performance.get_monitor(Performance.MEMORY_STATIC) / BYTES_PER_MB
	var video_mb: float = Performance.get_monitor(
			Performance.RENDER_VIDEO_MEM_USED) / BYTES_PER_MB
	lines.append("SIRAN ISLAND — PHASE 3 GREYBOX   |   %.0f×%.0f m, %d×%d chunks of %.0f m" % [
		_config.map_size_m, _config.map_size_m,
		_config.chunks_per_side(), _config.chunks_per_side(), _config.chunk_size_m])
	lines.append("FPS %d  |  draws %d  |  static %.0f MB  |  video %.0f MB"
			% [fps, draws, static_mb, video_mb])

	if _spectator != null:
		lines.append("pos %.0f, %.0f, %.0f  |  zone: %s  |  %s" % [
			_spectator.global_position.x, _spectator.global_position.y,
			_spectator.global_position.z, _spectator.get_current_zone_name(),
			"auto tour" if _spectator.is_touring() else "manual"])
	if _manager != null:
		lines.append("%s  |  focus chunk %s"
				% [_manager.describe(), str(_manager.get_focus_chunk())])
		if _manager.get_missing_count() > 0:
			lines.append("NO BAKED MAP — generate scenes/world/siran_island_baked.tscn "
					+ "(File > Run on tools/bake/terrain_generator_editor.gd)")
		elif not _manager.is_map_loaded():
			lines.append("LOADING %s …" % ChunkManager.DEFAULT_BAKED_SCENE_PATH)
	if _extras != null and _extras.get_summary() != null:
		var summary: SiranBakeSummary = _extras.get_summary()
		lines.append("bake v%d seed %d  |  %d chunks  |  %.0f..%.0f m  |  %s" % [
			summary.generator_version, summary.noise_seed, summary.chunks_written,
			summary.min_height_m, summary.max_height_m, summary.human_size()])
	lines.append("WASD/QE fly  Shift sprint  drag look  F tour  N zone  R reset  Esc back")
	return lines
