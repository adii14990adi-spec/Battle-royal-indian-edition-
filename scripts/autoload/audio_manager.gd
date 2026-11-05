extends Node
## AudioManager — placeholder for the offline audio layer (Phase 2+).
##
## Phase 1 only fixes the bus layout and the file conventions the rest of the
## project must follow, so that Phase 2 can drop .ogg files in without a refactor:
##   assets/audio/music/  streamed .ogg, music bus
##   assets/audio/sfx/    .ogg, 22050 Hz mono, sfx bus
##   assets/audio/voice/  .ogg, Hindi / regional VO packs, voice bus
##
## When it becomes an autoload, add to project.godot:
##   [autoload]
##   AudioManager="*res://scripts/autoload/audio_manager.gd"

const MUSIC_BUS: String = "Music"
const SFX_BUS: String = "SFX"
const VOICE_BUS: String = "Voice"

## Target sample rate for every imported .ogg (keeps decode cost and RAM low on
## a Unisoc T606). Enforce this at import time, not at runtime.
const SFX_SAMPLE_RATE: int = 22050


func get_bus_volume_db(bus_name: String) -> float:
	var index: int = AudioServer.get_bus_index(bus_name)
	if index < 0:
		return 0.0
	return AudioServer.get_bus_volume_db(index)


func set_bus_volume_db(bus_name: String, volume_db: float) -> void:
	var index: int = AudioServer.get_bus_index(bus_name)
	if index < 0:
		push_warning("AudioManager: unknown bus '%s'" % bus_name)
		return
	AudioServer.set_bus_volume_db(index, volume_db)
