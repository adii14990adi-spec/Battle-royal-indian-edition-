# CI diagnostics for fd5ce4caa2dc1520dfb605b11b2a3dffd6c5bab3

## unity-configure.log errors
92:[Licensing::Module] Error: Access token is unavailable; failed to update
113:FMOD failed to initialize the output device.: "Error initializing output device. " (60)
123:FMOD failed to initialize the output device.: "Error initializing output device. " (60)
191:			LogAssemblyErrors (0ms)
1609:			LogAssemblyErrors (0ms)
1743:UnityEngine.Debug:LogError (object,UnityEngine.Object)
1770:UnityEngine.Debug:LogError (object,UnityEngine.Object)
1793:UnityEngine.Rendering.RenderPipelineGlobalSettingsUtils:TryEnsure<UnityEngine.Rendering.Universal.UniversalRenderPipelineGlobalSettings, UnityEngine.Rendering.Universal.UniversalRenderPipeline> (UnityEngine.Rendering.Universal.UniversalRenderPipelineGlobalSettings&,string,bool,System.Exception&) (at ./Library/PackageCache/com.unity.render-pipelines.core@0a85889b8bdb/Runtime/RenderPipeline/RenderPipelineGlobalSettingsUtils.cs:126)

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

Killing ADB server in 0.091014 seconds.
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
##utp:{"type":"MemoryLeaks","version":2,"phase":"Immediate","time":1795265356259,"processId":4540,"allocatedMemory":11621067,"memoryLabels":[{"Default":23549},{"Permanent":16080},{"Thread":1084616},{"Manager":19261},{"VertexData":8},{"Geometry":408},{"VFX":-104},{"Texture":160},{"Shader":89273},{"Material":12590},{"GfxDevice":35976},{"Animation":456},{"Audio":4032},{"Font":2241811},{"FreeType":71363},{"FontEngine":320},{"Physics":2081},{"Serialization":272},{"Input":26752},{"JobScheduler":33024},{"TextLib":160},{"Mono":32},{"MonoCode":96},{"ScriptingNativeRuntime":155096},{"BaseObject":1656408},{"Resource":1032},{"Renderer":2928},{"Transform":4678},{"File":6674},{"WebCam":80},{"Culling":32},{"Terrain":11298},{"Wind":24},{"String":131170},{"DynamicArray":99227},{"HashMap":95291},{"Utility":2658704},{"PoolAlloc":1808},{"AI":72},{"TypeTree":5005},{"ScriptManager":512},{"RuntimeInitializeOnLoadManager":64},{"SpriteAtlas":112},{"GI":7448},{"Director":8208},{"CloudService":49368},{"WebRequest":552},{"VR":45689},{"SceneManager":528},{"Video":64},{"LazyScriptCache":32},{"Camera":17},{"Secure":8049},{"SerializationCache":1576},{"APIUpdating":10320},{"Subsystems":336},{"VirtualTexturing":57552},{"StaticSafetyDebugInfo":278528},{"Analytics":464},{"Hierarchy":832},{"CoreStats":12536},{"Identifiers":6192},{"Gui":112},{"EditorUtility":27110},{"VersionControl":4},{"Undo":979},{"AssetDatabase":2571622},{"EditorGi":328},{"UnityConnect":24792},{"Upm":1852},{"DrivenProperties":72},{"LocalIPC":149},{"ProfilerEditor":9746},{"CoreBusinessMetrics":2387},{"Licensing":1128},{"AssetReference":32},{"IPCStream":32}]}

## /tmp/unity-integration.log errors
383:[bh][import] OK   [clips] no animation FBX under Assets/Animations (animation library removed 2026-11-20)
397:[bh][import] OK   [rig] Female character: 52 mapped humanoid bones, 3 skinned mesh(es), avatar is human
411:[bh][import] OK   [rig] Male character: 52 mapped humanoid bones, 3 skinned mesh(es), avatar is human
435:[bh][integration]
451:[bh][integration] all character checks passed

## /tmp/unity-integration.log tail
UnityEngine.Debug:ExtractStackTraceNoAlloc (byte*,int,string)
UnityEngine.StackTraceUtility:ExtractStackTrace () (at /home/bokken/build/output/unity/unity/Runtime/Export/Scripting/StackTrace.cs:35)
UnityEngine.DebugLogHandler:Internal_Log (UnityEngine.LogType,UnityEngine.LogOption,string,UnityEngine.Object)
UnityEngine.DebugLogHandler:LogFormat (UnityEngine.LogType,UnityEngine.Object,string,object[])
UnityEngine.Logger:Log (UnityEngine.LogType,object)
UnityEngine.Debug:Log (object)
BattleHorizons.EditorTools.CharacterAnimationIntegration:BuildFromCommandLine () (at Assets/Editor/CharacterAnimationIntegration.cs:230)

