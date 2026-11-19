#!/usr/bin/env python3
"""Regenerate fbx-baseline.json from the FBX files currently in the worktree.

Run only after a deliberate, reviewed FBX addition. Never run it to paper over
an unexplained change to an existing source file.
"""
from __future__ import annotations
import hashlib, json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = Path(__file__).resolve().parent / "fbx-baseline.json"

def main() -> int:
    files = {}
    for path in sorted((ROOT / "unity-project").rglob("*.fbx")):
        files[path.relative_to(ROOT).as_posix()] = {
            "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
            "bytes": path.stat().st_size,
        }
    OUT.write_text(json.dumps({
        "generated_from": "current worktree",
        "note": "Immutable source hashes. Adding an FBX is allowed; changing or deleting one fails static validation. Regenerate with processing/unity/make_fbx_baseline.py.",
        "files": files,
    }, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(f"wrote {len(files)} FBX hashes to {OUT.relative_to(ROOT)}")
    return 0

if __name__ == "__main__":
    raise SystemExit(main())
