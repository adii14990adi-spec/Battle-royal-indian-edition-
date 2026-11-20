# CI diagnostics for adfe70f419ece732fa87f87d3bba0a2334097dc1

## unity-configure.log errors
95:[Licensing::Module] Error: Access token is unavailable; failed to update
104:FMOD failed to initialize the output device.: "Error initializing output device. " (60)
114:FMOD failed to initialize the output device.: "Error initializing output device. " (60)
193:			LogAssemblyErrors (0ms)
618:			LogAssemblyErrors (0ms)
2158:UnityEngine.Debug:LogError (object,UnityEngine.Object)
2189:UnityEngine.Debug:LogError (object,UnityEngine.Object)
2212:UnityEngine.Rendering.RenderPipelineGlobalSettingsUtils:TryEnsure<UnityEngine.Rendering.Universal.UniversalRenderPipelineGlobalSettings, UnityEngine.Rendering.Universal.UniversalRenderPipeline> (UnityEngine.Rendering.Universal.UniversalRenderPipelineGlobalSettings&,string,bool,System.Exception&) (at ./Library/PackageCache/com.unity.render-pipelines.core@0a85889b8bdb/Runtime/RenderPipeline/RenderPipelineGlobalSettingsUtils.cs:126)

## unity-configure.log tail
  created folder Assets/UserAssets/Materials
  created Assets/Settings/BattleHorizonsURPRenderer.asset
  created Assets/Settings/BattleHorizonsURP.asset
  URP base asset: HDR off, MSAA 1x, 2 cascades @ 60 m, 512 px shadows, per-vertex additional lights (limit 2), SRP batcher on; runtime profiles override their budget knobs
  created Assets/Materials/SiranTerrainMat.mat
  created Assets/Materials/SiranIslandMat.mat
  created Assets/Materials/SiranChunkMat.mat
  created Assets/Materials/SiranRoadMat.mat
  created Assets/Materials/SiranWaterMat.mat
  created Assets/Materials/PlayerCapsuleMat.mat
  wrote Assets/Scenes/SiranIsland_Greybox.unity
  wrote Assets/Scenes/Boot.unity
  wrote Assets/Scenes/MainMenu/MainMenu.unity
  wrote Assets/Scenes/Gameplay/SiranIsland.unity
  wrote Assets/Scenes/Test/PlayerTest.unity
  EditorBuildSettings: 5 scenes (first = Assets/Scenes/Boot.unity)
  Android: com.aurastudios.battlehorizons, IL2CPP + ARM64, minApi 26, ASTC, landscape, no INTERNET permission
  Configured Battle Horizons default application icon (Assets/UI/Icons/battle_horizons_icon.png).
  Project configuration complete.
UnityEngine.Debug:ExtractStackTraceNoAlloc (byte*,int,string)
UnityEngine.StackTraceUtility:ExtractStackTrace () (at /home/bokken/build/output/unity/unity/Runtime/Export/Scripting/StackTrace.cs:35)
UnityEngine.DebugLogHandler:Internal_Log (UnityEngine.LogType,UnityEngine.LogOption,string,UnityEngine.Object)
UnityEngine.DebugLogHandler:LogFormat (UnityEngine.LogType,UnityEngine.Object,string,object[])
UnityEngine.Logger:Log (UnityEngine.LogType,object)
UnityEngine.Debug:Log (object)
BattleHorizons.EditorTools.ProjectBootstrap:Configure () (at Assets/Editor/ProjectBootstrap.cs:87)
BattleHorizons.EditorTools.ProjectBootstrap:ConfigureFromCommandLine () (at Assets/Editor/ProjectBootstrap.cs:101)

(Filename: Assets/Editor/ProjectBootstrap.cs Line: 87)

[bh][setup] batch-mode configuration finished
UnityEngine.Debug:ExtractStackTraceNoAlloc (byte*,int,string)
UnityEngine.StackTraceUtility:ExtractStackTrace () (at /home/bokken/build/output/unity/unity/Runtime/Export/Scripting/StackTrace.cs:35)
UnityEngine.DebugLogHandler:Internal_Log (UnityEngine.LogType,UnityEngine.LogOption,string,UnityEngine.Object)
UnityEngine.DebugLogHandler:LogFormat (UnityEngine.LogType,UnityEngine.Object,string,object[])
UnityEngine.Logger:Log (UnityEngine.LogType,object)
UnityEngine.Debug:Log (object)
BattleHorizons.EditorTools.ProjectBootstrap:ConfigureFromCommandLine () (at Assets/Editor/ProjectBootstrap.cs:102)

(Filename: Assets/Editor/ProjectBootstrap.cs Line: 102)