(Filename: Assets/Editor/CharacterAnimationIntegration.cs Line: 230)

[bh][integration] all character checks passed
UnityEngine.Debug:ExtractStackTraceNoAlloc (byte*,int,string)
UnityEngine.StackTraceUtility:ExtractStackTrace () (at /home/bokken/build/output/unity/unity/Runtime/Export/Scripting/StackTrace.cs:35)
UnityEngine.DebugLogHandler:Internal_Log (UnityEngine.LogType,UnityEngine.LogOption,string,UnityEngine.Object)
UnityEngine.DebugLogHandler:LogFormat (UnityEngine.LogType,UnityEngine.Object,string,object[])
UnityEngine.Logger:Log (UnityEngine.LogType,object)
UnityEngine.Debug:Log (object)
BattleHorizons.EditorTools.CharacterAnimationIntegration:BuildFromCommandLine () (at Assets/Editor/CharacterAnimationIntegration.cs:244)

(Filename: Assets/Editor/CharacterAnimationIntegration.cs Line: 244)

Killing ADB server in 0.117335 seconds.
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
##utp:{"type":"MemoryLeaks","version":2,"phase":"Immediate","time":1795265369002,"processId":5535,"allocatedMemory":9000603,"memoryLabels":[{"Default":23549},{"Permanent":16080},{"Thread":1084656},{"Manager":19261},{"VertexData":8},{"Geometry":408},{"VFX":-104},{"Texture":160},{"Shader":79289},{"Material":24},{"GfxDevice":35976},{"Animation":456},{"Audio":4032},{"FontEngine":320},{"Physics":2081},{"Serialization":376},{"Input":26752},{"JobScheduler":33024},{"TextLib":160},{"Mono":32},{"MonoCode":96},{"ScriptingNativeRuntime":155096},{"BaseObject":1622832},{"Resource":1032},{"Renderer":2928},{"Transform":4678},{"File":828},{"WebCam":80},{"Culling":32},{"Terrain":11298},{"Wind":24},{"String":11682},{"DynamicArray":98551},{"HashMap":77706},{"Utility":2658704},{"PoolAlloc":1808},{"AI":72},{"TypeTree":5005},{"ScriptManager":512},{"RuntimeInitializeOnLoadManager":64},{"SpriteAtlas":112},{"GI":7448},{"Director":8208},{"CloudService":7896},{"WebRequest":589},{"VR":45689},{"SceneManager":528},{"Video":64},{"LazyScriptCache":32},{"Camera":17},{"Secure":8049},{"SerializationCache":1576},{"APIUpdating":10320},{"Subsystems":336},{"VirtualTexturing":57552},{"StaticSafetyDebugInfo":278528},{"Analytics":464},{"Hierarchy":832},{"CoreStats":12536},{"Identifiers":6192},{"Gui":112},{"EditorUtility":23494},{"VersionControl":4},{"Undo":979},{"AssetDatabase":2508960},{"EditorGi":328},{"UnityConnect":24792},{"Upm":1852},{"DrivenProperties":72},{"LocalIPC":149},{"ProfilerEditor":9746},{"CoreBusinessMetrics":2387},{"Licensing":1128},{"AssetReference":32},{"IPCStream":32}]}

## unity-test-results/editmode/unity.log errors
1349:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
1382:[bh][import] OK   [mesh] 3 mesh(es), 8477 vertices, 195 bone weights bound, UVs present
1403:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
1449:[bh][import] FAIL [destination] Assets/Characters/PipelineTest/bh_male_dedupe_test.fbx already exists; refusing to overwrite without interactive confirmation (batch mode)
1469:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
1549:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
1621:[bh][import] OK   [import] FBX imported to Assets/Characters/PipelineTest/bh_male_import_test.fbx
1641:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
1674:[bh][import] WARN [duplicate] identical file content was already imported — continuing so the slot stays valid
1694:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
1754:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
1791:[bh][import] OK   [rig] humanoid auto-map: Humanoid avatar valid (52 human bones mapped, 69 skeleton bones)
1811:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
1844:[bh][import] WARN [textures] model declares no embedded textures
1865:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
1898:[bh][import] WARN [textures] no texture assets available for Assets/Characters/PipelineTest/bh_male_import_test.fbx — materials will be created without base maps
1919:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
1952:[bh][import] WARN [materials] missing texture for material 'MI_Eyes' — add the file to Textures/ or map it manually in the material
1973:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
2009:[bh][import] WARN [materials] missing texture for material 'MI_Hair_1' — add the file to Textures/ or map it manually in the material
2030:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
2066:[bh][import] WARN [materials] missing texture for material 'MI_Superhero_Male' — add the file to Textures/ or map it manually in the material
2087:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
2150:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
2187:[bh][import] OK   [materials] 3 URP material(s) created, 3 slot(s) remapped (assignments preserved)
2208:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
2241:[bh][import] WARN [materials] 3 material slot(s) have no texture (see above)
2262:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
2295:[bh][import] OK   [mesh] 3 mesh(es), 8477 vertices, 195 bone weights bound, UVs present
2316:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
2391:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
2492:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
2569:[bh][import] OK   [clips] no animation FBX under Assets/Animations (animation library removed 2026-11-20)
2589:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
2622:[bh][import] OK   [rig] Female character: 52 mapped humanoid bones, 3 skinned mesh(es), avatar is human
2642:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
2675:[bh][import] OK   [rig] Male character: 52 mapped humanoid bones, 3 skinned mesh(es), avatar is human
2695:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
2749:[bh][gltf-test] built isolated scene Assets/Characters/GLTF_Import_Test/GLTF_Player_Import_Test.unity
2802:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)

