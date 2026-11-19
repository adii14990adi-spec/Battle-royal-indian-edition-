# Production FBX visual capture — Male and Female, Unity Editor software GL

| Check | Status | Detail |
| --- | --- | --- |
| verification boundary | PASS | Real Unity Editor Camera.Render output for both production FBX characters under graphics-enabled batch mode; not an Android device render, APK test or human visual inspection. |
| graphics device available | PASS | OpenGLCore / llvmpipe (LLVM 20.1.2, 256 bits) |
| character integration regenerated before capture | PASS | regenerated generated config/controllers from the production FBX and 74 clip sources; 0 failure(s) |
| generated character config | PASS | Assets/Generated/Resources/CharacterAnimationConfig.asset |
| Female visible player mesh is FBX, not capsule | PASS | PlayerCharacterVisual.TrySpawn instantiated Assets/Characters/Male/Male character.fbx; 3 SkinnedMeshRenderer(s), valid Humanoid Avatar, PlayerCapsuleVisual MeshRenderer disabled |
| Female spawned visual root found | PASS | Male character (CharacterVisual) |
| Female production material remap | FAIL | 3 material slots (0 external URP/Lit), 9 bound texture slots, 0 null, 0 error shader(s), 0 non-URP/Lit shader(s), 0 invalid texture(s), shaders: Universal Render Pipeline/Lit |
| Female FBX feet aligned to temporary floor | PASS | bounds before alignment minY=-0.102; aligned minY=0.000, size=(1.860, 1.916, 0.441) |
| Female runtime Walk clip lookup | PASS | config.FindClip(Walk)=Skateboarding; selected 'Skateboarding', humanMotion=True |
| Female no magenta / error-material pixels | PASS | 0 magenta pixels in rest and sampled frames |
| Female visual animation deformation | PASS | AnimationMode sampled 'Skateboarding' at 0.000 s and 0.500 s; 32205 pixels changed between genuine Unity camera renders |
| Female textured camera capture | PASS | 27635 chromatic pixels prove the production diffuse/normal material remap is visible, not a flat grey silhouette |
| Male visible player mesh is FBX, not capsule | PASS | PlayerCharacterVisual.TrySpawn instantiated Assets/Characters/Male/Male character.fbx; 3 SkinnedMeshRenderer(s), valid Humanoid Avatar, PlayerCapsuleVisual MeshRenderer disabled |
| Male spawned visual root found | PASS | Male character (CharacterVisual) |
| Male production material remap | PASS | 3 material slots (3 external URP/Lit), 9 bound texture slots, 0 null, 0 error shader(s), 0 non-URP/Lit shader(s), 0 invalid texture(s), shaders: Universal Render Pipeline/Lit |
| Male FBX feet aligned to temporary floor | PASS | bounds before alignment minY=-0.102; aligned minY=0.000, size=(1.860, 1.916, 0.441) |
| Male runtime Walk clip lookup | PASS | config.FindClip(Walk)=Skateboarding; selected 'Skateboarding', humanMotion=True |
| Male no magenta / error-material pixels | PASS | 0 magenta pixels in rest and sampled frames |
| Male visual animation deformation | FAIL | only 0 pixel(s) changed between Editor samples |
| Male textured camera capture | PASS | 27635 chromatic pixels prove the production diffuse/normal material remap is visible, not a flat grey silhouette |
| both production FBX characters captured | PASS | Male and Female each produced rest and deformed genuine Editor camera renders |
| front / facial / UV-seam quality | NOT VERIFIED | committed camera is a rear three-quarter view; no human close-up or front-face inspection was performed |
| Android device rendering / shader variants / FPS / memory / thermal | NOT VERIFIED | these are Editor software-GL renders, not an APK installed on a physical Android device |
| screenshot paths | INFO | docs/fbx_production_verification/screenshots/fbx_female_rest.png; docs/fbx_production_verification/screenshots/fbx_female_deformed.png; docs/fbx_production_verification/screenshots/fbx_male_rest.png; docs/fbx_production_verification/screenshots/fbx_male_deformed.png; docs/fbx_production_verification/screenshots/fbx_player_rest.png; docs/fbx_production_verification/screenshots/fbx_player_deformed.png |
| graphics device | INFO | OpenGLCore / llvmpipe (LLVM 20.1.2, 256 bits) |

## Summary

- PASS: 19
- FAIL: 2
- NOT VERIFIED: 2
