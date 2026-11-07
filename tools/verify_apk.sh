#!/usr/bin/env bash
# Phase 2 acceptance gates for a built APK.
#   Gate A: no android.permission.INTERNET (offline rule)
#   Gate B: android.permission.WAKE_LOCK present (manifest not silently empty)
#   Gate C: size <= APK_SIZE_LIMIT_MB (default 120)
#   Gate D: arm64-v8a present (Unisoc T606); armeabi-v7a warn-only
#   Gate E: the baked world is actually inside the package (the committed map
#           scene plus the river mesh, reachable at assets/…), so "it installs
#           and the boot screen works" can never again pass for a playable build.
#
# Gate E is what catches the worst failure mode this project has hit: the APK
# boots, the UI works, you tap "Enter Siran Island" and nothing happens, because
# the map never made it into the pack. The map is now one committed scene, so
# the check is simply "is that scene (or its .remap) in the APK". It can be skipped with APK_SKIP_PAYLOAD_GATE=1 for a
# deliberately unbaked build.
set -euo pipefail

APK="${1:-}"
if [[ -z "$APK" || ! -f "$APK" ]]; then
    echo "::error::APK missing: ${APK:-<none given>}" >&2
    exit 1
fi

# An unexpected non-zero exit used to surface as a bare "exit code 2" with no
# indication of which check produced it (CI annotations are capped, so the real
# message can be dropped). This trap always names the failing line.
on_unexpected_error() {
    local status=$?
    printf '::error::%s aborted with status %d at tools/verify_apk.sh line %s\n' \
        "$(basename "${APK:-no-apk}")" "$status" "${BASH_LINENO[0]}"
    exit "$status"
}
trap on_unexpected_error ERR

AAPT2="$(command -v aapt2 || true)"
AAPT="$(command -v aapt || true)"
if [[ -z "$AAPT2" && -z "$AAPT" ]]; then
    echo "::error::neither aapt2 nor aapt found on PATH" >&2
    exit 1
fi

dump_permissions() {
    if [[ -n "$AAPT2" ]]; then
        "$AAPT2" dump permissions "$APK" 2>/dev/null && return 0
    fi
    if [[ -n "$AAPT" ]]; then
        "$AAPT" dump permissions "$APK" 2>/dev/null && return 0
    fi
    return 1
}

dump_badging() {
    if [[ -n "$AAPT2" ]]; then
        "$AAPT2" dump badging "$APK" 2>/dev/null && return 0
    fi
    if [[ -n "$AAPT" ]]; then
        "$AAPT" dump badging "$APK" 2>/dev/null && return 0
    fi
    return 1
}

# --- Gate A: INTERNET must be absent ---------------------------------------
if ! PERMS="$(dump_permissions)"; then
    echo "::error::could not dump permissions from $APK" >&2
    exit 1
fi
if printf '%s\n' "$PERMS" | grep -qi "android.permission.INTERNET"; then
    echo "::error::INTERNET permission present in $APK — offline rule violated"
    printf '%s\n' "$PERMS"
    exit 1
fi

# --- Gate B: WAKE_LOCK must be present -------------------------------------
# A totally empty permission list means manifest generation silently failed.
if ! printf '%s\n' "$PERMS" | grep -qi "android.permission.WAKE_LOCK"; then
    echo "::error::WAKE_LOCK missing — manifest likely corrupted or export failed silently"
    printf '%s\n' "$PERMS"
    exit 1
fi

# --- Gate C: size budget ----------------------------------------------------
SIZE_BYTES=$(stat -c%s "$APK")
SIZE_MB=$(( SIZE_BYTES / 1024 / 1024 ))
LIMIT_MB="${APK_SIZE_LIMIT_MB:-120}"
if (( SIZE_MB > LIMIT_MB )); then
    echo "::error::APK size ${SIZE_MB}MB exceeds limit ${LIMIT_MB}MB"
    exit 1
fi

# --- Gate D: ABI coverage ---------------------------------------------------
if ! BADGING="$(dump_badging)"; then
    echo "::error::could not dump badging from $APK" >&2
    exit 1
fi

# 'native-code: 'arm64-v8a' 'armeabi-v7a'' — take everything after the label
# and strip quotes so BOTH ABIs are captured (a first-quote-only regex would
# silently drop every ABI after the first).
ARCHS=$(printf '%s\n' "$BADGING" \
    | sed -n "s/^native-code: //p" \
    | tr -d "'")

if ! printf '%s\n' "$ARCHS" | grep -q "arm64-v8a"; then
    echo "::error::arm64-v8a ABI missing — Unisoc T606 (64-bit) will fail to install"
    echo "arches detected: ${ARCHS:-<none>}"
    exit 1
fi
if ! printf '%s\n' "$ARCHS" | grep -q "armeabi-v7a"; then
    echo "::warn::armeabi-v7a ABI missing — Unisoc T310 (32-bit) devices excluded"
fi

echo "APK verified: ${SIZE_MB}MB, arches=[${ARCHS}], INTERNET absent, WAKE_LOCK present"

# --- Gate E: the baked world is inside the package ---------------------------
# Godot stores every exported file as its own APK entry under assets/
# (EditorExportPlatformAndroid::save_apk_file maps "res://x" to "assets/x"), so
# the payload can be audited from the entry list without a device.
# Entry listing helpers. `unzip -l` prints a formatted table (dates, sizes, and a
# 40-character truncation for long paths), so ONLY a names-only listing may be
# parsed: zipinfo -1, or Python's zipfile, which also copes with the APK signing
# block. Mixing in `unzip -l` output silently turns every `grep -x path` into a
# false negative and the gate then blames the pack for its own parsing bug.
list_apk_entries_unzip() {
    command -v unzip >/dev/null 2>&1 || return 1
    unzip -Z1 "$APK" 2>/dev/null
}

