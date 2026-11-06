class_name IslandZone
extends Resource
## One named, colour-coded region of Siran Island (Phase 3 greybox).
##
## A zone is a 2D footprint on the XZ plane plus a *bake height*: the terrain
## baker pulls the ground towards `flat_height` by `flatness` inside the
## footprint, which is what makes the aircraft runway "perfectly flat" and the
## graveyard "hilly but readable".
##
## Zones never touch the mesh at runtime — they are bake-time data plus a tint
## baked into the terrain vertex colours. At most the runtime asks
## `SiranMapConfig.get_zone_at()` / `get_zone_by_id()` for the zone the player
## is standing in.

## Footprint shape. Rounded rectangles suit runways and city blocks, ellipses
## suit organic areas.
enum Shape {
	ELLIPSE = 0,
	ROUNDED_RECT = 1,
}

## What the zone is, for terrain palette + gameplay hooks later.
enum Kind {
	INDUSTRIAL = 0,
	DOCK = 1,
	URBAN = 2,
	GRAVEYARD = 3,
	AIRFIELD = 4,
}

@export var id: StringName = &""
@export var display_name: String = ""
@export var kind: Kind = Kind.URBAN
## World XZ centre, in metres.
@export var center: Vector2 = Vector2.ZERO
## Full width (X) and depth (Z) of the footprint, in metres.
@export var size: Vector2 = Vector2(600.0, 600.0)
@export var rotation_deg: float = 0.0
@export var shape: Shape = Shape.ELLIPSE
## Corner radius for `ROUNDED_RECT` footprints, in metres.
@export var corner_radius_m: float = 80.0
## Ground height (metres above sea level) the zone is flattened towards.
@export var flat_height: float = 16.0
## 0 = keep the generated terrain, 1 = perfectly flat at `flat_height`.
@export_range(0.0, 1.0, 0.01) var flatness: float = 1.0
## Width of the smooth band between "fully flat" and "untouched terrain".
@export var feather_m: float = 120.0
## Flat colour used for the zone marker, applied as terrain vertex colour.
@export var tint: Color = Color(0.55, 0.55, 0.5)


func signed_distance(x: float, z: float) -> float:
	## Metres from (x, z) to the footprint edge; negative inside.
	if shape == Shape.ROUNDED_RECT:
		return IslandShapeMath.rounded_rect_signed_distance(
				x, z, center, size, rotation_deg, corner_radius_m)
	return IslandShapeMath.ellipse_signed_distance(x, z, center, size, rotation_deg)


func weight_at(x: float, z: float) -> float:
	## 1.0 inside the footprint, fading to 0.0 `feather_m` outside it.
	return IslandShapeMath.feather_weight(signed_distance(x, z), feather_m)


func contains(x: float, z: float) -> bool:
	return signed_distance(x, z) <= 0.0


func describe() -> String:
	return "%s (%s) h=%.1fm flat=%.2f" % [display_name, Kind.keys()[kind], flat_height, flatness]
