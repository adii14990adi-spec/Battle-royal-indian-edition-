@tool
extends EditorScript
## Editor front-end for `TerrainGenerator` — this is the "run it in the editor"
## button the map is generated from.
##
##     1. open this file in the Godot editor
##     2. File > Run  (Ctrl + Shift + X)
##
## It fills `siran_island_baked.tscn` with the whole island (hills, lake, river,
## graded roads, zone pads) and writes the matching `HeightMapShape3D` collision
## into the same scene. Everything it writes is committed to Git, so the APK
## build never generates terrain — it only packs the scene.
##
## The outputs, all of which belong in the same commit:
##
##     scenes/world/siran_island_baked.tscn              the map (mesh + collision)
##     resources/world/baked/siran_river_surface.res     river water ribbon
##     resources/world/baked/siran_island_summary.tres   version + size + zones
##     docs/images/siran_island_preview.png              top-down layout review
##
## The terminal equivalent (same code path, no editor needed) is
## `bash tools/generate_siran_island.sh`.


func _run() -> void:
	var generator := TerrainGenerator.new(SiranMapConfig.default_siran_island())
	var summary: SiranBakeSummary = generator.generate()
	if summary == null:
		push_error("[gen] FAILED — see the Output panel")
		return
	print("[gen] done: %s" % TerrainGenerator.BAKED_SCENE_PATH)
	print("[gen] %s" % summary.human_size())
	push_warning("[gen] reminder: commit scenes/world/siran_island_baked.tscn "
			+ "and resources/world/baked/")
	# Re-open the scene so the new terrain is on screen immediately instead of
	# asking the reader to guess whether anything happened.
	var packed: PackedScene = ResourceLoader.load(TerrainGenerator.BAKED_SCENE_PATH,
			"PackedScene", ResourceLoader.CACHE_MODE_IGNORE) as PackedScene
	if packed == null:
		push_error("[gen] the saved scene does not load back")
		return
	var interface: EditorInterface = get_editor_interface()
	if interface != null:
		interface.open_scene_from_path(TerrainGenerator.BAKED_SCENE_PATH)
