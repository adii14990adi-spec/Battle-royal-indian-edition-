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

# Rebuild canonical base64. Whitespace and quotes are never part of the
# alphabet, and '=' is padding rather than data — so stripping all three and
# re-applying the correct amount is lossless for any well-formed value, while
# repairing the common copy defects (lost trailing '=', stray line breaks,
# accidental surrounding quotes).
CLEAN=$(printf '%s' "$KEYSTORE_BASE64" | tr -d '[:space:]')
CLEAN="${CLEAN#\"}"
CLEAN="${CLEAN%\"}"
CLEAN="${CLEAN#\'}"
CLEAN="${CLEAN%\'}"
CLEAN=$(printf '%s' "$CLEAN" | tr -d '=')

CLEAN_LEN=${#CLEAN}
case $(( CLEAN_LEN % 4 )) in
    2) CLEAN="${CLEAN}==" ;;
    3) CLEAN="${CLEAN}=" ;;
    1) : ;; # length % 4 == 1 is unrecoverable; fall through to the error below
esac

# base64's own objection never contains the input, only what it objected to,
# so relaying stderr is safe. Use a file: ordering of 2>&1 against a redirect
# is easy to get wrong and would otherwise pollute the decoded keystore.
B64_ERRFILE="$(mktemp)"
if ! printf '%s' "$CLEAN" | base64 -d > "$KEYSTORE_PATH" 2> "$B64_ERRFILE"; then
    # Diagnose without ever echoing the secret. The cleaned length is what
    # actually reached the decoder, so that is what we report.
    BAD=$(printf '%s' "$CLEAN" | tr -d 'A-Za-z0-9+/=' | wc -c)
    B64_ERR=$(tr '\n' ' ' < "$B64_ERRFILE")
    # A well-formed base64 string is always a multiple of 4 after padding.
    # If the alphabet is clean yet the length still is not, characters were
    # lost from (or added to) the data itself — not merely from the '='
    # padding — so no amount of re-padding can recover it.
    if [ "$BAD" -eq 0 ]; then
        HINT="the alphabet is clean but the length is not a multiple of 4, so characters are missing from or extra in the DATA — the value is truncated/corrupt, not just missing '=' padding. Re-create it with: base64 -w0 release.keystore"
    else
        HINT="re-create it with: base64 -w0 release.keystore  (single line, no quotes)"
    fi
    echo "::error::KEYSTORE_BASE64 is not valid base64 (raw length=${#KEYSTORE_BASE64}, cleaned length=$CLEAN_LEN, cleaned mod 4=$(( CLEAN_LEN % 4 )), non-base64 chars=$BAD; base64 said: ${B64_ERR:-unknown}). $HINT" >&2
    rm -f "$B64_ERRFILE" "$KEYSTORE_PATH"
    exit 1
fi
rm -f "$B64_ERRFILE"

# A blank/truncated secret decodes to 0 bytes; Godot would then fail later with
# a much less actionable ERR_FILE_CANT_OPEN. Catch it here instead.
if [[ ! -s "$KEYSTORE_PATH" ]]; then
    echo "::error::decoded keystore is empty (0 bytes) — KEYSTORE_BASE64 is blank or truncated" >&2
    rm -f "$KEYSTORE_PATH"
    exit 1
fi

# Cheap sanity fingerprint. Not fatal — formats vary — but a decoded blob that
# matches no known keystore header almost always means the secret was corrupted
# in transit, and Godot's signing error would be much less direct.
HEAD4=$(head -c 4 "$KEYSTORE_PATH" | od -An -tx1 | tr -d ' \n')
case "$HEAD4" in
    feedfeed|2d2d2d2d|3082*|3083*|3081*) : ;;  # JKS | PEM "----" | ASN.1/PKCS#12
    *)
        echo "::warning::decoded KEYSTORE_BASE64 does not look like a keystore (first bytes: $HEAD4). Expected JKS (fe ed fe ed), PKCS#12 (30 82…), or PEM (----). If the export fails to sign, re-create the secret with: base64 -w0 release.keystore" >&2
        ;;
esac

# Fail fast, and say WHICH credential is wrong. Godot only reports
# "Release Username and/or Password is invalid for the given Release
# Keystore" after taking the whole export path down, and does not say which
# of the two is at fault. keytool is present under the CI's JDK; skip
# silently when it is not (local runs without a JDK).
if command -v keytool >/dev/null 2>&1; then
    if ! keytool -list -keystore "$KEYSTORE_PATH" -storepass "$KEYSTORE_PASSWORD" >/dev/null 2>&1; then
        echo "::error::KEYSTORE_PASSWORD does not open the decoded keystore. Check it with: keytool -list -keystore release.keystore -storepass '<password>'  — then update the KEYSTORE_PASSWORD secret." >&2
        exit 1
    fi
    # List WITHOUT -alias on purpose: the aliases are not secret, and printing
    # them makes a mismatch self-correcting — the log alone then says exactly
    # what KEYSTORE_USER must be set to. (KEYSTORE_USER itself is masked by
    # Actions, so the two together are what a reader needs.)
    KEYSTORE_LIST=$(keytool -list -keystore "$KEYSTORE_PATH" -storepass "$KEYSTORE_PASSWORD" 2>/dev/null || true)
    # Alias lines look like "name, 05-Nov-2026, PrivateKeyEntry," (PKCS#12) or
    # "name, Nov 5, 2026, PrivateKeyEntry," (JKS) — take the first field of any
    # line carrying an entry type, so both date orders parse.
    ALIASES=$(printf '%s\n' "$KEYSTORE_LIST" \
        | sed -nE 's/^([^,]+),.*(PrivateKeyEntry|TrustedCertEntry|SecretKeyEntry).*$/\1/p' \
        | sed 's/^[[:space:]]*//; s/[[:space:]]*$//' \
        | tr '\n' ' ' | sed 's/[[:space:]]*$//')
    echo "Keystore aliases found: [${ALIASES:-<none parsed>}]   KEYSTORE_USER='$KEYSTORE_USER'"
    if ! keytool -list -keystore "$KEYSTORE_PATH" -storepass "$KEYSTORE_PASSWORD" -alias "$KEYSTORE_USER" >/dev/null 2>&1; then
        echo "::error::KEYSTORE_USER is not an alias in this keystore (password was accepted). Aliases in this keystore: [${ALIASES:-<none parsed>}]. Update the KEYSTORE_USER secret to exactly one of those — case-sensitive, no surrounding spaces." >&2
        exit 1
    fi
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
