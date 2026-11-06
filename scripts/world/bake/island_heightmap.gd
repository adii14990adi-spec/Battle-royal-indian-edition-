class_name IslandHeightmap
extends RefCounted
## Bakes the Siran Island heightfield with FastNoiseLite.
##
## This is **bake-time only**: nothing in here runs in a shipped build. The
## Phase 3 rule is "never generate terrain at runtime", so this class is used by
## `tools/bake/bake_siran_island.gd` (headless CLI) and the editor bake script,
## and the results are written to `.res` / `.tscn` files that the game loads.
##
## Pipeline (order matters — see docs/PHASE3_WORLD.md §3):
##
##   1. island mask  — radial falloff whose radius is wobbled by coastline noise,
##                     so the shore is not a circle
##   2. land height  — FBM hills + ridged spines, boosted inside the Graveyard
##                     so the east side is genuinely hilly
##   3. water carve  — lake bowl, river channel (descending surface), moat
##   4. smooth       — one 3×3 pass to kill single-vertex spikes
##   5. zones        — pull the ground towards each zone's bake height
##   6. roads        — grade a corridor along each road, with causeways over
##                     water
##   7. hard flats   — zones with flatness == 1 are re-stamped exactly, so the
##                     runway is *perfectly* flat even where a road joins it
##
## Heights live on a regular grid shared by every chunk: `grid_vertices_per_axis()`
## squared samples at `vertex_spacing_m`, so neighbouring chunk meshes share their
## edge vertices and the seams are invisible.

## Distance between road profile samples along the centreline, metres.
const ROAD_SAMPLE_SPACING_M: float = 12.0
## Maximum road grade, as a rise over run. 9 % is a steep mountain road.
const ROAD_MAX_GRADE: float = 0.09
## How far a causeway carries the road above the water surface, metres.
const CAUSEWAY_CLEARANCE_M: float = 1.2
## Bucket edge for the road distance index, metres. Must be > 2× road reach.
const ROAD_BUCKET_M: float = 64.0
## One smoothing pass is enough: two starts erasing the hillspurs.
const SMOOTH_PASSES: int = 1
## Water is at least this deep at the nominal shore of a lake, so the plane is
## visible right up to the bank instead of stopping short of it.
const SHORE_DROP_M: float = 0.7
## Segments per road bucket entry are encoded as road * 4096 + segment.
const SEGMENT_ENCODE_STRIDE: int = 4096

var config: SiranMapConfig
## Number of samples along one axis of the *global* grid.
var grid_size: int = 0
## Full heightfield, row-major: `heights[z * grid_size + x]`.
var heights: PackedFloat32Array = PackedFloat32Array()
## Per-sample normals computed from `heights`, same indexing.
var normals: PackedVector3Array = PackedVector3Array()

var _continent: FastNoiseLite
var _coast: FastNoiseLite
var _hills: FastNoiseLite
var _ridges: FastNoiseLite
var _detail: FastNoiseLite
var _dither: FastNoiseLite
var _zones_by_area: Array[IslandZone] = []
## Bucket grid: Vector2i cell -> PackedInt32Array of encoded road segments.
var _road_buckets: Dictionary = {}
## Cached length of the river centreline (metres), used by the source blend.
var _river_length: float = 0.0


func _init(map_config: SiranMapConfig) -> void:
	config = map_config
	grid_size = config.grid_vertices_per_axis()
	_build_noise()
	_build_zone_order()
	if config.river != null and config.river.points.size() >= 2:
		_river_length = IslandShapeMath.polyline_length(config.river.points)


# --- Public API ---------------------------------------------------------------

