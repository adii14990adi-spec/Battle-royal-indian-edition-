# CI diagnostics for e84b18519cdc71cda6ec365572dc3f5d20c12bfe

## unity-configure.log errors
92:[Licensing::Module] Error: Access token is unavailable; failed to update
113:FMOD failed to initialize the output device.: "Error initializing output device. " (60)
123:FMOD failed to initialize the output device.: "Error initializing output device. " (60)
191:			LogAssemblyErrors (0ms)
1901:Assets/Tests/EditMode/GroundMaterialTests.cs(55,45): error CS1061: 'TextureImporter' does not contain a definition for 'mipBias' and no accessible extension method 'mipBias' accepting a first argument of type 'TextureImporter' could be found (are you missing a using directive or an assembly reference?)
1903:## Script Compilation Error for: Csc Library/Bee/artifacts/1300b0aE.dag/BattleHorizons.EditModeTests.dll (+2 others)
1906:Assets/Tests/EditMode/GroundMaterialTests.cs(55,45): error CS1061: 'TextureImporter' does not contain a definition for 'mipBias' and no accessible extension method 'mipBias' accepting a first argument of type 'TextureImporter' could be found (are you missing a using directive or an assembly reference?)
1924:Assets/Tests/EditMode/GroundMaterialTests.cs(55,45): error CS1061: 'TextureImporter' does not contain a definition for 'mipBias' and no accessible extension method 'mipBias' accepting a first argument of type 'TextureImporter' could be found (are you missing a using directive or an assembly reference?)

