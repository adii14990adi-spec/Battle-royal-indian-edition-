#!/usr/bin/env bash
# Injects CI signing material into export_presets.cfg.
# Required env vars (set by workflow from GitHub Secrets):
#   KEYSTORE_BASE64, KEYSTORE_USER, KEYSTORE_PASSWORD
# Fails loud (exit 1) if any is missing or the decode yields an empty file.
set -euo pipefail

if [[ -z "${KEYSTORE_BASE64:-}" || -z "${KEYSTORE_USER:-}" || -z "${KEYSTORE_PASSWORD:-}" ]]; then
    echo "::error::KEYSTORE_BASE64 / KEYSTORE_USER / KEYSTORE_PASSWORD secrets missing" >&2
    exit 1
fi

KEYSTORE_PATH="$(pwd)/release.keystore"

# Tolerate the common paste defects: whitespace (LF/CRLF/indentation) and a
# layer of surrounding quotes. Neither can ever appear in valid base64, so
# stripping them cannot corrupt a good value — but each will make a strict
# `base64 -d` fail.
CLEAN=$(printf '%s' "$KEYSTORE_BASE64" | tr -d '[:space:]')
CLEAN="${CLEAN#\"}"
CLEAN="${CLEAN%\"}"
CLEAN="${CLEAN#\'}"
CLEAN="${CLEAN%\'}"

# Restore stripped '=' padding. `base64 -d` requires a multiple of 4, but
# trailing '=' is routinely lost when a secret is copied by hand (many
# editors and copy flows trim it as if it were whitespace). Re-adding it is
# lossless: a correct value is already a multiple of 4, so this branch can
# only ever fire on a value that could not decode otherwise. len % 4 == 1 is
# genuinely malformed and is deliberately left to fail below.
case $(( ${#CLEAN} % 4 )) in
    2) CLEAN="${CLEAN}==" ;;
    3) CLEAN="${CLEAN}=" ;;
esac

if ! printf '%s' "$CLEAN" | base64 -d > "$KEYSTORE_PATH" 2>/dev/null; then
    # Diagnose without ever echoing the secret itself: length alone usually
    # identifies the mistake (a pasted file path is ~20 chars, a real keystore
    # base64 is several thousand), and the remainder tells us whether padding
    # was the issue.
    BAD=$(printf '%s' "$CLEAN" | tr -d 'A-Za-z0-9+/=' | wc -c)
    echo "::error::KEYSTORE_BASE64 is not valid base64 (length=${#KEYSTORE_BASE64}, length mod 4=$(( ${#KEYSTORE_BASE64} % 4 )), non-base64 chars=$BAD). Re-create it with: base64 -w0 release.keystore  (single line, no quotes)" >&2
    rm -f "$KEYSTORE_PATH"
    exit 1
fi

# A blank/truncated secret decodes to 0 bytes; Godot would then fail later with
# a much less actionable ERR_FILE_CANT_OPEN. Catch it here instead.
if [[ ! -s "$KEYSTORE_PATH" ]]; then
    echo "::error::decoded keystore is empty (0 bytes) — KEYSTORE_BASE64 is blank or truncated" >&2
    rm -f "$KEYSTORE_PATH"
    exit 1
fi

USER_ESC=$(printf '%s' "$KEYSTORE_USER" | sed -e 's/[\/&]/\\&/g')
PASS_ESC=$(printf '%s' "$KEYSTORE_PASSWORD" | sed -e 's/[\/&]/\\&/g')
PATH_ESC=$(printf '%s' "$KEYSTORE_PATH" | sed -e 's/[\/&]/\\&/g')

sed -i \
    -e "s|^keystore/release=.*|keystore/release=\"${PATH_ESC}\"|" \
    -e "s|^keystore/release_user=.*|keystore/release_user=\"${USER_ESC}\"|" \
    -e "s|^keystore/release_password=.*|keystore/release_password=\"${PASS_ESC}\"|" \
    export_presets.cfg

# Guard against the silent no-op that would otherwise happen if the
# keystore/release* lines were ever removed from export_presets.cfg:
# sed exits 0 having patched nothing, and the export fails much later.
if ! grep -q '^keystore/release=".\+"' export_presets.cfg; then
    echo "::error::keystore/release line missing or not patched in export_presets.cfg" >&2
    exit 1
fi

echo "Keystore patched: $KEYSTORE_PATH (user=$KEYSTORE_USER)"
