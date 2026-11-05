extends Node
## PerfManager — autoload singleton (registered as `PerfManager` in project.godot).
##
## Phase 1 stub, deliberately tiny. It does two jobs while the project still has
## no gameplay:
##
##   1. Proves the GLES3 / "Compatibility" pipeline is alive on the target device
##      by reporting model, GPU, driver and renderer method at boot.
##   2. Logs Performance.get_monitor(Performance.MEMORY_STATIC) once per second so
##      `adb logcat -s godot` gives a cheap memory trace during the Phase 1 soak
##      test (60 s, airplane mode, physical Unisoc T606).
##
## Budget it is guarding (see docs/UNISOC_BUDGET.md):
##   <= 800 MB PSS, <= 180 MB VRAM, <= 60 draw calls, 30 FPS floor.
##
## Keep _process() allocation-free: it runs every frame on a 4 GB device.

const LOG_INTERVAL_SEC: float = 1.0
const BYTES_PER_MB: float = 1024.0 * 1024.0

## Device / pipeline facts, collected once in _ready().
var device_model: String = "unknown"
var gpu_name: String = "unknown"
var gpu_vendor: String = "unknown"
var gl_api_version: String = "unknown"
var rendering_method: String = "unknown"
var rendering_driver: String = "unknown"

var _log_accumulator: float = 0.0


func _ready() -> void:
	_collect_device_info()
	_log_startup_banner()
	# Keep ticking even if the boot scene never loads (e.g. a crash during load),
	# so the logcat trace always tells us where the process died.
	set_process(true)


func _process(delta: float) -> void:
	_log_accumulator += delta
	if _log_accumulator < LOG_INTERVAL_SEC:
		return
	_log_accumulator = 0.0
	log_memory_once()


# --- Queries used by scenes ---------------------------------------------------

## True when the engine is running the GLES3 Compatibility baseline we ship.
func is_gles3_baseline_ok() -> bool:
	# Driver names seen in the wild: "opengl3" (desktop / Android), "opengl3_es",
	# "opengl3_angle". Anything else means we are not on the GLES3 path.
	var driver_ok: bool = rendering_driver.begins_with("opengl3")
	var method_ok: bool = rendering_method == "gl_compatibility"
	return driver_ok and method_ok


## One-line proof for the on-device label:
## "Device: Galaxy A03 | GPU: Mali-G57 | GLES3 OK"
func get_status_line() -> String:
	var state: String = "OK" if is_gles3_baseline_ok() else "FAIL (%s / %s)" % [rendering_method, rendering_driver]
	return "Device: %s | GPU: %s | GLES3 %s" % [device_model, gpu_name, state]


## Live counters, refreshed by the boot label twice a second.
func get_stats_line() -> String:
	var static_mb: float = Performance.get_monitor(Performance.MEMORY_STATIC) / BYTES_PER_MB
	var video_mb: float = Performance.get_monitor(Performance.RENDER_VIDEO_MEM_USED) / BYTES_PER_MB
	var fps: int = int(round(Performance.get_monitor(Performance.TIME_FPS)))
	var draw_calls: int = int(Performance.get_monitor(Performance.RENDER_TOTAL_DRAW_CALLS_IN_FRAME))
	return "FPS: %d | static: %.1f MB | video: %.1f MB | draws: %d" % [fps, static_mb, video_mb, draw_calls]


## The Phase 1 acceptance requirement: static memory to the console every second.
func log_memory_once() -> void:
	var static_bytes: float = Performance.get_monitor(Performance.MEMORY_STATIC)
	var static_mb: float = static_bytes / BYTES_PER_MB
	var video_mb: float = Performance.get_monitor(Performance.RENDER_VIDEO_MEM_USED) / BYTES_PER_MB
	var fps: int = int(round(Performance.get_monitor(Performance.TIME_FPS)))
	var draw_calls: int = int(Performance.get_monitor(Performance.RENDER_TOTAL_DRAW_CALLS_IN_FRAME))
	print("[perf] static_mem=%.2f MB (%.0f bytes) video_mem=%.2f MB fps=%d draw_calls=%d"
		% [static_mb, static_bytes, video_mb, fps, draw_calls])


# --- Internals ----------------------------------------------------------------

func _collect_device_info() -> void:
	var model: String = OS.get_model_name()
	if model.is_empty() or model == "unknown":
		# Desktop / CI fallback: OS.get_model_name() is only meaningful on mobile.
		model = "%s (%s)" % [OS.get_name(), OS.get_processor_name()]
	device_model = model

	gpu_name = RenderingServer.get_video_adapter_name()
	gpu_vendor = RenderingServer.get_video_adapter_vendor()
	gl_api_version = RenderingServer.get_video_adapter_api_version()
	rendering_method = RenderingServer.get_current_rendering_method()
	rendering_driver = RenderingServer.get_current_rendering_driver_name()


func _log_startup_banner() -> void:
	print("[perf] ---- Desi Royale boot ----")
	print("[perf] engine=%s" % Engine.get_version_info().string)
	print("[perf] device=%s os=%s" % [device_model, OS.get_name()])
	print("[perf] gpu=%s vendor=%s gl=%s" % [gpu_name, gpu_vendor, gl_api_version])
	print("[perf] renderer_method=%s driver=%s" % [rendering_method, rendering_driver])
	print("[perf] gles3_baseline=%s" % ("OK" if is_gles3_baseline_ok() else "FAIL"))
	print("[perf] max_fps=%d physics_ticks=%d vram_budget_mb=180"
		% [Engine.max_fps, Engine.physics_ticks_per_second])
