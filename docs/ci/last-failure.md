# CI diagnostics for 67beb573b2e39ecea675d83f7fd1647ab7ff4eb0

## unity-configure.log errors
95:[Licensing::Module] Error: Access token is unavailable; failed to update
104:FMOD failed to initialize the output device.: "Error initializing output device. " (60)
114:FMOD failed to initialize the output device.: "Error initializing output device. " (60)
191:			LogAssemblyErrors (0ms)
616:			LogAssemblyErrors (0ms)
2156:UnityEngine.Debug:LogError (object,UnityEngine.Object)
2187:UnityEngine.Debug:LogError (object,UnityEngine.Object)
2210:UnityEngine.Rendering.RenderPipelineGlobalSettingsUtils:TryEnsure<UnityEngine.Rendering.Universal.UniversalRenderPipelineGlobalSettings, UnityEngine.Rendering.Universal.UniversalRenderPipeline> (UnityEngine.Rendering.Universal.UniversalRenderPipelineGlobalSettings&,string,bool,System.Exception&) (at ./Library/PackageCache/com.unity.render-pipelines.core@0a85889b8bdb/Runtime/RenderPipeline/RenderPipelineGlobalSettingsUtils.cs:126)

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

Killing ADB server in 0.043919 seconds.
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
##utp:{"type":"MemoryLeaks","version":2,"phase":"Immediate","time":1795195001664,"processId":4491,"allocatedMemory":11801398,"memoryLabels":[{"Default":23549},{"Permanent":16080},{"Thread":1084616},{"Manager":19261},{"VertexData":8},{"Geometry":408},{"VFX":-104},{"Texture":160},{"Shader":89273},{"Material":12590},{"GfxDevice":35976},{"Animation":456},{"Audio":4032},{"Font":2241811},{"FreeType":71363},{"FontEngine":320},{"Physics":2081},{"Serialization":272},{"Input":26752},{"JobScheduler":33024},{"TextLib":160},{"Mono":32},{"MonoCode":96},{"ScriptingNativeRuntime":155096},{"BaseObject":1656408},{"Resource":1032},{"Renderer":2928},{"Transform":4678},{"File":6674},{"WebCam":80},{"Culling":32},{"Terrain":11298},{"Wind":24},{"String":267850},{"DynamicArray":109822},{"HashMap":95291},{"Utility":2658704},{"PoolAlloc":1808},{"AI":72},{"TypeTree":5005},{"ScriptManager":512},{"RuntimeInitializeOnLoadManager":64},{"SpriteAtlas":112},{"GI":7448},{"Director":8208},{"CloudService":81912},{"WebRequest":552},{"VR":45689},{"SceneManager":528},{"Video":64},{"LazyScriptCache":32},{"Camera":17},{"Secure":8049},{"SerializationCache":1576},{"APIUpdating":10320},{"Subsystems":336},{"VirtualTexturing":57552},{"StaticSafetyDebugInfo":278528},{"Analytics":464},{"Hierarchy":832},{"CoreStats":12536},{"Identifiers":6192},{"Gui":112},{"EditorUtility":27622},{"VersionControl":4},{"Undo":979},{"AssetDatabase":2571622},{"EditorGi":328},{"UnityConnect":24792},{"Upm":1852},{"DrivenProperties":72},{"LocalIPC":149},{"ProfilerEditor":9746},{"CoreBusinessMetrics":2387},{"Licensing":1128},{"AssetReference":32},{"IPCStream":32}]}

## /tmp/unity-integration.log errors

## unity-test-results/editmode/unity.log errors
1247:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
1311:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
1369:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
1423:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
1477:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
1543:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
1623:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
1680:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
1733:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
1793:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
1850:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
1904:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
1958:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
2012:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
2069:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
2126:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
2189:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
2247:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
2301:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
2355:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
2430:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
2498:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
2583:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
2636:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)
2754:UnityEngine.TestRunner.NUnitExtensions.Runner.UnityLogCheckDelegatingCommand:CaptureException (NUnit.Framework.Internal.TestResult,System.Action) (at ./Library/PackageCache/com.unity.test-framework@bd52b1c23814/UnityEngine.TestRunner/NUnitExtensions/Runner/UnityLogCheckDelegatingCommand.cs:72)

## failed test cases
