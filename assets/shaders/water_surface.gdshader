shader_type spatial;
// Ocean, lake and river surface — one unshaded shader, three materials.
//
// Unshaded + opaque: no transparency sorting, no refraction, no depth texture
// (none of which the GLES3 Compatibility renderer does cheaply). The "water
// look" is two scrolling samples of the same 256x256 tiling normal map, a
// fresnel mix towards sky colour and a single specular highlight against a
// fixed sun. That is roughly 3 texture fetches and 6 ALU ops per pixel.
//
// Scrolling is driven by TIME and *world* position, so the ripple pattern is
// continuous across chunk borders and across the river ribbon's triangles.

render_mode unshaded, cull_back, depth_draw_opaque;

uniform vec4 shallow_color : source_color = vec4(0.20, 0.44, 0.50, 1.0);
uniform vec4 deep_color : source_color = vec4(0.06, 0.17, 0.26, 1.0);
uniform vec4 sky_color : source_color = vec4(0.63, 0.73, 0.84, 1.0);
uniform float depth_mix : hint_range(0.0, 1.0) = 0.55;

// A/B layers scroll in different directions over the same tiling normal map.
uniform sampler2D water_normal : hint_normal, filter_linear_mipmap, repeat_enable;
uniform vec2 uv_scale = vec2(0.022, 0.022);
uniform float scroll_speed_a = 0.030;
uniform float scroll_speed_b = 0.019;
uniform float normal_strength : hint_range(0.0, 1.0) = 0.45;

uniform vec3 sun_direction = vec3(0.45, 0.78, 0.44);
uniform float sparkle : hint_range(0.0, 4.0) = 1.1;
uniform float flow_boost : hint_range(0.0, 4.0) = 1.0;

uniform vec3 fog_color = vec3(0.66, 0.72, 0.78);
uniform float fog_start = 420.0;
uniform float fog_density = 0.00085;

varying vec3 world_position;

void vertex() {
	world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	vec2 base_uv = world_position.xz * uv_scale;
	float time = TIME * flow_boost;

	vec3 normal_a = texture(water_normal, base_uv + vec2(time * scroll_speed_a,
			time * scroll_speed_a * 0.7)).xyz * 2.0 - 1.0;
	vec3 normal_b = texture(water_normal, base_uv * 1.9 - vec2(time * scroll_speed_b,
			time * scroll_speed_b * 0.55)).xyz * 2.0 - 1.0;

	// Clamped so a missing/white normal map degrades into calm water, not a
	// mirror-ball glitch.
	vec2 ripple = clamp(normal_a.xy + normal_b.xy, vec2(-1.4), vec2(1.4)) * normal_strength;
	vec3 normal = normalize(vec3(ripple.x, 1.0, ripple.y));

	vec3 view_direction = normalize(CAMERA_POSITION_WORLD - world_position);
	float fresnel = pow(1.0 - clamp(dot(view_direction, normal), 0.0, 1.0), 3.0);

	vec3 body = mix(deep_color.rgb, shallow_color.rgb, depth_mix);
	vec3 color = mix(body, sky_color.rgb, clamp(fresnel * 1.25, 0.0, 0.88));

	vec3 light_direction = normalize(sun_direction);
	float specular = pow(max(dot(reflect(-light_direction, normal), view_direction), 0.0),
			28.0);
	color += vec3(1.0, 0.97, 0.90) * specular * sparkle;

	float distance_to_camera = distance(world_position, CAMERA_POSITION_WORLD);
	float haze = clamp((distance_to_camera - fog_start) * fog_density, 0.0, 1.0);
	ALBEDO = mix(color, fog_color, haze);
}
