#!/usr/bin/env bash
# Phase 3 acceptance gates — the machine-checkable subset, for the world &
# map generation milestone (Siran Island greybox).
#
#   bash tools/verify_phase3.sh
#
# These are *static* gates: they inspect the source, the scene files and the
# baked output on disk. They cannot judge whether the island looks good or runs
# at 30 FPS — that is docs/PHASE3_ACCEPTANCE.md, on a phone.
#
# The gates that need the engine (collision matches the mesh, the runway is
# flat, the 50 m radius maths) live in scenes/tests/phase3_selfcheck.tscn and
# are run by `bash tools/generate_siran_island.sh` locally, and by the APK
# workflow against the committed map.
#
# Run from anywhere: the script cd's to the repository root itself.

set -uo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/.." || exit 2

FAILURES=0
PASSES=0
SKIPS=0

pass() { PASSES=$((PASSES + 1)); printf '  \033[32mPASS\033[0m  %s\n' "$1"; }
fail() {
	FAILURES=$((FAILURES + 1))
	printf '  \033[31mFAIL\033[0m  %s\n' "$1"
	# Actions keeps at most 10 annotations per step and the raw log archive is
	# not reachable from every network, so a failing gate names itself here.
	if [ "${GITHUB_ACTIONS:-}" = "true" ]; then
		printf '::error::%s\n' "$1"
	fi
}
skip() { SKIPS=$((SKIPS + 1)); printf '  \033[33mSKIP\033[0m  %s\n' "$1"; }
head2() { printf '\n\033[1m%s\033[0m\n' "$1"; }

# grep the tracked source tree, ignoring docs (which may quote forbidden names)
# and comment lines (a comment mentioning FastNoiseLite is not a runtime call).
code_hits() { # $1 = regex
	git grep -nE "$1" -- '*.gd' '*.tscn' '*.tres' '*.gdshader' 2>/dev/null \
		| grep -vE '^[^:]+:[0-9]+:[[:space:]]*#'
}

# --- Gate 1: terrain collision is a height field, never a trimesh -------------
head2 'Gate 1 — terrain collision uses HeightMapShape3D, never ConcavePolygonShape3D'
# The self-check and tools/check_baked_scene.gd name the class *in order to forbid
# it*; everything that builds or streams terrain must not mention it at all.
trimesh_hits=$(code_hits 'ConcavePolygonShape3D' | grep -vE '^(scripts/tests/|tools/)')
if [ -z "$trimesh_hits" ]; then
	pass "no ConcavePolygonShape3D in terrain code (comments and the self-check aside)"
else
	fail "ConcavePolygonShape3D found in terrain code:"
	printf '%s\n' "$trimesh_hits" | sed 's/^/        /'
fi
if grep -q 'HeightMapShape3D.new()' scripts/world/bake/terrain_mesh_builder.gd; then
	pass "baker builds HeightMapShape3D for every chunk"
else
	fail "terrain_mesh_builder.gd does not create a HeightMapShape3D"
fi
if grep -q 'shape_node.scale = Vector3(config.vertex_spacing_m, 1.0, config.vertex_spacing_m)' \
		scripts/world/bake/terrain_generator.gd; then
	pass "collision cells scaled to vertex_spacing_m on X/Z only (Jolt requirement)"
else
	fail "height field cell scale is missing or non-uniform in Z"
fi
if grep -q 'JOLT_MIN_WIDTH' scripts/world/bake/terrain_mesh_builder.gd; then
	pass "Jolt height-field width/symmetry constraint is guarded"
else
	fail "Jolt height-field constraint guard missing"
fi

# --- Gate 2: no terrain generation at runtime --------------------------------
head2 'Gate 2 — terrain is baked, never generated at runtime'
runtime_noise=$(code_hits 'FastNoiseLite' | grep -v '^scripts/world/bake/' | grep -v '^tools/')
if [ -z "$runtime_noise" ]; then
	pass "FastNoiseLite is confined to scripts/world/bake/ and tools/"
else
	fail "FastNoiseLite used outside bake tooling:"
	printf '%s\n' "$runtime_noise" | sed 's/^/        /'
fi
if grep -q 'SurfaceTool\|ArrayMesh.new()' scripts/world/chunk_manager.gd; then
	fail "ChunkManager builds geometry at runtime"
else
	pass "ChunkManager only instantiates the committed baked scene"
