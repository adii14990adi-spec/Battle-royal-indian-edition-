# Blender automation pipeline (Blender 4.2+)

Reusable, script-driven asset generation for the Unity project, living in
`processing/blender/`. Nothing in it needs a GUI; every script is a
`blender --background --python <script>` run.

> **Status honesty.** The Blender generators have not been executed against a
> real Blender installation in this workspace. Python syntax checks and the
> validator/unit tests against synthetic GLB/FBX files can run without Blender;
> they do not prove that a real `.blend` export succeeds. CI runs the generators
> only when Blender is present and records their actual outcome.

---

## 1. Layout

```
processing/blender/
├── scripts/
│   └── bh_blender_common.py        shared helpers (importable without bpy)
├── generators/
│   ├── generate_environment_kit.py 9 modular architecture pieces + PBR + collision
│   └── generate_characters.py      male/female humanoid rig harness
├── validators/
│   └── validate_assets.py          gates the output, needs no Blender
├── tests/
│   └── test_pipeline_tools.py      13 unittest cases (run in CI)
└── output/                         generated, git-ignored
    ├── environment/  (GLB + PNG + asset_manifest.json)
    └── characters/   (FBX + JSON sidecar + asset_manifest.json)
```

## 2. Running it

```bash
# environment kit (GLB, PBR PNGs, box collision, manifest)
blender --background --factory-startup \
        --python processing/blender/generators/generate_environment_kit.py

# character rig harness (FBX, skinned, JSON sidecar)
blender --background --factory-startup \
        --python processing/blender/generators/generate_characters.py

# gate the output (no Blender required — this is what CI runs)
python3 processing/blender/validators/validate_assets.py --output processing/blender/output --strict

# unit tests for the Python half
python3 -m unittest discover -s processing/blender/tests -v
```

`--factory-startup` matters: it guarantees the add-ons, unit system and scene
settings are Blender's defaults rather than whatever the operator last used, which
is what makes the output deterministic.

## 3. The contract every asset obeys

These rules are enforced in code (`bh_blender_common.py`) and re-checked by the
validator, because "we agreed to apply transforms" is not a verification.

| Rule | Implementation | Checked by |
| --- | --- | --- |
| **Units are metres**, 1 Blender unit = 1 Unity unit | `set_units_metric()`; scene `unit_settings.scale_length = 1.0` | validator extent sanity (0.05 m … 120 m) |
| **Rotation and scale applied** (`1,1,1`) | `apply_transforms()` | manifest records it; validator reads bounds |
| **Origin at the base/feet centre** | `set_origin_to_base()` (moves mesh data, not the object) | validator compares Y span against the JSON min |
| **Y-up on export** | glTF exports Y-up by spec; FBX exporter uses `axis_forward=-Z, axis_up=Y` | validator (glTF) |
| **UVs preserved** | every mesh gets a UV layer; `smart_uv_project()` only when missing | validator requires `TEXCOORD_0` |
| **PBR materials** | `make_pbr_material()` — base colour, metallic, roughness, optional normal | manifest lists material names |
| **Textures ≤ 512²**, PNG | `export_pbr_texture()` | `AssetImportRules` caps at 512² again on import |
| **Collision meshes** | `<name>_collision` box hull, `bh_collision` custom prop, `hide_render` | validator requires it when the manifest says `collision: true` |
| **Deterministic output** | no random values; fixed seeds; sorted manifest | manifest SHA-256 per file |
| **Triangles budget** | prop ≤ 8,000, character ≤ 40,000 | validator |
| **≤ 4 bone influences** | `limit_vertex_influences(4)` + `bh_vertex_influence_histogram()` | validator reads the sidecar histogram |
| **Idempotent** | re-running regenerates and replaces manifest entries by name | `test_manifest_merge_replaces_by_name_and_sorts` |

## 4. Shared module — `bh_blender_common.py`

The parts worth knowing:

| Function | Purpose |
| --- | --- |
| `blender_version()` | returns the version string, or `"unavailable"` when imported outside Blender (this is why the module imports cleanly under plain Python) |
| `reset_scene()` | empties the factory-startup scene, including orphan data |
| `set_units_metric(scene)` | metres, scale 1.0 |
| `apply_transforms(objects)` | location/rotation/scale applied, so Unity sees identity transforms |
| `set_origin_to_base(objects)` | origin at the lowest Z, centred in X/Y → Unity pivot lands on the ground |
| `world_bounds(objects)` | (min, max) in world space, used for sanity + manifests |
| `triangulate(mesh)`, `has_uv_layers()`, `smart_uv_project()` | topology + UV guarantees |
| `shade_smooth_by_angle()` | 4.2+ angle-based smooth shading without legacy auto-smooth |
| `limit_vertex_influences(mesh, 4)` | prunes and re-normalises weights for mobile skinning |
| `vertex_influence_histogram(mesh)` | `{influences: vertexCount}` → sidecar, so the ≤ 4 rule is auditable |
| `make_pbr_material(...)`, `export_pbr_texture(...)` | Principled BSDF setup + PNG export |
| `add_box_collision(...)` | 12-triangle box hull named `<name>_collision`, `hide_render`, tagged `bh_collision` |
| `export_glb(...)` / `export_fbx(...)` | the two exporters, with Unity-friendly axis settings |
| `sha256_file(path)` | for the manifest |
| `write_manifest(dir, entry)` | merge-by-name + sorted + timestamp, so CI diffs are stable |

## 5. Environment kit (`generate_environment_kit.py`)

Nine modular pieces covering the five Siran Island zones. All are 4 m-modular
(except where stated) so they snap together, and each carries a box collider.

