class_name IslandRoad
extends Resource
## A graded road: a polyline the terrain is flattened towards, plus the ribbon
## mesh that is baked on top of it.
##
## The baker resamples every road into `baked_points` (one every few metres),
## samples the terrain height under each sample, smooths it and clamps the grade,
## and stores the result in `baked_heights`. Terrain flattening *and* the road
## ribbon are both driven by those two arrays, which is why the asphalt never
## floats off the ground or sinks into it.

@export var id: StringName = &""
@export var display_name: String = ""
## Authored centreline, world XZ metres.
@export var points: PackedVector2Array = PackedVector2Array()
## Asphalt width (full), metres.
@export var width: float = 16.0
## Graded shoulder on each side, beyond the asphalt, metres.
@export var shoulder_width: float = 7.0
## Vertical offset of the asphalt above the graded ground, metres. Small
## positive value; kills z-fighting without a visible seam.
@export var surface_offset: float = 0.18

## --- Bake-time scratch data (filled by IslandBaker) -------------------------
## Resampled centreline, world XZ metres.
var baked_points: PackedVector2Array = PackedVector2Array()
## Ground height at each `baked_points` entry, in metres.
var baked_heights: PackedFloat32Array = PackedFloat32Array()


func total_half_width() -> float:
	return width * 0.5 + shoulder_width


func query(x: float, z: float) -> Vector3:
	## `Vector3(distance_m, t_normalised, segment_and_fraction)` against the
	## authored centreline (used before `baked_points` exist).
	return IslandShapeMath.query_polyline(points, x, z)


func graded_height_at(position: float) -> float:
	## Ground height at `segment_and_fraction` (the z of `query()`).
	if baked_heights.is_empty():
		return 0.0
	return IslandShapeMath.sample_packed_float(baked_heights, position)


func graded_distance_at(x: float, z: float) -> Vector3:
	## Nearest-point query against the resampled centreline, which is what the
	## flattening pass and the ribbon builder both use.
	var source: PackedVector2Array = baked_points if not baked_points.is_empty() else points
	return IslandShapeMath.query_polyline(source, x, z)