func generate() -> void:
	## Fills `heights` and `normals`. Prints throughput so a slow bake is obvious.
	var started: int = Time.get_ticks_msec()
	var size: int = grid_size
	heights.resize(size * size)
	heights.fill(0.0)

	for iz in range(size):
		var z: float = grid_z(iz)
		var row: int = iz * size
		for ix in range(size):
			heights[row + ix] = base_height(grid_x(ix), z)

	_smooth_heights(SMOOTH_PASSES)
	_apply_zones()
	_build_road_profiles()
	_build_road_index()
	_apply_roads()
	_apply_hard_flats()
	_clamp_heights()
	_compute_normals()

	print("[bake] heightfield %dx%d generated in %.0f ms"
			% [size, size, float(Time.get_ticks_msec() - started)])


func grid_x(ix: int) -> float:
	return -config.half_extent() + float(ix) * config.vertex_spacing_m


func grid_z(iz: int) -> float:
	return -config.half_extent() + float(iz) * config.vertex_spacing_m


func height_at_index(ix: int, iz: int) -> float:
	var cl_x: int = clampi(ix, 0, grid_size - 1)
	var cl_z: int = clampi(iz, 0, grid_size - 1)
	return heights[cl_z * grid_size + cl_x]


func normal_at_index(ix: int, iz: int) -> Vector3:
	var cl_x: int = clampi(ix, 0, grid_size - 1)
	var cl_z: int = clampi(iz, 0, grid_size - 1)
	return normals[cl_z * grid_size + cl_x]


func sample_height(x: float, z: float) -> float:
	## Bilinear height lookup anywhere in the map (bake-time helper).
	var spacing: float = config.vertex_spacing_m
	var fx: float = clampf((x + config.half_extent()) / spacing, 0.0, float(grid_size - 1))
	var fz: float = clampf((z + config.half_extent()) / spacing, 0.0, float(grid_size - 1))
	var x0: int = int(floor(fx))
	var z0: int = int(floor(fz))
	var x1: int = mini(x0 + 1, grid_size - 1)
	var z1: int = mini(z0 + 1, grid_size - 1)
	var tx: float = fx - float(x0)
	var tz: float = fz - float(z0)
	var top: float = lerpf(height_at_index(x0, z0), height_at_index(x1, z0), tx)
	var bottom: float = lerpf(height_at_index(x0, z1), height_at_index(x1, z1), tx)
	return lerpf(top, bottom, tz)


func sample_normal(x: float, z: float) -> Vector3:
	var spacing: float = config.vertex_spacing_m
	var left: float = sample_height(x - spacing, z)
	var right: float = sample_height(x + spacing, z)
	var back: float = sample_height(x, z - spacing)
	var front: float = sample_height(x, z + spacing)
	return Vector3(left - right, 2.0 * spacing, back - front).normalized()


func height_range() -> Vector2:
	## `(min, max)` of the generated terrain, in metres.
	if heights.is_empty():
		return Vector2.ZERO
	var lowest: float = INF
	var highest: float = -INF
	for value in heights:
		lowest = minf(lowest, value)
		highest = maxf(highest, value)
	return Vector2(lowest, highest)


func water_surface_at(x: float, z: float) -> float:
	## Sea, lake or river surface at a point — the highest one wins.
	var surface: float = config.sea_level
	if config.lake != null and config.lake.contains(x, z):
		surface = maxf(surface, config.lake.surface_height)
	if config.river != null and config.river.points.size() >= 2:
		var query: Vector3 = config.river.query(x, z)
		if query.x <= config.river.half_width_at(query.z):
			surface = maxf(surface, config.river.surface_at(query.y))
	return surface


# --- Terrain palette ----------------------------------------------------------

func color_at(x: float, z: float, height: float, normal: Vector3) -> Color:
	## Vertex colour = greybox "material". One colour per vertex means zero extra
	## draw calls, zero textures and no per-pixel cost on a Mali-G57.
	var flat: float = clampf(normal.y, 0.0, 1.0)
	var slope: float = 1.0 - flat
	var dither: float = _dither.get_noise_2d(x, z) * 0.5 + 0.5

	var color: Color = _terrain_base_color(height, slope, dither)

	# Zone tint first: the flat, coloured pads are what makes the layout legible.
	var tint_weight: float = 0.0
	var tint: Color = Color.BLACK
	for zone in _zones_by_area:
		if zone == null:
			continue
		var weight: float = zone.weight_at(x, z)
		if weight <= 0.0:
			continue
		tint = zone.tint if tint_weight <= 0.0 else tint.lerp(zone.tint, weight)
		tint_weight = maxf(tint_weight, weight)
	if tint_weight > 0.0:
		# Slightly broken up so the pads do not look like paint on paper.
		var grime: Color = tint.darkened(0.18 * dither)
		color = color.lerp(grime, tint_weight * 0.88)

	color = _apply_airfield_markings(x, z, color)
	return color


