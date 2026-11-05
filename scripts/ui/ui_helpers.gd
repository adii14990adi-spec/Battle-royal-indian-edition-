extends RefCounted
## Small, allocation-light helpers shared by the UI scenes.
## Keep this file free of node references so it can be used from anywhere.

const BYTES_PER_MB: float = 1024.0 * 1024.0


## "812.4 MB" — used by the boot label and (later) the debug HUD.
static func format_mb(bytes: float) -> String:
	return "%.1f MB" % (bytes / BYTES_PER_MB)


## "01:42" — match timer / storm countdown formatting.
static func format_clock(seconds: float) -> String:
	var total: int = maxi(0, int(seconds))
	return "%02d:%02d" % [total / 60, total % 60]


## Devices in the Unisoc T606 / T310 class have wide notches in landscape.
## Clamp a label's left inset so text never hides behind the cutout.
static func safe_left_inset(position_px: int, min_inset: int = 24) -> int:
	return maxi(position_px, min_inset)
