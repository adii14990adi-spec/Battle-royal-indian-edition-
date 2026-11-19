# Production FBX visual capture — Unity Editor software GL

| Check | Status | Detail |
| --- | --- | --- |
| verification boundary | PASS | Real Unity Editor Camera.Render output under graphics-enabled batch mode; not an Android device render, APK test or human visual inspection. |
| graphics device available | PASS | OpenGLCore / llvmpipe (LLVM 20.1.2, 256 bits) |
| character integration regenerated before capture | PASS | regenerated generated config/controllers from the production FBX and 74 clip sources; 0 failure(s) |
| generated character config | PASS | Assets/Generated/Resources/CharacterAnimationConfig.asset |
| visible player mesh is FBX, not capsule | PASS | PlayerCharacterVisual.TrySpawn instantiated Assets/Characters/Male/Male character.fbx; 3 SkinnedMeshRenderer(s), valid Humanoid Avatar, PlayerCapsuleVisual MeshRenderer disabled |
| spawned visual root found | PASS | Male character (CharacterVisual) |
| FBX feet aligned to temporary floor | PASS | bounds before alignment minY=-0.102; aligned minY=0.000, size=(1.860, 1.916, 0.441) |
| animation clip resolution | INFO | config.FindClip(Walk)=walking; selected verification clip=walking. The generated Animator controller is built from the assignment list, not FindClip. |
| no magenta / error-material pixels | PASS | 0 magenta pixels in rest and sampled frames |
| visual animation deformation | PASS | AnimationMode sampled 'walking' at 0.000 s and 0.683 s; 30367 pixels changed between genuine Unity camera renders |
| front / facial / UV-seam quality | NOT VERIFIED | committed camera is a rear three-quarter view; no human close-up or front-face inspection was performed |
| Android device rendering / shader variants / FPS / memory / thermal | NOT VERIFIED | these are Editor software-GL renders, not an APK installed on a physical Android device |
| screenshot paths | INFO | rest=docs/fbx_production_verification/screenshots/fbx_player_rest.png; deformed=docs/fbx_production_verification/screenshots/fbx_player_deformed.png |
| graphics device | INFO | OpenGLCore / llvmpipe (LLVM 20.1.2, 256 bits) |

## Summary

- PASS: 9
- FAIL: 0
- NOT VERIFIED: 2
