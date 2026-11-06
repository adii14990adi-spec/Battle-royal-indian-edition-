#!/usr/bin/env bash
# Bake Siran Island (Phase 3) and prove the result.
#
#   bash tools/bake_siran_island.sh                  # 10 m samples, 500 m chunks
#   bash tools/bake_siran_island.sh --spacing=5      # denser terrain, bigger repo
#   GODOT_BIN=/opt/godot bash tools/bake_siran_island.sh
#
# Steps:
#   1. import the project headlessly (creates .godot/ + the .import files for
#      any new asset — the water normal map, for instance)
#   2. run the baker, which writes scenes/world/terrain/* and resources/world/*
#   3. run the Phase 3 self-check with --require-bake, so a broken bake fails here
#
# Exit codes: 0 ok, 1 bake/self-check failed, 2 no Godot binary found.

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
    printf 'Godot 4.4+ is required: the bake runs FastNoiseLite and the Jolt physics\n' >&2
    printf 'engine inside the editor, and nothing in this repo pretends to replace it.\n' >&2
    exit 2
  }
fi

printf '\033[1m[bake] Godot:\033[0m %s\n' "$GODOT"
"$GODOT" --version || exit 2

printf '\n\033[1m[bake] 1/3 importing project…\033[0m\n'
"$GODOT" --headless --path . --import || exit 1

printf '\n\033[1m[bake] 2/3 baking terrain…\033[0m\n'
"$GODOT" --headless --path . --script res://tools/bake/bake_siran_island.gd -- "$@" || exit 1

printf '\n\033[1m[bake] 3/3 self-check…\033[0m\n'
"$GODOT" --headless --path . scenes/tests/phase3_selfcheck.tscn -- --require-bake || exit 1

printf '\n\033[32m[bake] Siran Island baked and verified.\033[0m\n'
printf 'Commit these together:\n'
printf '  scenes/world/terrain/*.tscn\n'
printf '  resources/world/terrain/*.res\n'
printf '  resources/world/baked/*\n'
printf '  docs/images/siran_island_preview.png\n'
printf '  assets/textures/water_normal_256.png.import  (first run only)\n'
