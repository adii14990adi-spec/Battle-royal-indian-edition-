extends Node3D
## Boot scene controller for "Hello Unisoc" (scenes/core/boot.tscn).
##
## Phase 1 acceptance target: on a physical Unisoc T606 device in airplane mode
## this scene must render the textured box and the label, with no crash and no
## GL errors in `adb logcat`.
##
## The box spins slowly so a still screenshot cannot hide a stalled render loop.
## No gameplay logic belongs in this file.

const SPIN_SPEED_RAD_PER_SEC: float = 0.6
const LABEL_REFRESH_SEC: float = 0.5

var _elapsed: float = 0.0

@onready var _test_box: MeshInstance3D = $TestBox
@onready var _info_label: Label = $UI/Root/InfoLabel


func _ready() -> void:
	_refresh_label()
	print("[boot] boot.tscn ready — mesh=%s texture=%s"
		% [str(_test_box.mesh != null), str(_box_texture_name())])


func _process(delta: float) -> void:
	# Unshaded + rotating: if the GPU stalls, the box freezes visibly.
	_test_box.rotation.y += SPIN_SPEED_RAD_PER_SEC * delta

	_elapsed += delta
	if _elapsed >= LABEL_REFRESH_SEC:
		_elapsed = 0.0
		_refresh_label()


func _refresh_label() -> void:
	if _info_label == null:
		return
	# PerfManager is an autoload, so it is already _ready() before this scene.
	_info_label.text = "DESI ROYALE — Phase 1 boot baseline\n%s\n%s" % [
		PerfManager.get_status_line(),
		PerfManager.get_stats_line(),
	]


func _box_texture_name() -> String:
	var material: Material = _test_box.material_override
	if material is StandardMaterial3D:
		var texture: Texture2D = (material as StandardMaterial3D).albedo_texture
		if texture != null and not texture.resource_path.is_empty():
			return texture.resource_path.get_file()
	return "none"