fi
if grep -q 'SurfaceTool.new()' scripts/world/bake/terrain_generator.gd; then
	pass "TerrainGenerator builds the terrain mesh with SurfaceTool (generation time)"
else
	fail "TerrainGenerator.gd must build the terrain mesh with SurfaceTool"
fi
if grep -q 'func _ready' scripts/world/chunk_manager.gd &&
	! grep -qE 'generate\(|add_surface_from_arrays' scripts/world/chunk_manager.gd; then
	pass "streaming path contains no mesh generation call"
else
	fail "mesh generation found in the streaming path"
fi

# --- Gate 3: threaded streaming ----------------------------------------------
head2 'Gate 3 — chunk streaming is threaded and radius driven'
for needle in 'ResourceLoader.load_threaded_request' 'ResourceLoader.load_threaded_get_status' \
		'ResourceLoader.load_threaded_get(' 'stream_radius_m' 'keep_ring'; do
	if grep -qF "$needle" scripts/world/chunk_manager.gd; then
		pass "ChunkManager uses $needle"
	else
		fail "ChunkManager missing $needle"
	fi
done
if grep -q 'max_loads_in_flight' scripts/world/chunk_manager.gd &&
		grep -q 'unloads_per_tick' scripts/world/chunk_manager.gd; then
	pass "load/unload budgets are explicit (eMMC + main-thread friendly)"
else
	fail "no load/unload budget in ChunkManager"
fi
if grep -q 'stream_radius_m = 50.0' scenes/world/siran_island.tscn; then
	pass "world scene ships the spec'd 50 m streaming radius"
else
	fail "world scene does not set a 50 m streaming radius"
fi

# --- Gate 4: map constants ---------------------------------------------------
head2 'Gate 4 — 4 km map, 500 m chunks, one source of truth'
check_config() { # $1 = regex, $2 = description
	if grep -qE "$1" scripts/world/siran_map_config.gd; then
		pass "$2"
	else
		fail "$2 — expected $1 in siran_map_config.gd"
	fi
}
check_config 'MAP_SIZE_DEFAULT_M: float = 4000\.0' 'map is 4000 m square'
check_config 'CHUNK_SIZE_DEFAULT_M: float = 500\.0' 'chunks are 500 m'
check_config 'VERTEX_SPACING_DEFAULT_M: float = 10\.0' 'terrain samples every 10 m'
check_config 'island_radius_m: float = 1950\.0' 'island radius is 1950 m'
if grep -q 'TerrainGenerator.BAKED_SCENE_PATH' scripts/world/chunk_manager.gd &&
		grep -q 'TerrainGenerator.CHUNK_NODE_PATTERN' scripts/world/chunk_manager.gd; then
	pass "streamer takes the scene path and chunk node names from TerrainGenerator"
else
	fail "ChunkManager must take its scene path / chunk node names from TerrainGenerator"
fi
zones=$(grep -c '_make_zone(&' scripts/world/siran_map_config.gd)
if [ "$zones" -eq 5 ]; then
	pass "all five zones are declared in the canonical layout"
else
	fail "expected 5 zones in default_siran_island(), found $zones"
fi
for id in factory_island konkan_docks central_state graveyard_center aircraft_industry; do
	if grep -q "&\"$id\"" scripts/world/siran_map_config.gd; then
		pass "zone id $id present"
	else
		fail "zone id $id missing"
	fi
done

# --- Gate 5: shaders stay Unisoc-safe ---------------------------------------
head2 'Gate 5 — shaders are unshaded, no PBR, no screen textures'
shaders=$(git ls-files 'assets/shaders/*.gdshader')
if [ -z "$shaders" ]; then
	fail "no shaders in assets/shaders/"
else
	for shader in $shaders; do
		if grep -qE '^render_mode .*unshaded' "$shader"; then
			pass "$(basename "$shader") is unshaded"
		else
			fail "$(basename "$shader") is not unshaded"
		fi
		forbidden=$(grep -nE 'SCREEN_TEXTURE|DEPTH_TEXTURE|NORMAL_ROUGHNESS_TEXTURE|METALLIC|ROUGHNESS|SPECULAR|light\(' "$shader")
		if [ -z "$forbidden" ]; then
			pass "$(basename "$shader") has no PBR/screen-space references"
		else
			fail "$(basename "$shader") uses a forbidden built-in:"
			printf '%s\n' "$forbidden" | sed 's/^/        /'
		fi
	done
