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
| animation clip resolution | INFO | config.FindClip(Walk)=walking; verification-only direct resolver=null. The generated Animator controller is built from the assignment list, not FindClip. |
| visual animation deformation | NOT VERIFIED | no Humanoid Walk clip was available in the generated assignments; only the rest-pose screenshot was rendered |
| screenshot paths | INFO | rest=docs/fbx_production_verification/screenshots/fbx_player_rest.png; deformed pose not captured |
| graphics device | INFO | OpenGLCore / llvmpipe (LLVM 20.1.2, 256 bits) |

## Summary

- PASS: 7
- FAIL: 0
- NOT VERIFIED: 1