func _terrain_base_color(height: float, slope: float, dither: float) -> Color:
	var sea: float = config.sea_level
	var grass: Color = Color(0.29, 0.38, 0.21).lerp(Color(0.40, 0.45, 0.26), dither)
	var scrub: Color = Color(0.42, 0.41, 0.30)
	var sand: Color = Color(0.72, 0.67, 0.50)
	var wet_sand: Color = Color(0.44, 0.42, 0.33)
	var rock: Color = Color(0.41, 0.39, 0.37)
	var bed: Color = Color(0.16, 0.22, 0.24)
	var color: Color = grass

	if height < sea - 2.5:
		color = bed
	elif height < sea + 0.4:
		color = wet_sand
	elif height < sea + 3.2:
		color = sand

	if height > sea + 3.2:
		var dry: float = clampf((height - sea - 30.0) / 60.0, 0.0, 1.0)
		color = grass.lerp(scrub, dry)

	# Rock shows through on steep faces, more of it the higher we are.
	if slope > 0.30:
		var rock_weight: float = clampf((slope - 0.30) / 0.35, 0.0, 1.0)
		rock_weight *= 0.55 + 0.45 * clampf((height - sea) / 80.0, 0.0, 1.0)
		color = color.lerp(rock, rock_weight)
	return color


func _apply_airfield_markings(x: float, z: float, color: Color) -> Color:
	## Greybox runway: a dark strip with white edge lines down the aircraft
	## industry pad. Done with vertex colours so it costs nothing at runtime.
	var airfield: IslandZone = null
	for zone in _zones_by_area:
		if zone != null and zone.kind == IslandZone.Kind.AIRFIELD:
			airfield = zone
			break
	if airfield == null:
		return color
	if airfield.signed_distance(x, z) > -40.0:
		return color

	var local := Vector2(x, z) - airfield.center
	var half_runway: float = 34.0
	var runway_half_length: float = airfield.size.y * 0.5 - 60.0
	if absf(local.x) <= half_runway + 3.0 and absf(local.y) <= runway_half_length:
		var asphalt: Color = Color(0.19, 0.19, 0.21)
		var edge: bool = absf(local.x) > half_runway - 3.0
		# Threshold bars at both ends of the strip.
		var threshold: bool = absf(local.y) > runway_half_length - 22.0
		if edge or threshold:
			return asphalt.lerp(Color(0.82, 0.82, 0.78), 0.85)
		return asphalt
	return color


# --- Noise layers -------------------------------------------------------------

func _build_noise() -> void:
	_continent = _make_noise(config.noise_seed + 101, 1.0 / 900.0,
			FastNoiseLite.FRACTAL_FBM, 4, 210.0)
	_coast = _make_noise(config.noise_seed + 202, 1.0 / 620.0,
			FastNoiseLite.FRACTAL_FBM, 3, 0.0)
	_hills = _make_noise(config.noise_seed + 303, 1.0 / 620.0,
			FastNoiseLite.FRACTAL_FBM, 4, 150.0)
	_ridges = _make_noise(config.noise_seed + 404, 1.0 / 780.0,
			FastNoiseLite.FRACTAL_RIDGED, 3, 0.0)
	_detail = _make_noise(config.noise_seed + 505, 1.0 / 140.0,
			FastNoiseLite.FRACTAL_FBM, 2, 0.0)
	_dither = _make_noise(config.noise_seed + 606, 1.0 / 90.0,
			FastNoiseLite.FRACTAL_NONE, 1, 0.0)


