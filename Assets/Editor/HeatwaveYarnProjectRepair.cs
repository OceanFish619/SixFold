using UnityEditor;
using UnityEngine;
using Yarn.Compiler;

public static class HeatwaveYarnProjectRepair
{
    const string YarnProjectPath = "Assets/Resources/Yarn/HeatwaveCity.yarnproject";

    [MenuItem("Tools/Heatwave/Repair Yarn Project")]
    public static void RepairYarnProject()
    {
        var project = new Project();
        project.ExcludeFilePatterns = new[] {
            "**/*~/*",
            "./Samples/Yarn Spinner*/*",
            "**/HeatwaveCity_SimpleEnglish.yarn",
        };

        project.SourceFilePatterns = new[] {
            "HeatwaveCity_Main.yarn",
        };

        project.SaveToFile(YarnProjectPath);
        AssetDatabase.ImportAsset(YarnProjectPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

        var asset = AssetDatabase.LoadMainAssetAtPath(YarnProjectPath);
        Selection.activeObject = asset;

        Debug.Log($"Heatwave: Recreated and reimported {YarnProjectPath}");
    }
}
