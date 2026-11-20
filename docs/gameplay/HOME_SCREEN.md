# Home Screen (code-built lobby)

The Home Screen is the first thing a player sees and it is now **100% built in
code** — no baked UI artwork, no invisible hotspots over a JPEG.

## Why the old screen was replaced

The previous lobby rendered `Resources/UI/battle_horizons_home_bg.jpg` (a 16:9
key-art image with the whole menu *burnt into the pixels*) and placed
transparent `Button` rects over the painted labels. On 20:9 phones the
full-bleed copy of that art showed duplicated, cropped buttons at both screen
edges, and the "UI" could not be restyled, localised or resized — it was a
picture, not an interface.

## Architecture

| File | Role |
| --- | --- |
| `Assets/Scripts/UI/LobbyArtFactory.cs` | Deterministic pixel painter (`Painter`) + cached procedural sprites: sunset vista with fortress silhouette, dusk sky gradient, rounded panels, gold START gradient and 18 glyphs (cart, luck crate, trophy, target, calendar, vault, rifle, operator, gear, bell, coin, gem, avatar, compass, solo/duo/squad, caret). |
| `Assets/Scripts/UI/MainMenuController.cs` | Lays the widgets out on a 1920×1080 reference canvas, wires every button, owns the Solo/Duo/Squad selector, the info/settings modals and the START → Siran Island flow. |
| `Assets/Scripts/UI/LobbyCharacterShowcase.cs` | Hosts the real-time operator on the stage: preview RT compositing, idle-clip auto-select, turntable drift, honest hide when no character is configured. |
| `Assets/Scripts/UI/LobbyProfile.cs` | PlayerPrefs-backed wallet & callsign shown in the top chips (defaults 45,820 coins / 1,240 gems / `Commander_K`), `GrantCoins` hook for missions/events. |

### Widget tree (names are stable — PlayMode tests resolve them by path)

```
MainMenuCanvas (ScreenSpaceOverlay, ScaleWithScreenSize 1920×1080)
├── Background            full-bleed dusk gradient (raycast off)
├── HomeArtworkFrame      16:9 painted vista, FitInParent (raycast off)
│   ├── CharacterShowcase  LIVE operator: configured production character rendered
│   │   │                  by CharacterPreviewController into a 720x1024 transparent
│   │   │                  RenderTexture (zero-alpha clear), idle clip playing, slow
│   │   │                  9°/s turntable; hides honestly when nothing is configured
│   │   └── Spotlight      radial ground glow under the operator's feet
│   ├── LogoMark / LogoTitle / LogoSubtitle
│   ├── ProfileButton (avatar + callsign + online dot)
│   ├── CoinChip / GemChip (icon + formatted balance)
│   ├── SettingsButton (gear) / NotificationsButton (bell + red badge)
│   ├── StoreButton / LuckRoyaleButton / BattlePassButton / MissionsButton / EventsButton
│   ├── VaultButton / WeaponButton / CharacterButton (opens Character Bay preview)
│   ├── ModePanel (caption + caret + dynamic "Battle Royale - X" title)
│   ├── ModeSolo / ModeDuo / ModeSquad (direct children; cyan ActiveHighlight)
│   ├── EnterSiranIsland  golden START → LoadingScreenView → Siran Island
│   └── FooterStatus      "MAP: FORT KARGHA • SEASON 05 • PATCH 2.4.1"
└── LobbyModal            info/settings card (quality toggle, PlayerTest scene)
```

### Procedural art rules

* Every texture is painted once at boot into an RGBA32 `Texture2D` (vista
  1024×576, icons 96², panels 128²) and cached — a few hundred KB total, no
  mipmaps, no disk I/O, well inside the UNISOC-class budget.
* Painting is pure math (gradients, sine ridges, scanline polygons, distance
  fields) — **no RNG**, so `LobbyArtFactoryTests` can hash pixels and lock the
  look across machines and CI.
* The vista keeps its bottom third dark so overlay widgets keep contrast; the
  sun glow, fortress hill and crenellated towers echo the Fort Kargha key art
  without shipping it.

## Extending the lobby

* New nav entry: one `NavButton(...)` call with an icon enum + modal copy.
* New glyph: add an `LobbyIcon` enum member and paint it in `PaintIcon`.
* Rewards: call `LobbyProfile.GrantCoins(n)` from missions/events; the coin
  chip reads the persisted balance on build.

## Validation

* `Assets/Tests/EditMode/LobbyArtFactoryTests.cs` — sprite caching, per-icon
  opacity, rounded-corner transparency, gold gradient, vista colour bands,
  painter determinism (pixel hash), scanline polygon fill.
* `Assets/Tests/EditMode/LobbyProfileTests.cs` — wallet defaults/formatting
  and `GrantCoins` guards.
* `Assets/Tests/PlayMode/WorldAndPlayerPlayModeTests.cs` — canvas, raycaster,
  EventSystem, vista sprite present, START/Settings/Character paths, mode
  switching ("Battle Royale - Solo/Duo/Squad").
* `Assets/Tests/PlayMode/CharacterPreviewRuntimeTests.cs` — CharacterButton
  opens the real-time Character Bay.