## unity-test-results/editmode/unity.log tail
UnityEditor.TestTools.TestRunner.TestRun.Tasks.CleanupVerificationTask:LogWarningForFilesIfAny (string[],bool) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEditor.TestRunner/TestRun/Tasks/CleanupVerificationTask.cs:55)
UnityEditor.TestTools.TestRunner.TestRun.Tasks.CleanupVerificationTask/<Execute>d__3:MoveNext () (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEditor.TestRunner/TestRun/Tasks/CleanupVerificationTask.cs:27)
UnityEditor.TestTools.TestRunner.TestRun.TestJobRunner:ExecuteStep () (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEditor.TestRunner/TestRun/TestJobRunner.cs:173)
UnityEditor.TestTools.TestRunner.TestRun.TestJobRunner:ExecuteCallback () (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEditor.TestRunner/TestRun/TestJobRunner.cs:108)
UnityEditor.EditorApplication:Internal_CallUpdateFunctions () (at /home/bokken/build/output/unity/unity/Editor/Mono/EditorApplication.cs:384)

(Filename: ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEditor.TestRunner/TestRun/Tasks/CleanupVerificationTask.cs Line: 55)

Test run completed. Exiting with code 2 (Failed). One or more tests failed.
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

Killing ADB server in 0.089898 seconds.
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
debugger-agent: Unable to listen on 38
[Package Manager] Server process was shutdown
Checking for leaked weakptr:
  Found no leaked weakptrs.
##utp:{"type":"MemoryLeaks","version":2,"phase":"Immediate","time":1795265416972,"processId":6130,"allocatedMemory":11320090,"memoryLabels":[{"Default":13965},{"Permanent":16080},{"Thread":1084616},{"Manager":19285},{"VertexData":8},{"Geometry":408},{"VFX":-104},{"Texture":296},{"Shader":79289},{"Material":2532},{"GfxDevice":35976},{"Animation":456},{"Audio":4032},{"Font":2241811},{"FreeType":71363},{"FontEngine":320},{"Physics":2081},{"Serialization":376},{"Input":26752},{"JobScheduler":33024},{"TextLib":160},{"Mono":32},{"MonoCode":96},{"ScriptingNativeRuntime":155096},{"BaseObject":1631568},{"Resource":1032},{"Renderer":2928},{"Transform":4678},{"File":6670},{"WebCam":80},{"Culling":32},{"Terrain":11298},{"Wind":24},{"String":11638},{"DynamicArray":99196},{"HashMap":82506},{"Utility":2658704},{"PoolAlloc":1808},{"AI":72},{"TypeTree":5005},{"ScriptManager":512},{"RuntimeInitializeOnLoadManager":64},{"SpriteAtlas":112},{"GI":7448},{"Director":8208},{"WebRequest":552},{"VR":45689},{"SceneManager":528},{"Video":64},{"LazyScriptCache":32},{"NativeArray":540},{"Camera":17},{"Secure":265},{"SerializationCache":1576},{"APIUpdating":10320},{"Subsystems":336},{"VirtualTexturing":57552},{"StaticSafetyDebugInfo":278528},{"Analytics":336},{"Hierarchy":832},{"CoreStats":12536},{"Identifiers":6192},{"Gui":112},{"EditorUtility":27630},{"VersionControl":4},{"Undo":979},{"AssetDatabase":2513392},{"EditorGi":328},{"UnityConnect":24816},{"Upm":1852},{"DrivenProperties":72},{"LocalIPC":149},{"ProfilerEditor":9746},{"CoreBusinessMetrics":2390},{"Licensing":1128},{"AssetReference":32},{"IPCStream":32}]}

## failed test cases
xml files found: 1
--- unity-test-results/editmode/results.xml
TEST-SUITE Failed Battle Horizons: Last Survive
  msg: One or more child tests had errors
TEST-SUITE Failed /home/runner/work/Battle-royal-indian-edition-/Battle-royal-indian-edition-/unity-project/Library/ScriptAssemblies/BattleHorizons.EditModeTests.dll
  msg: One or more child tests had errors