func _make_noise(seed_value: int, frequency: float, fractal: FastNoiseLite.FractalType,
		octaves: int, warp_amplitude_m: float) -> FastNoiseLite:
	var noise := FastNoiseLite.new()
	noise.noise_type = FastNoiseLite.TYPE_SIMPLEX_SMOOTH
	noise.seed = seed_value
	noise.frequency = frequency
	noise.fractal_type = fractal
	noise.fractal_octaves = octaves
	noise.fractal_lacunarity = 2.0
	noise.fractal_gain = 0.5
	noise.fractal_weighted_strength = 0.0
	if warp_amplitude_m > 0.0:
		# Domain warp breaks up the "blobby noise" look that screams "generated".
		# FastNoiseLite adds `amplitude x warp_noise` to the *input* coordinates,
		# so this is in metres, not a 0..1 factor: ~200 m of warp is what turns a
		# circular coastline into bays and headlands.
		noise.domain_warp_enabled = true
		noise.domain_warp_type = FastNoiseLite.DOMAIN_WARP_SIMPLEX
		noise.domain_warp_amplitude = warp_amplitude_m
		noise.domain_warp_frequency = noise.frequency * 1.4
		noise.domain_warp_fractal_type = FastNoiseLite.DOMAIN_WARP_FRACTAL_PROGRESSIVE
		noise.domain_warp_fractal_octaves = 3
		noise.domain_warp_fractal_lacunarity = 2.0
		noise.domain_warp_fractal_gain = 0.5
	return noise


func _build_zone_order() -> void:
	## Biggest footprint first, so a small zone stamped later (the runway) wins
	## over the big one it sits in (the urban feather band).
	_zones_by_area = config.zones.duplicate()
	_zones_by_area.sort_custom(func(a: IslandZone, b: IslandZone) -> bool:
		return a.size.x * a.size.y > b.size.x * b.size.y)


# --- Stage 1-3: base terrain and water ---------------------------------------

func base_height(x: float, z: float) -> float:
	## Terrain height *before* smoothing, zones and roads, in metres.
	var sea: float = config.sea_level
	var distance: float = sqrt(x * x + z * z)
	var coast_noise: float = _coast.get_noise_2d(x, z)
	var effective_radius: float = config.island_radius_m \
			+ coast_noise * config.coastline_noise_m
	var inland: float = 1.0 - distance / maxf(effective_radius, 1.0)
	var mask: float = IslandShapeMath.smooth01(inland * 8.0)

	var hills: float = _hills.get_noise_2d(x, z)
	var ridges: float = absf(_ridges.get_noise_2d(x, z))
	var detail: float = _detail.get_noise_2d(x, z)
	var continent: float = _continent.get_noise_2d(x, z)
	var hill_boost: float = _graveyard_weight(x, z)

	# `continent` is the slow 900 m tilt: it stops the island from looking like a
	# hill farm, tipping one side up and letting the other slide to the sea.
	var land: float = sea + 12.0 \
			+ hills * 22.0 \
			+ ridges * (10.0 + 44.0 * hill_boost) \
			+ hill_boost * 20.0 \
			+ detail * 2.4 \
			+ continent * 7.0
	var seabed: float = sea - 26.0 - 12.0 * (1.0 - mask) + hills * 5.0
	var height: float = lerpf(seabed, land, mask)

	height = _carve_lake(x, z, height)
	height = _carve_river(x, z, height)
	height = _carve_channels(x, z, height)
	return clampf(height, config.min_height_m, config.max_height_m)


func _graveyard_weight(x: float, z: float) -> float:
	## 0..1 weight that lifts the ridged noise into hills on the east side.
	for zone in config.zones:
		if zone != null and zone.kind == IslandZone.Kind.GRAVEYARD:
			return zone.weight_at(x, z)
	return 0.0


