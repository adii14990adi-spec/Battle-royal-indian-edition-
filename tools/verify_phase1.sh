#!/usr/bin/env bash
# Phase 1 acceptance gates — the machine-checkable subset of §12.
#
#   bash tools/verify_phase1.sh
#
# Exits 0 only when every automated gate passes. The two gates it cannot close
# (a real APK running on a physical Unisoc T606) are printed as SKIP with the
# exact commands, and documented in docs/PHASE1_ACCEPTANCE.md.
#
# Run from anywhere: the script cd's to the repository root itself.

set -uo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/.." || exit 2

FAILURES=0
PASSES=0
SKIPS=0

pass() { PASSES=$((PASSES + 1)); printf '  \033[32mPASS\033[0m  %s\n' "$1"; }
fail() { FAILURES=$((FAILURES + 1)); printf '  \033[31mFAIL\033[0m  %s\n' "$1"; }
skip() { SKIPS=$((SKIPS + 1)); printf '  \033[33mSKIP\033[0m  %s\n' "$1"; }
head2() { printf '\n\033[1m%s\033[0m\n' "$1"; }

tracked_matches() { # $1 = regex
	git ls-files | grep -cE "$1"
}

# --- Gate 1: Godot import cache is never committed ---------------------------
head2 'Gate 1 — .godot/ import cache never committed'
count=$(tracked_matches '^\.godot/')
if [ "$count" -eq 0 ]; then
	pass "0 tracked files under .godot/"
else
	fail "$count tracked files under .godot/ (gate requires 0)"
fi
if git check-ignore -q --no-index .godot/imported/probe.ctex; then
	pass ".gitignore ignores .godot/"
else
	fail ".gitignore does not ignore .godot/"
fi

# --- Gate 2: export/build artefacts are never committed ----------------------
head2 'Gate 2 — export/ and build artefacts never committed'
count=$(tracked_matches '^(export/|build/)|\.(apk|aab|pck)$')
if [ "$count" -eq 0 ]; then
	pass "0 tracked export/build artefacts"
else
	fail "$count tracked export/build artefacts"
fi
for probe in build/x.apk export/x.pck game.aab; do
	if git check-ignore -q --no-index "$probe"; then
		pass ".gitignore ignores $probe"
	else
		fail ".gitignore does not ignore $probe"
	fi
done

# --- Gate 3: Git LFS covers every binary type --------------------------------
head2 'Gate 3 — Git LFS tracks binary asset types'
for ext in png jpg glb ogg wav exr; do
	if grep -qE "^\*\.$ext[[:space:]]+filter=lfs" .gitattributes; then
		pass "*.${ext} routed through LFS"
	else
		fail "*.${ext} missing from .gitattributes LFS section"
	fi
done
for ext in tscn tres gd; do
	if grep -qE "^\*\.$ext[[:space:]]+.*filter=lfs" .gitattributes; then
		fail "*.${ext} must stay in plain Git, but is LFS-tracked"
	else
		pass "*.${ext} kept in plain Git (reviewable diffs)"
	fi
done
if command -v git-lfs >/dev/null 2>&1; then
	n=$(git lfs ls-files 2>/dev/null | wc -l | tr -d ' ')
	pass "git-lfs installed — $n object(s) currently stored in LFS"
else
	skip "git-lfs binary not installed here; run 'git lfs install && git lfs ls-files' on a dev machine"
fi

# --- Gate 4: Unsic-safe project settings -------------------------------------
head2 'Gate 4 — project.godot Unisoc-safe baseline'
check_godot() { # $1 = key=value line (regex), $2 = description
	if grep -qE "$1" project.godot; then
		pass "$2"
	else
		fail "$2 — expected $1 in project.godot"
	fi
}
check_godot 'renderer/rendering_method="gl_compatibility"' 'renderer/rendering_method="gl_compatibility"'
check_godot 'renderer/rendering_method\.mobile="gl_compatibility"' 'renderer/rendering_method.mobile="gl_compatibility"'
check_godot 'textures/vram_compression/import_etc2_astc=true' 'textures/vram_compression/import_etc2_astc=true'
check_godot 'run/max_fps=30' 'application/run/max_fps=30'
check_godot 'common/physics_ticks_per_second=30' 'physics ticks = 30'
check_godot '3d/physics_engine="Jolt Physics"' '3d/physics_engine="Jolt Physics"'
check_godot 'directional_shadow/size=512' 'directional shadow map = 512'
check_godot 'positional_shadow/atlas_size=512' 'positional shadow atlas = 512'
check_godot 'max_lights_per_object=2' 'max_lights_per_object = 2'
check_godot 'occlusion_culling/use_occlusion_culling=false' 'occlusion culling disabled'
check_godot 'anti_aliasing/quality/msaa_3d=0' 'MSAA 3D disabled'
check_godot 'screen_space_aa=0' 'screen-space AA disabled'
check_godot 'anisotropic_filtering_level=0' 'anisotropic filtering disabled'
check_godot 'texture_mipmap_bias=0\.75' 'texture mipmap bias = 0.75'
check_godot 'handheld/orientation=4' 'landscape sensor orientation'
check_godot 'run/main_scene="res://scenes/core/boot.tscn"' 'main scene = boot.tscn'
if grep -qE '^\[autoload\]' project.godot && grep -qE '^PerfManager="\*res://scripts/autoload/perf_manager\.gd"' project.godot; then
	pass "PerfManager autoload registered"
