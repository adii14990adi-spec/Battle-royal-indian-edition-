#!/usr/bin/env bash
# Phase 2 acceptance gates for a built APK.
#   Gate A: no android.permission.INTERNET (offline rule)
#   Gate B: android.permission.WAKE_LOCK present (manifest not silently empty)
#   Gate C: size <= APK_SIZE_LIMIT_MB (default 120)
#   Gate D: arm64-v8a present (Unisoc T606); armeabi-v7a warn-only
#   Gate E: the baked world is actually inside the package (chunk scenes and
#           collision resources reachable at assets/…), so "it installs and the
#           boot screen works" can never again pass for a playable build.
#
# Gate E is what catches the worst failure mode this project has hit: the APK
# boots, the UI works, you tap "Enter Siran Island" and nothing happens, because
# the baked files were created after the editor's last filesystem scan and never
# made it into the pack. It can be skipped with APK_SKIP_PAYLOAD_GATE=1 for a
# deliberately unbaked build.
set -euo pipefail

APK="${1:-}"
if [[ -z "$APK" || ! -f "$APK" ]]; then
    echo "::error::APK missing: ${APK:-<none given>}" >&2
    exit 1
fi

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
    echo "APK contains $entry_count entries, $asset_count under assets/"
    if [[ "${GITHUB_ACTIONS:-}" == "true" ]]; then
        echo "::notice::$(basename "$APK"): $entry_count entries, $asset_count under assets/"
    fi

    payload_problems=0
    for required in \
        "assets/scenes/world/siran_island.tscn" \
        "assets/assets/shaders/water_surface.gdshader"; do
        if printf '%s\n' "$ENTRIES" | grep -qx "$required"; then
            continue
        fi
        echo "::error::APK is missing $required — it was not packed" >&2
        payload_problems=$((payload_problems + 1))
    done

    # Scripts are re-encoded by script_export_mode, so accept either spelling:
    # a node whose script is absent still renders and still looks pressable.
    if ! printf '%s\n' "$ENTRIES" | grep -qE '^assets/scripts/ui/boot_world_link\.gdc?$'; then
        echo "::error::APK is missing assets/scripts/ui/boot_world_link.gd — the boot button would do nothing" >&2
        payload_problems=$((payload_problems + 1))
    fi

    # grep -c prints 0 and exits 1 when nothing matches, hence `|| true`.
    chunk_scenes=$(printf '%s\n' "$ENTRIES" \
        | grep -cE '^assets/scenes/world/terrain/chunk_[0-9]+_[0-9]+\.tscn$' || true)
    chunk_collisions=$(printf '%s\n' "$ENTRIES" \
        | grep -cE '^assets/resources/world/terrain/chunk_[0-9]+_[0-9]+_collision\.res$' || true)

    if [[ "$chunk_scenes" -lt 64 ]]; then
        echo "::error::$chunk_scenes baked chunk scenes in the APK, expected 64 — the bake is not in the pack" >&2
        echo "::error::bake BEFORE exporting, then re-import so the editor file list includes the new files; export_presets.cfg's include_filter must list *.tscn and *.res" >&2
        payload_problems=$((payload_problems + 1))
    fi
    if [[ "$chunk_collisions" -lt 64 ]]; then
        echo "::error::$chunk_collisions baked collision resources in the APK, expected 64" >&2
        payload_problems=$((payload_problems + 1))
    fi

    if [[ "$payload_problems" -ne 0 ]]; then
        echo "APK entry sample (first 12 lines):" >&2
        printf '%s\n' "$ENTRIES" | head -12 >&2
        echo "assets/ entries (first 12):" >&2
        printf '%s\n' "$ENTRIES" | grep '^assets/' | head -12 >&2
        exit 1
    fi
    echo "Payload verified: $chunk_scenes chunk scenes, $chunk_collisions collision resources, world scene and button script present"
    if [[ "${GITHUB_ACTIONS:-}" == "true" ]]; then
        echo "::notice::$(basename "$APK") payload: $chunk_scenes/64 chunk scenes, $chunk_collisions/64 collision resources"
    fi
fi