func _carve_lake(x: float, z: float, height: float) -> float:
	var lake: IslandWaterBody = config.lake
	if lake == null:
		return height
	var t: float = lake.normalised_distance(x, z)
	var shore_feather: float = lake.shore_feather_m / maxf(minf(lake.radius.x, lake.radius.y), 1.0)
	if t <= 1.0:
		var bed: float = lake.surface_height - lake.depth * (1.0 - t * t) - SHORE_DROP_M
		return minf(height, bed)
	if t <= 1.0 + shore_feather:
		var weight: float = 1.0 - IslandShapeMath.smooth01((t - 1.0) / shore_feather)
		var lip: float = lake.surface_height - SHORE_DROP_M
		return minf(height, lerpf(height, lip, weight * 0.9))
	return height


func _carve_river(x: float, z: float, height: float) -> float:
	var river: IslandRiver = config.river
	if river == null or river.points.size() < 2:
		return height
	var query: Vector3 = river.query(x, z)
	var surface: float = river.surface_at(query.y)
	var half_width: float = river.half_width_at(query.z)
	var distance: float = query.x
	if distance <= half_width:
		var k: float = distance / maxf(half_width, 0.001)
		var bed: float = surface - river.depth * (1.0 - k * k) - SHORE_DROP_M
		# Near the source, dig deeper so the channel merges with the lake.
		var blend: float = 1.0
		if river.source_blend_m > 0.0:
			var along: float = query.y * _river_length
			blend = clampf(1.0 - along / river.source_blend_m, 0.0, 1.0)
		bed -= blend * 2.0
		return minf(height, bed)
	if distance <= half_width + river.bank_width_m:
		var weight: float = 1.0 - IslandShapeMath.smooth01(
				(distance - half_width) / maxf(river.bank_width_m, 0.001))
		return minf(height, lerpf(height, surface - SHORE_DROP_M, weight * 0.9))
	return height


func _carve_channels(x: float, z: float, height: float) -> float:
	## Accumulates over every channel: overlapping moats must not cancel out.
	for channel in config.channels:
		if channel == null or channel.points.size() < 3:
			continue
		var query: Vector3 = channel.query(x, z)
		var distance: float = query.x
		var bed: float = config.sea_level - channel.depth_m
		if distance <= channel.half_width_m:
			var k: float = distance / maxf(channel.half_width_m, 0.001)
			height = minf(height, bed + channel.depth_m * k * k - 0.4)
		elif distance <= channel.half_width_m + channel.bank_width_m:
			var weight: float = 1.0 - IslandShapeMath.smooth01(
					(distance - channel.half_width_m) / maxf(channel.bank_width_m, 0.001))
			height = minf(height, lerpf(height, config.sea_level - 0.6, weight * 0.9))
	return height


# --- Stage 4: smoothing -------------------------------------------------------

func _smooth_heights(passes: int) -> void:
	var size: int = grid_size
	for _pass in range(passes):
		var source: PackedFloat32Array = heights.duplicate()
		for iz in range(1, size - 1):
			var row: int = iz * size
			for ix in range(1, size - 1):
				var sum: float = source[row - size + ix - 1] + 2.0 * source[row - size + ix] \
						+ source[row - size + ix + 1] \
						+ 2.0 * source[row + ix - 1] + 4.0 * source[row + ix] \
						+ 2.0 * source[row + ix + 1] \
						+ source[row + size + ix - 1] + 2.0 * source[row + size + ix] \
						+ source[row + size + ix + 1]
				heights[row + ix] = sum * 0.0625


# --- Stage 5: zones -----------------------------------------------------------

func _apply_zones() -> void:
	var size: int = grid_size
	for iz in range(size):
		var z: float = grid_z(iz)
		var row: int = iz * size
		for ix in range(size):
			var x: float = grid_x(ix)
			var height: float = heights[row + ix]
			for zone in _zones_by_area:
				if zone == null:
					continue
				var weight: float = zone.weight_at(x, z)
				if weight <= 0.0:
					continue
				height = lerpf(height, zone.flat_height, zone.flatness * weight)
			heights[row + ix] = height


