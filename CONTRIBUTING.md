# Contributing to Desi Royale

Offline-only, GLES3-only, 30 FPS floor. Read `docs/UNISOC_BUDGET.md` before you
add anything that renders, streams or allocates.

## 1. One-time machine setup

```bash
git lfs install                       # mandatory — assets are LFS-tracked
git config core.autocrlf false        # .gitattributes enforces LF
```

Godot **4.4 or newer** with Android export templates installed
(`Editor > Manage Export Templates`).

## 2. Branch strategy

| Branch | Purpose | Lifetime |
| --- | --- | --- |
| `main` | Always buildable, always tagged per phase. No direct pushes. | permanent |
| `develop` | Integration branch for phase work. | permanent |
| `feature/<phase>-<topic>` | One branch per feature, e.g. `feature/p2-ci-workflow`. | until merged |
| `release/<version>` | Cut from `develop`, merged to `main` at phase end. | per phase |

Rules:

- Branch features off `develop`: `git switch -c feature/p2-ci-workflow develop`.
- **Merge policy:** squash merge into `develop`; merge commit into `main`.
- Pull requests are required for both `develop` and `main`.
- `main` is protected on GitHub: no direct pushes, required review, and (once
  Phase 2 lands) required CI status checks.
- Never rewrite history on `develop` or `main`; never force-push either.

If `develop` does not exist yet on a fresh clone:

```bash
git switch -c develop main && git push -u origin develop
```

## 3. Commits

Conventional commits, imperative mood, scoped by area:

```
feat(p2): add GitHub Actions APK build
fix(perf): clamp zone particle count on GLES3
chore(setup): bump export preset version code
docs(budget): record T606 shadow cost measurements
```

Keep commits buildable. Do not commit generated artefacts — `.gitignore`
already excludes `.godot/`, `build/`, `export/`, `*.apk`, `*.aab`, `*.pck`.

## 4. Binary assets and Git LFS

`.gitattributes` routes these through LFS: `.png .jpg .glb .ogg .wav .exr`.
Text formats (`.tscn`, `.tres`, `.gd`, `.import`) stay in normal Git so diffs
remain reviewable — never LFS-track them.

```bash
git lfs install
git lfs track "*.png"        # already configured; only needed for new types
git add .gitattributes       # commit attribute changes on their own
```

Verify before pushing:

```bash
git lfs status
git lfs ls-files
```

### Never commit

- `.godot/` (import cache, can exceed 500 MB)
- `build/`, `export/`, `*.apk`, `*.aab`, `*.pck`
- `export_credentials.cfg` — **contains keystore paths and passwords.** The
  Phase 1 `.gitignore` follows the spec verbatim and does not list it; treat this
  as a hard rule and keep the file out of Git by hand.
- Debug/production keystores. The debug keystore lives outside the repo
  (`~/.android/desi-royale-debug.keystore`).

## 5. Before you open a PR

```bash
bash tools/verify_phase1.sh          # repo hygiene + preset/config invariants
godot --headless --path . --quit     # project must import and scripts must parse
```

PR checklist:

- [ ] No `.godot/`, `build/` or export artefacts staged.
- [ ] Renderer still `gl_compatibility`; no Vulkan/Forward+ feature crept in.
- [ ] No network API used (`HTTPRequest`, `HTTPClient`, `StreamPeerTCP`,
      `WebSocketPeer`). The APK has no INTERNET permission, so anything networked
      fails on device — and it violates the product constraint.
- [ ] New textures ≤512×512, VRAM-compressed (ASTC/ETC2) and imported with
      mipmaps; new SFX 22050 Hz mono `.ogg`. See `docs/ASSET_PIPELINE.md`.
- [ ] Budget impact noted in the PR description (PSS, VRAM, draw calls).
- [ ] Device smoke test on a Unisoc-class phone, or an explicit note that only
      the desktop GLES3 run was done.
- [ ] CI is green: `build-debug` passed and all four APK gates held (see §7).

## 6. Phase tags

Each phase ends with an annotated tag on `main`:

```bash
git tag -a v0.2.0-ci -m "Phase 2: GitHub Actions CI"
git push origin v0.2.0-ci
```

Versioning is SemVer-ish per docs: `v<phase>` tags (`v0.0.1-setup`,
`v0.2.0-ci`, ...), each with a one-line annotated message.

## 7. Phase 2 — CI/CD and required status checks

One workflow, [`.github/workflows/build.yml`](.github/workflows/build.yml):

| Job | Trigger | Gate |
| --- | --- | --- |
| `build-debug` | push to `develop`, PR to `develop`/`main` | required before merge |
| `build-release` | tag `v*` | creates the GitHub Release |

### Required status check

Mark **`build-debug`** as a required status check on `develop` (and on `main`,
if you protect it too). Repository settings → **Branches → Branch protection
rule** → **Require status checks to pass before merging** → add `build-debug`.

Do **not** add `build-release` to the required list. It only runs on tag
pushes, so it never reports on a pull request — requiring it would leave every
PR stuck waiting for a check that will never arrive.

> Creating a branch-protection rule needs admin access and cannot be done from
> a workflow, so this step is manual.

### What the job enforces

`build-debug` runs `tools/verify_apk.sh` against the exported APK and fails the
build unless all four gates hold:

- **A** — no `android.permission.INTERNET` (the game is fully offline)
- **B** — `android.permission.WAKE_LOCK` present (empty permission list ⇒
  manifest generation silently failed)
- **C** — size ≤ 120 MB
- **D** — `arm64-v8a` present; `armeabi-v7a` missing is a warning only

It also runs `tools/patch_keystore.sh`, which hard-fails with a `::error::`
annotation if `KEYSTORE_BASE64`, `KEYSTORE_USER` or `KEYSTORE_PASSWORD` is
missing, not valid base64, or decodes to an empty file. There is deliberately
no fallback to an unsigned or debug-signed release build.

### Rules for CI changes

- **One workflow file.** Do not split into `build-debug.yml` /
  `build-release.yml` — both jobs share setup logic and belong together.
- **No retries** on import or export steps. A flaky Godot import is a bug worth
  seeing, not papering over; retrying hides it.
- **Fail loud.** Every failure path emits `::error::` so it shows as an
  annotation on the PR rather than as an anonymous red cross.
- **Never** commit `release.keystore`, `release.keystore.b64`, `*.keystore` or
  `*.jks`. They are gitignored; signing material lives only in GitHub Secrets
  (see `docs/KEYSTORE_SETUP.md`).
- **Keep `export_presets.cfg` keystore fields blank** in Git. They are patched
  at build time and must not carry real paths or passwords.
- Changing `GODOT_VERSION` requires a new cache key
  (`godot-<version>-templates-v1`), otherwise a stale template set is restored.

### Local equivalents

```bash
bash tools/patch_keystore.sh   # needs KEYSTORE_* env vars; safe to run — it only rewrites export_presets.cfg
bash tools/verify_apk.sh build/desi-royale-debug.apk
bash tools/verify_phase1.sh    # unchanged Phase 1 gates
```