fi
if [ -f resources/world/materials/terrain_greybox.tres ] &&
		grep -q 'ShaderMaterial' resources/world/materials/terrain_greybox.tres; then
	pass "terrain material is a ShaderMaterial (no StandardMaterial3D PBR)"
else
	fail "resources/world/materials/terrain_greybox.tres missing or not a ShaderMaterial"
fi
# Passes as soon as the metadata exists: CI generates it during --import, so
# this gate is green there even before the .import file is committed. Committing
# it only saves the next clean clone one regeneration.
if [ -f assets/textures/water_normal_256.png.import ]; then
	if git ls-files --error-unmatch assets/textures/water_normal_256.png.import >/dev/null 2>&1; then
		pass "water normal map has a committed .import file"
	else
		pass "water normal map has import metadata (generated by --import; commit it for a deterministic clone)"
	fi
else
	skip "water_normal_256.png.import not generated yet — run 'godot --headless --path . --import'"
fi

# --- Gate 6: a fresh clone can still open the world scene --------------------
head2 'Gate 6 — world scene never hard-references bake output'
scene_refs=$(grep -v '^[[:space:]]*;' scenes/world/siran_island.tscn \
	| grep -E 'resources/world/baked|scenes/world/terrain/chunk_')
if [ -n "$scene_refs" ]; then
	fail "siran_island.tscn references baked files directly — a fresh clone cannot open it"
else
	pass "baked river/chunks are loaded at runtime, not as [ext_resource]"
fi
if grep -q 'baked_extras.gd' scenes/world/siran_island.tscn; then
	pass "river surface is loaded through BakedExtras"
else
	fail "RiverSurface does not use baked_extras.gd"
fi

# --- Gate 7: the committed baked map ----------------------------------------
# The map is generated once (TerrainGenerator) and committed, so this gate can
# be strict: the scene must be in Git, must hold 64 chunk nodes, must carry a
# HeightMapShape3D collider per chunk, and must never contain a trimesh.
head2 'Gate 7 — committed baked map (scenes/world/siran_island_baked.tscn)'
BAKED_SCENE="scenes/world/siran_island_baked.tscn"
if git ls-files --error-unmatch "$BAKED_SCENE" >/dev/null 2>&1; then
	pass "$BAKED_SCENE is committed"
else
	fail "$BAKED_SCENE is not in Git — the APK would ship without a world (run tools/generate_siran_island.sh)"
fi
if [ -f "$BAKED_SCENE" ]; then
	chunks=$(grep -cE '^\[node name="Chunk_[0-9]+_[0-9]+"' "$BAKED_SCENE" || true)
	[ "$chunks" -eq 64 ] && pass "64 Chunk_<x>_<z> nodes in the baked scene" \
		|| fail "expected 64 chunk nodes in the baked scene, found $chunks"
	terrains=$(grep -c 'name="Terrain" type="MeshInstance3D"' "$BAKED_SCENE" || true)
	[ "$terrains" -eq 64 ] && pass "every chunk has a Terrain MeshInstance3D" \
		|| fail "expected 64 Terrain MeshInstance3D nodes, found $terrains"
	heightmaps=$(grep -c 'type="HeightMapShape3D"' "$BAKED_SCENE" || true)
	[ "$heightmaps" -eq 64 ] && pass "64 HeightMapShape3D collision shapes baked into the scene" \
		|| fail "expected 64 HeightMapShape3D sub-resources, found $heightmaps"
	if grep -qa 'ConcavePolygonShape3D' "$BAKED_SCENE"; then
		fail "the baked scene contains a ConcavePolygonShape3D (terrain collision must be a height field)"
	else
		pass "no trimesh collision anywhere in the baked scene"
	fi
	# A scene with the right nodes but empty meshes would sail through the greps
	# above; the self-check in CI proves the meshes actually have surfaces. This
	# size floor catches the "packed with no owner" case early and cheaply.
	bytes=$(stat -c%s "$BAKED_SCENE" 2>/dev/null || echo 0)
	if [ "$bytes" -gt 1048576 ]; then
		pass "baked scene is $(du -h "$BAKED_SCENE" | cut -f1) — meshes are really in it"
	else
		fail "baked scene is only ${bytes} bytes; the chunk meshes were not packed (owner not set?)"
	fi
	printf '        baked map footprint: %s\n' "$(du -h "$BAKED_SCENE" | cut -f1)"
