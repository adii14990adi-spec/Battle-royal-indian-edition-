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
| Female canonical Walk deformation clip | FAIL | FindClip(Walk)=walking; canonical walking clip is unavailable |
| FBX visual capture completed | FAIL | Female has no Humanoid walking clip after config rebuild |
| screenshot files | NOT VERIFIED | capture failed before writing PNG files |

## Summary

- PASS: 11
- FAIL: 2
- NOT VERIFIED: 1
