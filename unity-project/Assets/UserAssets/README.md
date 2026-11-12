# UserAssets — drop-in location for your own 3D assets

This folder is intentionally empty. The island is generated from the committed
height/colour data, and **no placeholder buildings, trees or props are spawned**
by the project — these folders are reserved for the art you upload.

```text
Assets/UserAssets/
├── Models/      .fbx / .obj / .glb buildings, trees, props, vehicles
└── Materials/   materials and shaders for those models
```

## How to place your assets

1. Copy your model files into `Assets/UserAssets/Models/` (sub-folders are fine).
2. Copy or create their materials in `Assets/UserAssets/Materials/`.
3. Open `Assets/Scenes/SiranIsland_Greybox.unity` (created by
   **Battle Horizons > Project Setup > Configure**).
4. Drag the prefab/instance under the empty `World/UserAssets` object in the
   hierarchy so it stays grouped with the generated island.

## Import rules that apply automatically

`Assets/Editor/AssetImportRules.cs` runs on import:

* models use metre scale, no implicit collider, and character models use a
  Humanoid avatar with at most four bone influences;
* textures are capped at 512 × 512, mipmaps on, anisotropic off, ASTC 6×6 on
  Android;
* audio is Vorbis (mono for SFX, streamed for music).

## Ground height for placement

The terrain is generated from `Assets/Resources/Config/siran_island_heights.bytes`
(401 × 401 samples, 10 m apart, 4 km across). To sit an object on the ground use
`SiranIslandData.SampleHeight(x, z)` (`BattleHorizons.World`), or place it by eye
in the editor — the heightmap collider (`TerrainCollider`, layer `Terrain`)
matches the mesh exactly.

**Texture budget note:** `SiranChunkMat` and `SiranIslandMat` are matte
(`metallic 0`, `smoothness 0`) so the ground never turns into a white glare. Use
the same convention for new terrain materials.
