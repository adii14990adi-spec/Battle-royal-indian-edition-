extends Node
## SaveManager — placeholder for the offline save system (Phase 2+).
##
## Kept deliberately inert in Phase 1: no gameplay state exists yet, and nothing
## touches the network. Everything lives under `user://` (which is never
## committed — see .gitignore).
##
## When it becomes an autoload, add to project.godot:
##   [autoload]
##   SaveManager="*res://scripts/autoload/save_manager.gd"

const SAVE_DIR: String = "user://saves"
const SAVE_SLOT_TEMPLATE: String = "slot_%02d.save"
const CURRENT_SAVE_VERSION: int = 1


func has_slot(slot: int) -> bool:
	return FileAccess.file_exists(slot_path(slot))


func slot_path(slot: int) -> String:
	return SAVE_DIR.path_join(SAVE_SLOT_TEMPLATE % slot)


func delete_slot(slot: int) -> void:
	var path: String = slot_path(slot)
	if FileAccess.file_exists(path):
		DirAccess.remove_absolute(path)


func ensure_save_dir() -> Error:
	if DirAccess.dir_exists_absolute(SAVE_DIR):
		return OK
	return DirAccess.make_dir_recursive_absolute(SAVE_DIR)
