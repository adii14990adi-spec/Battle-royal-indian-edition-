class_name IslandShapeMath
extends RefCounted
## Stateless 2D shape helpers shared by the Siran Island generator, the baker
## and the runtime map config.
##
## Convention for every function in this file:
##   * A point is `Vector2(x, z)` in **world metres** (map is centred on the
##     origin, X = east, Y/Z = south).
##   * Distances are metres. Signed distances are negative inside a shape.
##   * `weight` is 1.0 deep inside a shape and 0.0 past its feather band.
##
## Nothing here allocates per frame: this file is only used at bake time and by
## a handful of static queries (`get_zone_at`) at runtime.


static func smooth01(t: float) -> float:
	## Hermite smoothstep on the unit interval, clamped.
	var x: float = clampf(t, 0.0, 1.0)
	return x * x * (3.0 - 2.0 * x)


static func rotate_xz(point: Vector2, degrees: float) -> Vector2:
	## Rotates a world XZ vector around the origin. +degrees = clockwise when
	## looked at from above (+Y down the screen), which is what the zone editor
	## in the inspector implies.
	if is_zero_approx(degrees):
		return point
	var radians: float = deg_to_rad(degrees)
	var c: float = cos(radians)
	var s: float = sin(radians)
	return Vector2(point.x * c - point.y * s, point.x * s + point.y * c)


static func ellipse_signed_distance(x: float, z: float, center: Vector2,
		size: Vector2, rotation_deg: float) -> float:
	## Approximate signed distance (metres) to an ellipse. Positive outside.
	var local: Vector2 = rotate_xz(Vector2(x, z) - center, -rotation_deg)
	var rx: float = maxf(size.x * 0.5, 0.001)
	var rz: float = maxf(size.y * 0.5, 0.001)
	var normalised: float = sqrt(pow(local.x / rx, 2.0) + pow(local.y / rz, 2.0))
	# Scale the normalised radius back into metres with the smaller semi-axis so
	# a wide, flat zone still gets a sane feather band on its short side.
	return (normalised - 1.0) * minf(rx, rz)


static func rounded_rect_signed_distance(x: float, z: float, center: Vector2,
		size: Vector2, rotation_deg: float, corner_radius: float) -> float:
	## Exact signed distance (metres) to an axis-aligned-then-rotated rounded
	## rectangle. Positive outside.
	var local: Vector2 = rotate_xz(Vector2(x, z) - center, -rotation_deg)
	var half: Vector2 = Vector2(maxf(size.x * 0.5, 0.0), maxf(size.y * 0.5, 0.0))
	var radius: float = clampf(corner_radius, 0.0, minf(half.x, half.y))
	var q: Vector2 = Vector2(absf(local.x), absf(local.y)) - (half - Vector2(radius, radius))
	var outside: Vector2 = Vector2(maxf(q.x, 0.0), maxf(q.y, 0.0))
	return outside.length() + minf(maxf(q.x, q.y), 0.0) - radius


static func feather_weight(signed_distance: float, feather_m: float) -> float:
	## 1.0 while the point is inside the shape, 0.0 past `feather_m` outside it.
	if feather_m <= 0.0:
		return 1.0 if signed_distance <= 0.0 else 0.0
	return 1.0 - smooth01(signed_distance / feather_m)


# --- Polylines (roads, rivers, channels) ---------------------------------------

static func polyline_length(points: PackedVector2Array) -> float:
	var total: float = 0.0
	for i in range(1, points.size()):
		total += points[i - 1].distance_to(points[i])
	return total


static func polyline_segment_lengths(points: PackedVector2Array) -> PackedFloat32Array:
	var lengths: PackedFloat32Array = PackedFloat32Array()
	lengths.resize(maxi(points.size() - 1, 0))
	for i in range(1, points.size()):
		lengths[i - 1] = points[i - 1].distance_to(points[i])
	return lengths


static func point_at_distance(points: PackedVector2Array, distance_m: float,
		closed: bool = false) -> Vector2:
	## Walks a polyline and returns the point `distance_m` along it.
	if points.is_empty():
		return Vector2.ZERO
	if points.size() == 1:
		return points[0]
	var total: float = polyline_length(points)
	if closed:
		total = 0.0
		for i in range(points.size()):
			total += points[i].distance_to(points[(i + 1) % points.size()])
	var remaining: float = clampf(distance_m, 0.0, total)
	var count: int = points.size() if closed else points.size() - 1
	for i in range(count):
		var a: Vector2 = points[i]
		var b: Vector2 = points[(i + 1) % points.size()]
		var segment: float = a.distance_to(b)
		if remaining <= segment and segment > 0.0:
			return a.lerp(b, remaining / segment)
		remaining -= segment
	return points[-1]


static func query_polyline(points: PackedVector2Array, x: float, z: float,
		closed: bool = false) -> Vector3:
	## Nearest-point query against a polyline.
	##
	## Returns `Vector3(distance_m, t_normalised, segment_and_fraction)`:
	##   x = distance from (x, z) to the polyline, in metres
	##   y = normalised position along the whole polyline (0..1), good for
	##       interpolating a width or a surface height
	##   z = `segment_index + fraction`, i.e. an index into the point array for
	##       per-point attributes (use `floorf` for the left point)
	if points.is_empty():
		return Vector3(INF, 0.0, 0.0)
	if points.size() == 1:
		return Vector3(Vector2(x, z).distance_to(points[0]), 0.0, 0.0)

	var point: Vector2 = Vector2(x, z)
	var count: int = points.size() if closed else points.size() - 1
	var segment_lengths := PackedFloat32Array()
	segment_lengths.resize(count)
	var total: float = 0.0
	for i in range(count):
		segment_lengths[i] = points[i].distance_to(points[(i + 1) % points.size()])
		total += segment_lengths[i]

	var best_distance_sq: float = INF
	var best_segment: int = 0
	var best_fraction: float = 0.0
	for i in range(count):
		var a: Vector2 = points[i]
		var ab: Vector2 = points[(i + 1) % points.size()] - a
		var length_sq: float = ab.length_squared()
		var fraction: float = 0.0
		if length_sq > 0.0001:
			fraction = clampf((point - a).dot(ab) / length_sq, 0.0, 1.0)
		var offset: Vector2 = point - (a + ab * fraction)
		var distance_sq: float = offset.length_squared()
		if distance_sq < best_distance_sq:
			best_distance_sq = distance_sq
			best_segment = i
			best_fraction = fraction

	var walked: float = 0.0
	for i in range(best_segment):
		walked += segment_lengths[i]
	walked += segment_lengths[best_segment] * best_fraction
	var normalised: float = 0.0 if total <= 0.0001 else walked / total
	return Vector3(sqrt(best_distance_sq), normalised, float(best_segment) + best_fraction)


static func sample_packed_float(values: PackedFloat32Array, position: float) -> float:
	## Linearly samples a per-point attribute array with the `z` component of
	## `query_polyline`, clamping at both ends.
	if values.is_empty():
		return 0.0
	if values.size() == 1:
		return values[0]
	var clamped: float = clampf(position, 0.0, float(values.size() - 1))
	var low: int = int(floor(clamped))
	var high: int = mini(low + 1, values.size() - 1)
	return lerpf(values[low], values[high], clamped - float(low))
