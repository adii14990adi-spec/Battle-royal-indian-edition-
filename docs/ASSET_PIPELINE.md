# Unity asset pipeline

Art shipped in the game belongs under `unity-project/Assets/`. Blender source
scripts and generated interchange files live under `processing/blender/`; the
Unity project does not depend on another engine project.

## Asset locations

```text
unity-project/Assets/Art/             authored environment, character and prop assets
unity-project/Assets/UserAssets/      drop-in Models/ and Materials/ for user-uploaded 3D assets
unity-project/Assets/Resources/       runtime-loaded textures and map data
unity-project/Assets/Materials/       materials created by ProjectBootstrap
unity-project/Assets/Shaders/         Unity shader source
processing/blender/generators/        reproducible Blender generation scripts
processing/blender/validators/        format and manifest validation
processing/blender/output/            generated output (git-ignored)
```

`Assets/UserAssets/Models` and `Assets/UserAssets/Materials` are created by
project setup and committed empty (with a README). The project spawns no
placeholder buildings, trees or props: that folder is where the user's own
assets go, grouped under the empty `World/UserAssets` object in the generated
scenes.

The Blender workflow is documented in
[`asset-pipeline/BLENDER_PIPELINE.md`](asset-pipeline/BLENDER_PIPELINE.md). It
creates a reusable modular kit and test harness; it does not claim that a
finished character, placed city or complete level art is present.

## Unity import rules

`Assets/Editor/AssetImportRules.cs` enforces these settings when assets are
imported:

* texture dimensions are capped at 512 × 512, mipmaps are enabled, anisotropic
  filtering is disabled and Android uses ASTC 6×6;
* audio imports as Vorbis, mono for sound effects, with music streamed;
* models use metre scale, no implicit collider, and character models use a
  Humanoid avatar with at most four bone influences.

The rules are centralized so a new asset does not need an undocumented manual
import setup. Inspect the Unity import log for `[bh][import]` errors after adding
assets.

## Version control

Commit source assets that are used by the project. Keep reproducible Blender
output under the ignored `processing/blender/output/` directory until an asset is
reviewed and promoted into `unity-project/Assets/`. Binary image, audio and model
formats listed in `.gitattributes` use Git LFS.

Never commit Unity `Library/`, local build output, APK/AAB packages, license
files, or Android signing keys. The root `.gitignore` covers these.

## Validation

```bash
python3 -m unittest discover -s processing/blender/tests -v
python3 processing/blender/validators/validate_assets.py --output processing/blender/output
python3 processing/unity/validate_structure.py
python3 processing/unity/validate_csharp.py
```

The asset validator reports `SKIP` when no generated asset manifest exists; that
is not an asset-generation pass. Unity editor import and Android rendering still
require an actual Unity editor run.
