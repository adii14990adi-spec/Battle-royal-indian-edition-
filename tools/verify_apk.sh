#!/usr/bin/env bash
# Phase 2 acceptance gates for a built APK.
#   Gate A: no android.permission.INTERNET (offline rule)
#   Gate B: android.permission.WAKE_LOCK present (manifest not silently empty)
#   Gate C: size <= APK_SIZE_LIMIT_MB (default 120)
#   Gate D: arm64-v8a present (Unisoc T606); armeabi-v7a warn-only
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
