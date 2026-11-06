class_name SiranMapConfig
extends Resource
## The whole layout of Siran Island in one auditable place.
##
## Everything the baker needs — extents, chunk grid, zone footprints, water
## bodies, rivers, carved channels, roads — lives here as plain data, so the
## terrain is reproducible from `noise_seed` alone and the runtime can answer
## "which zone am I in" without loading a single mesh.
##
## World space: metres, X = east, Z = south, Y = up. The map is centred on the
## origin, so a 4000 m map spans -2000 .. +2000 on both axes.
##
## The canonical layout is built by `default_siran_island()` — code, not a .tres,
## so a bad hand-merged resource can never silently move the runway. Designers
## can still `duplicate()` + save a .tres for experiments.

const MAP_SIZE_DEFAULT_M: float = 4000.0
const CHUNK_SIZE_DEFAULT_M: float = 500.0
const VERTEX_SPACING_DEFAULT_M: float = 10.0
## Process-wide layout instance. Runtime nodes (streamer, spectator, HUD) all
## read the same object so a zone edit can never be half-applied, and nobody
## pays for building the layout twice.
static var _shared: SiranMapConfig

@export var display_name: String = "Siran Island"
## Square map edge length in metres.
@export var map_size_m: float = MAP_SIZE_DEFAULT_M
## Edge length of one streaming chunk in metres.
@export var chunk_size_m: float = CHUNK_SIZE_DEFAULT_M
## Distance between two terrain vertices in metres (bake resolution).
@export var vertex_spacing_m: float = VERTEX_SPACING_DEFAULT_M
## Height of the open sea, in metres. The ocean plane sits here.
@export var sea_level: float = 0.0
## Radius of the landmass before the coastline noise wobbles it, in metres.
@export var island_radius_m: float = 1950.0
## How far the coastline noise may push the shore in or out, in metres.
@export var coastline_noise_m: float = 170.0
## Hard clamp for the generated heightmap, in metres.
@export var min_height_m: float = -38.0
@export var max_height_m: float = 132.0
## Written into the bake summary; bump it whenever the generator math changes so
## stale baked chunks are obvious.
@export var generator_version: int = 3
## Single seed for every FastNoiseLite layer.
@export var noise_seed: int = 20261106

@export var zones: Array[IslandZone] = []
@export var lake: IslandWaterBody
@export var river: IslandRiver
@export var channels: Array[IslandChannel] = []
@export var roads: Array[IslandRoad] = []

## Where a match starts (and where the debug spectator spawns), world metres.
@export var spawn_point: Vector3 = Vector3(40.0, 28.0, -30.0)




static func shared() -> SiranMapConfig:
	if _shared == null:
		_shared = default_siran_island()
	return _shared


# --- Canonical Siran Island layout --------------------------------------------