else
	fail "$BAKED_SCENE is missing from disk"
fi
if [ -f resources/world/baked/siran_island_summary.tres ]; then
	pass "generation summary written"
	if grep -q 'generator_version = ' resources/world/baked/siran_island_summary.tres; then
		baked_version=$(grep -m1 'generator_version = ' \
			resources/world/baked/siran_island_summary.tres | tr -dc '0-9')
		code_version=$(grep -m1 'generator_version: int = ' scripts/world/siran_map_config.gd |
			tr -dc '0-9')
		if [ "$baked_version" = "$code_version" ]; then
			pass "baked map matches the current generator version ($code_version)"
		else
			fail "baked map is stale (scene v$baked_version, code v$code_version)"
		fi
	fi
else
	fail "generation summary missing — the map was not generated with TerrainGenerator"
fi

# --- Gate 8: offline hard requirement ---------------------------------------
head2 'Gate 8 — no network API in Phase 3 code'
network=$(code_hits 'HTTPRequest|HTTPClient|StreamPeerTCP|WebSocketPeer|PacketPeerUDP' \
	| grep -E '^scripts/(world|tests)/|^scripts/ui/world_')
if [ -z "$network" ]; then
	pass "no networking API in world/streaming code"
else
	fail "networking API referenced:"
	printf '%s\n' "$network" | sed 's/^/        /'
fi

# --- Gate 9: docs ------------------------------------------------------------
head2 'Gate 9 — Phase 3 documentation'
for doc in docs/PHASE3_WORLD.md docs/PHASE3_ACCEPTANCE.md; do
	if git ls-files --error-unmatch "$doc" >/dev/null 2>&1; then
		pass "$doc committed"
	else
		fail "$doc missing from Git"
	fi
done

# --- Gate 10: tooling reachable ---------------------------------------------
head2 'Gate 10 — bake tooling is present and runnable'
for file in tools/generate_siran_island.sh tools/bake/generate_siran_island.gd \
		tools/bake/terrain_generator_editor.gd scripts/world/bake/terrain_generator.gd \
		scenes/tests/phase3_selfcheck.tscn scripts/tests/phase3_selfcheck.gd; do
	if [ -f "$file" ]; then
		pass "$file present"
	else
		fail "$file missing"
	fi
done
if [ -x tools/generate_siran_island.sh ]; then
	pass "tools/generate_siran_island.sh is executable"
else
	skip "tools/generate_siran_island.sh is not marked executable"
fi
# The APK workflow must not generate the map: that is what produced packs with
# no world in them. It verifies the committed scene instead.
if grep -q 'bake_siran_island' .github/workflows/build.yml; then
	fail ".github/workflows/build.yml still runs a bake — CI must only build the APK"
else
	pass "the APK workflow runs no bake step"
fi
if grep -q 'Verify the committed baked map' .github/workflows/build.yml; then
	pass "the APK workflow verifies the committed baked map before exporting"
else
	fail "the APK workflow must verify scenes/world/siran_island_baked.tscn before exporting"
fi

# --- Lint --------------------------------------------------------------------
# --- Gate 11: what actually reaches the phone --------------------------------
# The APK that shipped from main booted, drew the UI, and did nothing when the
# tester tapped "Enter Siran Island". Every gate above was green: the bake was
# correct, it just was not in the pack. These checks pin the packaging side.
head2 'Gate 11 — export packaging keeps the baked world and hides the logo'
if grep -q '^include_filter=".*\*\.tscn' export_presets.cfg &&
	grep -q '\*\.res' export_presets.cfg &&
	grep -q '\*\.scn' export_presets.cfg; then
	pass 'include_filter lists *.tscn, *.res and *.scn — disk walk, not the editor scan'
else
	fail 'export_presets.cfg include_filter must list *.tscn, *.res and *.scn: a resource the editor scan missed can otherwise be left out of the APK'
fi
# Comment lines are excluded: the scene's header explains where the map lives,
# and a `;` line is not an [ext_resource].
if grep -v '^[[:space:]]*;' scenes/world/siran_island.tscn \
		| grep -q 'siran_island_baked.tscn' \
		|| ! grep -q 'DEFAULT_BAKED_SCENE_PATH' scripts/world/chunk_manager.gd; then
	fail 'the world scene must not hard-reference the baked map, and ChunkManager must load it by path'
