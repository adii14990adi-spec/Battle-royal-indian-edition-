# Animation metadata sync

The Mixamo animation FBX library lives here, in
`unity-project/Assets/Animations/`, and is split into `Combat/`, `Crouch/`,
`Death/`, `Movement/` and `Weapon/`. The Unity `ModelImporter` settings for those
files are authored **somewhere else**: in the public companion repository
[`adii14990adi-spec/Animation-.meta-`](https://github.com/adii14990adi-spec/Animation-.meta-),
which owns `animations/<name>.fbx.meta`.

This page explains how those `.meta` files travel into this project, what the
automation refuses to do, and how to run it by hand.

---

## 1. The contract

| | |
| --- | --- |
| **Source of truth** | `adii14990adi-spec/Animation-.meta-`, folder `animations/` |
| **Destination** | `unity-project/Assets/Animations/**` |
| **Matched by** | exact FBX file name (case included), anywhere below the destination |
| **Written by the sync** | `<name>.fbx.meta` and `processing/animation-metadata/sync-manifest.json` |
| **Never touched** | every `.fbx`, every `.meta` the sync did not create, and every other Unity asset |

One source file can only be applied to one destination file. If two FBX files in
this project share a name, the sync reports the ambiguity and writes nothing for
that name rather than guessing.

FBX files with no matching `.fbx.meta` in the source (today: the two character
models under `Assets/Characters/`) are left exactly as they are.

---

## 2. What the sync guarantees

Each of these is enforced by `processing/animation-metadata/sync_animation_meta.py`
and covered by a test in `processing/animation-metadata/tests/`:

1. **Models are read-only.** Every `.fbx` is hashed before and after the run; a
   single changed byte fails the run. The workflow additionally fails if `git`
   reports any `.fbx` as added, modified, renamed or deleted.
2. **GUIDs are preserved.** When the source importer settings change, the GUID
   already committed in this project is kept and only the rest of the file is
   replaced, so scenes, prefabs and animator controllers keep resolving.
3. **GUIDs are unique.** Every GUID under `unity-project/Assets/` is checked
   before anything is written, and again afterwards. A collision aborts the run
   with exit code 2 and no partial write.
4. **Local work is respected.** A `.meta` that did not come from the sync (no
   manifest entry) is reported and skipped. A `.meta` the sync owns is only
   replaced when the source file actually changed; a hand edit is preserved
   while the source is unchanged.
5. **Runs are idempotent.** With nothing to do, the manifest and the metadata are
   byte-identical, so no commit is made and no pull request is opened.
6. **Line endings are normalised** to LF and UTF-8, so a Windows checkout of the
   source cannot produce a spurious diff.

---

## 3. The automation

`.github/workflows/sync-animation-metadata.yml`

* **Triggers** — `workflow_dispatch` and a daily `schedule` (`17 5 * * *`).
* **Source access** — anonymous shallow clone of the public source repository.
  The job never holds a token that could write there.
* **Token** — the target repository's `GITHUB_TOKEN`, with
  `contents: write` and `pull-requests: write` and nothing else. `main` is never
  pushed to; changes always land on `chore/sync-animation-metadata`.
* **Flow** — unit tests → pre-flight plan + GUID check (dry run) → apply →
  verify → model-touch check → stage → commit → open or refresh the PR.
* **Duplicate PRs** — the workflow looks for an open PR whose head is the sync
  branch and updates it. It pushes fast-forward only, so an existing PR is
  appended to, never rewritten.
* **Reports** — the job summary and the PR body list added, updated, unchanged,
  skipped and failed files; the JSON report is uploaded as a workflow artifact.

### Manual runs

| Input | Effect |
| --- | --- |
| *(none)* | Normal sync: add missing metadata, update changed metadata, open a PR if anything moved. |
| `force: true` | Re-apply the source importer settings to the files the sync owns, even when the source file is unchanged. Hand-written `.meta` files are still skipped. |
| `check_only: true` | Verify the committed state only (presence, GUID match, GUID uniqueness). Writes nothing and opens no pull request. |

```bash
gh workflow run sync-animation-metadata.yml \
  --repo adii14990adi-spec/Battle-royal-indian-edition-
```

---

## 4. Running it locally

```bash
git clone --depth 1 https://github.com/adii14990adi-spec/Animation-.meta- /tmp/animation-meta

# See what would happen, write nothing
python3 processing/animation-metadata/sync_animation_meta.py \
  --source-dir /tmp/animation-meta --dry-run --expect-count 74

# Apply it
python3 processing/animation-metadata/sync_animation_meta.py \
  --source-dir /tmp/animation-meta --expect-count 74

# Check the committed state (this is what CI runs)
python3 processing/animation-metadata/sync_animation_meta.py \
  --source-dir /tmp/animation-meta --verify-only --expect-count 74

# The tests need neither Unity nor the network
python3 -m unittest discover -s processing/animation-metadata/tests -v
```

Exit codes: `0` success, `1` verification or usage failure, `2` duplicate GUID.

---

## 5. The manifest

`processing/animation-metadata/sync-manifest.json` records, per synced file:

| Field | Meaning |
| --- | --- |
| `fbx_name` | the FBX this metadata belongs to |
| `guid` | the GUID committed in this project |
| `source_sha256` | hash of the source `.fbx.meta` when it was last applied |
| `meta_sha256` | hash of the file on disk after that run |
| `fbx_sha256` | hash of the model at that time (a change is reported, never "fixed") |

Do not edit it by hand — the hashes are the only way the sync can tell "the
source changed" from "someone edited this locally", and reviewing a manifest diff
is how you see which source files moved.

---

## 6. Why the project previously had no `.meta` files

The Unity side enforced the Mixamo import settings in code instead
(`Assets/Editor/MixamoAnimationImportRules.cs`, `MixamoImportSettings`,
`ProjectValidator`), because a `.meta` that is not committed is regenerated on
every clone. Committing the authoritative metadata is the durable fix; the code
rules stay in place and are the reason a mismatch is still caught:

* `MixamoAnimationImportRules.OnPreprocessModel` sets Rig = Humanoid on import.
  The synced metadata also records `animationType: 3` (Humanoid) with
  `avatarSetup: 1` (create from this model), so both agree, and Unity's importer
  rule is what runs when a human edits the file.
* `MixamoImportSettings.ApplyToClip` enforces Loop Time and baked root motion at
  import time. Note that the source metadata expresses "bake into pose" with
  `keepOriginal*: 1` and leaves the `lockRoot*` keys at Unity's default, whereas
  the audit compares `lockRoot*`; `MixamoAnimationImportService.Repair` closes
  that gap by writing the explicit keys on the next editor import. That repair
  writes the flags the audit checks and is idempotent, so the audit gate stays
  green and no clip loops or root-motion behaviour changes.

`docs/CHARACTER_ANIMATION_PIPELINE.md` keeps its "no `.meta` files are
committed" wording for the generated assets under `Assets/Generated/`; the
Mixamo library is now the one exception and is covered by this page.
