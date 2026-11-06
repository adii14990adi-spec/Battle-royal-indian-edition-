class_name IslandRiver
extends Resource
## A river: a polyline with a width profile, a *descending* water surface and a
## carved channel.
##
## Unlike the lake, a river's surface is not flat: it starts at the lake outlet
## and drops to sea level at the mouth. Both the terrain carve and the baked
## water ribbon use `surface_at()`, so they can never disagree.

@export var id: StringName = &"river"
@export var display_name: String = "Siran River"
## Centreline, world XZ metres. First point is the source (usually a lake
## outlet), last point is the mouth (below sea level).
@export var points: PackedVector2Array = PackedVector2Array()
## Full channel width at each centreline point, in metres. Same length as
## `points`; interpolated in between.
@export var widths: PackedFloat32Array = PackedFloat32Array()
## Water surface height above sea level at the first / last point, in metres.
@export var surface_start: float = 9.0
@export var surface_end: float = -0.4
## Water depth at the centreline, in metres.
@export var depth: float = 2.6
## Bank blend width outside the water edge, in metres.
@export var bank_width_m: float = 22.0
## Extra excavation below the water surface at the source, so the lake and the
## river connect without a lip of terrain between them.
@export var source_blend_m: float = 90.0


func surface_at(t: float) -> float:
	## Water surface height at normalised position `t` along the centreline.
	## Eased so the river falls fast out of the hills and flattens near the sea.
	var clamped: float = clampf(t, 0.0, 1.0)
	return lerpf(surface_start, surface_end, IslandShapeMath.smooth01(clamped))


func half_width_at(position: float) -> float:
	return IslandShapeMath.sample_packed_float(widths, position) * 0.5


func query(x: float, z: float) -> Vector3:
	## `Vector3(distance_m, t_normalised, segment_and_fraction)`.
	return IslandShapeMath.query_polyline(points, x, z)


func water_surface_at(x: float, z: float) -> float:
	## Convenience for road causeways: surface height at the nearest point.
	if points.size() < 2:
		return surface_start
	return surface_at(query(x, z).y)
