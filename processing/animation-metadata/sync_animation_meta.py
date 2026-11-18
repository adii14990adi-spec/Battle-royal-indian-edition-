#!/usr/bin/env python3
"""Copy Unity ``.fbx.meta`` files from the animation repository into this project.

The animation authoring repository (``Animation-.meta-``) owns the authoritative
Unity ``ModelImporter`` metadata for the Mixamo FBX library. This project owns
the FBX files themselves. The two must agree by *file name*: every
``animations/<name>.fbx.meta`` in the source repository belongs beside
``unity-project/Assets/Animations/**/<name>.fbx`` here, and nowhere else.

Run it by hand:

    python3 processing/animation-metadata/sync_animation_meta.py \
        --source-dir ../Animation-.meta- --dry-run

It is the only supported way to add animation metadata to this repository. The
GitHub Actions workflow ``.github/workflows/sync-animation-metadata.yml`` runs
the same script on a schedule and opens a pull request instead of pushing.

Guarantees (each one is enforced, not assumed):

* an ``.fbx`` file is only ever read — its bytes are hashed before and after and
  the run fails if any of them changed;
* metadata is only written for an FBX that exists here and matches a source
  ``.fbx.meta`` by exact file name (case included);
* an existing ``.meta`` that this tool did not write is never overwritten;
* an existing ``.meta`` this tool *did* write is only replaced when the source
  file changed since the last sync, and the Unity GUID is preserved so scene and
  prefab references keep resolving;
* every GUID is 32 lowercase hex characters and unique across the whole Unity
  project before anything is written; a collision aborts the run with exit
  code 2 instead of shipping a broken asset database.

Exit codes: 0 success, 1 verification or usage failure, 2 GUID conflict.
"""

from __future__ import annotations

import argparse
import dataclasses
import hashlib
import json
import re
import sys
from pathlib import Path
from typing import Iterable

SCHEMA_VERSION = 1

#: Source repository that owns the authoritative metadata.
DEFAULT_SOURCE_REPOSITORY = "adii14990adi-spec/Animation-.meta-"

#: Folder (relative to a repository root) that holds the FBX + .meta pairs.
DEFAULT_SOURCE_PATH = "animations"
DEFAULT_TARGET_PATH = "unity-project/Assets/Animations"

#: Where the record of "what have we already copied" lives.
DEFAULT_MANIFEST = "processing/animation-metadata/sync-manifest.json"

#: Unity asset GUIDs are exactly 32 lowercase hexadecimal characters.
GUID_RE = re.compile(r"^guid: ([0-9a-f]{32})$", re.MULTILINE)

ADD = "added"
UPDATE = "updated"
UNCHANGED = "unchanged"
ADOPTED = "adopted"
FOREIGN = "skipped-local-metadata"
LOCAL_EDIT = "skipped-local-edit"
NO_SOURCE_CHANGE = "skipped-source-unchanged"
AMBIGUOUS = "failed-ambiguous-fbx"
BAD_GUID = "failed-invalid-guid"
GUID_CLASH = "failed-guid-collision"
ORPHAN = "orphaned-meta"
FBX_CHANGED = "fbx-changed-since-sync"

WRITTEN_ACTIONS = (ADD, UPDATE)
FAILED_ACTIONS = (AMBIGUOUS, BAD_GUID, GUID_CLASH)


# --------------------------------------------------------------------------- #
# small helpers
# --------------------------------------------------------------------------- #
def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def read_text_normalized(path: Path) -> str:
    """Read text as LF-terminated UTF-8 with exactly one trailing newline.

    Unity writes CRLF on Windows and LF on macOS; normalizing here keeps the
    committed blobs stable and makes "did this change?" a byte comparison.
    """
    text = path.read_bytes().replace(b"\r\n", b"\n").replace(b"\r", b"\n").decode("utf-8")
    return text if text.endswith("\n") else text + "\n"


def parse_guid(text: str) -> str | None:
    match = GUID_RE.search(text)
    return match.group(1) if match else None


def replace_guid(text: str, guid: str) -> str:
    return GUID_RE.sub(f"guid: {guid}", text, count=1)


def relative(path: Path, root: Path) -> str:
    try:
        return path.relative_to(root).as_posix()
    except ValueError:
        return path.as_posix()


