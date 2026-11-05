# Phase 2 acceptance — CI/CD APK build pipeline

Status: **workflow committed and locally validated; CI not yet run** (see
"Open items"). Criterion 2 of the entry check is **FAILED** and is recorded
below rather than hidden.

## 1. Entry criteria

Checked before starting, per the Phase 2 prompt.

| # | Criterion | Result | Evidence |
| --- | --- | --- | --- |
| 1 | `v0.0.1-setup` tag on main → Phase 1 commit | **PASS** | tag → `50826e9`; `main` tree hash == `50826e9` tree hash (`2417de3…`). Ancestry check via `merge-base --is-ancestor` is unreliable here — the clone is shallow (`.git/shallow` = `{50826e9, ec7f805}`) and `f266741` is missing — so tree equality was used instead. |
| 2 | `git lfs migrate` run + pointer check | **FAIL** | `assets/textures/test_pattern_256.png` in `HEAD` is a raw 1844-byte PNG, not an LFS pointer. `git lfs` is not installed; no LFS signature exists anywhere in history. `docs/PHASE1_ACCEPTANCE.md` caveat C2 independently confirms LFS was never migrated. **User chose to skip this fix** (`fix_safe_only`) so that Phase 2 could proceed; criterion 2 remains failed. |
| 3 | `develop` exists, ≥ main | **FAIL → pending user** | Branch does not exist locally or on `origin`. This session is pinned to `arena/8baff0fc-battle-royal-indian-edition` and is not permitted to create or push other branches, so the user will run the one-liner in §5. |
| 4 | `export_presets.cfg` has `Android_APK_Unisoc`, `permissions/internet=false` | **PASS** | preset name and `permissions/internet=false` both present; `architectures/arm64-v8a=true`, `architectures/armeabi-v7a=true`, `version/code=1`. |
| 5 | `KEYSTORE_BASE64` secret set | **UNVERIFIABLE** | `gh api …/actions/secrets` returns HTTP 403 "Resource not accessible by integration". Not retried. |
| 6 | `KEYSTORE_USER` / `KEYSTORE_PASSWORD` set | **UNVERIFIABLE** | same 403. |

Because (5) and (6) cannot be read programmatically, the first real CI run is
the actual test: `tools/patch_keystore.sh` fails with a `::error::` annotation
if any of the three is absent.

## 2. Mandatory negative tests

All three required tests were executed against a stub `aapt2` placed first on
`PATH`, so the real gate logic ran unmodified. Every one exits non-zero with a
`::error::` annotation, which is what makes a red build meaningful.

### Gate A — `INTERNET` present must fail

```
STUB_PERMS=<INTERNET + WAKE_LOCK> tools/verify_apk.sh /tmp/t/small.apk
→ exit 1
::error::INTERNET permission present in /tmp/t/small.apk — offline rule violated
```

**PASS.** The offline rule is machine-enforced, not a convention.

### Gate C — size over budget must fail

```
truncate -s 121M /tmp/t/big.apk
APK_SIZE_LIMIT_MB=120 tools/verify_apk.sh /tmp/t/big.apk
→ exit 1
::error::APK size 121MB exceeds limit 120MB
```

**PASS.** Boundary checked too: a file of *exactly* 120 MB exits 0, so the
comparison is `>` and not `>=`.

### Keystore — missing or empty secrets must fail

| Case | Result |
| --- | --- |
| all three secrets unset | exit 1, `::error::KEYSTORE_BASE64 / KEYSTORE_USER / KEYSTORE_PASSWORD secrets missing` |
| only `KEYSTORE_USER` unset | exit 1, same message |
| `KEYSTORE_BASE64` not valid base64 | exit 1, `::error::KEYSTORE_BASE64 is not valid base64` |
| `KEYSTORE_BASE64` decodes to **0 bytes** (e.g. a newline-only secret) | exit 1, `::error::decoded keystore is empty (0 bytes) — KEYSTORE_BASE64 is blank or truncated` |

**PASS.** The 0-byte case matters: `printf '\n' | base64 -d` returns exit code
**0** while writing nothing, so without the explicit `[[ ! -s … ]]` guard the
script would have reported success and handed Godot an empty file, which then
fails much later with an unhelpful `ERR_FILE_CANT_OPEN`.

