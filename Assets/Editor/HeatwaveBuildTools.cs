using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;

public static class HeatwaveBuildTools
{
    public static readonly string[] Scenes =
    {
        "Assets/Scenes/SampleScene.unity",
    };

    [MenuItem("Tools/Heatwave/Build macOS Smoke")]
    public static void BuildMacSmokeMenu()
    {
        BuildMacSmoke();
    }

    public static void BuildMacSmoke()
    {
        string outputDir = Path.Combine(Directory.GetCurrentDirectory(), "Builds", "Smoke");
        Directory.CreateDirectory(outputDir);
        string locationPath = Path.Combine(outputDir, "HeatwaveMayor.app");

        var options = new BuildPlayerOptions
        {
            scenes = Scenes,
            locationPathName = locationPath,
            target = BuildTarget.StandaloneOSX,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report == null || report.summary.result != BuildResult.Succeeded)
        {
            string result = report == null ? "Unknown" : report.summary.result.ToString();
            throw new System.Exception($"Heatwave build failed: {result}");
        }

        UnityEngine.Debug.Log($"Heatwave build succeeded: {locationPath}");
    }
}