# --------------------------------------------------------------------------- #
# inputs
# --------------------------------------------------------------------------- #
@dataclasses.dataclass(frozen=True)
class SourceMeta:
    """One ``<name>.fbx.meta`` from the source repository."""

    name: str  # "idle.fbx"
    path: Path
    text: str
    guid: str

    @property
    def sha256(self) -> str:
        return sha256_bytes(self.text.encode("utf-8"))


@dataclasses.dataclass(frozen=True)
class TargetFbx:
    """One ``.fbx`` in this repository, with the hash we promise not to change."""

    name: str
    path: Path
    sha256: str


def collect_source_metas(source_root: Path) -> tuple[list[SourceMeta], list[str]]:
    """Every ``*.fbx.meta`` under the source ``animations/`` folder."""
    metas: list[SourceMeta] = []
    problems: list[str] = []
    for path in sorted(source_root.rglob("*")):
        if not path.is_file() or not path.name.lower().endswith(".fbx.meta"):
            continue
        text = read_text_normalized(path)
        guid = parse_guid(text)
        fbx_name = path.name[: -len(".meta")]  # "idle.fbx"
        if guid is None:
            problems.append(f"{relative(path, source_root)}: no 32-hex `guid:` line")
            continue
        metas.append(SourceMeta(name=fbx_name, path=path, text=text, guid=guid))
    return metas, problems

def collect_target_fbx(target_root: Path) -> dict[str, list[TargetFbx]]:
    """All ``.fbx`` files here, grouped by file name (exact, case-sensitive)."""
    found: dict[str, list[TargetFbx]] = {}
    if not target_root.is_dir():
        return found
    for path in sorted(target_root.rglob("*")):
        if not path.is_file() or not path.name.lower().endswith(".fbx"):
            continue
        fbx = TargetFbx(name=path.name, path=path, sha256=sha256_bytes(path.read_bytes()))
        found.setdefault(fbx.name, []).append(fbx)
    return found


def project_guids(assets_root: Path, skip: Iterable[Path]) -> dict[str, str]:
    """GUID -> asset path for every ``.meta`` in the Unity project."""
    skipped = {path.resolve() for path in skip}
    found: dict[str, str] = {}
    if not assets_root.is_dir():
        return found
    for path in sorted(assets_root.rglob("*.meta")):
        if path.resolve() in skipped or not path.is_file():
            continue
        try:
            guid = parse_guid(path.read_bytes().decode("utf-8", "replace"))
        except OSError:
            continue
        if guid:
            found.setdefault(guid, path.as_posix())
    return found


# --------------------------------------------------------------------------- #
# manifest
# --------------------------------------------------------------------------- #
def load_manifest(path: Path) -> dict:
    if not path.is_file():
        return {"schema_version": SCHEMA_VERSION, "entries": {}}
    data = json.loads(path.read_text(encoding="utf-8"))
    data.setdefault("schema_version", SCHEMA_VERSION)
    data.setdefault("entries", {})
    return data


def dump_manifest(manifest: dict, source_repository: str) -> str:
    """Serialise the manifest deterministically (no timestamps: reruns are no-ops)."""
    sorted_entries = {key: manifest["entries"][key] for key in sorted(manifest["entries"])}
    payload = {
        "schema_version": SCHEMA_VERSION,
        "source_repository": source_repository,
        "source_path": manifest.get("source_path", DEFAULT_SOURCE_PATH),
        "target_path": manifest.get("target_path", DEFAULT_TARGET_PATH),
        "note": (
            "Written by processing/animation-metadata/sync_animation_meta.py. Do not edit by hand: "
            "the hashes are what tell the sync whether the source metadata changed or the local "
            "copy was edited."
        ),
        "entries": sorted_entries,
    }
    return json.dumps(payload, indent=2, sort_keys=False) + "\n"


# --------------------------------------------------------------------------- #
# planning
# --------------------------------------------------------------------------- #
@dataclasses.dataclass
class Plan:
    """What the sync intends to do, computed before a single byte is written."""

    action: str
    fbx_name: str
    target_meta: Path | None
    source: SourceMeta | None = None
    detail: str = ""
    content: str | None = None  # exact text to write, for add/update
    guid: str | None = None
    fbx_path: Path | None = None
    fbx_sha256: str | None = None


