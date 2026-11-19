# Production FBX visual capture — Male and Female, Unity Editor software GL

| Check | Status | Detail |
| --- | --- | --- |
| verification boundary | PASS | Real Unity Editor Camera.Render output for both production FBX characters under graphics-enabled batch mode; each generated definition is instantiated directly from its production FBX to avoid Resources.Load selection caching. Not an Android device render, APK test or human visual inspection. |
| graphics device available | PASS | OpenGLCore / llvmpipe (LLVM 20.1.2, 256 bits) |
| character integration regenerated before capture | PASS | rebuilt generated controllers from the production FBX and 74 clip sources; 0 failure(s) |
| generated character config | PASS | Assets/Generated/Resources/CharacterAnimationConfig.asset |
| generated config isolated from EditMode fixtures | PASS | 0 per-character overrides in the generated config; EditMode controller fixtures use scratch configs |
| Female requested production FBX selected | PASS | generated definition points at Assets/Characters/Female/Female character.fbx |
| Female visible player mesh is FBX, not capsule | PASS | capture instantiated Assets/Characters/Female/Female character.fbx from the generated definition; 3 SkinnedMeshRenderer(s), valid Humanoid Avatar, PlayerCapsuleVisual MeshRenderer disabled |
| Female generated Animator controller assigned | PASS | generated per-character controller assigned to the spawned Animator |
| Female spawned visual root found | PASS | Female character (CharacterVisual) |
| Female production material remap | PASS | 3 material slots (3 external URP/Lit), 9 bound texture slots, 0 null, 0 error shader(s), 0 non-URP/Lit shader(s), 0 invalid texture(s), shaders: Universal Render Pipeline/Lit |
| Female FBX feet aligned to temporary floor | PASS | bounds before alignment minY=-0.085; aligned minY=0.000, size=(1.670, 1.854, 0.435) |
| Female runtime Walk deformation clip | PASS | config.FindClip(Walk)='walking', humanMotion=True, length=1.367 s; 74 registered production clips |
| Female URP/Lit shader warm-up | PASS | two discarded genuine Editor Camera.Render frames primed the material variants before evidence frames |
| Female no magenta / error-material pixels | PASS | 0 magenta pixels in rest and sampled frames |
| Female visual animation deformation | FAIL | only 0 pixel(s) changed between Editor samples |
| Female textured camera capture | PASS | 29481 chromatic pixels prove the production diffuse/normal material remap is visible, not a flat grey silhouette |
| Male requested production FBX selected | PASS | generated definition points at Assets/Characters/Male/Male character.fbx |
| Male visible player mesh is FBX, not capsule | PASS | capture instantiated Assets/Characters/Male/Male character.fbx from the generated definition; 3 SkinnedMeshRenderer(s), valid Humanoid Avatar, PlayerCapsuleVisual MeshRenderer disabled |
| Male generated Animator controller assigned | PASS | generated per-character controller assigned to the spawned Animator |
| Male spawned visual root found | PASS | Male character (CharacterVisual) |
| Male production material remap | PASS | 3 material slots (3 external URP/Lit), 9 bound texture slots, 0 null, 0 error shader(s), 0 non-URP/Lit shader(s), 0 invalid texture(s), shaders: Universal Render Pipeline/Lit |
| Male FBX feet aligned to temporary floor | PASS | bounds before alignment minY=-0.102; aligned minY=0.000, size=(1.860, 1.916, 0.441) |
| Male runtime Walk deformation clip | PASS | config.FindClip(Walk)='walking', humanMotion=True, length=1.367 s; 74 registered production clips |
| Male URP/Lit shader warm-up | PASS | two discarded genuine Editor Camera.Render frames primed the material variants before evidence frames |
| Male no magenta / error-material pixels | PASS | 0 magenta pixels in rest and sampled frames |
| Male visual animation deformation | FAIL | only 0 pixel(s) changed between Editor samples |
| Male textured camera capture | PASS | 30890 chromatic pixels prove the production diffuse/normal material remap is visible, not a flat grey silhouette |
| both production FBX characters captured | PASS | Male and Female each produced rest and deformed genuine Editor camera renders |
| front / facial / UV-seam quality | NOT VERIFIED | committed camera is a rear three-quarter view; no human close-up or front-face inspection was performed |
| Android device rendering / shader variants / FPS / memory / thermal | NOT VERIFIED | these are Editor software-GL renders, not an APK installed on a physical Android device |
| screenshot paths | INFO | docs/fbx_production_verification/screenshots/fbx_female_rest.png; docs/fbx_production_verification/screenshots/fbx_female_deformed.png; docs/fbx_production_verification/screenshots/fbx_male_rest.png; docs/fbx_production_verification/screenshots/fbx_male_deformed.png; docs/fbx_production_verification/screenshots/fbx_player_rest.png; docs/fbx_production_verification/screenshots/fbx_player_deformed.png |
| graphics device | INFO | OpenGLCore / llvmpipe (LLVM 20.1.2, 256 bits) |

## Summary

- PASS: 26
- FAIL: 2
- NOT VERIFIED: 2
