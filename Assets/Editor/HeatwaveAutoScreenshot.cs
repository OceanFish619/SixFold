using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class HeatwaveAutoScreenshot
{
    [MenuItem("Tools/Heatwave/Capture Clean Preview")]
    public static void CaptureCleanPreview()
    {
        const string scenePath = "Assets/Scenes/SampleScene.unity";
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError("Heatwave screenshot: failed to open SampleScene.");
            return;
        }

        var system = Object.FindFirstObjectByType<HeatwaveRoomNPCSystem>();
        if (system != null)
        {
            ForceRebuild(system);
        }

        var cam = Camera.main;
        if (cam == null)
        {
            cam = Object.FindFirstObjectByType<Camera>();
        }
        if (cam == null)
        {
            Debug.LogError("Heatwave screenshot: no camera found.");
            return;
        }

        cam.orthographic = true;
        cam.orthographicSize = 16f;
        cam.transform.position = new Vector3(0f, 0f, -10f);

        string outputDir = Path.Combine(Directory.GetCurrentDirectory(), "BUGS");
        Directory.CreateDirectory(outputDir);
        string outputPath = Path.Combine(outputDir, "auto_clean_preview.png");
        CaptureCamera(cam, outputPath, 1600, 900);
        Debug.Log($"Heatwave screenshot saved: {outputPath}");
    }

    [MenuItem("Tools/Heatwave/Validate Cover Mapping")]
    public static void ValidateCoverMapping()
    {
        const string scenePath = "Assets/Scenes/SampleScene.unity";
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError("Heatwave cover validation: failed to open SampleScene.");
            return;
        }

        var controller = Object.FindFirstObjectByType<HeatwaveCityGameController>();
        if (controller == null)
        {
            Debug.LogError("Heatwave cover validation: HeatwaveCityGameController not found.");
            return;
        }

        InvokePrivate(controller, "BuildFlowUI");
        InvokePrivate(controller, "ApplyCoverSkin");
        Debug.Log("Heatwave cover validation: BuildFlowUI + ApplyCoverSkin invoked.");
    }

    static void ForceRebuild(HeatwaveRoomNPCSystem system)
    {
        InvokePrivate(system, "FindSceneReferences");
        InvokePrivate(system, "EnsureWallColliderOptimization");
        InvokePrivate(system, "InitializeTilePixelsPalette");
        InvokePrivate(system, "RebuildHeatwaveMap");
        InvokePrivate(system, "BuildRoomDefinitions");
        InvokePrivate(system, "EnsureDecorRoots");
        InvokePrivate(system, "BuildRoomDecorations");
        InvokePrivate(system, "SpawnTaskSites");
        InvokePrivate(system, "CreateHeatHazeLayers");
        InvokePrivate(system, "EnsureObjectiveBeacon");
    }

    static void InvokePrivate(object instance, string methodName)
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var method = instance.GetType().GetMethod(methodName, flags);
        if (method != null)
        {
            method.Invoke(instance, null);
        }
    }

    static void CaptureCamera(Camera camera, string outputPath, int width, int height)
    {
        var rt = new RenderTexture(width, height, 24);
        var prevTarget = camera.targetTexture;
        var prevActive = RenderTexture.active;
        camera.targetTexture = rt;
        camera.Render();
        RenderTexture.active = rt;

        var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();
        File.WriteAllBytes(outputPath, tex.EncodeToPNG());

        camera.targetTexture = prevTarget;
        RenderTexture.active = prevActive;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);
    }
}