func _apply_hard_flats() -> void:
	## Zones that asked for flatness == 1 are stamped exactly. This runs after the
	## road pass on purpose: the runway must stay perfectly flat even where the
	## access road meets it.
	var size: int = grid_size
	for zone in _zones_by_area:
		if zone == null or zone.flatness < 0.999:
			continue
		var rect: Rect2 = _zone_bounds(zone)
		var ix_start: int = maxi(0, int(floor((rect.position.x + config.half_extent())
				/ config.vertex_spacing_m)) - 1)
		var ix_end: int = mini(size - 1, int(ceil((rect.end.x + config.half_extent())
				/ config.vertex_spacing_m)) + 1)
		var iz_start: int = maxi(0, int(floor((rect.position.y + config.half_extent())
				/ config.vertex_spacing_m)) - 1)
		var iz_end: int = mini(size - 1, int(ceil((rect.end.y + config.half_extent())
				/ config.vertex_spacing_m)) + 1)
		for iz in range(iz_start, iz_end + 1):
			var z: float = grid_z(iz)
			var row: int = iz * size
			for ix in range(ix_start, ix_end + 1):
				if zone.contains(grid_x(ix), z):
					heights[row + ix] = zone.flat_height


func _zone_bounds(zone: IslandZone) -> Rect2:
	var reach: float = zone.size.length() * 0.5
	return Rect2(zone.center - Vector2(reach, reach), Vector2(reach, reach) * 2.0)


# --- Stage 6: roads -----------------------------------------------------------

func _build_road_profiles() -> void:
	## Resamples each road, samples the (already flattened) ground under it,
	## hoists it over water, smooths it and clamps the grade. The result drives
	## both the terrain corridor and the baked asphalt ribbon.
	for road in config.roads:
		if road == null or road.points.size() < 2:
			continue
		var total_length: float = IslandShapeMath.polyline_length(road.points)
		var count: int = maxi(2, int(ceil(total_length / ROAD_SAMPLE_SPACING_M)) + 1)
		var points := PackedVector2Array()
		var raw := PackedFloat32Array()
		points.resize(count)
		raw.resize(count)
		for i in range(count):
			var t: float = float(i) / float(count - 1)
			var point: Vector2 = IslandShapeMath.point_at_distance(road.points, total_length * t)
			points[i] = point
			raw[i] = maxf(sample_height(point.x, point.y),
					water_surface_at(point.x, point.y) + CAUSEWAY_CLEARANCE_M)

		# Smooth, then clamp the grade in both directions.
		raw = _moving_average(raw, 6)
		raw = _moving_average(raw, 6)
		var spacing: float = total_length / float(count - 1)
		var max_step: float = ROAD_MAX_GRADE * spacing
		for i in range(1, count):
			raw[i] = clampf(raw[i], raw[i - 1] - max_step, raw[i - 1] + max_step)
		for i in range(count - 2, -1, -1):
			raw[i] = clampf(raw[i], raw[i + 1] - max_step, raw[i + 1] + max_step)

		road.baked_points = points
		road.baked_heights = raw


func _moving_average(values: PackedFloat32Array, radius: int) -> PackedFloat32Array:
	var result := PackedFloat32Array()
	result.resize(values.size())
	for i in range(values.size()):
		var sum: float = 0.0
		var count: int = 0
		for k in range(-radius, radius + 1):
			var index: int = clampi(i + k, 0, values.size() - 1)
			sum += values[index]
			count += 1
		result[i] = sum / float(count)
	return result


