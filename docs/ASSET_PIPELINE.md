# Asset pipeline (textures, models, audio, LFS)

The import rules here are part of the Unisoc budget (`docs/UNISOC_BUDGET.md`).
An asset that violates them will be rejected in review, because fixing it
afterwards means re-importing and re-testing everything that uses it.

## 1. Where things go

```
assets/models/<biome>/     .glb only (village, urban, heritage, port)
assets/textures/           .png sources, ≤512×512, atlas sources included
assets/audio/music/        .ogg, streamed (never preloaded)
assets/audio/sfx/          .ogg, 22050 Hz, mono
assets/audio/voice/        .ogg, Hindi + regional VO packs
assets/shaders/            .gdshader, unshaded / vertex-lit only
assets/fonts/              Devanagari + Latin UI fonts (.ttf/.otf)
resources/config/          .tres tunables (bot difficulty, zone phases)
resources/loot_tables/     .tres weighted rarity tables
resources/themes/          .tres UI themes
```

Naming: `snake_case`, biome-prefixed for models and textures
(`village_hut_a.glb`, `village_hut_a_albedo.png`). No spaces, no capitals, no
version suffixes — Git is the version control.

## 2. Textures

| Rule | Value |
| --- | --- |
| Max size | 512 × 512 (a single 1024² texture costs as much VRAM as four 512²) |
| Dimensions | power of two |
| Format on disk | `.png` (LFS-tracked) |
| Format on device | ASTC (ETC2 fallback) — enforced by `rendering/textures/vram_compression/import_etc2_astc=true` |
| Import mode | `VRAM Compressed` (`compress/mode=2`) |
| Mipmaps | on |

Per-texture import settings live in the committed `.png.import` file — **commit
those**, they are the contract. Global ASTC/ETC2 switch:

```
project.godot → [rendering] textures/vram_compression/import_etc2_astc=true
```

It must be `true` *before* the first import, otherwise textures are silently
imported as RGBA8 and the APK ships 4× the VRAM. After changing it:

```bash
godot --headless --path . --import      # re-build the .godot/imported cache
```

## 3. Models

- `.glb` only (glTF binary, LFS-tracked). No `.fbx`, no `.blend` in the repo.
- One material per model where possible; several meshes sharing one atlas.
- Static level geometry: merge into as few meshes as possible — the draw-call
  budget is 60 per frame.
- Vertex budget: ≤ 8k vertices per prop, ≤ 40k per character.
- Naming mirrors the biome folder it lives in.

## 4. Audio

| Type | Rules |
| --- | --- |
| SFX | `.ogg` (Vorbis), 22050 Hz, mono, ≤ 120 KB each |
| Music | `.ogg`, stereo allowed, imported as *streamed* (`loop` on, no preload) |
| Voice | `.ogg` mono, one folder per language pack (`voice/hi/`, `voice/ta/`, …) |

Never `preload()` music — it loads the entire track into RAM. Use
`AudioStreamPlayer` with the streamed import mode.

## 5. Git LFS

`.gitattributes` already routes binary types through LFS:

```
*.png  *.jpg  *.glb  *.ogg  *.wav  *.exr
```

`.tscn`, `.tres`, `.gd`, `.import`, `.cfg`, `.godot` stay in normal Git so diffs
stay reviewable. Never LFS-track them.

```bash
git lfs install          # once per clone, before touching assets
git lfs status           # what will be stored
git lfs ls-files         # what is already stored
```

If a binary slipped in as a normal blob (e.g. it was added on a machine without
`git lfs install`), renormalise it without rewriting history:

```bash
git lfs install
git add --renormalize assets/textures/test_pattern_256.png
git commit -m "chore(lfs): store boot test texture in LFS"
```

## 6. `.gitkeep` convention

Git does not track empty directories. Every intentional empty leaf folder in
`assets/`, `scenes/`, `scripts/`, `resources/` and `addons/` carries a
`.gitkeep`. When you add the first real file to a folder, leave the `.gitkeep`
in place — it keeps the skeleton stable across refactors and diffs.
