# GLTF URI corrections (test copy only)

The live source glTF files contain URI references to PNG filenames that are not present in the source folders. The original files are preserved unchanged. Only the separate `UnityTestCopy/*.gltf` JSON files were edited; no PNG was renamed and no BIN was modified.

| Sex | Original URI | Corrected test-copy URI | Reason |
| --- | --- | --- | --- |
| Male | `T_Hair_1_Normal_png.png` | `T_Hair_1_Normal.png` | source folder contains `T_Hair_1_Normal.png` |
| Male | `T_Eye_Normal_png.png` | `T_Eye_Normal.png` | source folder contains `T_Eye_Normal.png` |
| Female | `T_Eye_Normal_png.png` | `T_Eye_Normal.png` | source folder contains `T_Eye_Normal.png` |

All BIN buffer URIs and all other PNG image URIs already resolve case-sensitively and were left unchanged.