static func default_siran_island() -> SiranMapConfig:
	## The one true Phase 3 layout. Every number here was chosen so the five
	## zones read at a glance from 60 m up: north-west industrial plateau,
	## mid-west docks, a hilly east, a dead-flat southern runway, and a lake in
	## the north-middle feeding a river down the east side to the sea.
	var config := SiranMapConfig.new()
	config.display_name = "Siran Island"
	config.map_size_m = MAP_SIZE_DEFAULT_M
	config.chunk_size_m = CHUNK_SIZE_DEFAULT_M
	config.vertex_spacing_m = VERTEX_SPACING_DEFAULT_M
	config.sea_level = 0.0
	config.island_radius_m = 1950.0
	config.coastline_noise_m = 170.0
	config.min_height_m = -38.0
	config.max_height_m = 132.0
	config.noise_seed = 20261106
	config.generator_version = 3
	config.spawn_point = Vector3(40.0, 28.0, -30.0)

	config.zones = [
		_make_zone(&"factory_island", "Factory Island — Iron Mill", IslandZone.Kind.INDUSTRIAL,
				Vector2(-1250.0, -1300.0), Vector2(760.0, 640.0), -12.0,
				IslandZone.Shape.ELLIPSE, 26.0, 1.0, 90.0, Color(0.41, 0.39, 0.35)),
		_make_zone(&"konkan_docks", "Dock Island — Konkan Docks", IslandZone.Kind.DOCK,
				Vector2(-1470.0, 130.0), Vector2(780.0, 540.0), 6.0,
				IslandZone.Shape.ELLIPSE, 4.5, 1.0, 110.0, Color(0.38, 0.40, 0.43)),
		_make_zone(&"central_state", "Central State — Urban", IslandZone.Kind.URBAN,
				Vector2(40.0, -30.0), Vector2(1150.0, 1150.0), 0.0,
				IslandZone.Shape.ROUNDED_RECT, 16.0, 0.72, 170.0, Color(0.52, 0.50, 0.45)),
		_make_zone(&"graveyard_center", "Graveyard Center — Foggy Hills",
				IslandZone.Kind.GRAVEYARD, Vector2(1350.0, -80.0), Vector2(820.0, 900.0),
				12.0, IslandZone.Shape.ELLIPSE, 42.0, 0.55, 190.0,
				Color(0.33, 0.36, 0.32)),
		_make_zone(&"aircraft_industry", "Aircraft Industry — Hangars & Runway",
				IslandZone.Kind.AIRFIELD, Vector2(120.0, 1480.0), Vector2(1020.0, 740.0),
				0.0, IslandZone.Shape.ROUNDED_RECT, 12.0, 1.0, 90.0,
				Color(0.61, 0.60, 0.56)),
	]

	var lake_body := IslandWaterBody.new()
	lake_body.id = &"siran_lake"
	lake_body.display_name = "Siran Lake"
	lake_body.center = Vector2(250.0, -1150.0)
	lake_body.radius = Vector2(520.0, 380.0)
	lake_body.surface_height = 9.0
	lake_body.depth = 11.0
	lake_body.shore_feather_m = 80.0
	config.lake = lake_body

	var siran_river := IslandRiver.new()
	siran_river.id = &"siran_river"
	siran_river.display_name = "Siran River"
	siran_river.points = PackedVector2Array([
		Vector2(250.0, -790.0),
		Vector2(500.0, -520.0),
		Vector2(760.0, -300.0),
		Vector2(880.0, -40.0),
		Vector2(900.0, 420.0),
		Vector2(960.0, 900.0),
		Vector2(1080.0, 1380.0),
		Vector2(1160.0, 1750.0),
		Vector2(1200.0, 2060.0),
	])
	siran_river.widths = PackedFloat32Array([70.0, 72.0, 76.0, 80.0, 86.0, 92.0, 100.0, 110.0, 110.0])
	siran_river.surface_start = 9.0
	siran_river.surface_end = -0.5
	siran_river.depth = 2.8
	siran_river.bank_width_m = 24.0
	siran_river.source_blend_m = 90.0
	config.river = siran_river

	var moat := IslandChannel.new()
	moat.id = &"iron_mill_moat"
	moat.display_name = "Iron Mill Channel"
	moat.points = _ring(Vector2(-1250.0, -1300.0), Vector2(490.0, 430.0), -12.0, 20, 0.055)
	moat.half_width_m = 34.0
	moat.depth_m = 4.5
	moat.bank_width_m = 30.0
	config.channels = [moat]

	config.roads = [
		_make_road(&"docks_central", "Konkan Docks → Central State",
				PackedVector2Array([
					Vector2(-1470.0, 130.0), Vector2(-1180.0, 110.0),
					Vector2(-820.0, 70.0), Vector2(-430.0, 20.0), Vector2(40.0, -30.0),
				])),
		_make_road(&"factory_central", "Iron Mill → Central State",
				PackedVector2Array([
					Vector2(-1250.0, -1300.0), Vector2(-1080.0, -980.0),
					Vector2(-880.0, -700.0), Vector2(-560.0, -420.0),
					Vector2(-160.0, -160.0), Vector2(40.0, -30.0),
				])),
		_make_road(&"central_airbase", "Central State → Aircraft Industry",
				PackedVector2Array([
					Vector2(40.0, -30.0), Vector2(60.0, 320.0), Vector2(80.0, 760.0),
					Vector2(110.0, 1120.0), Vector2(120.0, 1480.0),
				])),
		_make_road(&"central_graveyard", "Central State → Graveyard Center",
				PackedVector2Array([
					Vector2(40.0, -30.0), Vector2(380.0, -140.0), Vector2(700.0, -160.0),
					Vector2(1000.0, -120.0), Vector2(1350.0, -80.0),
				])),
		_make_road(&"coast_highway", "Coast Highway — Aircraft Industry → Konkan Docks",
				PackedVector2Array([
					Vector2(120.0, 1480.0), Vector2(-350.0, 1330.0), Vector2(-900.0, 940.0),
					Vector2(-1280.0, 560.0), Vector2(-1470.0, 130.0),
				])),
	]
	return config


