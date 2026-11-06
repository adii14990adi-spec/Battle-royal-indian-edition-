@tool
extends EditorScript
## Editor front-end for the same bake as `bake_siran_island.gd`.
##
## Open this file in the Godot editor and press **File > Run** (Ctrl+Shift+X).
## Useful when you want to watch the terrain appear in the editor viewport right
## after the bake instead of switching to a terminal — the outputs are identical,
## because both call `IslandBaker`.
##
## Everything it writes is committed to Git, so the game never bakes at runtime.
## Budget check: see docs/PHASE3_WORLD.md §7 after a bake.


func _run() -> void:
	var baker := IslandBaker.new(SiranMapConfig.default_siran_island())
	var summary: SiranBakeSummary = baker.bake()
	if summary == null:
		push_error("[bake] FAILED — see the Output panel")
		return
	print("[bake] done. Reopen scenes/world/siran_island.tscn to see the result.")
	push_warning("[bake] reminder: commit scenes/world/terrain and resources/world")
