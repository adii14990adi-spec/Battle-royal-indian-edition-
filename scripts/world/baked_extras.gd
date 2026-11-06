class_name BakedExtras
extends MeshInstance3D
## Loads the pieces of the world that are *baked but not chunked*: the river's
## water ribbon and the bake summary.
##
## The world scene cannot reference these files directly, because they do not
## exist until the terrain has been baked — a `[ext_resource]` pointing at a
## missing file makes `siran_island.tscn` fail to open, which would mean a fresh
## clone could not even launch the boot scene. So the scene ships with this
## loader node and an empty mesh, and the loader fills it in if the bake exists.

const RIVER_MESH_PATH: String = "res://resources/world/baked/siran_river_surface.res"
const SUMMARY_PATH: String = "res://resources/world/baked/siran_island_summary.tres"

var summary: SiranBakeSummary


func _ready() -> void:
	_load_summary()
	_load_river()


func _load_summary() -> void:
	if not ResourceLoader.exists(SUMMARY_PATH):
		print("[world] no bake summary yet — run tools/bake_siran_island.sh")
		return
	summary = ResourceLoader.load(SUMMARY_PATH) as SiranBakeSummary
	if summary == null:
		push_warning("[world] %s is not a SiranBakeSummary" % SUMMARY_PATH)
		return
	print("[world] bake v%d seed=%d %s"
			% [summary.generator_version, summary.noise_seed, summary.human_size()])
	if summary.is_stale(SiranMapConfig.shared()):
		push_warning("[world] baked chunks are stale (bake v%d, config v%d) — re-bake"
				% [summary.generator_version, SiranMapConfig.shared().generator_version])


func _load_river() -> void:
	if not ResourceLoader.exists(RIVER_MESH_PATH):
		visible = false
		return
	mesh = ResourceLoader.load(RIVER_MESH_PATH) as Mesh
	visible = mesh != null


func get_summary() -> SiranBakeSummary:
	return summary