def plan_sync(
    repo_root: Path,
    source_root: Path,
    target_root: Path,
    manifest: dict,
    force: bool = False,
) -> tuple[list[Plan], list[str], list[str]]:
    """Decide the action for every source metadata file.

    Returns ``(plans, fatal, notes)``. ``fatal`` holds GUID-level problems that
    must abort the run.

    With ``force`` a file the sync owns is re-applied from the source even when
    the source hash is unchanged, which is how a deliberate re-application of
    importer settings is requested. A ``force`` run still never touches
    metadata it did not create, so a hand-written ``.meta`` stays put.
    """
    entries: dict = manifest["entries"]
    metas, source_problems = collect_source_metas(source_root)
    fbx_by_name = collect_target_fbx(target_root)

    notes: list[str] = list(source_problems)
    fatal: list[str] = []
    plans: list[Plan] = []

    # --- pre-flight: duplicate GUIDs inside the source library -------------
    source_guid_owner: dict[str, SourceMeta] = {}
    for meta in metas:
        if meta.guid in source_guid_owner:
            fatal.append(
                f"duplicate Unity GUID {meta.guid} is used by both "
                f"{source_guid_owner[meta.guid].name} and {meta.name}"
            )
        source_guid_owner.setdefault(meta.guid, meta)

    # --- pre-flight: duplicate GUIDs against assets already in the project --
    target_meta_paths = {fbx.path.with_suffix(fbx.path.suffix + ".meta") for group in fbx_by_name.values() for fbx in group}
    existing_guids = project_guids(repo_root / "unity-project/Assets", skip=target_meta_paths)

    matched_names = {meta.name for meta in metas}
    for meta in metas:
        matches = fbx_by_name.get(meta.name, [])
        if not matches:
            # No FBX with that exact name here: nothing to attach the metadata
            # to. Listed in the report, never an error (the FBX may arrive later).
            plans.append(Plan(ORPHAN, meta.name, None, meta, "no FBX with this exact name"))
            continue
        if len(matches) > 1:
            where = ", ".join(relative(m.path, repo_root) for m in matches)
            plans.append(
                Plan(AMBIGUOUS, meta.name, None, meta, f"FBX name is ambiguous here: {where}")
            )
            continue

        fbx = matches[0]
        target_meta = fbx.path.with_suffix(fbx.path.suffix + ".meta")
        key = relative(target_meta, repo_root)
        record = entries.get(key)
        local_text = read_text_normalized(target_meta) if target_meta.is_file() else None
        local_guid = parse_guid(local_text) if local_text else None

        # The GUID that must be preserved: an existing local asset keeps its
        # identity even when the source importer settings change under it.
        guid = local_guid or meta.guid
        if guid != meta.guid:
            notes.append(
                f"{key}: keeping local GUID {guid} (source says {meta.guid}); "
                "references in scenes and prefabs stay valid"
            )

        # A GUID may only be reused by the asset it already belongs to.
        owner = existing_guids.get(guid)
        if owner is not None:
            fatal.append(f"{meta.name}: GUID {guid} is already used by {owner}")
            plans.append(
                Plan(GUID_CLASH, meta.name, target_meta, meta, f"GUID {guid} already used by {owner}")
            )
            continue

        if local_text is None:
            plans.append(
                Plan(ADD, meta.name, target_meta, meta, "no local .meta", meta.text, guid, fbx.path, fbx.sha256)
            )
            continue

        if local_text == meta.text and guid == meta.guid:
            plans.append(
                Plan(UNCHANGED, meta.name, target_meta, meta, "identical to the source", None, guid, fbx.path, fbx.sha256)
            )
            continue

        if record is None:
            # Someone committed metadata we have no record of. Only adopt it if
            # it is already exactly what the source says; otherwise leave the
            # author's file alone and report it.
            plans.append(
                Plan(
                    FOREIGN,
                    meta.name,
                    target_meta,
                    meta,
                    "local .meta was not created by this sync; left untouched",
                    None,
                    guid,
                    fbx.path,
                    fbx.sha256,
                )
            )
            continue

        if record.get("source_sha256") != meta.sha256:
            # Source changed: take the new importer settings, keep the GUID.
            content = replace_guid(meta.text, guid) if guid != meta.guid else meta.text
            plans.append(
                Plan(UPDATE, meta.name, target_meta, meta, "source metadata changed", content, guid, fbx.path, fbx.sha256)
            )
            continue

        if force:
            content = replace_guid(meta.text, guid) if guid != meta.guid else meta.text
            plans.append(
                Plan(UPDATE, meta.name, target_meta, meta, "re-applied on request", content, guid, fbx.path, fbx.sha256)
            )
            continue

        # Source is unchanged but the local file drifted (a hand edit, or Unity
        # rewrote it on import). Preserve it: local wins until the source moves.
        plans.append(
            Plan(LOCAL_EDIT, meta.name, target_meta, meta, "local file differs; source unchanged", None, guid, fbx.path, fbx.sha256)
        )

    # --- manifest entries whose FBX and .meta both disappeared --------------
    for key, record in sorted(entries.items()):
        meta_path = repo_root / key
        if meta_path.exists():
            continue
        if record.get("fbx_name") and record["fbx_name"] not in matched_names:
            notes.append(f"{key}: orphaned record pruned (no FBX and no .meta)")
            entries.pop(key)
    return plans, fatal, notes


