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

## 6. Phase tags

Each phase ends with an annotated tag on `main`:

```bash
git tag -a v0.2.0-ci -m "Phase 2: GitHub Actions CI"
git push origin v0.2.0-ci
```

Versioning is SemVer-ish per docs: `v<phase>` tags (`v0.0.1-setup`,
`v0.2.0-ci`, ...), each with a one-line annotated message.
