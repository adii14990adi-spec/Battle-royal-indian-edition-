extends SceneTree
## Headless bake entry point for Siran Island. **Bake-time only.**
##
##     godot --headless --path . --script res://tools/bake/bake_siran_island.gd
##     godot --headless --path . --script res://tools/bake/bake_siran_island.gd -- --spacing=5
##
## Normally you run `bash tools/bake_siran_island.sh`, which imports the project
## first and runs the self-check afterwards.
##
## Options (after the `--` separator):
##   --spacing=<m>   terrain sample spacing, default 10
##   --chunk=<m>     chunk edge length, default 500
##   --seed=<int>    noise seed, default from SiranMapConfig
##   --help
##
## Exit codes: 0 baked, 1 failed (config rejected or a file could not be written).
##
## This script is loaded by `--script`, so it runs as the SceneTree itself. It
## does its work in `_init()` and quits, which is the pattern the engine
## documents for command-line tools — no window, no frame loop, no main scene.

const HELP_TEXT: String = """Siran Island baker (Phase 3 greybox)

  godot --headless --path . --script res://tools/bake/bake_siran_island.gd -- [options]

Options:
  --spacing=<metres>  terrain sample spacing (default 10; 5 doubles map size)
  --chunk=<metres>    chunk edge length (default 500; must divide map size)
  --seed=<int>        noise seed
  --help              this text

Outputs:
  scenes/world/terrain/chunk_<x>_<y>.tscn   one per 500 m chunk + collision
  resources/world/terrain/*.res             baked meshes and HeightMapShape3D
  resources/world/baked/                    river surface + bake summary
  docs/images/siran_island_preview.png      top-down layout preview
"""


func _init() -> void:
	var arguments: PackedStringArray = OS.get_cmdline_user_args()
	if arguments.has("--help") or arguments.has("-h"):
		print(HELP_TEXT)
		quit(0)
		return

	var config: SiranMapConfig = SiranMapConfig.default_siran_island()
	if not _apply_overrides(config, arguments):
		quit(1)
		return

	var baker := IslandBaker.new(config)
	var summary: SiranBakeSummary = baker.bake()
	if summary == null:
		printerr("[bake] FAILED — see the errors above")
		quit(1)
		return
	quit(0)


func _apply_overrides(config: SiranMapConfig, arguments: PackedStringArray) -> bool:
	## Every override is validated by `SiranMapConfig.validate()` inside the
	## baker, so an impossible combination fails there instead of baking a map
	## whose chunks do not line up.
	var ok: bool = true
	for argument in arguments:
		if argument.begins_with("--spacing="):
			var spacing: float = argument.trim_prefix("--spacing=").to_float()
			if spacing <= 0.0:
				printerr("[bake] --spacing must be positive")
				ok = false
			else:
				config.vertex_spacing_m = spacing
				# Generator output changed shape -> the version must change too,
				# or a stale-bake warning never fires.
				config.generator_version += 1
		elif argument.begins_with("--chunk="):
			var chunk: float = argument.trim_prefix("--chunk=").to_float()
			if chunk <= 0.0:
				printerr("[bake] --chunk must be positive")
				ok = false
			else:
				config.chunk_size_m = chunk
		elif argument.begins_with("--seed="):
			config.noise_seed = argument.trim_prefix("--seed=").to_int()
			config.generator_version += 1
		elif argument.begins_with("--"):
			printerr("[bake] unknown option: %s (try --help)" % argument)
			ok = false
	return ok