| Asset | Size | Zone it serves |
| --- | --- | --- |
| `wall_4m` | 4 × 4 × 0.3 m | all |
| `floor_4m` | 4 × 4 × 0.2 m | all |
| `doorway_4m` | 4 × 4 × 0.3 m with an opening | city, factory |
| `crate_1m` | 1 × 1 × 1 m | factory, docks |
| `container_6m` | 6 × 2.6 × 2.4 m | docks, factory |
| `factory_chimney` | 18 m tall | industrial NW |
| `runway_light` | small mast | airfield S |
| `graveyard_headstone` | small slab | graveyard E |
| `dock_plank` | deck module | docks SW |

Materials: `bh_concrete`, `bh_steel`, `bh_wood`, `bh_stone`, `bh_lamp` — all
Principled BSDF with metallic/roughness set for a 512² budget.

Deliberate omissions: **no buildings, no city blocks, no vegetation, no mansion.**
The kit is pieces, not a placed world. Placing them is level design, and the brief
forbids inventing assets that do not exist in the source project.

## 6. Character rig harness (`generate_characters.py`)

Two humanoid bodies (male/female proportions) with a **20-bone rig** that maps
1:1 onto Unity's humanoid avatar:

```
Hips → Spine → Spine1 → Spine2 → Neck → Head
      ├ LeftShoulder → LeftArm → LeftForeArm → LeftHand
      ├ RightShoulder → RightArm → RightForeArm → RightHand
LeftUpLeg → LeftLeg → LeftFoot
RightUpLeg → RightLeg → RightFoot
```

Each is exported as FBX with a JSON sidecar:

```json
{
  "name": "rig_harness_male",
  "role": "rig_test_harness",
  "height_m": 1.78,
  "bones": ["Hips", "Spine", "..."],
  "influences_per_vertex": {"1": 512, "2": 1904},
  "animations": ["idle", "walk", "run"],
  "export": "FBX"
}
```

**`role: rig_test_harness` is the important field.** These are proportioned
mannequins with weight-painted joints and three looping poses (idle / walk / run)
used to prove the import path end-to-end: FBX → Unity → humanoid avatar → ≤ 4
influences → animation playback. They are **not** shippable art and the manifest
says so. The brief's required animation set (idle, walk, run, jump, crouch, aim,
reload) needs a real character; three of the seven exist as harness poses, and
jump/crouch/aim/reload are gaps listed in the report.

The sidecar's `bones` array is the machine-readable contract: the validator checks
it contains every bone Unity's humanoid mapping needs, so a renamed bone fails the
build instead of silently producing a T-posed character in Unity.

## 7. Validation (`validate_assets.py`)

Runs in ~1 s on any machine with Python — no Blender, no GPU:

1. file exists and SHA-256 matches the manifest,
2. container is genuine (GLB magic + version + declared total length; FBX magic +
   version ≥ 7000),
3. GLB JSON chunk parses and its `nodes` / `meshes` / `accessors` are coherent,
4. **scale**: POSITION accessor min/max in metres, 0.05 m … 120 m,
5. **orientation**: no asset is upside down; extremes are sane,
6. **UV**: `TEXCOORD_0` on every primitive,
7. **characters**: `JOINTS_0` + a `skins` section, all humanoid bones present,
   ≤ 4 influences,
8. **collision**: `<name>_collision` node exists when the manifest requires it.

Exit code: 0 = pass, 1 = failure. `--strict` promotes warnings to failures. With
no manifest present it prints SKIP and exits 0 — which is the correct answer for
"I have not generated anything yet", and is why CI's `assets` job still passes on
a clean checkout.

## 8. Tests

`processing/blender/tests/test_pipeline_tools.py` — 13 cases, all passing
(`python3 -m unittest discover -s processing/blender/tests`):

| Area | Cases |
| --- | --- |
| GLB validation | valid passes; missing UV fails; 100× too small fails; missing collision fails when required; bad magic reported not raised; unskinned character fails; skinned character passes |
| FBX validation | valid passes; unversioned rejected; garbage rejected |
| Character sidecar | missing bones fail; > 4 influences fail |
| Manifest | merge replaces by name and sorts |

These use synthetic files built in the test itself (`make_glb`, `make_fbx`), so
they exercise the real validator code paths without needing Blender.

## 9. Getting generated assets into Unity

```bash
# 1. generate
blender --background --factory-startup --python processing/blender/generators/generate_environment_kit.py
# 2. validate
python3 processing/blender/validators/validate_assets.py --strict
# 3. copy (GLB for environment, FBX for characters)
cp processing/blender/output/environment/*.glb unity-project/Assets/Art/Environment/
cp processing/blender/output/characters/*.fbx  unity-project/Assets/Art/Characters/
# 4. Unity imports with Assets/Editor/AssetImportRules.cs applied automatically:
#    - model scale 1, no bake-axis surprises
#    - humanoid avatar for anything under /Characters/
#    - 4 bone influences, mesh compression off
#    - textures capped at 512², ASTC 6×6, mipmap on, aniso 0
```

`processing/blender/output/` is git-ignored: it is reproducible from the scripts,
and committing binary art that the pipeline can regenerate is how repositories
rot. If a generated asset becomes hand-edited art, promote it by copying it into
`unity-project/Assets/Art/…` (which is tracked) and removing it from the generator.

## 10. Extending the kit

To add a prop:

1. write a `build_x()` in `generate_environment_kit.py` using the shared helpers
   (`set_origin_to_base`, `apply_transforms`, `smart_uv_project`, `add_box_collision`),
2. add it to the export list with `collision=True`,
3. add its name to the validator's expected set (so a missing export fails),
4. run generator → validator → copy into Unity.

No step requires opening Blender's UI, and every step is reproducible in CI.