func _build_road_index() -> void:
	## Coarse bucket grid so the per-vertex road pass only tests the handful of
	## segments that can possibly be within reach. Without it the bake spends
	## minutes in `query_polyline`.
	_road_buckets.clear()
	var spacing: float = config.vertex_spacing_m
	for road_index in range(config.roads.size()):
		var road: IslandRoad = config.roads[road_index]
		if road == null or road.baked_points.size() < 2:
			continue
		var reach: float = road.total_half_width() + spacing
		for segment in range(road.baked_points.size() - 1):
			var a: Vector2 = road.baked_points[segment]
			var b: Vector2 = road.baked_points[segment + 1]
			var min_point := Vector2(minf(a.x, b.x) - reach, minf(a.y, b.y) - reach)
			var max_point := Vector2(maxf(a.x, b.x) + reach, maxf(a.y, b.y) + reach)
			var code: int = road_index * SEGMENT_ENCODE_STRIDE + segment
			var cell_start := _bucket_of(min_point.x, min_point.y)
			var cell_end := _bucket_of(max_point.x, max_point.y)
			for cz in range(cell_start.y, cell_end.y + 1):
				for cx in range(cell_start.x, cell_end.x + 1):
					var key: Vector2i = Vector2i(cx, cz)
					var bucket: PackedInt32Array = _road_buckets.get(key, PackedInt32Array())
					bucket.append(code)
					_road_buckets[key] = bucket


func _bucket_of(x: float, z: float) -> Vector2i:
	return Vector2i(int(floor((x + config.half_extent()) / ROAD_BUCKET_M)),
			int(floor((z + config.half_extent()) / ROAD_BUCKET_M)))


func _apply_roads() -> void:
	var size: int = grid_size
	for iz in range(size):
		var z: float = grid_z(iz)
		var row: int = iz * size
		for ix in range(size):
			var bucket: PackedInt32Array = _road_buckets.get(
					_bucket_of(grid_x(ix), z), PackedInt32Array())
			if bucket.is_empty():
				continue
			var x: float = grid_x(ix)
			var point := Vector2(x, z)
			var best_weight: float = 0.0
			var best_height: float = 0.0
			for code in bucket:
				var road_index: int = code / SEGMENT_ENCODE_STRIDE
				var segment: int = code % SEGMENT_ENCODE_STRIDE
				var road: IslandRoad = config.roads[road_index]
				if segment + 1 >= road.baked_points.size():
					continue
				var a: Vector2 = road.baked_points[segment]
				var b: Vector2 = road.baked_points[segment + 1]
				var ab: Vector2 = b - a
				var length_sq: float = ab.length_squared()
				var fraction: float = 0.0
				if length_sq > 0.0001:
					fraction = clampf((point - a).dot(ab) / length_sq, 0.0, 1.0)
				var distance: float = point.distance_to(a + ab * fraction)
				var asphalt_half: float = road.width * 0.5
				var outer: float = asphalt_half + road.shoulder_width
				if distance >= outer:
					continue
				var weight: float = 1.0
				if distance > asphalt_half:
					weight = 1.0 - IslandShapeMath.smooth01(
							(distance - asphalt_half) / maxf(road.shoulder_width, 0.001))
				if weight > best_weight:
					best_weight = weight
					best_height = lerpf(road.baked_heights[segment],
							road.baked_heights[segment + 1], fraction)
			if best_weight <= 0.0:
				continue
			heights[row + ix] = lerpf(heights[row + ix], best_height, best_weight)


func _clamp_heights() -> void:
	for i in range(heights.size()):
		heights[i] = clampf(heights[i], config.min_height_m, config.max_height_m)


func _compute_normals() -> void:
	var size: int = grid_size
	var spacing: float = config.vertex_spacing_m
	normals.resize(size * size)
	for iz in range(size):
		var row: int = iz * size
		for ix in range(size):
			var left: float = heights[row + clampi(ix - 1, 0, size - 1)]
			var right: float = heights[row + clampi(ix + 1, 0, size - 1)]
			var back: float = heights[clampi(iz - 1, 0, size - 1) * size + ix]
			var front: float = heights[clampi(iz + 1, 0, size - 1) * size + ix]
			normals[row + ix] = Vector3(left - right, 2.0 * spacing, back - front).normalized()
