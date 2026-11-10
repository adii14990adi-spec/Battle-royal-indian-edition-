using BattleHorizons.EditorTools;

/// <summary>
/// Stable GameCI entry point for building the Android APK.
/// The underlying project build configures and validates scenes/settings before
/// invoking BuildPipeline, and exits the editor with the actual build status.
/// </summary>
public static class BuildCommand
{
    public static void PerformBuild()
    {
        AndroidBuild.ConfigureValidateAndBuildApk();
    }
}