# --------------------------------------------------------------------------- #
# applying
# --------------------------------------------------------------------------- #
def apply_plan(repo_root: Path, plans: list[Plan], manifest: dict, dry_run: bool) -> None:
    entries: dict = manifest["entries"]
    for plan in plans:
        if plan.action in WRITTEN_ACTIONS and plan.target_meta and plan.content is not None:
            if not dry_run:
                plan.target_meta.parent.mkdir(parents=True, exist_ok=True)
                plan.target_meta.write_bytes(plan.content.encode("utf-8"))
        if plan.action in WRITTEN_ACTIONS + (UNCHANGED, ADOPTED, FOREIGN, LOCAL_EDIT) and plan.target_meta:
            key = relative(plan.target_meta, repo_root)
            if plan.action == FOREIGN:
                continue  # never claim a file we did not write
            entries[key] = {
                "fbx_name": plan.fbx_name,
                "source_name": plan.source.path.name if plan.source else plan.fbx_name + ".meta",
                "guid": plan.guid,
                "source_sha256": plan.source.sha256 if plan.source else None,
                "meta_sha256": sha256_bytes(plan.content.encode("utf-8")) if plan.content else None,
                "fbx_sha256": plan.fbx_sha256,
            }
            if plan.action in (UNCHANGED, ADOPTED, LOCAL_EDIT):
                # Record what is actually on disk, which may be a deliberate
                # local adaptation rather than the source text.
                entries[key]["meta_sha256"] = (
                    sha256_bytes(plan.target_meta.read_bytes()) if plan.target_meta.is_file() else None
                )


# --------------------------------------------------------------------------- #
# reporting
# --------------------------------------------------------------------------- #
def build_report(
    repo_root: Path,
    plans: list[Plan],
    fatal: list[str],
    notes: list[str],
    manifest: dict,
    source_repository: str,
    previous_entries: dict | None = None,
) -> dict:
    order = [ADD, UPDATE, UNCHANGED, ADOPTED, FOREIGN, LOCAL_EDIT, ORPHAN, FBX_CHANGED] + list(FAILED_ACTIONS)
    buckets: dict[str, list[dict]] = {action: [] for action in order}
    for plan in plans:
        buckets.setdefault(plan.action, []).append(
            {
                "fbx": plan.fbx_name,
                "meta": relative(plan.target_meta, repo_root) if plan.target_meta else None,
                "detail": plan.detail,
                "guid": plan.guid,
            }
        )

    # FBX files whose bytes changed since the last sync: we never modify them,
    # but a changed model may mean the clip ranges in the metadata are stale.
    # The comparison is against the *previous* manifest, because this run has
    # already recorded the hashes it observed.
    entries: dict = manifest["entries"] if previous_entries is None else previous_entries
    for key, record in entries.items():
        fbx_name = record.get("fbx_name")
        recorded = record.get("fbx_sha256")
        if not fbx_name or not recorded:
            continue
        for group in collect_target_fbx(repo_root / DEFAULT_TARGET_PATH).values():
            for fbx in group:
                if fbx.name == fbx_name and fbx.sha256 != recorded:
                    buckets.setdefault(FBX_CHANGED, []).append(
                        {"fbx": fbx_name, "meta": key, "detail": "model bytes changed since the last sync", "guid": None}
                    )

    # Source-side totals, so the report says how much was examined, not only
    # what moved.
    source_names = {plan.fbx_name for plan in plans}
    target_fbx = [fbx for group in collect_target_fbx(repo_root / DEFAULT_TARGET_PATH).values() for fbx in group]
    unmatched = sorted(fbx.name for fbx in target_fbx if fbx.name not in source_names)

    counts = {action: len(items) for action, items in buckets.items() if items}
    # A blocking problem and the file that caused it are both real; report the
    # larger of the two so the number is never lower than the visible failures.
    failed = max(len(fatal), sum(counts.get(action, 0) for action in FAILED_ACTIONS))
    return {
        "schema_version": SCHEMA_VERSION,
        "source_repository": source_repository,
        "changed": {action: buckets.get(action, []) for action in WRITTEN_ACTIONS},
        "counts": {
            "added": counts.get(ADD, 0),
            "updated": counts.get(UPDATE, 0),
            "unchanged": counts.get(UNCHANGED, 0),
            "skipped": sum(counts.get(action, 0) for action in (FOREIGN, LOCAL_EDIT, ADOPTED, ORPHAN)),
            "failed": failed,
            "fbx_changed": counts.get(FBX_CHANGED, 0),
        },
        "summary": {
            "source_metadata": len(plans),
            "matched": len(plans) - len(buckets.get(ORPHAN, [])) - len(buckets.get(AMBIGUOUS, [])),
            "unmatched_fbx": len(unmatched),
            "unmatched_fbx_names": unmatched,
        },
        "changes": bool(counts.get(ADD) or counts.get(UPDATE)),
        "fatal": fatal,
        "notes": notes,
        "files": {action: items for action, items in buckets.items() if items},
    }


