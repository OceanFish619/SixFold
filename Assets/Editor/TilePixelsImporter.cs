using UnityEditor;
using UnityEngine;

public static class TilePixelsImporter
{
    const string MenuPath = "Tools/Heatwave/Apply Tile Pixels Import Settings";

    [MenuItem(MenuPath)]
    public static void ApplyTilePixelsImportSettings()
    {
        Configure("Assets/Tile Pixels/TileSet.png", 256f);
        Configure("Assets/Tile Pixels/Character_SpriteSheet.png", 256f);
        Configure("Assets/Tile Pixels/Icon_set.png", 256f);
        Configure("Assets/Tile Pixels/UI_kit.png", 128f);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        Debug.Log("Heatwave: Tile Pixels import settings applied.");
    }

    static void Configure(string path, float pixelsPerUnit)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning($"Heatwave: texture not found for import setup: {path}");
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        importer.maxTextureSize = 4096;
        importer.spritePixelsPerUnit = pixelsPerUnit;
        importer.wrapMode = TextureWrapMode.Clamp;
        ApplyPlatform(importer, "Standalone", 4096);
        ApplyPlatform(importer, "WebGL", 4096);
        ApplyPlatform(importer, "iOS", 4096);
        ApplyPlatform(importer, "Android", 4096);
        ApplyPlatform(importer, "VisionOS", 4096);
        ApplyPlatform(importer, "tvOS", 4096);
        importer.SaveAndReimport();
    }

    static void ApplyPlatform(TextureImporter importer, string platformName, int maxTextureSize)
    {
        var settings = importer.GetPlatformTextureSettings(platformName);
        settings.name = platformName;
        settings.overridden = true;
        settings.maxTextureSize = maxTextureSize;
        settings.textureCompression = TextureImporterCompression.Uncompressed;
        settings.resizeAlgorithm = TextureResizeAlgorithm.Mitchell;
        importer.SetPlatformTextureSettings(settings);
    }
}