list_apk_entries_python() {
    command -v python3 >/dev/null 2>&1 || return 1
    python3 - "$APK" 2>/dev/null <<'PYZIP'
import sys
import zipfile

with zipfile.ZipFile(sys.argv[1]) as archive:
    sys.stdout.write("\n".join(archive.namelist()))
PYZIP
}

list_apk_entries() {
    local entries
    if entries="$(list_apk_entries_unzip)" && [[ -n "$entries" ]]; then
        printf '%s\n' "$entries"
        return 0
    fi
    if entries="$(list_apk_entries_python)" && [[ -n "$entries" ]]; then
        printf '%s\n' "$entries"
        return 0
    fi
    return 1
}

if [[ "${APK_SKIP_PAYLOAD_GATE:-0}" == "1" ]]; then
    echo "::warning::Gate E skipped (APK_SKIP_PAYLOAD_GATE=1)"
elif ! ENTRIES="$(list_apk_entries)"; then
    echo "::error::could not list the entries in $APK (no names-only listing available)" >&2
    exit 1
else
    entry_count=$(printf '%s\n' "$ENTRIES" | wc -l | tr -d ' ')
    asset_count=$(printf '%s\n' "$ENTRIES" | grep -c '^assets/' || true)

    packed_count() {
        printf '%s\n' "$ENTRIES" | grep -cE "$1" || true
    }

    # Godot may convert text resources at export time and store the result as
    # .godot/exported/<hash>/export-<md5>-<name>.scn, leaving a `<path>.remap`
    # beside the original path (editor_export_platform.cpp: _export_customize,
    # then save_file(from + ".remap", ...)). The engine resolves res://x.tscn
    # through that remap, so "packed" means the file itself OR its .remap is in
    # the APK. Three earlier runs of this gate failed on exactly that distinction
    # and blamed the pack for it.
    world_scene=$(packed_count '^assets/scenes/world/siran_island\.tscn(\.remap)?$')
    baked_map=$(packed_count '^assets/scenes/world/siran_island_baked\.tscn(\.remap)?$')
    river_mesh=$(packed_count '^assets/resources/world/baked/siran_river_surface\.res(\.remap)?$')
    boot_script=$(packed_count '^assets/scripts/ui/boot_world_link\.(gd|gdc)(\.remap)?$')
    water_shader=$(packed_count '^assets/assets/shaders/water_surface\.gdshader(\.remap)?$')
    converted=$(packed_count '^assets/\.godot/exported/')

    digest=$(printf '%s\n' "$ENTRIES" | grep '^assets/' \
        | sed -n 's/.*\.\([A-Za-z0-9]\{1,10\}\)$/\1/p' | sort | uniq -c | sort -rn \
        | sed -n '1,8p' | awk '{ printf "%s=%s ", $2, $1 }')

    echo "APK contains $entry_count entries, $asset_count under assets/, $converted converted under .godot/exported/"
    echo "assets/ extensions: ${digest:-<none>}"
    if [[ "${GITHUB_ACTIONS:-}" == "true" ]]; then
        echo "::notice::$(basename "$APK"): $entry_count entries, $asset_count under assets/, $converted converted"
        echo "::notice::assets/ extensions: ${digest:-<none>}"
    fi

    payload_problems=0
    if [[ "$world_scene" -lt 1 ]]; then
        echo "::error::the world scene is not in the APK (assets/scenes/world/siran_island.tscn or its .remap)" >&2
        payload_problems=$((payload_problems + 1))
    fi
    if [[ "$boot_script" -lt 1 ]]; then
        echo "::error::the boot button script is not in the APK — the button would look fine and do nothing" >&2
        payload_problems=$((payload_problems + 1))
    fi
    if [[ "$water_shader" -lt 1 ]]; then
        echo "::error::assets/shaders/water_surface.gdshader is not in the APK" >&2
        payload_problems=$((payload_problems + 1))
    fi
    if [[ "$baked_map" -lt 1 ]]; then
        echo "::error::scenes/world/siran_island_baked.tscn (or its .remap) is not in the APK — the world would be empty" >&2
        echo "::error::commit the scene, then re-import so the editor file list includes it; export_presets.cfg include_filter must list *.tscn, *.res and *.scn" >&2
        payload_problems=$((payload_problems + 1))
    fi
    if [[ "$river_mesh" -lt 1 ]]; then
        echo "::error::resources/world/baked/siran_river_surface.res is not in the APK" >&2
        payload_problems=$((payload_problems + 1))
    fi

    if [[ "$payload_problems" -ne 0 ]]; then
        echo "APK entry sample (first 12 lines):" >&2
        printf '%s\n' "$ENTRIES" | sed -n '1,12p' >&2
        echo "world/ entries:" >&2
        printf '%s\n' "$ENTRIES" | grep -E '^assets/(scenes|resources)/world/' \
            | sed -n '1,8p' >&2 || true
        exit 1
    fi
    echo "Payload verified: $world_scene world scene, $baked_map baked map scene," \
        "$river_mesh river mesh, water shader, button script"
fi