def render_summary(report: dict) -> str:
    counts = report["counts"]
    summary = report.get("summary", {})
    lines = [
        "## Animation metadata sync",
        "",
        f"Source: `{report['source_repository']}` — {summary.get('source_metadata', 0)} metadata "
        f"file(s) scanned, {summary.get('matched', 0)} matched a local FBX by exact file name, "
        f"{summary.get('unmatched_fbx', 0)} FBX left without metadata.",
        "",
        "| Result | Files |",
        "| --- | ---: |",
        f"| Added | {counts['added']} |",
        f"| Updated | {counts['updated']} |",
        f"| Unchanged | {counts['unchanged']} |",
        f"| Skipped | {counts['skipped']} |",
        f"| Failed | {counts['failed']} |",
        f"| FBX changed since sync (warning) | {counts['fbx_changed']} |",
        "",
    ]
    if report["fatal"]:
        lines += ["### Blocking problems", ""] + [f"* {item}" for item in report["fatal"]] + [""]

    for action, title in (
        (ADD, "Added"),
        (UPDATE, "Updated"),
        (FOREIGN, "Skipped — local metadata not created by the sync"),
        (LOCAL_EDIT, "Skipped — local file edited, source unchanged"),
        (ORPHAN, "Skipped — no matching FBX"),
        (FBX_CHANGED, "Warning — FBX changed since the last sync"),
    ):
        items = report["files"].get(action)
        if not items:
            continue
        lines += [f"<details><summary>{title} ({len(items)})</summary>", ""]
        lines += [f"* `{item['fbx']}` — {item['detail']}" for item in items]
        lines += ["", "</details>", ""]

    for action in FAILED_ACTIONS:
        items = report["files"].get(action)
        if items:
            lines += [f"<details><summary>Failed ({len(items)})</summary>", ""]
            lines += [f"* `{item['fbx']}` — {item['detail']}" for item in items]
            lines += ["", "</details>", ""]

    if summary.get("unmatched_fbx_names"):
        lines += [
            f"<details><summary>FBX files with no source metadata ({len(summary['unmatched_fbx_names'])}) "
            "— left untouched</summary>",
            "",
        ]
        lines += [f"* `{name}`" for name in summary["unmatched_fbx_names"]]
        lines += ["", "</details>", ""]

    if report["notes"]:
        lines += ["<details><summary>Notes</summary>", ""]
        lines += [f"* {note}" for note in report["notes"]]
        lines += ["", "</details>", ""]

    if not report["changes"]:
        lines += ["No metadata changes: the project is in sync with the source.", ""]
    return "\n".join(lines)


