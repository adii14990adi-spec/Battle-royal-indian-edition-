# CI diagnostics for 12ad5d8063fd7c21be30fb95aaa3191525028acb

## unity-configure.log errors
92:FMOD failed to initialize the output device.: "Error initializing output device. " (60)
102:FMOD failed to initialize the output device.: "Error initializing output device. " (60)
125:[Licensing::Module] Error: Access token is unavailable; failed to update
193:			LogAssemblyErrors (0ms)
835:Assets/Scripts/UI/WorldDebugHud.cs(19,13): error CS0103: The name 'GameLog' does not exist in the current context
836:Assets/Scripts/UI/LobbyCharacterShowcase.cs(80,17): error CS0103: The name 'GameLog' does not exist in the current context
837:Assets/Scripts/UI/LobbyCharacterShowcase.cs(88,13): error CS0103: The name 'GameLog' does not exist in the current context
840:## Script Compilation Error for: Csc Library/Bee/artifacts/1300b0aE.dag/BattleHorizons.Runtime.dll (+2 others)
843:Assets/Scripts/UI/WorldDebugHud.cs(19,13): error CS0103: The name 'GameLog' does not exist in the current context
844:Assets/Scripts/UI/LobbyCharacterShowcase.cs(80,17): error CS0103: The name 'GameLog' does not exist in the current context
845:Assets/Scripts/UI/LobbyCharacterShowcase.cs(88,13): error CS0103: The name 'GameLog' does not exist in the current context
849:Assets/Scripts/UI/WorldDebugHud.cs(19,13): error CS0103: The name 'GameLog' does not exist in the current context
850:Assets/Scripts/UI/LobbyCharacterShowcase.cs(80,17): error CS0103: The name 'GameLog' does not exist in the current context
851:Assets/Scripts/UI/LobbyCharacterShowcase.cs(88,13): error CS0103: The name 'GameLog' does not exist in the current context

## unity-configure.log tail
"Assets/Scripts/Player/VirtualJoystick.cs"
"Assets/Scripts/UI/GameLogo.cs"
"Assets/Scripts/UI/HoldButton.cs"
"Assets/Scripts/UI/HudArtFactory.cs"
"Assets/Scripts/UI/LoadingScreenView.cs"
"Assets/Scripts/UI/LobbyArtFactory.cs"
"Assets/Scripts/UI/LobbyCharacterShowcase.cs"
"Assets/Scripts/UI/LobbyProfile.cs"
"Assets/Scripts/UI/MainMenuController.cs"
"Assets/Scripts/UI/MobileControlsBuilder.cs"
"Assets/Scripts/UI/TouchControlsHost.cs"
"Assets/Scripts/UI/TouchLookArea.cs"
"Assets/Scripts/UI/WorldDebugHud.cs"
"Assets/Scripts/World/IslandShapeMath.cs"
"Assets/Scripts/World/SiranChunkStreamer.cs"
"Assets/Scripts/World/SiranIslandData.cs"
"Assets/Scripts/World/SiranIslandTerrain.cs"
"Assets/Scripts/World/SiranIslandWater.cs"
"Assets/Scripts/World/SiranMapConfig.cs"
"Assets/Scripts/World/SiranWorldBuilder.cs"
-langversion:9.0
/deterministic
/optimize+
/debug:portable
/nologo
/RuntimeMetadataVersion:v4.0.30319
/nowarn:0169
/nowarn:0649
/nowarn:0282
/nowarn:1701
/nowarn:1702
/utf8output
/preferreduilang:en-US
/additionalfile:"Library/Bee/artifacts/1300b0aE.dag/BattleHorizons.Runtime.UnityAdditionalFile.txt"
##### Custom Environment Variables
DOTNET_MULTILEVEL_LOOKUP=0
##### ExitCode
1
##### Output
Assets/Scripts/UI/WorldDebugHud.cs(19,13): error CS0103: The name 'GameLog' does not exist in the current context
Assets/Scripts/UI/LobbyCharacterShowcase.cs(80,17): error CS0103: The name 'GameLog' does not exist in the current context
Assets/Scripts/UI/LobbyCharacterShowcase.cs(88,13): error CS0103: The name 'GameLog' does not exist in the current context
Assets/Scripts/UI/TouchControlsHost.cs(25,39): warning CS0414: The field 'TouchControlsHost.buildInEditor' is assigned but its value is never used
*** Tundra build failed (0.86 seconds), 5 items updated, 663 evaluated
## Script Compilation Error for: Csc Library/Bee/artifacts/1300b0aE.dag/BattleHorizons.Runtime.dll (+2 others)
## CmdLine: "/home/runner/Unity/Hub/Editor/6000.0.85f2/Editor/Data/NetCoreRuntime/dotnet" exec "/home/runner/Unity/Hub/Editor/6000.0.85f2/Editor/Data/DotNetSdkRoslyn/csc.dll" /nostdlib /noconfig /shared "@Library/Bee/artifacts/1300b0aE.dag/BattleHorizons.Runtime.rsp" "@Library/Bee/artifacts/1300b0aE.dag/BattleHorizons.Runtime.rsp2"
## Output:
Assets/Scripts/UI/WorldDebugHud.cs(19,13): error CS0103: The name 'GameLog' does not exist in the current context
Assets/Scripts/UI/LobbyCharacterShowcase.cs(80,17): error CS0103: The name 'GameLog' does not exist in the current context
Assets/Scripts/UI/LobbyCharacterShowcase.cs(88,13): error CS0103: The name 'GameLog' does not exist in the current context
Assets/Scripts/UI/TouchControlsHost.cs(25,39): warning CS0414: The field 'TouchControlsHost.buildInEditor' is assigned but its value is never used
gcU
Total cache size 0
Assets/Scripts/UI/WorldDebugHud.cs(19,13): error CS0103: The name 'GameLog' does not exist in the current context
Assets/Scripts/UI/LobbyCharacterShowcase.cs(80,17): error CS0103: The name 'GameLog' does not exist in the current context
Assets/Scripts/UI/LobbyCharacterShowcase.cs(88,13): error CS0103: The name 'GameLog' does not exist in the current context
Assets/Scripts/UI/TouchControlsHost.cs(25,39): warning CS0414: The field 'TouchControlsHost.buildInEditor' is assigned but its value is never used
Total cache size after purge 0, took 00:00:00.0062242
AssetDatabase: script compilation time: 4.390065s
Scripts have compiler errors.

## failed test cases