else
	fail "PerfManager autoload missing from project.godot"
fi
if grep -qE 'config/features=PackedStringArray\("4\.[0-9]+", "GL Compatibility"\)' project.godot; then
	pass "config/features declares GL Compatibility"
else
	fail "config/features must declare \"GL Compatibility\""
fi

# --- Gate 5: Android export preset -------------------------------------------
head2 'Gate 5 — Android export preset (APK, offline)'
check_preset() { # $1 = grep regex, $2 = description
	if grep -qE "$1" export_presets.cfg; then
		pass "$2"
	else
		fail "$2 — expected $1 in export_presets.cfg"
	fi
}
check_preset 'name="Android_APK_Unisoc"' 'preset name Android_APK_Unisoc'
check_preset 'export_format=0' 'export_format=0 (APK, not AAB)'
check_preset 'permissions/internet=false' 'permissions/internet=false (offline hard requirement)'
check_preset 'architectures/arm64-v8a=true' 'arm64-v8a enabled'
check_preset 'architectures/armeabi-v7a=true' 'armeabi-v7a enabled (T310-class devices)'
check_preset 'architectures/x86=false' 'x86 disabled'
check_preset 'architectures/x86_64=false' 'x86_64 disabled'
check_preset 'version/code=1' 'version/code=1'
check_preset 'version/name="0\.0\.1-setup"' 'version/name matches project version'
if grep -qE 'permissions/custom_permissions=.*INTERNET' export_presets.cfg; then
	fail 'custom_permissions re-adds INTERNET'
else
	pass "no custom permission re-adds INTERNET"
fi

# --- Gate 8: Folder structure ------------------------------------------------
head2 'Gate 8 — Folder structure matches §5 (all leaf dirs tracked)'
EXPECTED_DIRS="
assets/models/village
assets/models/urban
assets/models/heritage
assets/models/port
assets/textures
assets/audio/music
assets/audio/sfx
assets/audio/voice
assets/shaders
assets/fonts
scenes/core
scenes/gameplay
scenes/characters
scenes/weapons
scenes/loot
scenes/ui
scripts/autoload
scripts/systems
scripts/player
scripts/ui
resources/loot_tables
resources/themes
resources/config
addons
export_presets
"
missing=0
for dir in $EXPECTED_DIRS; do
	if [ ! -d "$dir" ]; then
		fail "directory missing on disk: $dir"
		missing=$((missing + 1))
		continue
	fi
	if [ "$(git ls-files "$dir" | wc -l | tr -d ' ')" -eq 0 ]; then
		fail "directory not tracked by Git (needs a file or .gitkeep): $dir"
		missing=$((missing + 1))
	fi
done
[ "$missing" -eq 0 ] && pass "all $(echo "$EXPECTED_DIRS" | wc -w | tr -d ' ') expected directories exist and are tracked"

# --- Gate 10: Documentation + tag -------------------------------------------
head2 'Gate 10 — README, CONTRIBUTING, tag'
for f in README.md CONTRIBUTING.md docs/UNISOC_BUDGET.md docs/PHASE1_ACCEPTANCE.md docs/ASSET_PIPELINE.md; do
	if git ls-files --error-unmatch "$f" >/dev/null 2>&1; then
		pass "$f committed"
	else
		fail "$f is not committed"
	fi
done
if git rev-parse -q --verify 'refs/tags/v0.0.1-setup' >/dev/null; then
	pass "tag v0.0.1-setup exists locally"
else
	fail "tag v0.0.1-setup missing"
fi
if git ls-remote --tags origin 'v0.0.1-setup' 2>/dev/null | grep -q 'v0.0.1-setup'; then
	pass "tag v0.0.1-setup pushed to origin"
else
	skip "tag not visible on origin (offline or not pushed yet)"
fi

# --- Secret hygiene (not a numbered gate, but it protects the keystores) -----
head2 'Extra — secret hygiene'
if git ls-files --error-unmatch export_credentials.cfg >/dev/null 2>&1; then
	fail "export_credentials.cfg is tracked — it holds keystore passwords"
else
	pass "export_credentials.cfg not tracked"
fi
keystores=$(tracked_matches '\.(keystore|jks)$' || true)
if [ "${keystores:-0}" -ne 0 ]; then
	fail "$keystores keystore file(s) tracked — keystores must live outside the repo"
else
	pass "no keystore in the repository"
fi

# --- Hardware gates ----------------------------------------------------------
head2 'Gates 6 & 7 — physical Unisoc T606 (cannot run here)'
skip 'APK built, installed and running in airplane mode — see docs/PHASE1_ACCEPTANCE.md'
skip 'adb logcat free of gl_error / vulkan / crash after 60 s — see docs/PHASE1_ACCEPTANCE.md'

printf '\n\033[1mSummary:\033[0m %d passed, %d failed, %d skipped\n' "$PASSES" "$FAILURES" "$SKIPS"
if [ "$FAILURES" -ne 0 ]; then
	printf '\033[31mPhase 1 is NOT done — fix the failures above.\033[0m\n'
	exit 1
fi
printf '\033[32mAll automated Phase 1 gates pass.\033[0m\n'
exit 0
