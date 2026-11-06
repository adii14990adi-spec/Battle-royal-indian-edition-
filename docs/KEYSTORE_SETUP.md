# Keystore setup (Phase 2)

CI signs a **release** APK with a keystore that lives only in GitHub Secrets.
Nothing signing-related is ever committed — `.gitignore` blocks `release.keystore`,
`release.keystore.b64`, `*.keystore` and `*.jks`.

## 1. Generate the keystore (once, locally)

```bash
keytool -genkeypair -v \
  -keystore release.keystore \
  -alias battlehorizons \
  -keyalg RSA -keysize 2048 -validity 10000 \
  -storepass "<STRONG_PASSWORD>" \
  -keypass  "<STRONG_PASSWORD>" \
  -dname "CN=Battle Horizons, O=Aura Studios, C=IN"
```

> **Lose this file and you can never update the app on existing devices.**
> Keep an offline backup somewhere that is not this repository.

## 2. Encode it

```bash
base64 -w0 release.keystore > release.keystore.b64   # macOS: base64 -i release.keystore
```

Both files are gitignored. Delete them from your working copy when you are done:

```bash
rm -f release.keystore release.keystore.b64
```

## 3. Add the three GitHub Secrets

Repo → **Settings → Secrets and variables → Actions → New repository secret**.

| Secret name | Value |
| --- | --- |
| `KEYSTORE_BASE64` | contents of `release.keystore.b64` (single line, no wrapping) |
| `KEYSTORE_USER` | the `-alias` you chose above (e.g. `battlehorizons`) |
| `KEYSTORE_PASSWORD` | the `-storepass` value |

All three are **required**. `tools/patch_keystore.sh` fails the build with a
`::error::` annotation if any is missing or if `KEYSTORE_BASE64` decodes to an
empty file — it never falls back to an unsigned or debug-signed release build.

## 4. What CI does with them

`.github/workflows/build.yml` calls `tools/patch_keystore.sh` before every
export. The script base64-decodes to `release.keystore` in the workspace and
rewrites these three lines in `export_presets.cfg`:

```
keystore/release="/path/to/release.keystore"
keystore/release_user="..."
keystore/release_password="..."
```

The committed `export_presets.cfg` always keeps those fields **blank** — the
values exist only for the lifetime of the workflow run.

> The decoded file is written into the repo working tree but is gitignored, and
> the job never prints the password (`set -x` is not used anywhere in the
> tooling).

## 5. Debug builds

Debug APKs are signed with a throwaway keystore generated inside the job
(`~/.android/debug.keystore`, alias `androiddebugkey`, password `android` —
the standard Android debug identity). No secrets are involved.

Godot 4.4.1 does **not** read `~/.android/debug.keystore` on its own: it falls
back to its own editor-settings path and aborts with `ERR_FILE_CANT_OPEN` if
that file is missing. The workflow therefore also exports
`GODOT_ANDROID_KEYSTORE_DEBUG_PATH`, `GODOT_ANDROID_KEYSTORE_DEBUG_USER` and
`GODOT_ANDROID_KEYSTORE_DEBUG_PASSWORD`, which Godot's `get_or_env()` prefers
over both the preset and the editor setting.

The same mechanism is the fallback for `--export-release`: when
`KEYSTORE_BASE64` is absent the workflow points
`GODOT_ANDROID_KEYSTORE_RELEASE_{PATH,USER,PASSWORD}` at that debug keystore, so
the run still produces an installable `battle-horizons.apk` and only warns. If
the artifact name is right but the APK is signed by `androiddebugkey`, the three
secrets from §2 are missing.
