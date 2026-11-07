#!/usr/bin/env bash
# Generate Siran Island (Phase 3) and prove the result — on a dev machine.
#
#   bash tools/generate_siran_island.sh                  # 10 m samples, 500 m chunks
#   bash tools/generate_siran_island.sh --spacing=5      # denser terrain, bigger scene
#   GODOT_BIN=/opt/godot bash tools/generate_siran_island.sh
#
# This is **not** part of the APK build. GitHub Actions used to bake the map and
# ship the result; that failed (the freshly written files were not always in the
# pack), so the map is generated once, committed, and the APK build only packs
# what is already in Git. Run this when the terrain math changes, then commit
# scenes/world/siran_island_baked.tscn with resources/world/baked/.
#
# Steps:
#   1. import the project headlessly (creates .godot/ + .import files for any new
#      asset — the water normal map, for instance)
#   2. run TerrainGenerator, which writes the single baked scene + river + summary
#   3. check the result structurally, then run the Phase 3 self-check with
#      --require-bake, so a broken map fails here
#
# Exit codes: 0 ok, 1 generation/self-check failed, 2 no Godot binary found.

set -uo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/.." || exit 2

find_godot() { # $1 = candidate list
  for candidate in $1; do
    if command -v "$candidate" >/dev/null 2>&1; then
      command -v "$candidate"
      return 0
    fi
  done
  return 1
}

if [ -n "${GODOT_BIN:-}" ]; then
  GODOT="$GODOT_BIN"
else
  GODOT="$(find_godot "godot godot4 godot4.4 Godot")" || {
    printf '\033[31mNo Godot binary found.\033[0m\n' >&2
    printf 'Set GODOT_BIN, e.g. GODOT_BIN=~/Godot_v4.4-stable_linux.x86_64 bash %s\n' "$0" >&2
    printf 'Godot 4.4+ is required: the generator runs FastNoiseLite and SurfaceTool.\n' >&2
    exit 2
  }
fi

printf '\033[1m[gen] Godot:\033[0m %s\n' "$GODOT"
"$GODOT" --version || exit 2

printf '\n\033[1m[gen] 1/4 importing project…\033[0m\n'
"$GODOT" --headless --path . --import || exit 1

printf '\n\033[1m[gen] 2/4 generating scenes/world/siran_island_baked.tscn…\033[0m\n'
"$GODOT" --headless --path . --script res://tools/bake/generate_siran_island.gd -- "$@" || exit 1

printf '\n\033[1m[gen] 3/4 structural check…\033[0m\n'
"$GODOT" --headless --path . --script res://tools/check_baked_scene.gd || exit 1

printf '\n\033[1m[gen] 4/4 self-check…\033[0m\n'
"$GODOT" --headless --path . scenes/tests/phase3_selfcheck.tscn -- --require-bake || exit 1

printf '\n\033[32m[gen] Siran Island generated and verified.\033[0m\n'
printf 'Commit these together:\n'
printf '  scenes/world/siran_island_baked.tscn\n'
printf '  resources/world/baked/*\n'
printf '  docs/images/siran_island_preview.png\n'