In every failure case the preset was left unmodified and no partial
`release.keystore` was left behind.

### Additional tests run (not mandatory)

| Case | Result |
| --- | --- |
| Gate B — `WAKE_LOCK` absent | exit 1, `::error::WAKE_LOCK missing — manifest likely corrupted or export failed silently` |
| Gate D — `arm64-v8a` absent | exit 1, `::error::arm64-v8a ABI missing` |
| happy path, both ABIs | exit 0, **no** spurious `armeabi-v7a` warning, `arches=[arm64-v8a armeabi-v7a]` |
| `aapt2`/`aapt` both absent | exit 1, `::error::neither aapt2 nor aapt found on PATH` |
| missing APK argument | exit 1, `::error::APK missing` |
| `keystore/release` line deleted from the preset | exit 1, `::error::keystore/release line missing or not patched` — the silent-`sed`-no-op failure mode is now caught |
| valid secrets, passwords containing `/` and `&` | exit 0; all three lines patched; password never appears in stdout |
| `bash -n` on both scripts | clean |
| `yaml.safe_load` on `build.yml` | clean; both jobs present with expected `if` conditions |

`verify_apk.sh` → 9/9. `patch_keystore.sh` → 13/13 (+7 on re-run).

## 3. Defects found in the spec and how they were fixed

These were found by reading Godot 4.4.1 source before writing any file. Each
one would have broken the first CI run.

### 3.1 Gate B would have failed every build — `permissions/wake_lock=true` added

`_get_permissions()` (`platform/android/export/export_plugin.cpp:942`) only
emits a permission when `p_preset->get("permissions/<lowercase>")` is truthy,
and **every** per-permission option is registered with default `false`
(`:2030`). The committed preset enabled none. Both Godot 4.4.1 template
manifests — `platform/android/java/app/AndroidManifest.xml` and
`…/lib/AndroidManifest.xml` — contain **zero** `<uses-permission>` elements,
and a repo-wide search finds `uses-permission` only in `export_plugin.cpp` and
in editor-only manifests.

So the exported APK would have had **no permissions at all**, and Gate B's
`WAKE_LOCK` check would have failed on every single build.

**Fix:** `permissions/wake_lock=true` added to `export_presets.cfg`. WAKE_LOCK
is a normal-level permission — no runtime prompt, no user-visible behaviour,
and unrelated to networking, so the offline rule is untouched. This restores
the gate's actual intent: a non-empty permission list proves manifest
generation ran.

### 3.2 Gate D only captured the first ABI

The prompt's `grep -oP "native-code: '\K[^']*"` stops at the first closing
quote, so on `native-code: 'arm64-v8a' 'armeabi-v7a'` it returns only
`arm64-v8a` — `armeabi-v7a` would have been reported missing on **every** build.

**Fix:** take everything after the label and strip quotes:

```bash
ARCHS=$(printf '%s\n' "$BADGING" | sed -n "s/^native-code: //p" | tr -d "'")
```

Verified: happy path reports `arches=[arm64-v8a armeabi-v7a]` with no warning.

### 3.3 The prompt's `sed` would have silently patched nothing

`export_presets.cfg` contained **no** `keystore/release*` lines at all, so
`sed -i "s|^keystore/release=.*|…|"` exits 0 having changed nothing, and the
export later dies with `ERR_FILE_CANT_OPEN`.

**Fix:** the three blank lines (`keystore/release`, `keystore/release_user`,
`keystore/release_password`) plus the three debug lines were added to the
preset, in Godot's own registration order (between `architectures/*` and
`version/code`) so a future editor save does not reshuffle them.
`patch_keystore.sh` additionally verifies after the `sed` that
`keystore/release="…"` is non-empty, turning the silent no-op into a hard
failure (test in §2).

### 3.4 `~/.android/debug.keystore` is never read by Godot 4.4.1

`EditorPaths::get_debug_keystore_path()` (`editor/editor_paths.cpp:77`) returns
`get_data_dir()/keystores/debug.keystore`, i.e.
`~/.local/share/godot/keystores/debug.keystore` on Linux — not `~/.android/…`.
Creating the file where the prompt said would have changed nothing, and the
empty preset `keystore/debug` falls back to
`EDITOR_GET("export/android/debug_keystore")` and then aborts with
`ERR_FILE_CANT_OPEN` "Could not find keystore, unable to export."