Killing ADB server in 0.043803 seconds.
[Physics::Module] Cleanup current backned.
[Physics::Module] Id: 0xf2b8ea05
Input System module state changed to: ShutdownInProgress.
Input System polling thread exited.
Input System module state changed to: Shutdown.
[Licensing::IpcConnector] LicenseClient-runner-notifications channel disconnected successfully.
[Licensing::IpcConnector] LicenseClient-runner channel disconnected successfully.
AcceleratorClientConnectionCallback - disconnected - :0
[40m[32minfo[39m[22m[49m: Microsoft.Hosting.Lifetime[0]
      Application is shutting down...
[40m[32minfo[39m[22m[49m: Unity.ILPP.Runner.PostProcessingAssemblyLoadContext[0]
      ALC ILPP context 1 is unloading
Cleanup mono
debugger-agent: Unable to listen on 40
[Package Manager] Server process was shutdown
Checking for leaked weakptr:
  Found no leaked weakptrs.
##utp:{"type":"MemoryLeaks","version":2,"phase":"Immediate","time":1795200458782,"processId":4453,"allocatedMemory":11801422,"memoryLabels":[{"Default":23549},{"Permanent":16080},{"Thread":1084616},{"Manager":19261},{"VertexData":8},{"Geometry":408},{"VFX":-104},{"Texture":160},{"Shader":89273},{"Material":12590},{"GfxDevice":35976},{"Animation":456},{"Audio":4032},{"Font":2241811},{"FreeType":71363},{"FontEngine":320},{"Physics":2081},{"Serialization":272},{"Input":26752},{"JobScheduler":33024},{"TextLib":160},{"Mono":32},{"MonoCode":96},{"ScriptingNativeRuntime":155096},{"BaseObject":1656408},{"Resource":1032},{"Renderer":2928},{"Transform":4678},{"File":6674},{"WebCam":80},{"Culling":32},{"Terrain":11298},{"Wind":24},{"String":267874},{"DynamicArray":109822},{"HashMap":95291},{"Utility":2658704},{"PoolAlloc":1808},{"AI":72},{"TypeTree":5005},{"ScriptManager":512},{"RuntimeInitializeOnLoadManager":64},{"SpriteAtlas":112},{"GI":7448},{"Director":8208},{"CloudService":81912},{"WebRequest":552},{"VR":45689},{"SceneManager":528},{"Video":64},{"LazyScriptCache":32},{"Camera":17},{"Secure":8049},{"SerializationCache":1576},{"APIUpdating":10320},{"Subsystems":336},{"VirtualTexturing":57552},{"StaticSafetyDebugInfo":278528},{"Analytics":464},{"Hierarchy":832},{"CoreStats":12536},{"Identifiers":6192},{"Gui":112},{"EditorUtility":27622},{"VersionControl":4},{"Undo":979},{"AssetDatabase":2571622},{"EditorGi":328},{"UnityConnect":24792},{"Upm":1852},{"DrivenProperties":72},{"LocalIPC":149},{"ProfilerEditor":9746},{"CoreBusinessMetrics":2387},{"Licensing":1128},{"AssetReference":32},{"IPCStream":32}]}

## /tmp/unity-integration.log errors
383:[bh][import] OK   [clips] 74 clip(s) registered from 74 FBX; 74 new, 0 duplicate source(s) skipped; 16 category assignment(s)
397:[bh][import] OK   [rig] Female character: 52 mapped humanoid bones, 3 skinned mesh(es), avatar is human
411:[bh][import] OK   [rig] Male character: 52 mapped humanoid bones, 3 skinned mesh(es), avatar is human
438:[bh][import] OK   [controller] controller built at Assets/AnimationControllers/female_Locomotion.controller (2 layer(s), 0 missing clip(s), 0 warning(s))
461:[bh][import] OK   [controller] controller built at Assets/AnimationControllers/male_Locomotion.controller (2 layer(s), 0 missing clip(s), 0 warning(s))
475:[bh][import] OK   [references] all references valid (2 characters)
499:[bh][integration]
518:[bh][integration] all character & animation checks passed

## /tmp/unity-integration.log tail
UnityEngine.Debug:ExtractStackTraceNoAlloc (byte*,int,string)
UnityEngine.StackTraceUtility:ExtractStackTrace () (at /home/bokken/build/output/unity/unity/Runtime/Export/Scripting/StackTrace.cs:35)
UnityEngine.DebugLogHandler:Internal_Log (UnityEngine.LogType,UnityEngine.LogOption,string,UnityEngine.Object)
UnityEngine.DebugLogHandler:LogFormat (UnityEngine.LogType,UnityEngine.Object,string,object[])
UnityEngine.Logger:Log (UnityEngine.LogType,object)
UnityEngine.Debug:Log (object)
BattleHorizons.EditorTools.CharacterAnimationIntegration:BuildFromCommandLine () (at Assets/Editor/CharacterAnimationIntegration.cs:427)

(Filename: Assets/Editor/CharacterAnimationIntegration.cs Line: 427)

[bh][integration] all character & animation checks passed
UnityEngine.Debug:ExtractStackTraceNoAlloc (byte*,int,string)
UnityEngine.StackTraceUtility:ExtractStackTrace () (at /home/bokken/build/output/unity/unity/Runtime/Export/Scripting/StackTrace.cs:35)
UnityEngine.DebugLogHandler:Internal_Log (UnityEngine.LogType,UnityEngine.LogOption,string,UnityEngine.Object)
UnityEngine.DebugLogHandler:LogFormat (UnityEngine.LogType,UnityEngine.Object,string,object[])
UnityEngine.Logger:Log (UnityEngine.LogType,object)
UnityEngine.Debug:Log (object)
BattleHorizons.EditorTools.CharacterAnimationIntegration:BuildFromCommandLine () (at Assets/Editor/CharacterAnimationIntegration.cs:441)

(Filename: Assets/Editor/CharacterAnimationIntegration.cs Line: 441)

Killing ADB server in 0.042856 seconds.
[Physics::Module] Cleanup current backned.
[Physics::Module] Id: 0xf2b8ea05
Input System module state changed to: ShutdownInProgress.
Input System polling thread exited.
Input System module state changed to: Shutdown.
[Licensing::IpcConnector] LicenseClient-runner-notifications channel disconnected successfully.
[Licensing::IpcConnector] LicenseClient-runner channel disconnected successfully.
AcceleratorClientConnectionCallback - disconnected - :0
[40m[32minfo[39m[22m[49m: Microsoft.Hosting.Lifetime[0]
      Application is shutting down...
[40m[32minfo[39m[22m[49m: Unity.ILPP.Runner.PostProcessingAssemblyLoadContext[0]
      ALC ILPP context 1 is unloading
Cleanup mono
debugger-agent: Unable to listen on 39
[Package Manager] Server process was shutdown
Checking for leaked weakptr:
  Found no leaked weakptrs.
##utp:{"type":"MemoryLeaks","version":2,"phase":"Immediate","time":1795200470503,"processId":4964,"allocatedMemory":9011239,"memoryLabels":[{"Default":23549},{"Permanent":16080},{"Thread":1084656},{"Manager":19261},{"VertexData":8},{"Geometry":408},{"VFX":-104},{"Texture":160},{"Shader":79289},{"Material":24},{"GfxDevice":35976},{"Animation":456},{"Audio":4032},{"FontEngine":320},{"Physics":2081},{"Serialization":376},{"Input":26752},{"JobScheduler":33024},{"TextLib":160},{"Mono":32},{"MonoCode":96},{"ScriptingNativeRuntime":155096},{"BaseObject":1622832},{"Resource":1032},{"Renderer":2928},{"Transform":4678},{"File":828},{"WebCam":80},{"Culling":32},{"Terrain":11298},{"Wind":24},{"String":11683},{"DynamicArray":109146},{"HashMap":77706},{"Utility":2658704},{"PoolAlloc":1808},{"AI":72},{"TypeTree":5005},{"ScriptManager":512},{"RuntimeInitializeOnLoadManager":64},{"SpriteAtlas":112},{"GI":7448},{"Director":8208},{"CloudService":7896},{"WebRequest":589},{"VR":45689},{"SceneManager":528},{"Video":64},{"LazyScriptCache":32},{"Camera":17},{"Secure":8049},{"SerializationCache":1576},{"APIUpdating":10320},{"Subsystems":336},{"VirtualTexturing":57552},{"StaticSafetyDebugInfo":278528},{"Analytics":464},{"Hierarchy":832},{"CoreStats":12536},{"Identifiers":6192},{"Gui":112},{"EditorUtility":23494},{"VersionControl":4},{"Undo":979},{"AssetDatabase":2509000},{"EditorGi":328},{"UnityConnect":24792},{"Upm":1852},{"DrivenProperties":72},{"LocalIPC":149},{"ProfilerEditor":9746},{"CoreBusinessMetrics":2387},{"Licensing":1128},{"AssetReference":32},{"IPCStream":32}]}

## /tmp/unity-validate.log errors
410:[bh][validate]
446:  FAIL runway is flat  -- worst deviation 0.568 m
493:  FAILURES:
506:[bh][validate] 1 validation failure(s)

## /tmp/unity-validate.log tail
UnityEngine.Debug:ExtractStackTraceNoAlloc (byte*,int,string)
UnityEngine.StackTraceUtility:ExtractStackTrace () (at /home/bokken/build/output/unity/unity/Runtime/Export/Scripting/StackTrace.cs:35)
UnityEngine.DebugLogHandler:Internal_Log (UnityEngine.LogType,UnityEngine.LogOption,string,UnityEngine.Object)
UnityEngine.DebugLogHandler:LogFormat (UnityEngine.LogType,UnityEngine.Object,string,object[])
UnityEngine.Logger:Log (UnityEngine.LogType,object)
UnityEngine.Debug:Log (object)
BattleHorizons.EditorTools.ProjectBootstrap:ValidateFromCommandLine () (at Assets/Editor/ProjectBootstrap.cs:122)

(Filename: Assets/Editor/ProjectBootstrap.cs Line: 122)

[bh][validate] 1 validation failure(s)
UnityEngine.Debug:ExtractStackTraceNoAlloc (byte*,int,string)
UnityEngine.StackTraceUtility:ExtractStackTrace () (at /home/bokken/build/output/unity/unity/Runtime/Export/Scripting/StackTrace.cs:35)
UnityEngine.DebugLogHandler:Internal_Log (UnityEngine.LogType,UnityEngine.LogOption,string,UnityEngine.Object)
UnityEngine.DebugLogHandler:LogFormat (UnityEngine.LogType,UnityEngine.Object,string,object[])
UnityEngine.Logger:Log (UnityEngine.LogType,object)
UnityEngine.Debug:LogError (object)
BattleHorizons.EditorTools.ProjectBootstrap:ValidateFromCommandLine () (at Assets/Editor/ProjectBootstrap.cs:125)

(Filename: Assets/Editor/ProjectBootstrap.cs Line: 125)

Killing ADB server in 0.044449 seconds.
[Physics::Module] Cleanup current backned.
[Physics::Module] Id: 0xf2b8ea05
Input System module state changed to: ShutdownInProgress.
Input System polling thread exited.
Input System module state changed to: Shutdown.
[Licensing::IpcConnector] LicenseClient-runner-notifications channel disconnected successfully.
[Licensing::IpcConnector] LicenseClient-runner channel disconnected successfully.
AcceleratorClientConnectionCallback - disconnected - :0
[40m[32minfo[39m[22m[49m: Microsoft.Hosting.Lifetime[0]
      Application is shutting down...
[40m[32minfo[39m[22m[49m: Unity.ILPP.Runner.PostProcessingAssemblyLoadContext[0]
      ALC ILPP context 1 is unloading
Cleanup mono
debugger-agent: Unable to listen on 39
[Package Manager] Server process was shutdown
Checking for leaked weakptr:
  Found no leaked weakptrs.
##utp:{"type":"MemoryLeaks","version":2,"phase":"Immediate","time":1795200541082,"processId":6132,"allocatedMemory":9026597,"memoryLabels":[{"Default":23549},{"Permanent":16080},{"Thread":1084616},{"Manager":19317},{"VertexData":8},{"Geometry":408},{"VFX":-104},{"Texture":160},{"Shader":79289},{"Material":24},{"GfxDevice":35976},{"Animation":456},{"Audio":4032},{"FontEngine":320},{"Physics":2081},{"Serialization":376},{"Input":26752},{"JobScheduler":33024},{"TextLib":160},{"Mono":32},{"MonoCode":96},{"ScriptingNativeRuntime":155096},{"BaseObject":1622832},{"Resource":1032},{"Renderer":2928},{"Transform":4678},{"File":15449},{"WebCam":80},{"Culling":32},{"Terrain":11298},{"Wind":24},{"String":13516},{"DynamicArray":109146},{"HashMap":77739},{"Utility":2658704},{"PoolAlloc":1808},{"AI":72},{"TypeTree":5005},{"ScriptManager":512},{"RuntimeInitializeOnLoadManager":64},{"SpriteAtlas":112},{"GI":7448},{"Director":8208},{"CloudService":7752},{"WebRequest":552},{"VR":45689},{"SceneManager":528},{"Video":64},{"LazyScriptCache":32},{"Camera":17},{"Secure":8049},{"SerializationCache":1576},{"APIUpdating":10320},{"Subsystems":336},{"VirtualTexturing":57552},{"StaticSafetyDebugInfo":278528},{"Analytics":464},{"Hierarchy":832},{"CoreStats":12536},{"Identifiers":6192},{"Gui":112},{"EditorUtility":24006},{"VersionControl":4},{"Undo":979},{"AssetDatabase":2507524},{"EditorGi":328},{"UnityConnect":24792},{"Upm":1852},{"DrivenProperties":72},{"LocalIPC":149},{"ProfilerEditor":9746},{"CoreBusinessMetrics":2387},{"Licensing":1128},{"AssetReference":32},{"IPCStream":32}]}

## unity-test-results/editmode/unity.log errors
1369:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
1402:[bh][import] WARN [materials] 3 material slot(s) have no texture (see above)
1423:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
1456:[bh][import] OK   [mesh] 3 mesh(es), 8477 vertices, 195 bone weights bound, UVs present
1477:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
1523:[bh][import] FAIL [destination] Assets/Characters/PipelineTest/bh_male_dedupe_test.fbx already exists; refusing to overwrite without interactive confirmation (batch mode)
1543:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
1623:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
1660:[bh][import] OK   [import] FBX imported to Assets/Characters/PipelineTest/bh_male_import_test.fbx
1680:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
1713:[bh][import] WARN [duplicate] identical file content was already imported — continuing so the slot stays valid
1733:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
1793:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
1830:[bh][import] OK   [rig] humanoid auto-map: Humanoid avatar valid (52 human bones mapped, 69 skeleton bones)
1850:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
1883:[bh][import] WARN [textures] model declares no embedded textures
1904:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
1937:[bh][import] WARN [textures] no texture assets available for Assets/Characters/PipelineTest/bh_male_import_test.fbx — materials will be created without base maps
1958:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
1991:[bh][import] WARN [materials] missing texture for material 'MI_Eyes' — add the file to Textures/ or map it manually in the material
2012:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
2048:[bh][import] WARN [materials] missing texture for material 'MI_Hair_1' — add the file to Textures/ or map it manually in the material
2069:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
2105:[bh][import] WARN [materials] missing texture for material 'MI_Superhero_Male' — add the file to Textures/ or map it manually in the material
2126:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
2189:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
2226:[bh][import] OK   [materials] 3 URP material(s) created, 3 slot(s) remapped (assignments preserved)
2247:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
2280:[bh][import] WARN [materials] 3 material slot(s) have no texture (see above)
2301:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
2334:[bh][import] OK   [mesh] 3 mesh(es), 8477 vertices, 195 bone weights bound, UVs present
2355:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
2430:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
2498:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
2563:[bh][import] OK   [repair] 0 of 74 animation file(s) repaired under Assets/Animations; 0 explicit Generic rig(s) kept
2583:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
2616:[bh][import] OK   [repair] 0 of 74 animation file(s) repaired under Assets/Animations; 0 explicit Generic rig(s) kept
2636:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
2701:[bh][gltf-test] built isolated scene Assets/Characters/GLTF_Import_Test/GLTF_Player_Import_Test.unity
2754:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)

## unity-test-results/editmode/unity.log tail
UnityEditor.TestTools.TestRunner.TestRun.Tasks.CleanupVerificationTask:LogWarningForFilesIfAny (string[],bool) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEditor.TestRunner/TestRun/Tasks/CleanupVerificationTask.cs:55)
UnityEditor.TestTools.TestRunner.TestRun.Tasks.CleanupVerificationTask/<Execute>d__3:MoveNext () (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEditor.TestRunner/TestRun/Tasks/CleanupVerificationTask.cs:27)
UnityEditor.TestTools.TestRunner.TestRun.TestJobRunner:ExecuteStep () (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEditor.TestRunner/TestRun/TestJobRunner.cs:173)
UnityEditor.TestTools.TestRunner.TestRun.TestJobRunner:ExecuteCallback () (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEditor.TestRunner/TestRun/TestJobRunner.cs:108)
UnityEditor.EditorApplication:Internal_CallUpdateFunctions () (at /home/bokken/build/output/unity/unity/Editor/Mono/EditorApplication.cs:384)

(Filename: ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEditor.TestRunner/TestRun/Tasks/CleanupVerificationTask.cs Line: 55)

Test run completed. Exiting with code 0 (Ok). Run completed.
UnityEngine.Debug:ExtractStackTraceNoAlloc (byte*,int,string)
UnityEngine.StackTraceUtility:ExtractStackTrace () (at /home/bokken/build/output/unity/unity/Runtime/Export/Scripting/StackTrace.cs:35)
UnityEngine.DebugLogHandler:Internal_Log (UnityEngine.LogType,UnityEngine.LogOption,string,UnityEngine.Object)
UnityEngine.DebugLogHandler:LogFormat (UnityEngine.LogType,UnityEngine.Object,string,object[])
UnityEngine.Logger:Log (UnityEngine.LogType,object)
UnityEngine.Debug:Log (object)
UnityEditor.TestTools.TestRunner.CommandLineTest.Executer:ExitApplication (UnityEditor.TestTools.TestRunner.CommandLineTest.Executer/ReturnCodes,string) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEditor.TestRunner/CommandLineTest/Executer.cs:81)
UnityEditor.TestTools.TestRunner.CommandLineTest.Executer:ExitIfRunIsCompleted () (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEditor.TestRunner/CommandLineTest/Executer.cs:74)
UnityEditor.EditorApplication:Internal_CallUpdateFunctions () (at /home/bokken/build/output/unity/unity/Editor/Mono/EditorApplication.cs:384)

(Filename: ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEditor.TestRunner/CommandLineTest/Executer.cs Line: 81)

Killing ADB server in 0.046386 seconds.
[Physics::Module] Cleanup current backned.
[Physics::Module] Id: 0xf2b8ea05
Input System module state changed to: ShutdownInProgress.
Input System polling thread exited.
Input System module state changed to: Shutdown.
[Licensing::IpcConnector] LicenseClient-runner-notifications channel disconnected successfully.
[Licensing::IpcConnector] LicenseClient-runner channel disconnected successfully.
AcceleratorClientConnectionCallback - disconnected - :0
[40m[32minfo[39m[22m[49m: Microsoft.Hosting.Lifetime[0]
      Application is shutting down...
[40m[32minfo[39m[22m[49m: Unity.ILPP.Runner.PostProcessingAssemblyLoadContext[0]
      ALC ILPP context 1 is unloading
Cleanup mono
debugger-agent: Unable to listen on 39
[Package Manager] Server process was shutdown
Checking for leaked weakptr:
  Found no leaked weakptrs.
##utp:{"type":"MemoryLeaks","version":2,"phase":"Immediate","time":1795200507613,"processId":5732,"allocatedMemory":11330517,"memoryLabels":[{"Default":13965},{"Permanent":16080},{"Thread":1084616},{"Manager":19285},{"VertexData":8},{"Geometry":408},{"VFX":-104},{"Texture":296},{"Shader":79289},{"Material":2532},{"GfxDevice":35976},{"Animation":456},{"Audio":4032},{"Font":2241811},{"FreeType":71363},{"FontEngine":320},{"Physics":2081},{"Serialization":376},{"Input":26752},{"JobScheduler":33024},{"TextLib":160},{"Mono":32},{"MonoCode":96},{"ScriptingNativeRuntime":155096},{"BaseObject":1631568},{"Resource":1032},{"Renderer":2928},{"Transform":4678},{"File":6670},{"WebCam":80},{"Culling":32},{"Terrain":11298},{"Wind":24},{"String":11638},{"DynamicArray":109815},{"HashMap":82314},{"Utility":2658704},{"PoolAlloc":1808},{"AI":72},{"TypeTree":5005},{"ScriptManager":512},{"RuntimeInitializeOnLoadManager":64},{"SpriteAtlas":112},{"GI":7448},{"Director":8208},{"WebRequest":552},{"VR":45689},{"SceneManager":528},{"Video":64},{"LazyScriptCache":32},{"NativeArray":540},{"Camera":17},{"Secure":265},{"SerializationCache":1576},{"APIUpdating":10320},{"Subsystems":336},{"VirtualTexturing":57552},{"StaticSafetyDebugInfo":278528},{"Analytics":336},{"Hierarchy":832},{"CoreStats":12536},{"Identifiers":6192},{"Gui":112},{"EditorUtility":27630},{"VersionControl":4},{"Undo":979},{"AssetDatabase":2513392},{"EditorGi":328},{"UnityConnect":24816},{"Upm":1852},{"DrivenProperties":72},{"LocalIPC":149},{"ProfilerEditor":9746},{"CoreBusinessMetrics":2390},{"Licensing":1128},{"AssetReference":32},{"IPCStream":32}]}

## unity-test-results/playmode/unity.log errors
1674:[bh][world] road ribbons built: 36 meshes, 2076 triangles
1691:UnityEngine.TestTools.EnumerableTestMethodCommand/<ExecuteEnumerableAndRecordExceptions>d__4:MoveNext () (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Commands/EnumerableTestMethodCommand.cs:79)
1708:[bh][stream] map 2000x2000 m, 64 tiles of 250 m, circular radius 50 m
1725:UnityEngine.TestTools.EnumerableTestMethodCommand/<ExecuteEnumerableAndRecordExceptions>d__4:MoveNext () (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Commands/EnumerableTestMethodCommand.cs:79)
1742:[bh][world] Siran Island loaded: 401 x 401 m grid, 64 chunks, 36 road ribbons, river ribbon 
1758:UnityEngine.TestTools.EnumerableTestMethodCommand/<ExecuteEnumerableAndRecordExceptions>d__4:MoveNext () (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Commands/EnumerableTestMethodCommand.cs:79)
1775:[bh][world] terrain collision built: 513x513 heightmap, heights -12.0..23.3 m, Terrain renderer hidden
1791:UnityEngine.TestTools.EnumerableTestMethodCommand/<ExecuteEnumerableAndRecordExceptions>d__4:MoveNext () (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Commands/EnumerableTestMethodCommand.cs:79)
1808:[bh][world] island overview built: 2000 x 2000 m base, 40401 vertices, 80000 triangles, matte (metallic 0, smoothness 0)
1825:UnityEngine.TestTools.EnumerableTestMethodCommand/<ExecuteEnumerableAndRecordExceptions>d__4:MoveNext () (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Commands/EnumerableTestMethodCommand.cs:79)
1842:[bh][world] road collision built: 2076 triangles in one static MeshCollider
1859:UnityEngine.TestTools.EnumerableTestMethodCommand/<ExecuteEnumerableAndRecordExceptions>d__4:MoveNext () (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Commands/EnumerableTestMethodCommand.cs:79)
1876:[bh][world] road ribbons built: 36 meshes, 2076 triangles
1893:UnityEngine.TestTools.EnumerableTestMethodCommand/<ExecuteEnumerableAndRecordExceptions>d__4:MoveNext () (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Commands/EnumerableTestMethodCommand.cs:79)
1910:[bh][stream] map 2000x2000 m, 64 tiles of 250 m, circular radius 50 m
1927:UnityEngine.TestTools.EnumerableTestMethodCommand/<ExecuteEnumerableAndRecordExceptions>d__4:MoveNext () (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Commands/EnumerableTestMethodCommand.cs:79)
1944:[bh][world] Siran Island loaded: 401 x 401 m grid, 64 chunks, 36 road ribbons, river ribbon 
1960:UnityEngine.TestTools.EnumerableTestMethodCommand/<ExecuteEnumerableAndRecordExceptions>d__4:MoveNext () (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Commands/EnumerableTestMethodCommand.cs:79)
1977:[bh][world] terrain collision built: 513x513 heightmap, heights -12.0..23.3 m, Terrain renderer hidden
1993:UnityEngine.TestTools.EnumerableTestMethodCommand/<ExecuteEnumerableAndRecordExceptions>d__4:MoveNext () (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Commands/EnumerableTestMethodCommand.cs:79)
2010:[bh][world] island overview built: 2000 x 2000 m base, 40401 vertices, 80000 triangles, matte (metallic 0, smoothness 0)
2027:UnityEngine.TestTools.EnumerableTestMethodCommand/<ExecuteEnumerableAndRecordExceptions>d__4:MoveNext () (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Commands/EnumerableTestMethodCommand.cs:79)
2044:[bh][world] road collision built: 2076 triangles in one static MeshCollider
2061:UnityEngine.TestTools.EnumerableTestMethodCommand/<ExecuteEnumerableAndRecordExceptions>d__4:MoveNext () (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Commands/EnumerableTestMethodCommand.cs:79)
2078:[bh][world] road ribbons built: 36 meshes, 2076 triangles
2095:UnityEngine.TestTools.EnumerableTestMethodCommand/<ExecuteEnumerableAndRecordExceptions>d__4:MoveNext () (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Commands/EnumerableTestMethodCommand.cs:79)
2112:[bh][stream] map 2000x2000 m, 64 tiles of 250 m, circular radius 50 m
2129:UnityEngine.TestTools.EnumerableTestMethodCommand/<ExecuteEnumerableAndRecordExceptions>d__4:MoveNext () (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Commands/EnumerableTestMethodCommand.cs:79)
2146:[bh][world] Siran Island loaded: 401 x 401 m grid, 64 chunks, 36 road ribbons, river ribbon 
2162:UnityEngine.TestTools.EnumerableTestMethodCommand/<ExecuteEnumerableAndRecordExceptions>d__4:MoveNext () (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Commands/EnumerableTestMethodCommand.cs:79)
2179:[bh][world] terrain collision built: 513x513 heightmap, heights -12.0..23.3 m, Terrain renderer hidden
2195:UnityEngine.TestTools.EnumerableTestMethodCommand/<ExecuteEnumerableAndRecordExceptions>d__4:MoveNext () (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Commands/EnumerableTestMethodCommand.cs:79)
2212:[bh][world] island overview built: 2000 x 2000 m base, 40401 vertices, 80000 triangles, matte (metallic 0, smoothness 0)
2229:UnityEngine.TestTools.EnumerableTestMethodCommand/<ExecuteEnumerableAndRecordExceptions>d__4:MoveNext () (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Commands/EnumerableTestMethodCommand.cs:79)
2246:[bh][world] road collision built: 2076 triangles in one static MeshCollider
2263:UnityEngine.TestTools.EnumerableTestMethodCommand/<ExecuteEnumerableAndRecordExceptions>d__4:MoveNext () (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Commands/EnumerableTestMethodCommand.cs:79)
2280:[bh][world] road ribbons built: 36 meshes, 2076 triangles
2297:UnityEngine.TestTools.EnumerableTestMethodCommand/<ExecuteEnumerableAndRecordExceptions>d__4:MoveNext () (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Commands/EnumerableTestMethodCommand.cs:79)
2314:[bh][stream] map 2000x2000 m, 64 tiles of 250 m, circular radius 50 m
2331:UnityEngine.TestTools.EnumerableTestMethodCommand/<ExecuteEnumerableAndRecordExceptions>d__4:MoveNext () (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Commands/EnumerableTestMethodCommand.cs:79)

## unity-test-results/playmode/unity.log tail
Refreshing native plugins compatible for Editor in 0.15 ms, found 0 plugins.
Preloading 0 native plugins for Editor in 0.00 ms.
Asset Pipeline Refresh (id=f7022c43d3e874342a4a33509f1c89c5): Total: 0.022 seconds - Initiated by StopAssetImportingV2(NoUpdateAssetOptions)
DisplayProgressbar: Undo
Test run completed. Exiting with code 0 (Ok). Run completed.
UnityEngine.Debug:ExtractStackTraceNoAlloc (byte*,int,string)
UnityEngine.StackTraceUtility:ExtractStackTrace () (at /home/bokken/build/output/unity/unity/Runtime/Export/Scripting/StackTrace.cs:35)
UnityEngine.DebugLogHandler:Internal_Log (UnityEngine.LogType,UnityEngine.LogOption,string,UnityEngine.Object)
UnityEngine.DebugLogHandler:LogFormat (UnityEngine.LogType,UnityEngine.Object,string,object[])
UnityEngine.Logger:Log (UnityEngine.LogType,object)
UnityEngine.Debug:Log (object)
UnityEditor.TestTools.TestRunner.CommandLineTest.Executer:ExitApplication (UnityEditor.TestTools.TestRunner.CommandLineTest.Executer/ReturnCodes,string) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEditor.TestRunner/CommandLineTest/Executer.cs:81)
UnityEditor.TestTools.TestRunner.CommandLineTest.Executer:ExitIfRunIsCompleted () (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEditor.TestRunner/CommandLineTest/Executer.cs:74)
UnityEditor.EditorApplication:Internal_CallUpdateFunctions () (at /home/bokken/build/output/unity/unity/Editor/Mono/EditorApplication.cs:384)

(Filename: ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEditor.TestRunner/CommandLineTest/Executer.cs Line: 81)

Killing ADB server in 0.046009 seconds.
Start importing ProjectSettings/ProjectSettings.asset using Guid(00000000000000004000000000000000) (LibraryAssetImporter) -> (artifact id: '0df84219137e6b01ed8e679a6a24438e') in 0.001652466 seconds
Refreshing native plugins compatible for Editor in 0.13 ms, found 0 plugins.
Preloading 0 native plugins for Editor in 0.00 ms.
Asset Pipeline Refresh (id=695292128b1414cfcbae74a9f91faad8): Total: 0.009 seconds - Initiated by StopAssetImportingV2(NoUpdateAssetOptions)
[Physics::Module] Cleanup current backned.
[Physics::Module] Id: 0xf2b8ea05
Input System module state changed to: ShutdownInProgress.
Input System polling thread exited.
Input System module state changed to: Shutdown.
[Licensing::IpcConnector] LicenseClient-runner-notifications channel disconnected successfully.
[Licensing::IpcConnector] LicenseClient-runner channel disconnected successfully.
AcceleratorClientConnectionCallback - disconnected - :0
[40m[32minfo[39m[22m[49m: Microsoft.Hosting.Lifetime[0]
      Application is shutting down...
[40m[32minfo[39m[22m[49m: Unity.ILPP.Runner.PostProcessingAssemblyLoadContext[0]
      ALC ILPP context 1 is unloading
Cleanup mono
debugger-agent: Unable to listen on 39
[Package Manager] Server process was shutdown
Checking for leaked weakptr:
  Found no leaked weakptrs.
##utp:{"type":"MemoryLeaks","version":2,"phase":"Immediate","time":1795200534356,"processId":5926,"allocatedMemory":12124193,"memoryLabels":[{"Default":13965},{"Permanent":16080},{"Thread":1084616},{"Manager":806037},{"VertexData":8},{"Geometry":408},{"VFX":-104},{"Texture":296},{"Shader":79289},{"Material":2532},{"GfxDevice":35976},{"Animation":456},{"Audio":4032},{"Font":2241811},{"FreeType":71363},{"FontEngine":320},{"Physics":2081},{"Serialization":376},{"Input":26752},{"JobScheduler":33024},{"TextLib":160},{"Mono":32},{"MonoCode":96},{"ScriptingNativeRuntime":155096},{"BaseObject":1637392},{"Resource":1032},{"Renderer":2928},{"Transform":4678},{"File":6670},{"WebCam":80},{"Culling":32},{"Terrain":11298},{"Wind":24},{"String":11638},{"DynamicArray":109123},{"HashMap":83850},{"Utility":2658704},{"PoolAlloc":1904},{"AI":72},{"TypeTree":5005},{"ScriptManager":512},{"RuntimeInitializeOnLoadManager":64},{"SpriteAtlas":112},{"GI":7448},{"Director":8208},{"WebRequest":552},{"VR":45689},{"SceneManager":528},{"Video":64},{"LazyScriptCache":32},{"NativeArray":540},{"Camera":17},{"Secure":265},{"SerializationCache":1576},{"APIUpdating":10320},{"Subsystems":336},{"VirtualTexturing":57552},{"StaticSafetyDebugInfo":278528},{"Analytics":336},{"Hierarchy":832},{"CoreStats":12536},{"Identifiers":6192},{"Gui":112},{"EditorUtility":27790},{"VersionControl":4},{"Undo":979},{"AssetDatabase":2513392},{"EditorGi":328},{"UnityConnect":24816},{"Upm":1852},{"DrivenProperties":72},{"LocalIPC":149},{"ProfilerEditor":9746},{"CoreBusinessMetrics":2390},{"Licensing":1128},{"AssetReference":32},{"IPCStream":32}]}

## failed test cases
xml files found: 2
--- unity-test-results/editmode/results.xml
--- unity-test-results/playmode/results.xml

## editmode unity.log tail
UnityEditor.TestTools.TestRunner.TestRun.Tasks.CleanupVerificationTask:LogWarningForFilesIfAny (string[],bool) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEditor.TestRunner/TestRun/Tasks/CleanupVerificationTask.cs:55)
UnityEditor.TestTools.TestRunner.TestRun.Tasks.CleanupVerificationTask/<Execute>d__3:MoveNext () (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEditor.TestRunner/TestRun/Tasks/CleanupVerificationTask.cs:27)
UnityEditor.TestTools.TestRunner.TestRun.TestJobRunner:ExecuteStep () (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEditor.TestRunner/TestRun/TestJobRunner.cs:173)
UnityEditor.TestTools.TestRunner.TestRun.TestJobRunner:ExecuteCallback () (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEditor.TestRunner/TestRun/TestJobRunner.cs:108)
UnityEditor.EditorApplication:Internal_CallUpdateFunctions () (at /home/bokken/build/output/unity/unity/Editor/Mono/EditorApplication.cs:384)

(Filename: ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEditor.TestRunner/TestRun/Tasks/CleanupVerificationTask.cs Line: 55)

Test run completed. Exiting with code 0 (Ok). Run completed.
UnityEngine.Debug:ExtractStackTraceNoAlloc (byte*,int,string)
UnityEngine.StackTraceUtility:ExtractStackTrace () (at /home/bokken/build/output/unity/unity/Runtime/Export/Scripting/StackTrace.cs:35)
UnityEngine.DebugLogHandler:Internal_Log (UnityEngine.LogType,UnityEngine.LogOption,string,UnityEngine.Object)
UnityEngine.DebugLogHandler:LogFormat (UnityEngine.LogType,UnityEngine.Object,string,object[])
UnityEngine.Logger:Log (UnityEngine.LogType,object)
UnityEngine.Debug:Log (object)
UnityEditor.TestTools.TestRunner.CommandLineTest.Executer:ExitApplication (UnityEditor.TestTools.TestRunner.CommandLineTest.Executer/ReturnCodes,string) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEditor.TestRunner/CommandLineTest/Executer.cs:81)
UnityEditor.TestTools.TestRunner.CommandLineTest.Executer:ExitIfRunIsCompleted () (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEditor.TestRunner/CommandLineTest/Executer.cs:74)
UnityEditor.EditorApplication:Internal_CallUpdateFunctions () (at /home/bokken/build/output/unity/unity/Editor/Mono/EditorApplication.cs:384)

(Filename: ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEditor.TestRunner/CommandLineTest/Executer.cs Line: 81)

Killing ADB server in 0.046386 seconds.
[Physics::Module] Cleanup current backned.
[Physics::Module] Id: 0xf2b8ea05
Input System module state changed to: ShutdownInProgress.
Input System polling thread exited.
Input System module state changed to: Shutdown.
[Licensing::IpcConnector] LicenseClient-runner-notifications channel disconnected successfully.
[Licensing::IpcConnector] LicenseClient-runner channel disconnected successfully.
AcceleratorClientConnectionCallback - disconnected - :0
[40m[32minfo[39m[22m[49m: Microsoft.Hosting.Lifetime[0]
      Application is shutting down...
[40m[32minfo[39m[22m[49m: Unity.ILPP.Runner.PostProcessingAssemblyLoadContext[0]
      ALC ILPP context 1 is unloading
Cleanup mono
debugger-agent: Unable to listen on 39
[Package Manager] Server process was shutdown
Checking for leaked weakptr:
  Found no leaked weakptrs.
##utp:{"type":"MemoryLeaks","version":2,"phase":"Immediate","time":1795200507613,"processId":5732,"allocatedMemory":11330517,"memoryLabels":[{"Default":13965},{"Permanent":16080},{"Thread":1084616},{"Manager":19285},{"VertexData":8},{"Geometry":408},{"VFX":-104},{"Texture":296},{"Shader":79289},{"Material":2532},{"GfxDevice":35976},{"Animation":456},{"Audio":4032},{"Font":2241811},{"FreeType":71363},{"FontEngine":320},{"Physics":2081},{"Serialization":376},{"Input":26752},{"JobScheduler":33024},{"TextLib":160},{"Mono":32},{"MonoCode":96},{"ScriptingNativeRuntime":155096},{"BaseObject":1631568},{"Resource":1032},{"Renderer":2928},{"Transform":4678},{"File":6670},{"WebCam":80},{"Culling":32},{"Terrain":11298},{"Wind":24},{"String":11638},{"DynamicArray":109815},{"HashMap":82314},{"Utility":2658704},{"PoolAlloc":1808},{"AI":72},{"TypeTree":5005},{"ScriptManager":512},{"RuntimeInitializeOnLoadManager":64},{"SpriteAtlas":112},{"GI":7448},{"Director":8208},{"WebRequest":552},{"VR":45689},{"SceneManager":528},{"Video":64},{"LazyScriptCache":32},{"NativeArray":540},{"Camera":17},{"Secure":265},{"SerializationCache":1576},{"APIUpdating":10320},{"Subsystems":336},{"VirtualTexturing":57552},{"StaticSafetyDebugInfo":278528},{"Analytics":336},{"Hierarchy":832},{"CoreStats":12536},{"Identifiers":6192},{"Gui":112},{"EditorUtility":27630},{"VersionControl":4},{"Undo":979},{"AssetDatabase":2513392},{"EditorGi":328},{"UnityConnect":24816},{"Upm":1852},{"DrivenProperties":72},{"LocalIPC":149},{"ProfilerEditor":9746},{"CoreBusinessMetrics":2390},{"Licensing":1128},{"AssetReference":32},{"IPCStream":32}]}