static func _make_zone(id: StringName, display_name: String, kind: IslandZone.Kind,
		center: Vector2, size: Vector2, rotation_deg: float, shape: IslandZone.Shape,
		flat_height: float, flatness: float, feather_m: float,
		tint: Color) -> IslandZone:
	var zone := IslandZone.new()
	zone.id = id
	zone.display_name = display_name
	zone.kind = kind
	zone.center = center
	zone.size = size
	zone.rotation_deg = rotation_deg
	zone.shape = shape
	zone.flat_height = flat_height
	zone.flatness = flatness
	zone.feather_m = feather_m
	zone.tint = tint
	zone.corner_radius_m = minf(size.x, size.y) * 0.22
	return zone


static func _make_road(id: StringName, display_name: String,
		points: PackedVector2Array) -> IslandRoad:
	var road := IslandRoad.new()
	road.id = id
	road.display_name = display_name
	road.points = points
	road.width = 16.0
	road.shoulder_width = 7.0
	return road


static func _ring(center: Vector2, radii: Vector2, rotation_deg: float, segments: int,
		wobble: float) -> PackedVector2Array:
	## Deterministic dented ellipse, used for the Iron Mill moat. No noise object
	## is created here: the wobble has to be identical on every machine.
	var points := PackedVector2Array()
	for i in range(segments):
		var angle: float = TAU * float(i) / float(segments)
		var wobble_scale: float = 1.0 + wobble * sin(angle * 3.0) + wobble * 0.6 * cos(angle * 5.0)
		var local := Vector2(cos(angle) * radii.x, sin(angle) * radii.y) * wobble_scale
		points.append(center + IslandShapeMath.rotate_xz(local, rotation_deg))
	return points


# --- Derived grid geometry ----------------------------------------------------

func chunks_per_side() -> int:
	return maxi(1, int(round(map_size_m / chunk_size_m)))


func chunk_count() -> int:
	var side: int = chunks_per_side()
	return side * side


func chunk_vertices_per_side() -> int:
	## Vertices along one edge of a chunk (shared with the neighbouring chunk).
	return maxi(2, int(round(chunk_size_m / vertex_spacing_m)) + 1)


func chunk_cells_per_side() -> int:
	return chunk_vertices_per_side() - 1


func grid_vertices_per_axis() -> int:
	return chunks_per_side() * chunk_cells_per_side() + 1


func half_extent() -> float:
	return map_size_m * 0.5


func is_valid_chunk(chunk: Vector2i) -> bool:
	var side: int = chunks_per_side()
	return chunk.x >= 0 and chunk.y >= 0 and chunk.x < side and chunk.y < side


func chunk_rect(chunk: Vector2i) -> Rect2:
	## World-XZ bounds of a chunk. Only X/Z are meaningful.
	var origin := Vector2(-half_extent() + float(chunk.x) * chunk_size_m,
			-half_extent() + float(chunk.y) * chunk_size_m)
	return Rect2(origin, Vector2(chunk_size_m, chunk_size_m))


func chunk_center(chunk: Vector2i) -> Vector3:
	var rect: Rect2 = chunk_rect(chunk)
	return Vector3(rect.position.x + rect.size.x * 0.5, 0.0, rect.position.y + rect.size.y * 0.5)


func world_to_chunk(x: float, z: float) -> Vector2i:
	## Chunk coordinate containing the world XZ point, clamped to the grid.
	var side: int = chunks_per_side()
	var cx: int = clampi(int(floor((x + half_extent()) / chunk_size_m)), 0, side - 1)
	var cz: int = clampi(int(floor((z + half_extent()) / chunk_size_m)), 0, side - 1)
	return Vector2i(cx, cz)


func chunk_at_position(position: Vector3) -> Vector2i:
	return world_to_chunk(position.x, position.z)


func world_to_grid(x: float, z: float) -> Vector2:
	## Continuous global grid coordinates (0..grid_vertices_per_axis()-1).
	var spacing: float = vertex_spacing_m
	return Vector2((x + half_extent()) / spacing, (z + half_extent()) / spacing)