TEST-SUITE Failed BattleHorizons
  msg: One or more child tests had errors
TEST-SUITE Failed BattleHorizons.Tests
  msg: One or more child tests had errors
TEST-SUITE Failed BattleHorizons.Tests.EditMode
  msg: One or more child tests had errors
TEST-SUITE Failed BattleHorizons.Tests.EditMode.GroundMaterialTests
  msg: One or more child tests had errors
TEST-CASE Failed BattleHorizons.Tests.EditMode.GroundMaterialTests.GroundPhotoImporters_KeepFullResolutionAndSharpMips
  msg: Assets/Resources/Art/Textures/ground_grass_dirt.jpg: photos must keep their 2048 px resolution
  msg:   Expected: greater than or equal to 2048
  msg:   But was:  512
  at: at BattleHorizons.Tests.EditMode.GroundMaterialTests.GroundPhotoImporters_KeepFullResolutionAndSharpMips () [0x0005e] in /home/runner/work/Battle-royal-indian-edition-/Battle-royal-indian-edition-/unity-project/Assets/Tests/EditMode/GroundM

## editmode unity.log tail
UnityEditor.TestTools.TestRunner.TestRun.Tasks.CleanupVerificationTask:LogWarningForFilesIfAny (string[],bool) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEditor.TestRunner/TestRun/Tasks/CleanupVerificationTask.cs:55)
UnityEditor.TestTools.TestRunner.TestRun.Tasks.CleanupVerificationTask/<Execute>d__3:MoveNext () (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEditor.TestRunner/TestRun/Tasks/CleanupVerificationTask.cs:27)
UnityEditor.TestTools.TestRunner.TestRun.TestJobRunner:ExecuteStep () (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEditor.TestRunner/TestRun/TestJobRunner.cs:173)
UnityEditor.TestTools.TestRunner.TestRun.TestJobRunner:ExecuteCallback () (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEditor.TestRunner/TestRun/TestJobRunner.cs:108)
UnityEditor.EditorApplication:Internal_CallUpdateFunctions () (at /home/bokken/build/output/unity/unity/Editor/Mono/EditorApplication.cs:384)

(Filename: ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEditor.TestRunner/TestRun/Tasks/CleanupVerificationTask.cs Line: 55)

Test run completed. Exiting with code 2 (Failed). One or more tests failed.
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

Killing ADB server in 0.089898 seconds.
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
debugger-agent: Unable to listen on 38
[Package Manager] Server process was shutdown
Checking for leaked weakptr:
  Found no leaked weakptrs.
##utp:{"type":"MemoryLeaks","version":2,"phase":"Immediate","time":1795265416972,"processId":6130,"allocatedMemory":11320090,"memoryLabels":[{"Default":13965},{"Permanent":16080},{"Thread":1084616},{"Manager":19285},{"VertexData":8},{"Geometry":408},{"VFX":-104},{"Texture":296},{"Shader":79289},{"Material":2532},{"GfxDevice":35976},{"Animation":456},{"Audio":4032},{"Font":2241811},{"FreeType":71363},{"FontEngine":320},{"Physics":2081},{"Serialization":376},{"Input":26752},{"JobScheduler":33024},{"TextLib":160},{"Mono":32},{"MonoCode":96},{"ScriptingNativeRuntime":155096},{"BaseObject":1631568},{"Resource":1032},{"Renderer":2928},{"Transform":4678},{"File":6670},{"WebCam":80},{"Culling":32},{"Terrain":11298},{"Wind":24},{"String":11638},{"DynamicArray":99196},{"HashMap":82506},{"Utility":2658704},{"PoolAlloc":1808},{"AI":72},{"TypeTree":5005},{"ScriptManager":512},{"RuntimeInitializeOnLoadManager":64},{"SpriteAtlas":112},{"GI":7448},{"Director":8208},{"WebRequest":552},{"VR":45689},{"SceneManager":528},{"Video":64},{"LazyScriptCache":32},{"NativeArray":540},{"Camera":17},{"Secure":265},{"SerializationCache":1576},{"APIUpdating":10320},{"Subsystems":336},{"VirtualTexturing":57552},{"StaticSafetyDebugInfo":278528},{"Analytics":336},{"Hierarchy":832},{"CoreStats":12536},{"Identifiers":6192},{"Gui":112},{"EditorUtility":27630},{"VersionControl":4},{"Undo":979},{"AssetDatabase":2513392},{"EditorGi":328},{"UnityConnect":24816},{"Upm":1852},{"DrivenProperties":72},{"LocalIPC":149},{"ProfilerEditor":9746},{"CoreBusinessMetrics":2390},{"Licensing":1128},{"AssetReference":32},{"IPCStream":32}]}