# --------------------------------------------------------------------------- #
# verification
# --------------------------------------------------------------------------- #
def verify(
    repo_root: Path,
    source_root: Path,
    target_root: Path,
    manifest: dict,
    expected_count: int | None,
) -> tuple[bool, list[str]]:
    """Check the committed state, without writing anything.

    Fails when a source ``.fbx.meta`` that has a matching FBX here is missing, is
    attached to the wrong file, has a GUID that disagrees with the manifest, or
    when any GUID in the Unity project is duplicated. A changed FBX is reported
    as a warning only: the sync never writes to ``.fbx`` files.

    Metadata that exists here but that this tool did not write (no manifest
    entry) is skipped, exactly like the sync skips it — it is somebody's
    deliberate work, not an error.
    """
    problems: list[str] = []
    warnings: list[str] = []

    metas, source_problems = collect_source_metas(source_root)
    warnings += source_problems
    fbx_by_name = collect_target_fbx(target_root)
    entries: dict = manifest["entries"]

    present = 0
    for meta in metas:
        matches = fbx_by_name.get(meta.name, [])
        if not matches:
            continue  # reported by plan_sync/notes; not a metadata error
        target_meta_hint = matches[0].path.with_suffix(matches[0].path.suffix + ".meta")
        record = entries.get(relative(target_meta_hint, repo_root))
        if len(matches) > 1:
            # Only an error if this sync is responsible for the file. Two
            # unrelated FBX files that happen to share a name are the project's
            # business, not the metadata sync's.
            if record is not None:
                problems.append(f"{meta.name}: {len(matches)} FBX files share this exact name")
            continue
        fbx = matches[0]
        target_meta = fbx.path.with_suffix(fbx.path.suffix + ".meta")
        key = relative(target_meta, repo_root)
        if not target_meta.is_file():
            if record is None:
                warnings.append(f"{key}: not synced yet (will be added on the next sync)")
            else:
                problems.append(f"{key}: missing (no metadata beside {relative(fbx.path, repo_root)})")
            continue
        present += 1
        text = read_text_normalized(target_meta)
        guid = parse_guid(text)
        if guid is None:
            problems.append(f"{key}: no 32-hex `guid:` line")
            continue
        if record is None:
            warnings.append(f"{key}: local metadata not created by the sync; left alone")
            continue
        if record.get("guid") != guid:
            problems.append(
                f"{key}: GUID {guid} does not match the manifest ({record.get('guid')}) — "
                "scene references would break"
            )
        recorded_fbx = record.get("fbx_sha256")
        if recorded_fbx and recorded_fbx != fbx.sha256:
            warnings.append(
                f"{relative(fbx.path, repo_root)}: FBX bytes changed since the last sync — "
                "re-check the animation in Unity"
            )

    if expected_count is not None and present != expected_count:
        problems.append(f"expected {expected_count} synced metadata files, found {present}")

    # GUID uniqueness across every asset in the project.
    seen: dict[str, str] = {}
    for path in sorted((repo_root / "unity-project/Assets").rglob("*.meta")):
        if not path.is_file():
            continue
        guid = parse_guid(path.read_bytes().decode("utf-8", "replace"))
        if not guid:
            problems.append(f"{relative(path, repo_root)}: no 32-hex `guid:` line")
            continue
        if guid in seen:
            problems.append(f"duplicate GUID {guid}: {seen[guid]} and {relative(path, repo_root)}")
        seen.setdefault(guid, relative(path, repo_root))

    for warning in warnings:
        print(f"  WARN  {warning}")
    for problem in problems:
        print(f"  FAIL  {problem}")
    print(f"  INFO  {present} synced metadata file(s); {len(seen)} unique GUID(s) in Assets/")
    return not problems, problems