else
	pass 'the baked map is loaded by ChunkManager, not referenced by the world scene'
fi
if grep -q '^boot_splash/show_image=false' project.godot; then
	pass 'boot splash image disabled (no Godot logo over the boot screen)'
else
	fail 'project.godot must set boot_splash/show_image=false'
fi
if grep -q 'func _show_error' scripts/ui/boot_world_link.gd &&
	grep -q 'ResourceLoader.exists(path)' scripts/ui/boot_world_link.gd; then
	pass 'boot button checks with ResourceLoader.exists (follows .remap) and shows failures'
else
	fail 'boot button must check existence via ResourceLoader.exists() and put failures on screen — FileAccess.file_exists() is remap-blind inside an APK'
fi
if grep -q 'func _unhandled_input' scripts/ui/boot_screen.gd; then
	pass 'boot screen takes a tap the UI did not consume (no dead end)'
else
	fail 'scripts/ui/boot_screen.gd must keep the unhandled-tap fallback'
fi
if grep -qE '^[[:space:]]*#' project.godot export_presets.cfg export_presets/*.cfg 2>/dev/null; then
	fail "'#' is not a comment in Godot config files — use ';', or ConfigFile.load() fails and the export dies with one parse-error line"
else
	pass "no '#' comment lines in the Godot config files (';' only)"
fi

# --- Gate 12: the real parser reads them ------------------------------------
# The '#' above is the cheap version. This is the authoritative one: load both
# files with Godot's own ConfigFile and assert the settings the APK depends on.
head2 'Gate 12 — project.godot and export_presets.cfg load in Godot'
CONFIG_CHECK_GODOT="${GODOT_BIN:-}"
if [[ -z "$CONFIG_CHECK_GODOT" ]]; then
	CONFIG_CHECK_GODOT="$(command -v godot4 || command -v godot || true)"
fi
if [[ -z "$CONFIG_CHECK_GODOT" ]]; then
	skip 'no Godot binary on PATH — the same script runs in CI after the import'
elif CONFIG_OUT="$("$CONFIG_CHECK_GODOT" --headless --path "$PWD" \
		--script res://tools/check_project_config.gd 2>&1)"; then
	pass 'ConfigFile parses both files; include_filter, splash and main scene intact'
else
	printf '%s\n' "$CONFIG_OUT" | grep 'CONFIG ERROR' | head -5
	fail 'tools/check_project_config.gd reported problems'
fi

head2 'Extra — GDScript parses and lints'
if command -v gdparse >/dev/null 2>&1; then
	broken=0
	while IFS= read -r file; do
		gdparse "$file" >/dev/null 2>&1 || { broken=$((broken + 1)); printf '        parse error: %s\n' "$file"; }
	done < <(git ls-files '*.gd')
	[ "$broken" -eq 0 ] && pass "all $(git ls-files '*.gd' | wc -l | tr -d ' ') scripts parse" \
		|| fail "$broken script(s) do not parse"
else
	skip "gdparse not installed (pip install gdtoolkit)"
fi
if command -v gdlint >/dev/null 2>&1; then
	if gdlint $(git ls-files '*.gd') >/dev/null 2>&1; then
		pass "gdlint clean"
	else
		fail "gdlint reports problems — run: gdlint \$(git ls-files '*.gd')"
	fi
else
	skip "gdlint not installed (pip install gdtoolkit)"
fi

# --- Phase 1 regression ------------------------------------------------------
head2 'Extra — Phase 1 gates still pass'
if bash tools/verify_phase1.sh >/tmp/phase1.log 2>&1; then
	pass "tools/verify_phase1.sh still green"
else
	fail "tools/verify_phase1.sh regressed — see /tmp/phase1.log"
fi

printf '\n\033[1mSummary:\033[0m %d passed, %d failed, %d skipped\n' "$PASSES" "$FAILURES" "$SKIPS"
if [ "$FAILURES" -ne 0 ]; then
	printf '\033[31mPhase 3 is NOT done — fix the failures above.\033[0m\n'
	exit 1
fi
printf '\033[32mAll automated Phase 3 gates pass.\033[0m\n'
printf 'Device gates (30 FPS, draw calls, memory, streaming) — see docs/PHASE3_ACCEPTANCE.md\n'
exit 0
