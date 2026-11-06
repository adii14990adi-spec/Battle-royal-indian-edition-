extends SceneTree
## Prove that Godot can read its own config files, and that the settings the
## shipped APK depends on are still what Phase 3 needs.
##
## This exists because a build died in "Export release APK" with a single line —
## `ConfigFile parse error at res://export_presets.cfg:9` — after every static
## gate had passed. Godot config files have exactly one comment character, `;`.
## A `#` line is not a comment: VariantParser reads it as a token, the whole
## preset becomes unreadable, and the export fails where nothing is grepping.
##
## Run it with:
##   godot --headless --path . --script res://tools/check_project_config.gd
## Exit code 0 = clean, 1 = at least one problem (each printed as CONFIG ERROR).

const PROJECT_CONFIG: String = "res://project.godot"
const PRESET_CONFIG: String = "res://export_presets.cfg"
const PRESET_SECTION: String = "preset.0"
const INVALID_COMMENT_MARKER: String = "#"
const REQUIRED_INCLUDE_PATTERNS: Array[String] = ["*.tscn", "*.res", "*.gd"]

var _problems: int = 0


func _init() -> void:
	for path in [PROJECT_CONFIG, PRESET_CONFIG]:
		_check_parses(path)
		_check_no_hash_comments(path)
	_check_preset_settings()
	_check_project_settings()
	if _problems == 0:
		print("Config check: both files parse and keep the Phase 3 settings")
	quit(1 if _problems > 0 else 0)


func _check_parses(path: String) -> void:
	var config := ConfigFile.new()
	var error: int = config.load(path)
	if error != OK:
		_fail("%s does not parse (error %d)" % [path, error])
		return
	print("%s parses — %d section(s)" % [path, config.get_sections().size()])


func _check_no_hash_comments(path: String) -> void:
	var file := FileAccess.open(path, FileAccess.READ)
	if file == null:
		return
	var line_number: int = 0
	while not file.eof_reached():
		line_number += 1
		var line: String = file.get_line().lstrip(" \t")
		if line.begins_with(INVALID_COMMENT_MARKER):
			_fail("%s:%d starts with '#' — Godot config comments are ';', not '#'"
					% [path, line_number])


func _check_preset_settings() -> void:
	var config := ConfigFile.new()
	if config.load(PRESET_CONFIG) != OK:
		return
	var include: String = str(config.get_value(PRESET_SECTION, "include_filter", ""))
	for pattern in REQUIRED_INCLUDE_PATTERNS:
		if not include.contains(pattern):
			_fail("export_presets.cfg include_filter is missing %s" % pattern)
	var export_path: String = str(config.get_value(PRESET_SECTION, "export_path", ""))
	if not export_path.ends_with("battle-horizons.apk"):
		_fail("export_path must stay build/battle-horizons.apk (got %s)" % export_path)


func _check_project_settings() -> void:
	var config := ConfigFile.new()
	if config.load(PROJECT_CONFIG) != OK:
		return
	if bool(config.get_value("application", "boot_splash/show_image", true)):
		_fail("application/boot_splash/show_image must be false")
	var main_scene: String = str(config.get_value("application", "run/main_scene", ""))
	if main_scene != "res://scenes/core/boot.tscn":
		_fail("run/main_scene must stay res://scenes/core/boot.tscn (got %s)" % main_scene)


func _fail(message: String) -> void:
	_problems += 1
	printerr("CONFIG ERROR: %s" % message)