# --------------------------------------------------------------------------- #
# entry point
# --------------------------------------------------------------------------- #
def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Sync Unity .fbx.meta files from the animation repository into unity-project/Assets/Animations.",
    )
    parser.add_argument("--repo-root", type=Path, default=Path(__file__).resolve().parents[2],
                        help="root of this repository (default: two levels above this script)")
    parser.add_argument("--source-dir", type=Path, required=True,
                        help="checkout of the animation repository that holds animations/*.fbx.meta")
    parser.add_argument("--source-repository", default=DEFAULT_SOURCE_REPOSITORY,
                        help="owner/name recorded in the manifest and reports")
    parser.add_argument("--source-path", default=DEFAULT_SOURCE_PATH, help="source folder holding the .fbx.meta files")
    parser.add_argument("--target-path", default=DEFAULT_TARGET_PATH, help="target folder holding the .fbx files")
    parser.add_argument("--manifest", default=DEFAULT_MANIFEST, help="manifest path, relative to the repository root")
    parser.add_argument("--report", type=Path, default=None, help="write a JSON report here")
    parser.add_argument("--summary", type=Path, default=None, help="write a Markdown report here")
    parser.add_argument("--dry-run", action="store_true", help="report only; never write metadata or the manifest")
    parser.add_argument("--force", action="store_true",
                        help="re-apply source metadata to files this sync owns even when the source is unchanged")
    parser.add_argument("--verify-only", action="store_true", help="check the committed state and exit")
    parser.add_argument("--expect-count", type=int, default=None, help="fail unless exactly N metadata files are synced")
    parser.add_argument("--fail-on-changes", action="store_true", help="exit 1 when the sync would change files")
    return parser.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    args = parse_args(argv)
    repo_root: Path = args.repo_root.resolve()
    source_root = (args.source_dir / args.source_path).resolve()
    target_root = (repo_root / args.target_path).resolve()
    manifest_path = repo_root / args.manifest

    if not source_root.is_dir():
        print(f"::error::source folder not found: {source_root}")
        return 1
    if not target_root.is_dir():
        print(f"::error::target folder not found: {target_root}")
        return 1

    fbx_before = {path: sha256_bytes(path.read_bytes()) for path in target_root.rglob("*.fbx") if path.is_file()}
    manifest = load_manifest(manifest_path)
    manifest["source_path"] = args.source_path
    manifest["target_path"] = args.target_path
    previous_entries = dict(manifest["entries"])

    if args.verify_only:
        ok, _problems = verify(repo_root, source_root, target_root, manifest, args.expect_count)
        print("\nVerification passed." if ok else "\nVerification failed.")
        return 0 if ok else 1

    plans, fatal, notes = plan_sync(repo_root, source_root, target_root, manifest, force=args.force)

    # --- GUID pre-flight: nothing is written when this fails ---------------
    if fatal:
        for problem in fatal:
            print(f"::error::duplicate Unity GUID — {problem}")
        report = build_report(repo_root, plans, fatal, notes, manifest,
                              args.source_repository, previous_entries)
        if args.report:
            args.report.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
        if args.summary:
            args.summary.write_text(render_summary(report), encoding="utf-8")
        print("Aborted before writing: resolve the duplicate GUIDs first.")
        return 2

    apply_plan(repo_root, plans, manifest, args.dry_run)

    # --- the model files must be byte-identical ---------------------------
    fbx_after = {path: sha256_bytes(path.read_bytes()) for path in target_root.rglob("*.fbx") if path.is_file()}
    if fbx_before != fbx_after:
        print("::error::an .fbx file changed during the sync — refusing to continue")
        return 1

    if not args.dry_run:
        manifest_text = dump_manifest(manifest, args.source_repository)
        if not manifest_path.is_file() or manifest_path.read_text(encoding="utf-8") != manifest_text:
            manifest_path.parent.mkdir(parents=True, exist_ok=True)
            manifest_path.write_text(manifest_text, encoding="utf-8")

    report = build_report(repo_root, plans, fatal, notes, manifest,
                          args.source_repository, previous_entries)
    summary = render_summary(report)
    if args.report:
        args.report.parent.mkdir(parents=True, exist_ok=True)
        args.report.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    if args.summary:
        args.summary.parent.mkdir(parents=True, exist_ok=True)
        args.summary.write_text(summary, encoding="utf-8")

    counts = report["counts"]
    print(
        f"added {counts['added']}, updated {counts['updated']}, unchanged {counts['unchanged']}, "
        f"skipped {counts['skipped']}, failed {counts['failed']}, "
        f"fbx changed {counts['fbx_changed']}"
    )

    # Verification of the resulting state, so a report can never claim success
    # for a file that is not actually on disk and unique.
    if not args.dry_run:
        ok, _ = verify(repo_root, source_root, target_root, manifest, args.expect_count)
        if not ok:
            return 1

    if args.fail_on_changes and report["changes"]:
        print("Changes detected (--fail-on-changes).")
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