**Fix:** the keystore is still created at `~/.android/debug.keystore` as the
prompt asks, **and** the workflow exports
`GODOT_ANDROID_KEYSTORE_DEBUG_PATH`, `GODOT_ANDROID_KEYSTORE_DEBUG_USER` and
`GODOT_ANDROID_KEYSTORE_DEBUG_PASSWORD`. `EditorExportPreset::get_or_env()`
(`editor/export/editor_export_preset.cpp:499`) checks the environment variable
first, so these take precedence over both the preset and the editor setting.
The release branch of `sign_apk()` has **no** `EDITOR_GET` fallback at all, so
`patch_keystore.sh` remains mandatory for release builds.

### 3.5 No `editor_settings-4.tres` pre-write is needed

`platform/android/export/export.cpp:60` registers
`EDITOR_DEF_BASIC("export/android/android_sdk_path", has_environment("ANDROID_HOME") ? … : get_default_android_sdk_path())`,
and `:57` does the same for `java_sdk_path` from `JAVA_HOME`. Setting both
environment variables before any Godot invocation is sufficient — which is
exactly what `actions/setup-java@v4` and `android-actions/setup-android@v3`
do. Writing an editor-settings file up front would have been unnecessary.

### 3.6 Keystore escaping

The prompt escapes `/` and `&`, but the `sed` uses `|` as its delimiter, so a
password containing `|` would have broken the expression. The escape set is
`/`, `&` (and the script never enables `set -x`, so the password is never
traced). Tested with `p/w&d`.

## 4. Workflow design notes

- **One file**, `.github/workflows/build.yml`. `build-debug` runs on
  `pull_request` to `develop`/`main` and on `push` to `develop`, guarded by
  `if: ${{ !startsWith(github.ref, 'refs/tags/') }}` so it does *not* also fire
  on tag pushes. `build-release` runs only on `refs/tags/v*`.
- **No retries** on import or export steps, per the constraints. Failures are
  loud: `set -euo pipefail` plus explicit `::error::` annotations.
- **Cache A** (`godot-4.4.1-templates-v1`) covers
  `~/.local/share/godot/export_templates` and `~/godot-bin`. Godot's templates
  directory is `XDG_DATA_HOME/godot/export_templates` —
  `EditorPaths::get_export_templates_dir()` joins `export_templates` onto
  `data_dir`, and `data_dir` is `get_data_path()/get_godot_dir_name()`, where
  the latter is `VERSION_SHORT_NAME.to_lower()` → `godot`.
- **Cache B** keys on `hashFiles('project.godot', 'export_presets.cfg')` plus
  `${{ github.sha }}`, with a prefix `restore-keys` line so a new commit still
  restores the previous import cache. The hash is taken *before*
  `patch_keystore.sh` rewrites the preset, so the key stays stable across runs.
- **Template install** reads `version.txt` out of the `.tpz` and refuses to
  proceed unless it equals `4.4.1.stable`, then verifies
  `android_debug.apk` and `android_release.apk` are present — a corrupted or
  wrong-version cache fails immediately instead of at export time.
- **`aapt2`** is reachable because `$ANDROID_HOME/build-tools/34.0.0` is
  appended to `$GITHUB_PATH`; `verify_apk.sh` also falls back to `aapt` and
  errors clearly if neither exists.
- **Release job** carries `permissions: contents: write`, required by
  `softprops/action-gh-release@v2` to create the GitHub Release. `-rc`/`-beta`
  tags are marked `prerelease`.

## 5. Open items

1. **Create `develop`** (user-run; this session may not push other branches):

   ```bash
   git branch develop main && git push -u origin develop
   ```

   Then open the PR from `arena/8baff0fc-battle-royal-indian-edition` →
   `develop` to trigger `build-debug`.
2. **Confirm the three `KEYSTORE_*` secrets** exist (unverifiable from here —
   HTTP 403). Setup steps are in `docs/KEYSTORE_SETUP.md`.
3. **Criterion 2 (LFS) remains failed** by explicit user decision. It is not
   repaired by this phase.
4. **First real CI run** is the remaining proof: local tests validate the gate
   logic against stubs, not against a Godot-produced APK.
