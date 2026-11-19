# Licensed Unity Editor validation — production FBX characters

| Check | Status | Detail |
| --- | --- | --- |
| verification boundary | PASS | Licensed Unity 6 Editor inspection of importers, sub-assets, generated config/controllers, PlayerTest scene, AnimationMode sampling and Android configuration. No APK, device, FPS, PSS or thermal test. |
| generated config references | PASS | all references valid (2 characters); asset path Assets/Generated/Resources/CharacterAnimationConfig.asset |
| two production character definitions | PASS | 2 character definitions; 74 registered clips; 16 assignments |
| config references production FBX only | PASS | 0 GLTF references, 0 non-production character model references |
| generated assignment characterId representation | INFO | 0 null, 16 empty/whitespace shared, 0 per-character entries; Unity serializes a null string as an empty string after domain reload |
| selected character clip assignment: Idle | PASS | idle, length 2.100 s, humanMotion=True, frameRate=30.0 |
| selected character clip assignment: Walk | PASS | walking, length 1.367 s, humanMotion=True, frameRate=30.0 |
| selected character clip assignment: Run | PASS | Running, length 0.700 s, humanMotion=True, frameRate=30.0 |
| selected character clip assignment: Jump | PASS | jump up, length 0.533 s, humanMotion=True, frameRate=30.0 |
| selected character clip assignment: Aim | PASS | idle aiming, length 2.100 s, humanMotion=True, frameRate=30.0 |
| selected character clip assignment: Shoot | PASS | firing rifle, length 0.267 s, humanMotion=True, frameRate=30.0 |
| selected character clip assignment: Reload | PASS | reloading, length 3.300 s, humanMotion=True, frameRate=30.0 |
| runtime CharacterAnimationConfig.FindClip lookup | PASS | FindClip returns the direct assignment for all 7 required categories |
| Male Unity ModelImporter settings | PASS | Humanoid, CreateFromThisModel, importAnimation=true, isReadable=false, Standard/4 bones per vertex, optimal compression |
| Male Unity material import mode | PASS | materialImportMode=LegacyImport; exact committed URP/Lit external material remaps are active |
| Male Humanoid Avatar configuration | PASS | 52/55 human bones mapped; 52 HumanDescription entries; isValid && isHuman |
| Male imported materials/textures | PASS | 3 renderers, 3 SkinnedMeshRenderers, 3 material slots (3 external URP/Lit), 9 bound texture slots, 0 Texture2D sub-assets, 0 Material sub-assets, shaders: Universal Render Pipeline/Lit |
| Male normal map resolves as NormalMap | PASS | Assets/Characters/Male/Textures/T_Superhero_Male_Normal.png: TextureImporterType.NormalMap, linear sRGB=false, convertToNormalMap=false |
| Male upright scale and ground alignment | PASS | renderer bounds size=(1.860, 1.916, 0.441), minY=-0.102 |
| Male FBX sub-assets | INFO | 0 Texture2D, 0 Material, 1 Avatar, 3 Mesh sub-assets at Assets/Characters/Male/Male character.fbx |
| Male Avatar compatibility: Idle | PASS | clip 'idle' is Humanoid and target Avatar is valid/human |
| Male Avatar compatibility: Walk | PASS | clip 'walking' is Humanoid and target Avatar is valid/human |
| Male Avatar compatibility: Run | PASS | clip 'Running' is Humanoid and target Avatar is valid/human |
| Male Avatar compatibility: Jump | PASS | clip 'jump up' is Humanoid and target Avatar is valid/human |
| Male Avatar compatibility: Aim | PASS | clip 'idle aiming' is Humanoid and target Avatar is valid/human |
| Male Avatar compatibility: Shoot | PASS | clip 'firing rifle' is Humanoid and target Avatar is valid/human |
| Male Avatar compatibility: Reload | PASS | clip 'reloading' is Humanoid and target Avatar is valid/human |
| Female Unity ModelImporter settings | PASS | Humanoid, CreateFromThisModel, importAnimation=true, isReadable=false, Standard/4 bones per vertex, optimal compression |
| Female Unity material import mode | PASS | materialImportMode=LegacyImport; exact committed URP/Lit external material remaps are active |
| Female Humanoid Avatar configuration | PASS | 52/55 human bones mapped; 52 HumanDescription entries; isValid && isHuman |
| Female imported materials/textures | PASS | 3 renderers, 3 SkinnedMeshRenderers, 3 material slots (3 external URP/Lit), 9 bound texture slots, 0 Texture2D sub-assets, 0 Material sub-assets, shaders: Universal Render Pipeline/Lit |
| Female normal map resolves as NormalMap | PASS | Assets/Characters/Female/Textures/T_Superhero_Female_Normal.png: TextureImporterType.NormalMap, linear sRGB=false, convertToNormalMap=false |
| Female upright scale and ground alignment | PASS | renderer bounds size=(1.670, 1.854, 0.435), minY=-0.085 |
| Female FBX sub-assets | INFO | 0 Texture2D, 0 Material, 1 Avatar, 3 Mesh sub-assets at Assets/Characters/Female/Female character.fbx |
| Female Avatar compatibility: Idle | PASS | clip 'idle' is Humanoid and target Avatar is valid/human |
| Female Avatar compatibility: Walk | PASS | clip 'walking' is Humanoid and target Avatar is valid/human |
| Female Avatar compatibility: Run | PASS | clip 'Running' is Humanoid and target Avatar is valid/human |
| Female Avatar compatibility: Jump | PASS | clip 'jump up' is Humanoid and target Avatar is valid/human |
| Female Avatar compatibility: Aim | PASS | clip 'idle aiming' is Humanoid and target Avatar is valid/human |
| Female Avatar compatibility: Shoot | PASS | clip 'firing rifle' is Humanoid and target Avatar is valid/human |
| Female Avatar compatibility: Reload | PASS | clip 'reloading' is Humanoid and target Avatar is valid/human |
| Animator Controller asset: female | PASS | Assets/AnimationControllers/female_Locomotion.controller, 2 layer(s), 10 parameter(s) |
| Animator parameters: female | PASS | 10/10 shared runtime parameters present with expected types |
| Animator parameter/transition uniqueness: female | PASS | 10 parameters with unique names; exactly one IsAiming Any-State entry transition |
| Animator states / clip motions: female | PASS | 7 required gameplay states each have a Motion: Idle, Walk, Run, Jump, Aim, Shoot, Reload |
| Animator transitions: female | PASS | Walk/Run Speed, Jump IsAirborne, Aim IsAiming, Shoot/Reload trigger Any-State entries and Idle exits are present; all checked entry transitions have no forced exit time |
| UpperBody aiming layer: female | PASS | layer 1 is UpperBody with AvatarMask; RifleIdle/RifleRun states and motions are present |
| Animator Controller asset: male | PASS | Assets/AnimationControllers/male_Locomotion.controller, 2 layer(s), 10 parameter(s) |
| Animator parameters: male | PASS | 10/10 shared runtime parameters present with expected types |
| Animator parameter/transition uniqueness: male | PASS | 10 parameters with unique names; exactly one IsAiming Any-State entry transition |
| Animator states / clip motions: male | PASS | 7 required gameplay states each have a Motion: Idle, Walk, Run, Jump, Aim, Shoot, Reload |
| Animator transitions: male | PASS | Walk/Run Speed, Jump IsAirborne, Aim IsAiming, Shoot/Reload trigger Any-State entries and Idle exits are present; all checked entry transitions have no forced exit time |
| UpperBody aiming layer: male | PASS | layer 1 is UpperBody with AvatarMask; RifleIdle/RifleRun states and motions are present |
| deformation sampling: Male/Idle | PASS | AnimationMode sampled 'idle' at 0.000 s and 1.050 s across 70 transforms; max bone rotation 4.836 deg, max local position delta 0.0087 m |
| deformation sampling: Male/Walk | PASS | AnimationMode sampled 'walking' at 0.000 s and 0.683 s across 70 transforms; max bone rotation 170.503 deg, max local position delta 0.6774 m |
| deformation sampling: Male/Run | PASS | AnimationMode sampled 'Running' at 0.000 s and 0.350 s across 70 transforms; max bone rotation 61.420 deg, max local position delta 1.6879 m |
| deformation sampling: Male/Jump | PASS | AnimationMode sampled 'jump up' at 0.000 s and 0.267 s across 70 transforms; max bone rotation 25.534 deg, max local position delta 0.0407 m |
| deformation sampling: Male/Aim | PASS | AnimationMode sampled 'idle aiming' at 0.000 s and 1.050 s across 70 transforms; max bone rotation 5.293 deg, max local position delta 0.0047 m |
| deformation sampling: Male/Shoot | PASS | AnimationMode sampled 'firing rifle' at 0.000 s and 0.133 s across 70 transforms; max bone rotation 4.675 deg, max local position delta 0.0073 m |
| deformation sampling: Male/Reload | PASS | AnimationMode sampled 'reloading' at 0.000 s and 1.650 s across 70 transforms; max bone rotation 43.246 deg, max local position delta 0.0577 m |
| deformation sampling: Female/Idle | PASS | AnimationMode sampled 'idle' at 0.000 s and 1.050 s across 70 transforms; max bone rotation 4.836 deg, max local position delta 0.0081 m |
| deformation sampling: Female/Walk | PASS | AnimationMode sampled 'walking' at 0.000 s and 0.683 s across 70 transforms; max bone rotation 170.032 deg, max local position delta 0.6592 m |
| deformation sampling: Female/Run | PASS | AnimationMode sampled 'Running' at 0.000 s and 0.350 s across 70 transforms; max bone rotation 61.416 deg, max local position delta 1.6436 m |
| deformation sampling: Female/Jump | PASS | AnimationMode sampled 'jump up' at 0.000 s and 0.267 s across 70 transforms; max bone rotation 25.544 deg, max local position delta 0.0401 m |
| deformation sampling: Female/Aim | PASS | AnimationMode sampled 'idle aiming' at 0.000 s and 1.050 s across 70 transforms; max bone rotation 5.294 deg, max local position delta 0.0045 m |
| deformation sampling: Female/Shoot | PASS | AnimationMode sampled 'firing rifle' at 0.000 s and 0.133 s across 70 transforms; max bone rotation 4.675 deg, max local position delta 0.0072 m |
| deformation sampling: Female/Reload | PASS | AnimationMode sampled 'reloading' at 0.000 s and 1.650 s across 70 transforms; max bone rotation 43.246 deg, max local position delta 0.0553 m |
| PlayerTest gameplay references | PASS | Player tag/root has CharacterController, ThirdPersonMotor, PlayerController and PlayerCharacterVisual; PlayerCamera targets the Player transform |
| visible player mesh is FBX, not capsule | PASS | PlayerCharacterVisual.TrySpawn instantiated Assets/Characters/Male/Male character.fbx; 3 SkinnedMeshRenderer(s), valid Humanoid Avatar, generated controller assigned, PlayerCapsuleVisual MeshRenderer disabled |
| spawned Animator controller matches generated config | PASS | runtime Animator controller is the per-character generated controller |
| Aim Animator parameter is driven by runtime | PASS | found SetBool(IsAiming) in /home/runner/work/Battle-royal-indian-edition-/Battle-royal-indian-edition-/unity-project/Assets/Scripts/Characters/PlayerCharacterVisual.cs:110, /home/runner/work/Battle-royal-indian-edition-/Battle-royal-indian-edition-/unity-project/Assets/Scripts/Characters/PlayerCharacterVisual.cs:137 |
| Aim gameplay hook is called | PASS | found runtime caller(s): /home/runner/work/Battle-royal-indian-edition-/Battle-royal-indian-edition-/unity-project/Assets/Scripts/Characters/PlayerCharacterVisual.cs:186 |
| actual runtime aim state and touch input exist | PASS | motor AimInput/IsAiming and PlayerInputRouter/MobileControlsBuilder AIM hold-button wiring found |
| IsAiming parameter definition is not duplicated | PASS | exactly one AddParameter(CharacterAnimParameters.IsAiming) builder declaration |
| live shooting gameplay integration | NOT VERIFIED | aiming state now drives IsAiming and SetAiming, but no weapon/fire gameplay system is present; only PlayerCharacterVisual.TriggerShoot/TriggerReload animation hooks and PlayMode hook safety tests exist |
| Android-compatible rendering configuration | PASS | active target Android, URP BattleHorizonsURP, Linear, IL2CPP, ARM64, min SDK 26, ASTC, graphics APIs [Vulkan,OpenGLES3] |
| Editor shader support under current build target | INFO | 6 supported and 0 unsupported renderer/material slot(s); Editor shader.isSupported is not an Android device shader-variant test |
| animation visual quality / bad skinning / front and facial detail | NOT VERIFIED | AnimationMode bone sampling and a software-GL rear view do not prove visual quality; human inspection is still required |
| Android device runtime / shader variants / FPS / memory / thermal | NOT VERIFIED | no APK was built or installed and no physical Android device measurement was performed |

## Summary

- PASS: 72
- FAIL: 0
- NOT VERIFIED: 3
