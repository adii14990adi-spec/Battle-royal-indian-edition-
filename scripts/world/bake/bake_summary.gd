class_name SiranBakeSummary
extends Resource
## Written by `IslandBaker`, read by the debug HUD and `verify_phase3.sh`.
##
## Its whole job is to make a stale bake visible: `generator_version` and
## `noise_seed` are compared against `SiranMapConfig` at runtime, and the HUD
## shouts if the chunk on disk was produced by an older generator than the code.

@export var display_name: String = ""
@export var generator_version: int = 0
@export var noise_seed: int = 0
@export var baked_at_utc: String = ""
@export var bake_duration_ms: int = 0

@export var map_size_m: float = 0.0
@export var chunk_size_m: float = 0.0
@export var vertex_spacing_m: float = 0.0
@export var chunk_count: int = 0
@export var chunks_written: int = 0
@export var grid_vertices_per_axis: int = 0
@export var terrain_vertices_total: int = 0
@export var terrain_triangles_total: int = 0

@export var min_height_m: float = 0.0
@export var max_height_m: float = 0.0
@export var sea_level: float = 0.0
@export var spawn_ground_height_m: float = 0.0

## Per-zone integrity numbers, keyed by zone id (Dictionary of Dictionary:
## `min_height`, `max_height`, `flat_deviation`, `samples`).
@export var zone_stats: Dictionary = {}
## Non-fatal issues found while baking (roads through water, etc.).
@export var warnings: PackedStringArray = PackedStringArray()

## Byte size of everything the bake wrote, for the APK budget.
@export var chunk_scene_bytes: int = 0
@export var mesh_bytes: int = 0
@export var collision_bytes: int = 0


func is_stale(config: SiranMapConfig) -> bool:
	return config != null and not (
		generator_version == config.generator_version and noise_seed == config.noise_seed)


func human_size() -> String:
	var total_mb: float = float(chunk_scene_bytes + mesh_bytes + collision_bytes) / 1048576.0
	return "%.1f MB baked (%.0f KB scenes, %.1f MB meshes, %.1f MB collision)" % [
		total_mb,
		float(chunk_scene_bytes) / 1024.0,
		float(mesh_bytes) / 1048576.0,
		float(collision_bytes) / 1048576.0,
	]