# --- Runtime queries ----------------------------------------------------------

func get_zone_at(x: float, z: float) -> IslandZone:
	## Innermost zone containing the point (smallest footprint wins on overlap).
	var best: IslandZone = null
	var best_area: float = INF
	for zone in zones:
		if zone == null or not zone.contains(x, z):
			continue
		var area: float = zone.size.x * zone.size.y
		if area < best_area:
			best_area = area
			best = zone
	return best


func get_zone_by_id(id: StringName) -> IslandZone:
	for zone in zones:
		if zone != null and zone.id == id:
			return zone
	return null


func water_surface_at(x: float, z: float, river_water_visible: bool = true) -> float:
	## Highest water surface at a point: sea, lake or river.
	var surface: float = sea_level
	if lake != null and lake.contains(x, z):
		surface = maxf(surface, lake.surface_height)
	if river_water_visible and river != null and river.points.size() >= 2:
		var query: Vector3 = river.query(x, z)
		if query.x <= river.half_width_at(query.z):
			surface = maxf(surface, river.surface_at(query.y))
	return surface


func describe() -> String:
	return "%s %.0f×%.0fm, %d chunks of %.0fm, %d zones, %d roads" % [
		display_name, map_size_m, map_size_m, chunk_count(), chunk_size_m,
		zones.size(), roads.size(),
	]


# --- Integrity checks ---------------------------------------------------------

func validate() -> PackedStringArray:
	## Returns human-readable problems; empty means the layout is coherent.
	## Called by the baker before it writes anything and by the Phase 3
	## self-check scene, so a bad edit fails loudly instead of baking a map with
	## a runway in the sea.
	var problems := PackedStringArray()
	if map_size_m <= 0.0 or chunk_size_m <= 0.0 or vertex_spacing_m <= 0.0:
		problems.append("map/chunk/spacing sizes must be positive")
		return problems

	var chunks: float = map_size_m / chunk_size_m
	if absf(chunks - round(chunks)) > 0.001:
		problems.append("map_size_m (%.1f) is not a whole number of %.1f m chunks"
				% [map_size_m, chunk_size_m])
	var cells: float = chunk_size_m / vertex_spacing_m
	if absf(cells - round(cells)) > 0.001:
		problems.append("chunk_size_m (%.1f) is not a whole number of %.1f m cells"
				% [chunk_size_m, vertex_spacing_m])

	if zones.is_empty():
		problems.append("no zones defined")
	for zone in zones:
		if zone == null:
			problems.append("zones contains a null entry")
			continue
		if zone.id == &"":
			problems.append("zone '%s' has no id" % zone.display_name)
		if zone.size.x <= 0.0 or zone.size.y <= 0.0:
			problems.append("zone '%s' has a non-positive size" % zone.id)
		if absf(zone.center.x) + zone.size.x * 0.5 > half_extent() \
				or absf(zone.center.y) + zone.size.y * 0.5 > half_extent():
			problems.append("zone '%s' extends past the map edge" % zone.id)
		if zone.flat_height <= sea_level + 1.0 and zone.kind != IslandZone.Kind.DOCK:
			problems.append("zone '%s' bake height %.1f m is at or below sea level"
					% [zone.id, zone.flat_height])

	if lake != null and lake.surface_height > max_height_m:
		problems.append("lake surface is above max_height_m")
	for channel in channels:
		if channel == null or channel.points.size() < 3:
			problems.append("channel with fewer than 3 points")
			continue
		if channel.half_width_m <= 0.0:
			problems.append("channel '%s' has a non-positive half width" % channel.id)
	if river != null:
		if river.points.size() < 2:
			problems.append("river needs at least 2 points")
		elif river.widths.size() != river.points.size():
			problems.append("river widths (%d) must match points (%d)"
					% [river.widths.size(), river.points.size()])
		if river.surface_start < river.surface_end:
			problems.append("river flows uphill (start %.1f < end %.1f)"
					% [river.surface_start, river.surface_end])
	for road in roads:
		if road == null or road.points.size() < 2:
			problems.append("road with fewer than 2 points")
			continue
		if road.width <= 0.0:
			problems.append("road '%s' has a non-positive width" % road.id)
	return problems
