shader_type spatial;
// Greybox terrain + roads, Unisoc-safe.
//
// Unshaded on purpose: the Compatibility renderer's forward lighting re-shades
// every pixel per light, and a Mali-G57 MP1 feels it. Instead this shader bakes
// one hard-coded directional term from the vertex normal and multiplies the
// vertex colour (the "material" of the greybox) — no PBR, no specular, no
// light loops, no shadows, no screen textures.
//
// Vertex colour comes from the bake (`IslandHeightmap.color_at`): biome palette
// + zone tint + runway markings. That is why the whole 4 km map is one
// material and one draw call per chunk.

render_mode unshaded, cull_back, depth_draw_opaque;

// Direction *towards* the sun, matching scenes/world/siran_island.tscn.
uniform vec3 sun_direction = vec3(0.45, 0.78, 0.44);
uniform float sun_strength : hint_range(0.0, 2.0) = 0.85;
uniform float ambient_strength : hint_range(0.0, 1.0) = 0.42;

// Large-scale per-pixel grain so the flat pads do not read as untextured paint.
// Two sin() calls per pixel; set strength to 0.0 if the fill rate is tight.
uniform float grain_scale = 0.035;
uniform float grain_strength : hint_range(0.0, 0.5) = 0.10;

// Cheap distance haze, so the far side of the island fades like the foggy
// graveyard is supposed to. Tuned for a 4 km map.
uniform vec3 fog_color = vec3(0.66, 0.72, 0.78);
uniform float fog_start = 420.0;
uniform float fog_density = 0.00085;

varying vec3 world_position;
varying vec3 world_normal;

void vertex() {
	world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	world_normal = (MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz;
}

void fragment() {
	vec3 base_color = COLOR.rgb;
	float grain = sin(world_position.x * grain_scale) * sin(world_position.z * grain_scale);
	base_color *= 1.0 + grain * grain_strength;

	float lambert = max(dot(normalize(world_normal), normalize(sun_direction)), 0.0);
	vec3 lit = base_color * (ambient_strength + sun_strength * lambert);

	float distance_to_camera = distance(world_position, CAMERA_POSITION_WORLD);
	float haze = clamp((distance_to_camera - fog_start) * fog_density, 0.0, 1.0);
	ALBEDO = mix(lit, fog_color, haze);
}
