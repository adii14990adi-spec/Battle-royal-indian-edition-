# Battle Horizons — Unity 6 (URP) project

Android-first 3D battle royale. This directory is the Unity 6 / Universal Render
Pipeline port of the Godot 4.4 project that lives at the **repository root**.

> The Godot project is still there and still builds. Nothing in this folder
> replaces it: `../project.godot`, `../scenes/`, `../scripts/` and
> `../.github/workflows/build.yml` are untouched and keep working. This is the
> new, parallel target.

* Engine: **Unity 6** (`6000.0.32f1`) with **URP 17**
* Platform: **Android** (arm64-v8a, min API 26, landscape, offline)
* Target device baseline: Android 16, Snapdragon 6s Gen 3, 8 GB RAM — plus a
  Low profile for the 4 GB Unisoc T606 the Godot build was tuned for
* Language: C# (IL2CPP, .NET Standard 2.1, medium stripping)

---

## Open the project

```bash
# 1. Unity Hub -> Add -> select this folder (unity-project/)
#    Editor version: 6000.0.32f1 (any 6000.0.x LTS works; Hub will offer an upgrade)
# 2. First open takes a few minutes: URP + Input System packages resolve,
#    scripts compile, and the importer generates the Library folder.
# 3. In the editor:  Battle Horizons > Project Setup > Configure
```

`Configure` is the single setup step. It creates or repairs:

| What | Where |
| --- | --- |
| URP pipeline asset (mobile numbers: no HDR, MSAA off, 2 cascades, 60 m shadows) | `Assets/Settings/BattleHorizonsURP.asset` |
| Four URP materials (terrain, chunk, water, road) | `Assets/Materials/*.mat` |
| Four scenes (Boot, MainMenu, SiranIsland, PlayerTest) | `Assets/Scenes/**` |
| Build settings (Boot first) | `ProjectSettings/EditorBuildSettings.asset` |
| Android player settings (ARM64, IL2CPP, landscape, no INTERNET, ASTC) | `ProjectSettings/ProjectSettings.asset` |

Everything it does is idempotent — running it again changes nothing that is
already correct.

### Command line (CI / no GUI)

```bash
Unity -batchmode -quit \
      -projectPath unity-project \
      -executeMethod BattleHorizons.EditorTools.ProjectBootstrap.ConfigureFromCommandLine

# validation only (exit code 1 on any failure)
Unity -batchmode -quit \
      -projectPath unity-project \
      -executeMethod BattleHorizons.EditorTools.ProjectBootstrap.ValidateFromCommandLine
```

### Play

| Scene | What it is |
| --- | --- |
| `Scenes/Boot.unity` | Entry point: picks a quality profile, then loads the main menu |
| `Scenes/MainMenu/MainMenu.unity` | Title, quality toggle, "ENTER SIRAN ISLAND" |
| `Scenes/Gameplay/SiranIsland.unity` | The 4 km map with a free camera and the debug HUD |
| `Scenes/Test/PlayerTest.unity` | Third-person player + camera + touch controls on the map |

In the editor: WASD/arrows to move, Shift to sprint, Space to jump, C to crouch,
mouse to look (right button, or always in the editor), **F3** to toggle the HUD.
The touch overlay is built in the editor too (`buildInEditor` on
`TouchControlsHost`), so the phone layout can be checked without a device.

On a phone: bottom-left stick moves (full deflection sprints), right stick or a
drag anywhere on the screen looks, **JUMP** jumps, **CROUCH** and **SPRINT** are
hold buttons, three-finger tap toggles the HUD.

---

## Build an Android APK

### From the editor

1. **File > Build Settings** → platform **Android** → **Switch Platform**
2. **Player Settings** are already configured by `Configure`; to use your own
   release key set **Publishing Settings > Custom Keystore** and point it at your
   `.keystore` (never commit it — `.gitignore` blocks `*.keystore` / `*.jks`)
3. **Build** (APK) or **Build App Bundle** (AAB)
4. Install: `adb install -r build/battle-horizons.apk`

### From the command line

```bash
Unity -batchmode -quit \
      -projectPath unity-project \
      -buildTarget Android \
      -executeMethod BattleHorizons.EditorTools.AndroidBuild.BuildApk \
      -logFile build.log
# output: unity-project/Builds/battle-horizons.apk
```

### From CI

`.github/workflows/unity.yml` has an `android` job that builds and uploads the
APK. It only runs when a Unity licence is configured as a repository secret
(`UNITY_LICENSE`, or `UNITY_EMAIL` + `UNITY_PASSWORD`); without a licence it
reports SKIP and says exactly what it did not verify. Signing keys come from
secrets and are never written into the repository.

---

## Layout

```
unity-project/
├── Assets/
│   ├── Art/{Characters,Environment,Buildings,Vegetation,Weapons,Textures}/
│   ├── Animations/  Audio/{Music,SFX,Voice}/  Materials/  Prefabs/
│   ├── Editor/          ProjectBootstrap, ProjectValidator, AssetImportRules
│   ├── Resources/
│   │   ├── Config/      converted Siran Island data (do not hand-edit)
│   │   └── Art/Textures/water_normal_256.png (copied from the Godot project)
│   ├── Scenes/{MainMenu,Gameplay,Test}/
│   ├── Scripts/
│   │   ├── Core/        bootstrap, quality profiles, perf monitor, pooling
│   │   ├── World/       Siran island data, terrain, water, chunk streaming
│   │   ├── Player/      third-person motor, input router, virtual joystick
│   │   ├── Camera/      third-person orbit camera
│   │   ├── Combat/ Weapons/ Inventory/ Vehicles/ UI/
│   ├── Settings/        URP assets
│   └── Tests/{EditMode,PlayMode}/
├── Packages/manifest.json
└── ProjectSettings/
```

### Where the map comes from

`Assets/Resources/Config/siran_island_*` and `SiranMapConfig.cs` are **generated**
from the Godot project:

```bash
python3 processing/unity/port_siran_config.py     # GDScript layout -> C# + JSON
python3 processing/unity/convert_godot_island.py  # baked scene -> height/colour/road blobs
```

Do not edit them by hand. Edit `../scripts/world/siran_map_config.gd`, re-run the
two commands, and commit both sides; CI fails if they drift apart. Details:
`../docs/migration/BAKE_FORMAT.md`.

---

## What is and is not done

Read `../docs/migration/REPORT.md` for the audited status table. Short version:

* **Working**: island terrain + collision + water + roads from the real Godot
  bake, chunk streaming, third-person player + camera + touch input, two quality
  profiles, URP/Android configuration, tests, CI.
* **Not present in the source project, therefore not migrated**: characters,
  animations, weapons, vehicles, inventory, bots, buildings/props/vegetation
  (the Godot project has `.gitkeep` folders for these, and its own README says
  "no buildings, props or vegetation yet").
* **Not verifiable here**: Unity editor runs, on-device frame rate and input.
  Those steps are marked BLOCKED / NOT TESTED in the report, never PASS.