## unity-configure.log tail
/utf8output
/preferreduilang:en-US
/additionalfile:"Library/Bee/artifacts/1300b0aE.dag/BattleHorizons.EditModeTests.UnityAdditionalFile.txt"
##### Custom Environment Variables
DOTNET_MULTILEVEL_LOOKUP=0
##### ExitCode
1
##### Output
Assets/Tests/EditMode/GroundMaterialTests.cs(55,45): error CS1061: 'TextureImporter' does not contain a definition for 'mipBias' and no accessible extension method 'mipBias' accepting a first argument of type 'TextureImporter' could be found (are you missing a using directive or an assembly reference?)
*** Tundra build failed (2.14 seconds), 65 items updated, 663 evaluated
## Script Compilation Error for: Csc Library/Bee/artifacts/1300b0aE.dag/BattleHorizons.EditModeTests.dll (+2 others)
## CmdLine: "/home/runner/Unity/Hub/Editor/6000.0.85f2/Editor/Data/NetCoreRuntime/dotnet" exec "/home/runner/Unity/Hub/Editor/6000.0.85f2/Editor/Data/DotNetSdkRoslyn/csc.dll" /nostdlib /noconfig /shared "@Library/Bee/artifacts/1300b0aE.dag/BattleHorizons.EditModeTests.rsp" "@Library/Bee/artifacts/1300b0aE.dag/BattleHorizons.EditModeTests.rsp2"
## Output:
Assets/Tests/EditMode/GroundMaterialTests.cs(55,45): error CS1061: 'TextureImporter' does not contain a definition for 'mipBias' and no accessible extension method 'mipBias' accepting a first argument of type 'TextureImporter' could be found (are you missing a using directive or an assembly reference?)
s(245,30): warning CS0618: 'PlayerSettings.GetApplicationIdentifier(BuildTargetGroup)' is obsolete: 'Use GetApplicationIdentifier(NamedBuildTarget buildTarget) instead'
Assets/Editor/ProjectBootstrap.cs(673,13): warning CS0618: 'PlayerSettings.SetApplicationIdentifier(BuildTargetGroup, string)' is obsolete: 'Use SetApplicationIdentifier(NamedBuildTarget buildTarget, string identifier) instead'
Assets/Editor/ProjectBootstrap.cs(698,13): warning CS0618: 'PlayerSettings.SetScriptingBackend(BuildTargetGroup, ScriptingImplementation)' is obsolete: 'Use SetScriptingBackend(NamedBuildTarget buildTarget, ScriptingImplementation backend) instead'
Assets/Editor/ProjectBootstrap.cs(699,13): warning CS0618: 'PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup, ApiCompatibilityLevel)' is obsolete: 'Use SetApiCompatibilityLevel(NamedBuildTarget buildTarget, ApiCompatibilityLevel value) instead'
Assets/Editor/ProjectBootstrap.cs(700,13): warning CS0618: 'PlayerSettings.SetIl2CppCompilerConfiguration(BuildTargetGroup, Il2CppCompilerConfiguration)' is obsolete: 'Use SetIl2CppCompilerConfiguration(NamedBuildTarget buildTarget, Il2CppCompilerConfiguration configuration) instead'
Assets/Editor/ProjectBootstrap.cs(701,13): warning CS0618: 'PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup, ManagedStrippingLevel)' is obsolete: 'Use SetManagedStrippingLevel(NamedBuildTarget buildTarget, ManagedStrippingLevel level) instead'
Assets/Editor/ProjectBootstrap.cs(719,13): warning CS0618: 'PlayerSettings.SetMobileMTRendering(BuildTargetGroup, bool)' is obsolete: 'Use SetMobileMTRendering(NamedBuildTarget buildTarget, bool enable) instead'
Total cache size 0
Assets/Scripts/UI/TouchControlsHost.cs(25,39): warning CS0414: The field 'TouchControlsHost.buildInEditor' is assigned but its value is never used
Assets/Editor/ProjectBootstrap.cs(241,21): warning CS0618: 'GraphicsSettings.RegisterRenderPipelineSettings<T>(RenderPipelineGlobalSettings)' is obsolete: 'Please use EditorGraphicsSettings.SetRenderPipelineGlobalSettingsAsset<TRenderPipelineType>(settings). #from(23.2)'
Assets/Editor/ProjectValidator.cs(245,30): warning CS0618: 'PlayerSettings.GetApplicationIdentifier(BuildTargetGroup)' is obsolete: 'Use GetApplicationIdentifier(NamedBuildTarget buildTarget) instead'
Assets/Editor/ProjectBootstrap.cs(673,13): warning CS0618: 'PlayerSettings.SetApplicationIdentifier(BuildTargetGroup, string)' is obsolete: 'Use SetApplicationIdentifier(NamedBuildTarget buildTarget, string identifier) instead'
Assets/Editor/ProjectBootstrap.cs(698,13): warning CS0618: 'PlayerSettings.SetScriptingBackend(BuildTargetGroup, ScriptingImplementation)' is obsolete: 'Use SetScriptingBackend(NamedBuildTarget buildTarget, ScriptingImplementation backend) instead'
Assets/Editor/ProjectBootstrap.cs(699,13): warning CS0618: 'PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup, ApiCompatibilityLevel)' is obsolete: 'Use SetApiCompatibilityLevel(NamedBuildTarget buildTarget, ApiCompatibilityLevel value) instead'
Assets/Editor/ProjectBootstrap.cs(700,13): warning CS0618: 'PlayerSettings.SetIl2CppCompilerConfiguration(BuildTargetGroup, Il2CppCompilerConfiguration)' is obsolete: 'Use SetIl2CppCompilerConfiguration(NamedBuildTarget buildTarget, Il2CppCompilerConfiguration configuration) instead'
Assets/Editor/ProjectBootstrap.cs(701,13): warning CS0618: 'PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup, ManagedStrippingLevel)' is obsolete: 'Use SetManagedStrippingLevel(NamedBuildTarget buildTarget, ManagedStrippingLevel level) instead'
Assets/Editor/ProjectBootstrap.cs(719,13): warning CS0618: 'PlayerSettings.SetMobileMTRendering(BuildTargetGroup, bool)' is obsolete: 'Use SetMobileMTRendering(NamedBuildTarget buildTarget, bool enable) instead'
Assets/Tests/EditMode/GroundMaterialTests.cs(55,45): error CS1061: 'TextureImporter' does not contain a definition for 'mipBias' and no accessible extension method 'mipBias' accepting a first argument of type 'TextureImporter' could be found (are you missing a using directive or an assembly reference?)
s(245,30): warning CS0618: 'PlayerSettings.GetApplicationIdentifier(BuildTargetGroup)' is obsolete: 'Use GetApplicationIdentifier(NamedBuildTarget buildTarget) instead'
Assets/Editor/ProjectBootstrap.cs(673,13): warning CS0618: 'PlayerSettings.SetApplicationIdentifier(BuildTargetGroup, string)' is obsolete: 'Use SetApplicationIdentifier(NamedBuildTarget buildTarget, string identifier) instead'
Assets/Editor/ProjectBootstrap.cs(698,13): warning CS0618: 'PlayerSettings.SetScriptingBackend(BuildTargetGroup, ScriptingImplementation)' is obsolete: 'Use SetScriptingBackend(NamedBuildTarget buildTarget, ScriptingImplementation backend) instead'
Assets/Editor/ProjectBootstrap.cs(699,13): warning CS0618: 'PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup, ApiCompatibilityLevel)' is obsolete: 'Use SetApiCompatibilityLevel(NamedBuildTarget buildTarget, ApiCompatibilityLevel value) instead'
Assets/Editor/ProjectBootstrap.cs(700,13): warning CS0618: 'PlayerSettings.SetIl2CppCompilerConfiguration(BuildTargetGroup, Il2CppCompilerConfiguration)' is obsolete: 'Use SetIl2CppCompilerConfiguration(NamedBuildTarget buildTarget, Il2CppCompilerConfiguration configuration) instead'
Assets/Editor/ProjectBootstrap.cs(701,13): warning CS0618: 'PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup, ManagedStrippingLevel)' is obsolete: 'Use SetManagedStrippingLevel(NamedBuildTarget buildTarget, ManagedStrippingLevel level) instead'
Assets/Editor/ProjectBootstrap.cs(719,13): warning CS0618: 'PlayerSettings.SetMobileMTRendering(BuildTargetGroup, bool)' is obsolete: 'Use SetMobileMTRendering(NamedBuildTarget buildTarget, bool enable) instead'
Assets/Editor/ProjectBootstrap.cs(673,13): warning CS0618: 'PlayerSettings.SetApplicationIdentifier(BuildTargetGroup, string)' is obsolete: 'Use SetApplicationIdentifier(NamedBuildTarget buildTarget, string identifier) instead'
Assets/Editor/ProjectBootstrap.cs(698,13): warning CS0618: 'PlayerSettings.SetScriptingBackend(BuildTargetGroup, ScriptingImplementation)' is obsolete: 'Use SetScriptingBackend(NamedBuildTarget buildTarget, ScriptingImplementation backend) instead'
Assets/Editor/ProjectBootstrap.cs(699,13): warning CS0618: 'PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup, ApiCompatibilityLevel)' is obsolete: 'Use SetApiCompatibilityLevel(NamedBuildTarget buildTarget, ApiCompatibilityLevel value) instead'
Assets/Editor/ProjectBootstrap.cs(700,13): warning CS0618: 'PlayerSettings.SetIl2CppCompilerConfiguration(BuildTargetGroup, Il2CppCompilerConfiguration)' is obsolete: 'Use SetIl2CppCompilerConfiguration(NamedBuildTarget buildTarget, Il2CppCompilerConfiguration configuration) instead'
Assets/Editor/ProjectBootstrap.cs(701,13): warning CS0618: 'PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup, ManagedStrippingLevel)' is obsolete: 'Use SetManagedStrippingLevel(NamedBuildTarget buildTarget, ManagedStrippingLevel level) instead'
Assets/Editor/ProjectBootstrap.cs(719,13): warning CS0618: 'PlayerSettings.SetMobileMTRendering(BuildTargetGroup, bool)' is obsolete: 'Use SetMobileMTRendering(NamedBuildTarget buildTarget, bool enable) instead'
Assets/Editor/ProjectBootstrap.cs(673,13): warning CS0618: 'PlayerSettings.SetApplicationIdentifier(BuildTargetGroup, string)' is obsolete: 'Use SetApplicationIdentifier(NamedBuildTarget buildTarget, string identifier) instead'
Assets/Editor/ProjectBootstrap.cs(698,13): warning CS0618: 'PlayerSettings.SetScriptingBackend(BuildTargetGroup, ScriptingImplementation)' is obsolete: 'Use SetScriptingBackend(NamedBuildTarget buildTarget, ScriptingImplementation backend) instead'
Assets/Editor/ProjectBootstrap.cs(699,13): warning CS0618: 'PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup, ApiCompatibilityLevel)' is obsolete: 'Use SetApiCompatibilityLevel(NamedBuildTarget buildTarget, ApiCompatibilityLevel value) instead'
Assets/Editor/ProjectBootstrap.cs(700,13): warning CS0618: 'PlayerSettings.SetIl2CppCompilerConfiguration(BuildTargetGroup, Il2CppCompilerConfiguration)' is obsolete: 'Use SetIl2CppCompilerConfiguration(NamedBuildTarget buildTarget, Il2CppCompilerConfiguration configuration) instead'
Assets/Editor/ProjectBootstrap.cs(701,13): warning CS0618: 'PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup, ManagedStrippingLevel)' is obsolete: 'Use SetManagedStrippingLevel(NamedBuildTarget buildTarget, ManagedStrippingLevel level) instead'
Assets/Editor/ProjectBootstrap.cs(719,13): warning CS0618: 'PlayerSettings.SetMobileMTRendering(BuildTargetGroup, bool)' is obsolete: 'Use SetMobileMTRendering(NamedBuildTarget buildTarget, bool enable) instead'
Assets/Editor/ProjectBootstrap.cs(673,13): warning CS0618: 'PlayerSettings.SetApplicationIdentifier(BuildTargetGroup, string)' is obsolete: 'Use SetApplicationIdentifier(NamedBuildTarget buildTarget, string identifier) instead'
Assets/Editor/ProjectBootstrap.cs(698,13): warning CS0618: 'PlayerSettings.SetScriptingBackend(BuildTargetGroup, ScriptingImplementation)' is obsolete: 'Use SetScriptingBackend(NamedBuildTarget buildTarget, ScriptingImplementation backend) instead'
Assets/Editor/ProjectBootstrap.cs(699,13): warning CS0618: 'PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup, ApiCompatibilityLevel)' is obsolete: 'Use SetApiCompatibilityLevel(NamedBuildTarget buildTarget, ApiCompatibilityLevel value) instead'
Assets/Editor/ProjectBootstrap.cs(700,13): warning CS0618: 'PlayerSettings.SetIl2CppCompilerConfiguration(BuildTargetGroup, Il2CppCompilerConfiguration)' is obsolete: 'Use SetIl2CppCompilerConfiguration(NamedBuildTarget buildTarget, Il2CppCompilerConfiguration configuration) instead'
Assets/Editor/ProjectBootstrap.cs(701,13): warning CS0618: 'PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup, ManagedStrippingLevel)' is obsolete: 'Use SetManagedStrippingLevel(NamedBuildTarget buildTarget, ManagedStrippingLevel level) instead'
Assets/Editor/ProjectBootstrap.cs(719,13): warning CS0618: 'PlayerSettings.SetMobileMTRendering(BuildTargetGroup, bool)' is obsolete: 'Use SetMobileMTRendering(NamedBuildTarget buildTarget, bool enable) instead'
Total cache size after purge 0, took 00:00:00.0049187
AssetDatabase: script compilation time: 5.521791s
Scripts have compiler errors.

## failed test cases
xml files found: 0

## editmode unity.log tail
(no editmode log)
