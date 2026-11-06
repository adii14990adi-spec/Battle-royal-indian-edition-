extends Button
## "Enter Siran Island" button on the Phase 1 boot screen.
##
## Phase 3 needs a way onto the map that does not involve plugging in a keyboard:
## the tester taps this, the greybox world loads, and the HUD does the rest. The
## boot scene stays the main scene (Phase 1 gate 4 / verify_phase1.sh depends on
## it), so this is an entry point rather than a replacement.
##
## It also tells the truth when the terrain has not been baked yet, instead of
## dropping the player into an empty world.

const WORLD_SCENE: String = "res://scenes/world/siran_island.tscn"
const FIRST_CHUNK: String = "res://scenes/world/terrain/chunk_0_0.tscn"


func _ready() -> void:
	action_mode = BaseButton.ACTION_MODE_BUTTON_RELEASE
	if FileAccess.file_exists(FIRST_CHUNK):
		text = "ENTER SIRAN ISLAND — PHASE 3 GREYBOX"
		tooltip_text = "Fly the baked 4 km map and watch chunks stream"
	else:
		text = "ENTER SIRAN ISLAND — NO BAKE YET"
		tooltip_text = "Run: bash tools/bake_siran_island.sh"
	pressed.connect(_on_pressed)


func _on_pressed() -> void:
	print("[boot] loading %s" % WORLD_SCENE)
	var error: int = get_tree().change_scene_to_file(WORLD_SCENE)
	if error != OK:
		push_error("[boot] could not load %s (error %d)" % [WORLD_SCENE, error])
