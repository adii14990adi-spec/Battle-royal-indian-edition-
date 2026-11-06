class_name IslandChannel
extends Resource
## A carved water channel — a moat, a strait, a canal.
##
## Used to cut Factory Island loose from the mainland: a closed loop of water
## around the zone. Terrain inside the channel is pushed below sea level, then
## the roads are allowed to build a causeway back across it.

@export var id: StringName = &""
@export var display_name: String = ""
## Closed loop, world XZ metres. The last point implicitly connects to the first.
@export var points: PackedVector2Array = PackedVector2Array()
## Half width of the wet channel, in metres.
@export var half_width_m: float = 28.0
## How far below sea level the channel bed sits, in metres.
@export var depth_m: float = 3.5
## Bank blend outside the channel, in metres.
@export var bank_width_m: float = 26.0


func query(x: float, z: float) -> Vector3:
	return IslandShapeMath.query_polyline(points, x, z, true)
