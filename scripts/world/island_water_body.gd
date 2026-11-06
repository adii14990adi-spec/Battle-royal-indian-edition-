class_name IslandWaterBody
extends Resource
## A still water body: the sea is implicit (a plane at `SiranMapConfig.sea_level`),
## a lake is one of these.
##
## The surface is a flat rectangle in the world scene; because the terrain under
## it is carved into a bowl, only the submerged part of the plane is visible. No
## transparency, no refraction, one draw call.

@export var id: StringName = &""
@export var display_name: String = ""
## World XZ centre, in metres.
@export var center: Vector2 = Vector2.ZERO
## Semi-axes of the basin ellipse, in metres.
@export var radius: Vector2 = Vector2(500.0, 400.0)
## Water surface height above sea level, in metres.
@export var surface_height: float = 9.0
## How far the lake bed drops below the surface at its centre.
@export var depth: float = 11.0
## Width of there shore band where the bowl blends back into the terrain.
@export var shore_feather_m: float = 70.0


func normalised_distance(x: float, z: float) -> float:
	## 0.0 at the centre, 1.0 on the shoreline ellipse, >1 outside.
	var dx: float = (x - center.x) / maxf(radius.x, 0.001)
	var dz: float = (z - center.y) / maxf(radius.y, 0.001)
	return sqrt(dx * dx + dz * dz)


func contains(x: float, z: float) -> bool:
	return normalised_distance(x, z) <= 1.0
