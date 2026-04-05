using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

public class HeatwaveRoomNPCSystem : MonoBehaviour
{
    [Header("Room Layout")]
    [SerializeField] float enterMessageDuration = 1.8f;
    [SerializeField] bool cleanVisualStyle = true;

    [Header("NPC")]
    [SerializeField] int maxNpcCount = 6;
    [SerializeField] Color npcTint = new Color(0.89f, 0.78f, 0.34f, 1f);
    [SerializeField] float npcVisualScale = 1.22f;

    readonly List<RoomData> rooms = new List<RoomData>();
    readonly HashSet<int> npcSpawnedRooms = new HashSet<int>();
    readonly HashSet<string> spawnedTaskSites = new HashSet<string>();
    readonly Dictionary<string, Sprite> npcSpritesByRoom = new Dictionary<string, Sprite>();
    readonly Dictionary<string, Transform> objectiveAnchors = new Dictionary<string, Transform>();
    static readonly Dictionary<string, Texture2D> textureCache = new Dictionary<string, Texture2D>();
    static readonly Dictionary<string, Sprite> externalNpcSpriteCache = new Dictionary<string, Sprite>();
    static readonly Dictionary<string, Sprite> externalTileSpriteCache = new Dictionary<string, Sprite>();
    static Sprite solidSpriteCache;
    const int AtlasColumns = 11;
    const int AtlasRows = 6;

    Transform player;
    Tilemap groundTilemap;
    Tilemap wallsTilemap;
    Transform decorRoot;
    Transform taskRoot;
    GameObject enteringRoot;
    TMP_Text enteringText;
    Sprite defaultNpcSprite;
    BoundsInt mapBounds;
    int leftDividerX;
    int rightDividerX;
    int middleDividerY;

    int currentRoomIndex = -1;
    int npcCount;
    float messageUntil;
    Transform objectiveBeacon;
    SpriteRenderer objectiveBeaconRenderer;
    TextMeshPro objectiveBeaconText;
    SpriteRenderer hazeA;
    SpriteRenderer hazeB;
    TileBase[] terrainTiles;
    TileBase roadTile;
    TileBase[] roadVariantTiles;
    TileBase roadHorizontalTile;
    TileBase roadVerticalTile;
    TileBase roadCenterTile;
    TileBase roadShoulderTile;
    TileBase wallPaletteTile;
    bool tilePaletteReady;
    Texture2D characterSheetTexture;

    struct RoomData
    {
        public string Name;
        public RectInt CellRect;
    }

    void Awake()
    {
        var startupStopwatch = System.Diagnostics.Stopwatch.StartNew();

        MeasureAwakePhase("FindSceneReferences", FindSceneReferences);
        MeasureAwakePhase("EnsureWallColliderOptimization", EnsureWallColliderOptimization);
        MeasureAwakePhase("InitializeTilePixelsPalette", InitializeTilePixelsPalette);
        MeasureAwakePhase("RebuildHeatwaveMap", RebuildHeatwaveMap);
        MeasureAwakePhase("BuildRoomDefinitions", BuildRoomDefinitions);
        MeasureAwakePhase("EnsureDecorRoots", EnsureDecorRoots);
        MeasureAwakePhase("BuildRoomDecorations", BuildRoomDecorations);
        MeasureAwakePhase("SpawnTaskSites", SpawnTaskSites);
        MeasureAwakePhase("CreateHeatHazeLayers", CreateHeatHazeLayers);
        MeasureAwakePhase("EnsureObjectiveBeacon", EnsureObjectiveBeacon);
        MeasureAwakePhase("EnsureEnteringTextUI", EnsureEnteringTextUI);
        MeasureAwakePhase("EnsureNpcDialogueUI", EnsureNpcDialogueUI);
        MeasureAwakePhase("SpawnAllRoomNpcs", SpawnAllRoomNpcs);

        startupStopwatch.Stop();
        Debug.Log($"HeatwaveStartup Awake.Total {startupStopwatch.Elapsed.TotalMilliseconds:F2}ms");
    }

    void Update()
    {
        if (player == null)
        {
            FindPlayer();
            if (player == null) return;
        }

        int roomIndex = GetCurrentRoomIndex();
        if (roomIndex >= 0 && roomIndex != currentRoomIndex)
        {
            currentRoomIndex = roomIndex;
            HandleRoomEntered(roomIndex);
        }

        UpdateObjectiveBeacon();
        UpdateHeatHaze();
        UpdateEnteringTextVisibility();
    }

    static void MeasureAwakePhase(string phaseName, System.Action action)
    {
        if (action == null) return;

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        action();
        stopwatch.Stop();
        Debug.Log($"HeatwaveStartup {phaseName} {stopwatch.Elapsed.TotalMilliseconds:F2}ms");
    }

    void FindSceneReferences()
    {
        if (groundTilemap == null)
        {
            var groundObj = GameObject.Find("Ground");
            if (groundObj != null) groundTilemap = groundObj.GetComponent<Tilemap>();
        }

        if (wallsTilemap == null)
        {
            var wallsObj = GameObject.Find("Walls");
            if (wallsObj != null) wallsTilemap = wallsObj.GetComponent<Tilemap>();
        }

        FindPlayer();
        CacheDefaultNpcSprite();
    }

    void InitializeTilePixelsPalette()
    {
        if (tilePaletteReady) return;

        characterSheetTexture = FindTextureByName("Character_SpriteSheet");

        if (TryInitializeExternalDroughtFloorPalette())
        {
            return;
        }

        InitializeFlatFallbackPalette();
    }

    bool TryInitializeExternalDroughtFloorPalette()
    {
        // Web source: Kenney New Platformer Pack (CC0)
        var floorSprites = new[]
        {
            LoadExternalTileSprite("drought_sand_block", "Assets/ExternalAssets/Kenney/new-platformer-pack/Sprites/Tiles/Default/terrain_sand_block.png"),
            LoadExternalTileSprite("drought_sand_center", "Assets/ExternalAssets/Kenney/new-platformer-pack/Sprites/Tiles/Default/terrain_sand_block_center.png"),
            LoadExternalTileSprite("drought_sand_top", "Assets/ExternalAssets/Kenney/new-platformer-pack/Sprites/Tiles/Default/terrain_sand_block_top.png"),
            LoadExternalTileSprite("drought_sand_bottom", "Assets/ExternalAssets/Kenney/new-platformer-pack/Sprites/Tiles/Default/terrain_sand_block_bottom.png"),
            LoadExternalTileSprite("drought_sand_left", "Assets/ExternalAssets/Kenney/new-platformer-pack/Sprites/Tiles/Default/terrain_sand_block_left.png"),
            LoadExternalTileSprite("drought_sand_right", "Assets/ExternalAssets/Kenney/new-platformer-pack/Sprites/Tiles/Default/terrain_sand_block_right.png"),
            LoadExternalTileSprite("drought_sand_tl", "Assets/ExternalAssets/Kenney/new-platformer-pack/Sprites/Tiles/Default/terrain_sand_block_top_left.png"),
            LoadExternalTileSprite("drought_sand_tr", "Assets/ExternalAssets/Kenney/new-platformer-pack/Sprites/Tiles/Default/terrain_sand_block_top_right.png"),
            LoadExternalTileSprite("drought_sand_bl", "Assets/ExternalAssets/Kenney/new-platformer-pack/Sprites/Tiles/Default/terrain_sand_block_bottom_left.png"),
            LoadExternalTileSprite("drought_sand_br", "Assets/ExternalAssets/Kenney/new-platformer-pack/Sprites/Tiles/Default/terrain_sand_block_bottom_right.png"),
            LoadExternalTileSprite("drought_dirt_block", "Assets/ExternalAssets/Kenney/new-platformer-pack/Sprites/Tiles/Default/terrain_dirt_block.png"),
            LoadExternalTileSprite("drought_dirt_center", "Assets/ExternalAssets/Kenney/new-platformer-pack/Sprites/Tiles/Default/terrain_dirt_block_center.png"),
            LoadExternalTileSprite("drought_dirt_top", "Assets/ExternalAssets/Kenney/new-platformer-pack/Sprites/Tiles/Default/terrain_dirt_block_top.png"),
            LoadExternalTileSprite("drought_dirt_bottom", "Assets/ExternalAssets/Kenney/new-platformer-pack/Sprites/Tiles/Default/terrain_dirt_block_bottom.png"),
            LoadExternalTileSprite("drought_dirt_left", "Assets/ExternalAssets/Kenney/new-platformer-pack/Sprites/Tiles/Default/terrain_dirt_block_left.png"),
            LoadExternalTileSprite("drought_dirt_right", "Assets/ExternalAssets/Kenney/new-platformer-pack/Sprites/Tiles/Default/terrain_dirt_block_right.png"),
        };

        terrainTiles = BuildRuntimeTiles(floorSprites);
        if (terrainTiles == null || terrainTiles.Length == 0)
        {
            return false;
        }

        var roadSprites = new[]
        {
            LoadExternalTileSprite("drought_road_h_mid", "Assets/ExternalAssets/Kenney/new-platformer-pack/Sprites/Tiles/Default/terrain_dirt_horizontal_middle.png"),
            LoadExternalTileSprite("drought_road_h_left", "Assets/ExternalAssets/Kenney/new-platformer-pack/Sprites/Tiles/Default/terrain_dirt_horizontal_left.png"),
            LoadExternalTileSprite("drought_road_h_right", "Assets/ExternalAssets/Kenney/new-platformer-pack/Sprites/Tiles/Default/terrain_dirt_horizontal_right.png"),
            LoadExternalTileSprite("drought_road_v_mid", "Assets/ExternalAssets/Kenney/new-platformer-pack/Sprites/Tiles/Default/terrain_dirt_vertical_middle.png"),
            LoadExternalTileSprite("drought_road_v_top", "Assets/ExternalAssets/Kenney/new-platformer-pack/Sprites/Tiles/Default/terrain_dirt_vertical_top.png"),
            LoadExternalTileSprite("drought_road_v_bottom", "Assets/ExternalAssets/Kenney/new-platformer-pack/Sprites/Tiles/Default/terrain_dirt_vertical_bottom.png"),
            LoadExternalTileSprite("drought_road_center", "Assets/ExternalAssets/Kenney/new-platformer-pack/Sprites/Tiles/Default/terrain_dirt_block_center.png"),
            LoadExternalTileSprite("drought_road_block", "Assets/ExternalAssets/Kenney/new-platformer-pack/Sprites/Tiles/Default/terrain_dirt_block.png"),
        };
        roadVariantTiles = BuildRuntimeTiles(roadSprites);
        roadHorizontalTile = CreateRuntimeTile(roadSprites[0] ?? roadSprites[1] ?? roadSprites[2], Tile.ColliderType.None);
        roadVerticalTile = CreateRuntimeTile(roadSprites[3] ?? roadSprites[4] ?? roadSprites[5], Tile.ColliderType.None);
        roadCenterTile = CreateRuntimeTile(roadSprites[6] ?? roadSprites[7] ?? roadSprites[0], Tile.ColliderType.None);
        roadShoulderTile = CreateRuntimeTile(roadSprites[7] ?? roadSprites[6] ?? roadSprites[0], Tile.ColliderType.None);
        roadTile = roadCenterTile ?? roadHorizontalTile ?? roadVerticalTile ?? roadShoulderTile;

        var wallSprite =
            LoadExternalTileSprite("drought_wall", "Assets/ExternalAssets/Kenney/new-platformer-pack/Sprites/Tiles/Default/terrain_dirt_block_top.png")
            ?? LoadExternalTileSprite("drought_wall_fallback", "Assets/ExternalAssets/Kenney/new-platformer-pack/Sprites/Tiles/Default/terrain_dirt_block.png");
        wallPaletteTile = CreateRuntimeTile(wallSprite, Tile.ColliderType.Grid);

        tilePaletteReady = true;
        return true;
    }

    void InitializeFlatFallbackPalette()
    {
        var floorA = CreateFlatTileSprite(new Color(0.79f, 0.58f, 0.34f, 1f), "flat_floor_a");
        var floorB = CreateFlatTileSprite(new Color(0.74f, 0.53f, 0.31f, 1f), "flat_floor_b");
        var road = CreateFlatTileSprite(new Color(0.58f, 0.42f, 0.27f, 1f), "flat_road");
        var wall = CreateFlatTileSprite(new Color(0.34f, 0.24f, 0.16f, 1f), "flat_wall");
        terrainTiles = BuildRuntimeTiles(new[] { floorA, floorB });
        roadHorizontalTile = CreateRuntimeTile(road, Tile.ColliderType.None);
        roadVerticalTile = roadHorizontalTile;
        roadCenterTile = roadHorizontalTile;
        roadShoulderTile = roadHorizontalTile;
        roadVariantTiles = BuildRuntimeTiles(new[] { road });
        roadTile = roadHorizontalTile;
        wallPaletteTile = CreateRuntimeTile(wall, Tile.ColliderType.Grid);
        tilePaletteReady = true;
    }

    static Texture2D FindTextureByName(string textureName)
    {
        if (string.IsNullOrWhiteSpace(textureName)) return null;
        if (textureCache.TryGetValue(textureName, out var cached) && cached != null)
        {
            return cached;
        }

        var textures = Resources.FindObjectsOfTypeAll<Texture2D>();
        for (int i = 0; i < textures.Length; i++)
        {
            if (textures[i] != null && textures[i].name == textureName)
            {
                textureCache[textureName] = textures[i];
                return textures[i];
            }
        }

        var resourceTexture = Resources.Load<Texture2D>(textureName);
        if (resourceTexture != null)
        {
            textureCache[textureName] = resourceTexture;
            return resourceTexture;
        }

#if UNITY_EDITOR
        string[] guids = UnityEditor.AssetDatabase.FindAssets($"{textureName} t:Texture2D");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
            var tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex != null)
            {
                textureCache[textureName] = tex;
                return tex;
            }
        }
#endif

        var diskTexture = LoadTextureFromAssetsFolder(textureName);
        if (diskTexture != null)
        {
            textureCache[textureName] = diskTexture;
            return diskTexture;
        }

        return null;
    }

    static Texture2D LoadTextureFromAssetsFolder(string textureName)
    {
        try
        {
            string[] files = Directory.GetFiles(Application.dataPath, $"{textureName}.png", SearchOption.AllDirectories);
            if (files == null || files.Length == 0) return null;

            byte[] bytes = File.ReadAllBytes(files[0]);
            if (bytes == null || bytes.Length == 0) return null;

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            if (!texture.LoadImage(bytes, false))
            {
                Object.Destroy(texture);
                return null;
            }

            texture.name = textureName;
            return texture;
        }
        catch
        {
            return null;
        }
    }

    static Sprite LoadExternalTileSprite(string cacheKey, string projectRelativePath, float pixelsPerUnit = 64f)
    {
        if (externalTileSpriteCache.TryGetValue(cacheKey, out var cached) && cached != null)
        {
            return cached;
        }

        var fromResources = LoadSpriteFromResourcesMirror(projectRelativePath);
        if (fromResources != null)
        {
            externalTileSpriteCache[cacheKey] = fromResources;
            return fromResources;
        }

#if UNITY_EDITOR
        var imported = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(projectRelativePath);
        if (imported != null)
        {
            externalTileSpriteCache[cacheKey] = imported;
            return imported;
        }
#endif

        string relativeFromAssets = projectRelativePath.StartsWith("Assets/")
            ? projectRelativePath.Substring("Assets/".Length)
            : projectRelativePath;
        string diskPath = Path.Combine(Application.dataPath, relativeFromAssets);
        if (!File.Exists(diskPath))
        {
            return null;
        }

        try
        {
            byte[] bytes = File.ReadAllBytes(diskPath);
            if (bytes == null || bytes.Length == 0) return null;

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            if (!tex.LoadImage(bytes, false))
            {
                Object.Destroy(tex);
                return null;
            }

            tex.name = cacheKey;
            var sprite = Sprite.Create(
                tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit,
                0,
                SpriteMeshType.FullRect);
            externalTileSpriteCache[cacheKey] = sprite;
            return sprite;
        }
        catch
        {
            return null;
        }
    }

    static Sprite CreateFlatTileSprite(Color color, string name, int size = 64)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                tex.SetPixel(x, y, color);
            }
        }
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.Apply();
        tex.name = name;
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect);
    }

    static Sprite CreateAtlasSprite(Texture2D texture, int colFromLeft, int rowFromTop)
    {
        if (texture == null) return null;

        int cellW = texture.width / AtlasColumns;
        int cellH = texture.height / AtlasRows;
        if (cellW <= 0 || cellH <= 0) return null;

        int pad = Mathf.Clamp(2, 0, Mathf.Min(cellW, cellH) / 8);
        int useW = cellW - pad * 2;
        int useH = cellH - pad * 2;
        if (useW <= 0 || useH <= 0) return null;

        int x = colFromLeft * cellW + pad;
        int y = texture.height - ((rowFromTop + 1) * cellH) + pad;
        if (x < 0 || y < 0 || x + useW > texture.width || y + useH > texture.height)
        {
            return null;
        }

        int ppu = Mathf.Max(1, Mathf.Min(useW, useH));
        var rect = new Rect(x, y, useW, useH);
        var sprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect);
        sprite.name = $"TileSet_{colFromLeft}_{rowFromTop}";
        return sprite;
    }

    static TileBase[] BuildRuntimeTiles(Sprite[] sprites)
    {
        if (sprites == null) return System.Array.Empty<TileBase>();

        var list = new List<TileBase>();
        for (int i = 0; i < sprites.Length; i++)
        {
            var tile = CreateRuntimeTile(sprites[i], Tile.ColliderType.None);
            if (tile != null) list.Add(tile);
        }

        return list.ToArray();
    }

    static TileBase CreateRuntimeTile(Sprite sprite, Tile.ColliderType colliderType)
    {
        if (sprite == null) return null;
        var tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = sprite;
        tile.color = Color.white;
        tile.colliderType = colliderType;
        return tile;
    }

    void EnsureWallColliderOptimization()
    {
        if (wallsTilemap == null) return;

        var wallCollider = wallsTilemap.GetComponent<TilemapCollider2D>();
        if (wallCollider == null)
        {
            wallCollider = wallsTilemap.gameObject.AddComponent<TilemapCollider2D>();
        }
        wallCollider.isTrigger = false;
        var compositeOperationProperty = typeof(Collider2D).GetProperty("compositeOperation");
        if (compositeOperationProperty != null)
        {
            try
            {
                var enumType = compositeOperationProperty.PropertyType;
                var mergeValue = System.Enum.Parse(enumType, "Merge");
                compositeOperationProperty.SetValue(wallCollider, mergeValue);
            }
            catch
            {
#pragma warning disable CS0618
                wallCollider.usedByComposite = true;
#pragma warning restore CS0618
            }
        }
        else
        {
#pragma warning disable CS0618
            wallCollider.usedByComposite = true;
#pragma warning restore CS0618
        }
        wallCollider.extrusionFactor = 0.01f;

        var rb = wallsTilemap.GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = wallsTilemap.gameObject.AddComponent<Rigidbody2D>();
        }
        rb.bodyType = RigidbodyType2D.Static;
        rb.simulated = true;

        var composite = wallsTilemap.GetComponent<CompositeCollider2D>();
        if (composite == null)
        {
            composite = wallsTilemap.gameObject.AddComponent<CompositeCollider2D>();
        }
        composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
    }

    void FindPlayer()
    {
        var mover = FindFirstObjectByType<PlayerMovement>();
        if (mover != null) player = mover.transform;
    }

    void CacheDefaultNpcSprite()
    {
        if (defaultNpcSprite != null) return;

        if (player != null)
        {
            var playerRenderer = player.GetComponent<SpriteRenderer>();
            if (playerRenderer != null && playerRenderer.sprite != null)
            {
                defaultNpcSprite = playerRenderer.sprite;
                return;
            }
        }

        if (groundTilemap != null)
        {
            var tile = FindAnyTile(groundTilemap);
            if (tile != null)
            {
                defaultNpcSprite = groundTilemap.GetSprite(tile.Value);
                if (defaultNpcSprite != null) return;
            }
        }

        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        defaultNpcSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 16f);
    }

    void RebuildHeatwaveMap()
    {
        if (groundTilemap == null || wallsTilemap == null) return;

        var bounds = groundTilemap.cellBounds;
        if (bounds.size.x <= 0 || bounds.size.y <= 0)
        {
            bounds = new BoundsInt(-24, -15, 0, 48, 30, 1);
        }
        mapBounds = bounds;

        TileBase groundTile = GetAnyTileBase(groundTilemap);
        TileBase wallTile = GetAnyTileBase(wallsTilemap) ?? groundTile;
        if (groundTile == null || wallTile == null) return;

        TileBase collisionWallTile = BuildCollisionWallTile(wallTile, groundTile);

        groundTilemap.ClearAllTiles();
        wallsTilemap.ClearAllTiles();

        int xMin = bounds.xMin;
        int xMax = bounds.xMax - 1;
        int yMin = bounds.yMin;
        int yMax = bounds.yMax - 1;
        int interiorXMin = xMin + 1;
        int interiorXMax = xMax - 1;
        int interiorYMin = yMin + 1;
        int interiorYMax = yMax - 1;

        ComputeDividerPositions(interiorXMin, interiorXMax, interiorYMin, interiorYMax);

        for (int x = xMin; x <= xMax; x++)
        {
            for (int y = yMin; y <= yMax; y++)
            {
                groundTilemap.SetTile(new Vector3Int(x, y, 0), groundTile);
            }
        }

        for (int x = xMin; x <= xMax; x++)
        {
            wallsTilemap.SetTile(new Vector3Int(x, yMin, 0), collisionWallTile);
            wallsTilemap.SetTile(new Vector3Int(x, yMax, 0), collisionWallTile);
        }

        for (int y = yMin; y <= yMax; y++)
        {
            wallsTilemap.SetTile(new Vector3Int(xMin, y, 0), collisionWallTile);
            wallsTilemap.SetTile(new Vector3Int(xMax, y, 0), collisionWallTile);
        }

        int verticalBottomDoor = interiorYMin + (interiorYMax - interiorYMin) / 4;
        int verticalMiddleDoor = middleDividerY;
        int verticalTopDoor = interiorYMin + ((interiorYMax - interiorYMin) * 3) / 4;

        BuildVerticalDivider(leftDividerX, interiorYMin, interiorYMax, collisionWallTile, verticalBottomDoor, verticalMiddleDoor, verticalTopDoor, 2);
        BuildVerticalDivider(rightDividerX, interiorYMin, interiorYMax, collisionWallTile, verticalBottomDoor, verticalMiddleDoor, verticalTopDoor, 2);

        int horizontalLeftDoor = interiorXMin + (leftDividerX - interiorXMin) / 2;
        int horizontalCenterDoor = leftDividerX + (rightDividerX - leftDividerX) / 2;
        int horizontalRightDoor = rightDividerX + (interiorXMax - rightDividerX) / 2;

        BuildHorizontalDivider(middleDividerY, interiorXMin, interiorXMax, collisionWallTile, horizontalLeftDoor, horizontalCenterDoor, horizontalRightDoor, 2);
        ApplyPixelPalette(xMin, xMax, yMin, yMax);
    }

    TileBase BuildCollisionWallTile(TileBase preferredWallTile, TileBase fallbackGroundTile)
    {
        if (tilePaletteReady && wallPaletteTile != null)
        {
            return wallPaletteTile;
        }

        if (preferredWallTile is Tile preferredTile && preferredTile.colliderType != Tile.ColliderType.None)
        {
            return preferredWallTile;
        }

        if (preferredWallTile is Tile preferredTileWithSprite && preferredTileWithSprite.sprite != null)
        {
            return CreateRuntimeTile(preferredTileWithSprite.sprite, Tile.ColliderType.Grid);
        }

        if (fallbackGroundTile is Tile groundTile && groundTile.sprite != null)
        {
            return CreateRuntimeTile(groundTile.sprite, Tile.ColliderType.Grid);
        }

        var solidSprite = GetSolidSprite();
        return CreateRuntimeTile(solidSprite, Tile.ColliderType.Grid);
    }

    void ComputeDividerPositions(int interiorXMin, int interiorXMax, int interiorYMin, int interiorYMax)
    {
        int interiorWidth = Mathf.Max(1, interiorXMax - interiorXMin + 1);
        int interiorHeight = Mathf.Max(1, interiorYMax - interiorYMin + 1);

        leftDividerX = interiorXMin + interiorWidth / 3;
        rightDividerX = interiorXMin + (interiorWidth * 2) / 3;
        middleDividerY = interiorYMin + interiorHeight / 2;

        leftDividerX = Mathf.Clamp(leftDividerX, interiorXMin + 2, interiorXMax - 4);
        rightDividerX = Mathf.Clamp(rightDividerX, leftDividerX + 3, interiorXMax - 2);
        middleDividerY = Mathf.Clamp(middleDividerY, interiorYMin + 2, interiorYMax - 2);
    }

    void BuildVerticalDivider(int x, int yMin, int yMax, TileBase wallTile, int doorY1, int doorY2, int doorY3, int doorHalfWidth)
    {
        for (int y = yMin; y <= yMax; y++)
        {
            bool door1 = Mathf.Abs(y - doorY1) <= doorHalfWidth;
            bool door2 = Mathf.Abs(y - doorY2) <= doorHalfWidth;
            bool door3 = Mathf.Abs(y - doorY3) <= doorHalfWidth;
            if (door1 || door2 || door3) continue;

            wallsTilemap.SetTile(new Vector3Int(x, y, 0), wallTile);
        }
    }

    void BuildHorizontalDivider(int y, int xMin, int xMax, TileBase wallTile, int doorX1, int doorX2, int doorX3, int doorHalfWidth)
    {
        for (int x = xMin; x <= xMax; x++)
        {
            bool door1 = Mathf.Abs(x - doorX1) <= doorHalfWidth;
            bool door2 = Mathf.Abs(x - doorX2) <= doorHalfWidth;
            bool door3 = Mathf.Abs(x - doorX3) <= doorHalfWidth;
            if (door1 || door2 || door3) continue;

            wallsTilemap.SetTile(new Vector3Int(x, y, 0), wallTile);
        }
    }

    void ApplyPixelPalette(int xMin, int xMax, int yMin, int yMax)
    {
        if (groundTilemap == null || wallsTilemap == null) return;

        var earthA = new Color(0.46f, 0.34f, 0.23f, 1f);
        var earthB = new Color(0.52f, 0.39f, 0.28f, 1f);
        var earthC = new Color(0.42f, 0.30f, 0.21f, 1f);
        var earthD = new Color(0.56f, 0.43f, 0.31f, 1f);
        var road = new Color(0.63f, 0.49f, 0.35f, 1f);
        var sidewalk = new Color(0.69f, 0.55f, 0.40f, 1f);
        var wallColor = new Color(0.23f, 0.17f, 0.12f, 1f);

        for (int x = xMin; x <= xMax; x++)
        {
            for (int y = yMin; y <= yMax; y++)
            {
                var cell = new Vector3Int(x, y, 0);
                if (!groundTilemap.HasTile(cell)) continue;

                bool onMainRoad = Mathf.Abs(x - leftDividerX) <= 1 ||
                                  Mathf.Abs(x - rightDividerX) <= 1 ||
                                  Mathf.Abs(y - middleDividerY) <= 1;
                bool onRoadEdge = Mathf.Abs(x - leftDividerX) == 2 ||
                                  Mathf.Abs(x - rightDividerX) == 2 ||
                                  Mathf.Abs(y - middleDividerY) == 2;

                int hash = Mathf.Abs(((x / 2) * 73856093) ^ ((y / 2) * 19349663) ^ ((x + y) * 83492791));
                Color c = onMainRoad
                    ? road
                    : (onRoadEdge
                        ? sidewalk
                        : ((hash % 4 == 0) ? earthA : ((hash % 4 == 1) ? earthB : ((hash % 4 == 2) ? earthC : earthD))));

                if (tilePaletteReady && terrainTiles != null && terrainTiles.Length > 0)
                {
                    int terrainCount = Mathf.Max(1, terrainTiles.Length);
                    TileBase terrainTile = terrainTiles[hash % terrainCount];

                    if (onMainRoad)
                    {
                        bool verticalRoad = Mathf.Abs(x - leftDividerX) <= 1 || Mathf.Abs(x - rightDividerX) <= 1;
                        bool horizontalRoad = Mathf.Abs(y - middleDividerY) <= 1;
                        if (verticalRoad && horizontalRoad)
                        {
                            terrainTile = roadCenterTile ?? roadTile ?? terrainTile;
                        }
                        else if (verticalRoad)
                        {
                            terrainTile = roadVerticalTile ?? roadTile ?? terrainTile;
                        }
                        else
                        {
                            terrainTile = roadHorizontalTile ?? roadTile ?? terrainTile;
                        }
                    }
                    else if (onRoadEdge)
                    {
                        terrainTile = roadShoulderTile ?? roadTile ?? terrainTile;
                    }
                    else if (roadVariantTiles != null && roadVariantTiles.Length > 0 && (hash % 17 == 0))
                    {
                        terrainTile = roadVariantTiles[hash % roadVariantTiles.Length];
                    }

                    groundTilemap.SetTile(cell, terrainTile);

                    float shade = 0.92f + ((hash % 11) * 0.014f);
                    float warm = 0.95f + ((hash / 7) % 5) * 0.01f;
                    c = new Color(
                        Mathf.Clamp01(shade),
                        Mathf.Clamp01(shade * 0.97f),
                        Mathf.Clamp01(shade * 0.93f),
                        1f
                    );
                    if (onMainRoad)
                    {
                        c = new Color(
                            Mathf.Clamp01(warm),
                            Mathf.Clamp01(warm * 0.97f),
                            Mathf.Clamp01(warm * 0.91f),
                            1f
                        );
                    }
                }

                groundTilemap.SetTileFlags(cell, TileFlags.None);
                groundTilemap.SetColor(cell, c);

                if (wallsTilemap.HasTile(cell))
                {
                    if (tilePaletteReady && wallPaletteTile != null)
                    {
                        wallsTilemap.SetTile(cell, wallPaletteTile);
                    }
                    wallsTilemap.SetTileFlags(cell, TileFlags.None);
                    wallsTilemap.SetColor(cell, tilePaletteReady ? Color.white : wallColor);
                }
            }
        }
    }

    void BuildRoomDefinitions()
    {
        rooms.Clear();

        int xMin = mapBounds.xMin + 1;
        int xMax = mapBounds.xMax - 2;
        int yMin = mapBounds.yMin + 1;
        int yMax = mapBounds.yMax - 2;

        TryAddRoom("Old Quarter", xMin, leftDividerX - 1, middleDividerY + 1, yMax);
        TryAddRoom("Library Block", leftDividerX + 1, rightDividerX - 1, middleDividerY + 1, yMax);
        TryAddRoom("Cooling Center", rightDividerX + 1, xMax, middleDividerY + 1, yMax);
        TryAddRoom("South District", xMin, leftDividerX - 1, yMin, middleDividerY - 1);
        TryAddRoom("Transit Hub", leftDividerX + 1, rightDividerX - 1, yMin, middleDividerY - 1);
        TryAddRoom("Mayor Plaza", rightDividerX + 1, xMax, yMin, middleDividerY - 1);

        if (rooms.Count == 0)
        {
            AddRoom("Old Quarter", -23, -9, 1, 13);
            AddRoom("Library Block", -7, 7, 1, 13);
            AddRoom("Cooling Center", 9, 22, 1, 13);
            AddRoom("South District", -23, -9, -14, -1);
            AddRoom("Transit Hub", -7, 7, -14, -1);
            AddRoom("Mayor Plaza", 9, 22, -14, -1);
        }
    }

    void TryAddRoom(string name, int xMin, int xMax, int yMin, int yMax)
    {
        if (xMin > xMax || yMin > yMax) return;
        AddRoom(name, xMin, xMax, yMin, yMax);
    }

    void EnsureDecorRoots()
    {
        var decorObj = GameObject.Find("HeatwaveDecor");
        if (decorObj != null)
        {
            decorRoot = decorObj.transform;
            for (int i = decorRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(decorRoot.GetChild(i).gameObject);
            }
        }
        else
        {
            decorRoot = new GameObject("HeatwaveDecor").transform;
        }

        var taskObj = GameObject.Find("HeatwaveTaskSites");
        if (taskObj != null)
        {
            taskRoot = taskObj.transform;
            for (int i = taskRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(taskRoot.GetChild(i).gameObject);
            }
        }
        else
        {
            taskRoot = new GameObject("HeatwaveTaskSites").transform;
        }

        spawnedTaskSites.Clear();
        objectiveAnchors.Clear();
    }

    void BuildRoomDecorations()
    {
        if (decorRoot == null || rooms.Count == 0) return;

        if (cleanVisualStyle)
        {
            BuildMinimalRoomDecorations();
            return;
        }

        for (int i = 0; i < rooms.Count; i++)
        {
            var room = rooms[i];
            var center = RoomCenterWorld(room);
            var size = room.CellRect.size;
            AddRoomSign(room.Name, center + new Vector3(0f, size.y * 0.42f, 0f));

            switch (room.Name)
            {
                case "Old Quarter":
                    CreateDecorBlock("Apartments_A", center + new Vector3(-size.x * 0.18f, 1.8f, 0f), new Vector2(5f, 3f), new Color(0.41f, 0.33f, 0.26f, 1f));
                    CreateDecorBlock("Apartments_B", center + new Vector3(size.x * 0.16f, -0.3f, 0f), new Vector2(4f, 2.8f), new Color(0.49f, 0.38f, 0.30f, 1f));
                    CreateDecorBlock("CourtyardShade", center + new Vector3(0f, -2.7f, 0f), new Vector2(3.2f, 1.2f), new Color(0.32f, 0.40f, 0.26f, 1f));
                    break;
                case "Library Block":
                    CreateDecorBlock("LibraryHall", center + new Vector3(0f, 1.2f, 0f), new Vector2(6.2f, 3.2f), new Color(0.53f, 0.41f, 0.29f, 1f));
                    CreateDecorBlock("WaterStand", center + new Vector3(-2.4f, -2.2f, 0f), new Vector2(1.2f, 1.2f), new Color(0.39f, 0.58f, 0.72f, 1f));
                    CreateDecorBlock("BenchRow", center + new Vector3(2.2f, -2.0f, 0f), new Vector2(2.4f, 0.8f), new Color(0.46f, 0.34f, 0.22f, 1f));
                    break;
                case "Cooling Center":
                    CreateDecorBlock("CoolingTentA", center + new Vector3(-2.5f, 1.5f, 0f), new Vector2(3.4f, 2.0f), new Color(0.60f, 0.66f, 0.74f, 1f));
                    CreateDecorBlock("CoolingTentB", center + new Vector3(2.3f, 1.2f, 0f), new Vector2(3.0f, 1.8f), new Color(0.67f, 0.73f, 0.80f, 1f));
                    CreateDecorBlock("SupplyCrates", center + new Vector3(0.1f, -2.4f, 0f), new Vector2(2.6f, 1.0f), new Color(0.45f, 0.35f, 0.25f, 1f));
                    break;
                case "South District":
                    CreateDecorBlock("Substation", center + new Vector3(0f, 1.3f, 0f), new Vector2(5.4f, 2.6f), new Color(0.37f, 0.34f, 0.31f, 1f));
                    CreateDecorBlock("BatteryRack", center + new Vector3(-2.4f, -2.0f, 0f), new Vector2(2.4f, 1.0f), new Color(0.29f, 0.30f, 0.33f, 1f));
                    CreateDecorBlock("ControlBooth", center + new Vector3(2.4f, -2.0f, 0f), new Vector2(1.8f, 1.4f), new Color(0.45f, 0.43f, 0.37f, 1f));
                    break;
                case "Transit Hub":
                    CreateDecorBlock("Platform", center + new Vector3(0f, 0.8f, 0f), new Vector2(6.5f, 1.3f), new Color(0.56f, 0.50f, 0.38f, 1f));
                    CreateDecorBlock("TicketKiosk", center + new Vector3(-2.7f, -1.9f, 0f), new Vector2(1.6f, 1.6f), new Color(0.50f, 0.39f, 0.28f, 1f));
                    CreateDecorBlock("ShadeAwning", center + new Vector3(2.4f, -1.9f, 0f), new Vector2(2.8f, 1.0f), new Color(0.40f, 0.45f, 0.30f, 1f));
                    break;
                case "Mayor Plaza":
                    CreateDecorBlock("CityHall", center + new Vector3(0f, 1.6f, 0f), new Vector2(6.8f, 3.2f), new Color(0.58f, 0.46f, 0.34f, 1f));
                    CreateDecorBlock("InfoBoard", center + new Vector3(-2.4f, -2.0f, 0f), new Vector2(1.6f, 1.4f), new Color(0.44f, 0.32f, 0.23f, 1f));
                    CreateDecorBlock("Fountain", center + new Vector3(2.2f, -2.2f, 0f), new Vector2(1.8f, 1.8f), new Color(0.42f, 0.56f, 0.65f, 1f));
                    break;
            }
        }

        BuildRoomDoorFrames();
        BuildInteriorPartitions();
    }

    void BuildMinimalRoomDecorations()
    {
        for (int i = 0; i < rooms.Count; i++)
        {
            var room = rooms[i];
            var center = RoomCenterWorld(room);
            var size = room.CellRect.size;
            AddRoomSign(room.Name, center + new Vector3(0f, size.y * 0.42f, 0f));

            switch (room.Name)
            {
                case "Old Quarter":
                    CreateDecorBlock("CleanRock", center + new Vector3(-2.0f, -1.8f, 0f), new Vector2(1.1f, 1.1f), Color.white);
                    CreateDecorBlock("CleanCactus", center + new Vector3(2.0f, -1.8f, 0f), new Vector2(1.0f, 1.0f), Color.white);
                    break;
                case "Library Block":
                    CreateDecorBlock("CleanSign", center + new Vector3(0f, -2.0f, 0f), new Vector2(1.1f, 1.1f), Color.white);
                    break;
                case "Cooling Center":
                    CreateDecorBlock("CleanWindow", center + new Vector3(0f, -1.9f, 0f), new Vector2(1.0f, 1.0f), Color.white);
                    break;
                case "South District":
                    CreateDecorBlock("CleanFence", center + new Vector3(0f, -2.0f, 0f), new Vector2(1.3f, 1.0f), Color.white);
                    break;
                case "Transit Hub":
                    CreateDecorBlock("CleanSign", center + new Vector3(-1.6f, -1.9f, 0f), new Vector2(1.0f, 1.0f), Color.white);
                    CreateDecorBlock("CleanRock", center + new Vector3(1.8f, -1.9f, 0f), new Vector2(1.0f, 1.0f), Color.white);
                    break;
                case "Mayor Plaza":
                    CreateDecorBlock("CleanWindow", center + new Vector3(0f, -1.9f, 0f), new Vector2(1.0f, 1.0f), Color.white);
                    break;
            }
        }
    }

    void AddRoomSign(string roomName, Vector3 worldPos)
    {
        var signText = roomName.ToUpperInvariant();
        var sign = new GameObject($"Sign_{roomName.Replace(' ', '_')}");
        sign.transform.SetParent(decorRoot, false);
        sign.transform.position = worldPos;

        AddWorldLabel(
            sign.transform,
            signText,
            0f,
            new Color(1f, 0.94f, 0.76f, 1f),
            0.36f,
            7
        );
    }

    void BuildRoomDoorFrames()
    {
        float[] doorYs =
        {
            mapBounds.yMin + 1 + (mapBounds.size.y - 2) / 4f,
            middleDividerY,
            mapBounds.yMin + 1 + ((mapBounds.size.y - 2) * 3f) / 4f
        };
        for (int i = 0; i < doorYs.Length; i++)
        {
            CreateDecorBlock("DoorPostL", new Vector3(leftDividerX - 0.6f, doorYs[i], 0f), new Vector2(0.35f, 1.4f), new Color(0.30f, 0.21f, 0.14f, 1f));
            CreateDecorBlock("DoorPostR", new Vector3(leftDividerX + 0.6f, doorYs[i], 0f), new Vector2(0.35f, 1.4f), new Color(0.30f, 0.21f, 0.14f, 1f));
            CreateDecorBlock("DoorPostL", new Vector3(rightDividerX - 0.6f, doorYs[i], 0f), new Vector2(0.35f, 1.4f), new Color(0.30f, 0.21f, 0.14f, 1f));
            CreateDecorBlock("DoorPostR", new Vector3(rightDividerX + 0.6f, doorYs[i], 0f), new Vector2(0.35f, 1.4f), new Color(0.30f, 0.21f, 0.14f, 1f));
        }
    }

    void BuildInteriorPartitions()
    {
        if (decorRoot == null) return;

        for (int i = 0; i < rooms.Count; i++)
        {
            var room = rooms[i];
            var center = RoomCenterWorld(room);
            var size = room.CellRect.size;
            Color partitionColor = new Color(0.32f, 0.24f, 0.17f, 0.88f);

            if (i % 2 == 0)
            {
                CreateDecorBlock(
                    $"PartitionV_{room.Name}",
                    center + new Vector3(0f, 0.2f, 0f),
                    new Vector2(0.42f, Mathf.Clamp(size.y * 0.42f, 3.2f, 5.8f)),
                    partitionColor
                );
                CreateDecorBlock(
                    $"PartitionV_Door_{room.Name}",
                    center + new Vector3(0f, -1.6f, 0f),
                    new Vector2(1.6f, 0.52f),
                    new Color(0.71f, 0.56f, 0.37f, 0.95f)
                );
            }
            else
            {
                CreateDecorBlock(
                    $"PartitionH_{room.Name}",
                    center + new Vector3(0f, 0.2f, 0f),
                    new Vector2(Mathf.Clamp(size.x * 0.44f, 3.6f, 6.0f), 0.42f),
                    partitionColor
                );
                CreateDecorBlock(
                    $"PartitionH_Door_{room.Name}",
                    center + new Vector3(1.6f, 0.2f, 0f),
                    new Vector2(0.52f, 1.4f),
                    new Color(0.71f, 0.56f, 0.37f, 0.95f)
                );
            }
        }
    }

    void CreateDecorBlock(string baseName, Vector3 worldPos, Vector2 size, Color color)
    {
        if (decorRoot == null) return;

        var go = new GameObject($"{baseName}_{decorRoot.childCount}");
        go.transform.SetParent(decorRoot, false);
        go.transform.position = new Vector3(worldPos.x, worldPos.y, 0f);

        var sr = go.AddComponent<SpriteRenderer>();
        var decorSprite = GetDecorSpriteForName(baseName);
        if (decorSprite != null)
        {
            sr.sprite = decorSprite;
            sr.color = Color.white;
            sr.sortingOrder = 2;
            float sx = Mathf.Clamp(size.x * 0.55f, 0.9f, 6.2f);
            float sy = Mathf.Clamp(size.y * 0.55f, 0.9f, 4.8f);
            go.transform.localScale = new Vector3(sx, sy, 1f);
            return;
        }

        sr.sprite = GetSolidSprite();
        sr.color = color;
        sr.sortingOrder = 2;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
    }

    Sprite GetDecorSpriteForName(string baseName)
    {
        if (baseName.StartsWith("CleanCactus")) return LoadExternalTileSprite("clean_cactus", "Assets/ExternalAssets/Kenney/new-platformer-pack/Sprites/Tiles/Default/cactus.png");
        if (baseName.StartsWith("CleanRock")) return LoadExternalTileSprite("clean_rock", "Assets/ExternalAssets/Kenney/new-platformer-pack/Sprites/Tiles/Default/rock.png");
        if (baseName.StartsWith("CleanSign")) return LoadExternalTileSprite("clean_sign", "Assets/ExternalAssets/Kenney/new-platformer-pack/Sprites/Tiles/Default/sign.png");
        if (baseName.StartsWith("CleanWindow")) return LoadExternalTileSprite("clean_window", "Assets/ExternalAssets/Kenney/new-platformer-pack/Sprites/Tiles/Default/window.png");
        if (baseName.StartsWith("CleanFence")) return LoadExternalTileSprite("clean_fence", "Assets/ExternalAssets/Kenney/new-platformer-pack/Sprites/Tiles/Default/fence.png");

        return null;
    }

    void SpawnTaskSites()
    {
        if (taskRoot == null || rooms.Count == 0) return;

        SpawnTaskSite("Library Block", "TASK_MAYA", "Cooling Hall Check", new Vector3(0f, -2.8f, 0f), 1);
        SpawnTaskSite("South District", "TASK_DANIEL", "Transformer Block Audit", new Vector3(0f, -2.6f, 0f), 1);
        SpawnTaskSite("Old Quarter", "TASK_ALVAREZ", "Resident Home Visit", new Vector3(0f, -2.9f, 0f), 1);
        SpawnTaskSite("Transit Hub", "TASK_TRANSIT", "Platform Heat Survey", new Vector3(0f, -2.6f, 0f), 3);
        SpawnTaskSite("Cooling Center", "TASK_KAI", "Shade Corridor Survey", new Vector3(0f, -2.7f, 0f), 4);
        SpawnTaskSite("Mayor Plaza", "TASK_ROWAN", "City Alert Audit", new Vector3(-1.5f, -2.7f, 0f), 2);
        SpawnTaskSite("Mayor Plaza", "TASK_FINAL", "Final Audit Submission", new Vector3(1.5f, -2.7f, 0f), 7);
    }

    void SpawnTaskSite(string roomName, string taskKey, string taskName, Vector3 offset, int minDay)
    {
        if (spawnedTaskSites.Contains(taskKey)) return;
        if (!TryGetRoom(roomName, out var room)) return;

        var go = new GameObject($"Task_{taskKey}");
        go.transform.SetParent(taskRoot, false);
        go.transform.position = RoomCenterWorld(room) + offset;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetSolidSprite();
        sr.color = new Color(0.87f, 0.69f, 0.34f, 1f);
        sr.sortingOrder = 4;
        go.transform.localScale = new Vector3(0.9f, 0.9f, 1f);

        var col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(0.9f, 0.9f);

        var interactable = go.AddComponent<HeatwaveFieldTaskInteractable>();
        interactable.taskKey = taskKey;
        interactable.taskName = $"{taskName} (Day {minDay}+)";
        interactable.interactionDistance = 1.25f;

        AddWorldLabel(go.transform, taskName, 1.16f, new Color(1f, 0.89f, 0.48f, 1f), 0.50f, 7);
        spawnedTaskSites.Add(taskKey);
        objectiveAnchors[taskKey] = go.transform;
    }

    bool TryGetRoom(string roomName, out RoomData roomData)
    {
        for (int i = 0; i < rooms.Count; i++)
        {
            if (rooms[i].Name == roomName)
            {
                roomData = rooms[i];
                return true;
            }
        }

        roomData = default;
        return false;
    }

    Vector3 RoomCenterWorld(RoomData room)
    {
        var centerCell = new Vector3Int(
            room.CellRect.xMin + room.CellRect.width / 2,
            room.CellRect.yMin + room.CellRect.height / 2,
            0
        );
        return groundTilemap != null ? groundTilemap.GetCellCenterWorld(centerCell) : Vector3.zero;
    }

    void AddRoom(string name, int xMin, int xMax, int yMin, int yMax)
    {
        rooms.Add(new RoomData
        {
            Name = name,
            CellRect = new RectInt(xMin, yMin, xMax - xMin + 1, yMax - yMin + 1)
        });
    }

    int GetCurrentRoomIndex()
    {
        if (groundTilemap == null || player == null) return -1;
        var cell = groundTilemap.WorldToCell(player.position);
        var point = new Vector2Int(cell.x, cell.y);

        for (int i = 0; i < rooms.Count; i++)
        {
            if (rooms[i].CellRect.Contains(point))
            {
                return i;
            }
        }

        return -1;
    }

    void HandleRoomEntered(int roomIndex)
    {
        if (roomIndex < 0 || roomIndex >= rooms.Count) return;

        string roomName = rooms[roomIndex].Name;
        ShowEnteringText($"Entering... {roomName}");

        bool roomAlreadyHasNpc = npcSpawnedRooms.Contains(roomIndex);
        if (!roomAlreadyHasNpc && npcCount < maxNpcCount)
        {
            SpawnNpcInRoom(roomIndex);
            npcSpawnedRooms.Add(roomIndex);
        }
    }

    void CreateHeatHazeLayers()
    {
        if (decorRoot == null || mapBounds.size.x <= 0 || mapBounds.size.y <= 0) return;

        Vector3 center = new Vector3(
            mapBounds.xMin + mapBounds.size.x * 0.5f,
            mapBounds.yMin + mapBounds.size.y * 0.5f,
            0f
        );
        Vector3 scale = new Vector3(mapBounds.size.x * 1.2f, mapBounds.size.y * 1.2f, 1f);

        hazeA = CreateHazeLayer("HeatHaze_A", center + new Vector3(0f, 0.6f, 0f), scale, new Color(1f, 0.73f, 0.34f, 0.065f), 1);
        hazeB = CreateHazeLayer("HeatHaze_B", center + new Vector3(0f, -0.4f, 0f), scale * 0.88f, new Color(1f, 0.58f, 0.28f, 0.048f), 1);
    }

    SpriteRenderer CreateHazeLayer(string name, Vector3 worldPos, Vector3 worldScale, Color color, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(decorRoot, false);
        go.transform.position = worldPos;
        go.transform.localScale = worldScale;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetSolidSprite();
        sr.color = color;
        sr.sortingOrder = order;
        return sr;
    }

    void UpdateHeatHaze()
    {
        float t = Time.time;
        if (hazeA != null)
        {
            var c = hazeA.color;
            c.a = 0.052f + Mathf.Sin(t * 0.31f) * 0.018f;
            hazeA.color = c;
        }

        if (hazeB != null)
        {
            var c = hazeB.color;
            c.a = 0.040f + Mathf.Sin(t * 0.47f + 1.2f) * 0.016f;
            hazeB.color = c;
        }
    }

    void EnsureObjectiveBeacon()
    {
        if (objectiveBeacon != null) return;

        var go = new GameObject("ObjectiveBeacon");
        objectiveBeacon = go.transform;
        Transform worldParent = groundTilemap != null ? groundTilemap.transform.parent : null;
        objectiveBeacon.SetParent(worldParent, false);

        objectiveBeaconRenderer = go.AddComponent<SpriteRenderer>();
        objectiveBeaconRenderer.sprite = GetSolidSprite();
        objectiveBeaconRenderer.color = new Color(1f, 0.84f, 0.42f, 0.78f);
        objectiveBeaconRenderer.sortingOrder = 10;
        objectiveBeacon.localScale = new Vector3(0.56f, 0.56f, 1f);

        var textGo = new GameObject("ObjectiveBeaconText");
        textGo.transform.SetParent(objectiveBeacon, false);
        textGo.transform.localPosition = new Vector3(0f, 1.1f, 0f);
        textGo.transform.localScale = new Vector3(0.26f, 0.26f, 0.26f);
        objectiveBeaconText = textGo.AddComponent<TextMeshPro>();
        objectiveBeaconText.text = "OBJECTIVE";
        objectiveBeaconText.fontSize = 9.1f;
        objectiveBeaconText.alignment = TextAlignmentOptions.Center;
        HeatwaveUIFontKit.ApplyReadableTMP(
            objectiveBeaconText,
            9.1f,
            new Color(1f, 0.95f, 0.72f, 1f),
            new Color(0.05f, 0.04f, 0.03f, 1f),
            0.26f
        );
        objectiveBeaconText.textWrappingMode = TextWrappingModes.NoWrap;
    }

    void UpdateObjectiveBeacon()
    {
        if (objectiveBeacon == null || objectiveBeaconRenderer == null) return;
        if (HeatwaveCityGameController.Instance == null)
        {
            objectiveBeacon.gameObject.SetActive(false);
            return;
        }

        bool hasObjective = HeatwaveCityGameController.Instance.TryGetCurrentObjective(out var objectiveCode, out _);
        if (!hasObjective || string.IsNullOrWhiteSpace(objectiveCode) || !objectiveAnchors.TryGetValue(objectiveCode, out var target) || target == null)
        {
            objectiveBeacon.gameObject.SetActive(false);
            return;
        }

        objectiveBeacon.gameObject.SetActive(true);
        float pulse = 0.54f + Mathf.Sin(Time.time * 5.1f) * 0.10f;
        objectiveBeacon.localScale = new Vector3(pulse, pulse, 1f);
        objectiveBeacon.position = target.position + new Vector3(0f, 1.95f, 0f);
        objectiveBeaconRenderer.color = new Color(1f, 0.84f, 0.42f, 0.56f + Mathf.Sin(Time.time * 5.1f) * 0.22f);
    }

    void EnsureEnteringTextUI()
    {
        var canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        var existingRoot = GameObject.Find("RoomEnteringRoot");
        if (existingRoot != null)
        {
            enteringRoot = existingRoot;
            enteringText = existingRoot.GetComponentInChildren<TMP_Text>(true);
            if (enteringText != null)
            {
                HeatwaveUIFontKit.ApplyReadableTMP(
                    enteringText,
                    36f,
                    new Color(1f, 0.94f, 0.76f, 1f),
                    new Color(0.06f, 0.04f, 0.03f, 1f),
                    0.29f
                );
            }
            var rootRect = existingRoot.GetComponent<RectTransform>();
            if (rootRect != null)
            {
                HeatwaveUIFontKit.ApplyPixelFrame(
                    rootRect,
                    new Color(0.09f, 0.08f, 0.07f, 0.93f),
                    new Color(0.70f, 0.54f, 0.31f, 1f),
                    new Color(1f, 0.88f, 0.58f, 1f),
                    5f,
                    12f
                );
            }
            return;
        }

        var existing = GameObject.Find("RoomEnteringText");
        if (existing != null)
        {
            enteringRoot = existing;
            enteringText = existing.GetComponent<TMP_Text>();
            if (enteringText != null)
            {
                HeatwaveUIFontKit.ApplyReadableTMP(
                    enteringText,
                    36f,
                    new Color(1f, 0.94f, 0.76f, 1f),
                    new Color(0.06f, 0.04f, 0.03f, 1f),
                    0.29f
                );
            }
            return;
        }

        var root = new GameObject("RoomEnteringRoot", typeof(RectTransform), typeof(Image));
        root.transform.SetParent(canvas.transform, false);
        enteringRoot = root;

        var rect = root.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -16f);
        rect.sizeDelta = new Vector2(840f, 74f);
        HeatwaveUIFontKit.ApplyPixelFrame(
            rect,
            new Color(0.09f, 0.08f, 0.07f, 0.93f),
            new Color(0.70f, 0.54f, 0.31f, 1f),
            new Color(1f, 0.88f, 0.58f, 1f),
            5f,
            12f
        );

        var textGO = new GameObject("RoomEnteringText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGO.transform.SetParent(root.transform, false);
        var textRect = textGO.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(14f, 10f);
        textRect.offsetMax = new Vector2(-14f, -10f);

        enteringText = textGO.GetComponent<TextMeshProUGUI>();
        enteringText.alignment = TextAlignmentOptions.Center;
        enteringText.textWrappingMode = TextWrappingModes.NoWrap;
        HeatwaveUIFontKit.ApplyReadableTMP(
            enteringText,
            36f,
            new Color(1f, 0.94f, 0.76f, 1f),
            new Color(0.06f, 0.04f, 0.03f, 1f),
            0.29f
        );
        enteringRoot.SetActive(false);
    }

    void EnsureNpcDialogueUI()
    {
        if (FindFirstObjectByType<HeatwaveNpcDialogueUI>() != null) return;

        var canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;
        canvas.gameObject.AddComponent<HeatwaveNpcDialogueUI>();
    }

    void ShowEnteringText(string text)
    {
        if (enteringText == null || enteringRoot == null) return;
        enteringText.text = text;
        enteringRoot.SetActive(true);
        messageUntil = Time.time + enterMessageDuration;
    }

    void UpdateEnteringTextVisibility()
    {
        if (enteringRoot == null) return;
        if (enteringRoot.activeSelf && Time.time > messageUntil)
        {
            enteringRoot.SetActive(false);
        }
    }

    void SpawnAllRoomNpcs()
    {
        if (rooms.Count == 0) return;
        int toSpawn = Mathf.Clamp(Mathf.Min(maxNpcCount, rooms.Count), 0, rooms.Count);

        for (int i = 0; i < toSpawn; i++)
        {
            int roomIndex = i;
            SpawnNpcInRoom(roomIndex);
            npcSpawnedRooms.Add(roomIndex);
        }
    }

    void SpawnNpcInRoom(int roomIndex)
    {
        if (roomIndex < 0 || roomIndex >= rooms.Count) return;
        if (defaultNpcSprite == null) CacheDefaultNpcSprite();

        if (!TryGetFixedNpcSpawnWorld(rooms[roomIndex], out var spawnWorld) &&
            !TryFindSpawnWorld(rooms[roomIndex], out spawnWorld))
        {
            spawnWorld = player != null ? player.position + new Vector3(1.2f, 0f, 0f) : Vector3.zero;
        }

        var room = rooms[roomIndex];
        var npc = new GameObject($"NPC_{npcCount + 1}_{room.Name.Replace(' ', '_')}");
        npcCount++;

        npc.transform.position = new Vector3(spawnWorld.x, spawnWorld.y, 0f);
        npc.transform.localScale = new Vector3(npcVisualScale, npcVisualScale, 1f);

        var sr = npc.AddComponent<SpriteRenderer>();
        sr.sprite = GetNpcSpriteForRoom(room.Name);
        sr.sortingOrder = 3;
        sr.color = Color.white;

        var collider = npc.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = 0.35f;

        var marker = npc.AddComponent<HeatwaveNpcMarker>();
        marker.roomName = room.Name;

        var interactable = npc.AddComponent<HeatwaveNpcInteractable>();
        interactable.npcName = GetNpcNameForRoom(room.Name);
        interactable.primaryYarnNode = GetPrimaryYarnNodeForRoom(room.Name);
        interactable.secondaryYarnNode = GetSecondaryYarnNodeForRoom(room.Name);
        interactable.completionVariable = GetCompletionVariableForRoom(room.Name);
        interactable.unlockVariable = GetUnlockVariableForRoom(room.Name);
        interactable.lockedLine = GetLockedLineForRoom(room.Name);
        interactable.minDay = GetMinDayForRoom(room.Name);
        interactable.dayLockedLine = GetDayLockedLineForRoom(room.Name);
        interactable.firstMeetVariable = GetFirstMeetVariableForRoom(room.Name);
        interactable.firstMeetLines = GetFirstMeetLinesForRoom(room.Name);
        interactable.lines = GetLinesForRoom(room.Name);
        interactable.interactionDistance = 1.45f;

        AddNpcNameTag(npc.transform, interactable.npcName);

        string objectiveCode = GetNpcObjectiveCodeForRoom(room.Name);
        if (!string.IsNullOrWhiteSpace(objectiveCode))
        {
            objectiveAnchors[objectiveCode] = npc.transform;
        }
    }

    bool TryGetFixedNpcSpawnWorld(RoomData room, out Vector3 spawnWorld)
    {
        spawnWorld = Vector3.zero;
        if (groundTilemap == null) return false;

        Vector3 center = RoomCenterWorld(room);
        Vector3 offset = GetNpcSpawnOffset(room.Name);
        Vector3 desired = center + offset;
        Vector3Int cell = groundTilemap.WorldToCell(desired);

        if (!groundTilemap.HasTile(cell)) return false;
        if (wallsTilemap != null && wallsTilemap.HasTile(cell)) return false;

        spawnWorld = groundTilemap.GetCellCenterWorld(cell);
        return true;
    }

    static Vector3 GetNpcSpawnOffset(string roomName)
    {
        switch (roomName)
        {
            case "Old Quarter": return new Vector3(-2.2f, 1.1f, 0f);
            case "Library Block": return new Vector3(0f, 1.2f, 0f);
            case "Cooling Center": return new Vector3(2.1f, 0.9f, 0f);
            case "South District": return new Vector3(-1.9f, 0.8f, 0f);
            case "Transit Hub": return new Vector3(1.8f, 0.9f, 0f);
            case "Mayor Plaza": return new Vector3(0.2f, 1.0f, 0f);
            default: return Vector3.zero;
        }
    }

    static string GetNpcNameForRoom(string roomName)
    {
        switch (roomName)
        {
            case "Old Quarter": return "Mrs. Alvarez";
            case "Library Block": return "Maya";
            case "Cooling Center": return "Kai";
            case "South District": return "Daniel";
            case "Transit Hub": return "Station Worker Jae";
            case "Mayor Plaza": return "City Clerk Rowan";
            default: return "Resident";
        }
    }

    static string GetNpcObjectiveCodeForRoom(string roomName)
    {
        switch (roomName)
        {
            case "Library Block": return "NPC_MAYA";
            case "South District": return "NPC_DANIEL";
            case "Old Quarter": return "NPC_ALVAREZ";
            case "Transit Hub": return "NPC_JAE";
            case "Cooling Center": return "NPC_KAI";
            case "Mayor Plaza": return "NPC_ROWAN";
            default: return string.Empty;
        }
    }

    static string[] GetLinesForRoom(string roomName)
    {
        switch (roomName)
        {
            case "Old Quarter":
                return new[]
                {
                    "Hi Mayor, I am Mrs. Alvarez from Old Quarter.",
                    "Our apartments trap heat overnight, and seniors are getting weaker each day.",
                    "Please help us with buddy checks, transport, and cooling support."
                };
            case "Library Block":
                return new[]
                {
                    "Hi Mayor, I am Maya from Library Block.",
                    "Families here need safe cooling spaces tonight, not next week.",
                    "Give us staffing and water support, and volunteers will mobilize fast."
                };
            case "Cooling Center":
                return new[]
                {
                    "Hi Mayor, I am Kai, urban climate planner.",
                    "This district has too much concrete and too little shade.",
                    "We need long-term cooling routes, trees, and fair access planning."
                };
            case "South District":
                return new[]
                {
                    "Hi Mayor, I am Daniel from South District grid operations.",
                    "Grid pressure peaks after sunset when everyone turns on cooling at once.",
                    "Without intervention, one transformer failure can cascade across blocks."
                };
            case "Transit Hub":
                return new[]
                {
                    "Hi Mayor, I am Jae from Transit Hub operations.",
                    "Platforms become extreme heat pockets at noon.",
                    "We need shade, water points, and stable service to protect workers and riders."
                };
            case "Mayor Plaza":
                return new[]
                {
                    "Hi Mayor, I am Rowan, city clerk at Mayor Plaza.",
                    "The council is watching outcomes, not slogans.",
                    "Public trust rises when your policy protects vulnerable groups first."
                };
            default:
                return new[] { "Hi Mayor, it is getting hotter every day." };
        }
    }

    static string GetFirstMeetVariableForRoom(string roomName)
    {
        switch (roomName)
        {
            case "Library Block": return "$met_maya";
            case "South District": return "$met_daniel";
            case "Old Quarter": return "$met_alvarez";
            case "Transit Hub": return "$met_jae";
            case "Cooling Center": return "$met_kai";
            case "Mayor Plaza": return "$met_rowan";
            default: return string.Empty;
        }
    }

    static string[] GetFirstMeetLinesForRoom(string roomName)
    {
        switch (roomName)
        {
            case "Library Block":
                return new[]
                {
                    "Hi Mayor, I'm Maya from Library Block.",
                    "You just arrived, so here's the short version: heat is now a fairness crisis.",
                    "Meet all district leads first, then come back and we'll launch Day 1 together."
                };
            case "South District":
                return new[]
                {
                    "Hi Mayor. Daniel here, lead grid engineer for South District.",
                    "Peak load hit 96% yesterday, and old transformers are overheating at night.",
                    "Meet the other leaders, then return to me for a grid policy choice."
                };
            case "Old Quarter":
                return new[]
                {
                    "Hello Mayor. I'm Mrs. Alvarez from Old Quarter.",
                    "Seniors in our buildings are suffering after midnight because rooms keep heat.",
                    "Please meet everyone first, then return with a care plan we can trust."
                };
            case "Transit Hub":
                return new[]
                {
                    "Hi Mayor, I'm Jae from Transit Hub operations.",
                    "Platforms reached extreme heat yesterday, and workers still had to run shifts.",
                    "Meet all leads first, then we can plan transit cooling and safety."
                };
            case "Cooling Center":
                return new[]
                {
                    "Hi Mayor, I'm Kai, urban climate planner.",
                    "This city traps heat block by block because concrete outnumbers shade.",
                    "Finish orientation first, then we choose a structural cooling strategy."
                };
            case "Mayor Plaza":
                return new[]
                {
                    "Hi Mayor, Rowan here, city clerk.",
                    "Your decisions this week will be measured by safety, trust, infrastructure, and vulnerable risk.",
                    "Meet every district lead first, then return to Maya to start Day 1."
                };
            default:
                return new[]
                {
                    "Hi Mayor.",
                    "Please hear every district report before finalizing your first plan."
                };
        }
    }

    static string GetPrimaryYarnNodeForRoom(string roomName)
    {
        switch (roomName)
        {
            case "Mayor Plaza": return "C1_CH1_RED_ALERT";
            case "Library Block": return "C1_SIDE_PETITION_START";
            case "South District": return "C1_SIDE_GRID_START";
            case "Old Quarter": return "C1_SIDE_BUDDY_START";
            case "Transit Hub": return "C1_CH2_HEATWAVE_WEEK";
            case "Cooling Center": return "C1_CH3_SHADE_CITY_START";
            default: return string.Empty;
        }
    }

    static string GetSecondaryYarnNodeForRoom(string roomName)
    {
        switch (roomName)
        {
            case "Mayor Plaza": return "C1_CH1_BRANCH_HUB";
            case "Library Block": return "C1_SIDE_PETITION_STAGE2";
            case "South District": return "C1_CH1_BRANCH_HUB";
            case "Old Quarter": return "C1_CH1_BRANCH_HUB";
            case "Transit Hub": return "C1_CH2_WORKER_BRANCH";
            case "Cooling Center": return "C1_CH3_SECOND_CHOICE";
            default: return string.Empty;
        }
    }

    static string GetCompletionVariableForRoom(string roomName)
    {
        switch (roomName)
        {
            case "Mayor Plaza": return "";
            case "Library Block": return "$quest_petition_done";
            case "South District": return "$quest_grid_done";
            case "Old Quarter": return "$quest_buddy_done";
            case "Transit Hub": return "$quest_transit_done";
            case "Cooling Center": return "$quest_shade_done";
            default: return string.Empty;
        }
    }

    static string GetUnlockVariableForRoom(string roomName)
    {
        switch (roomName)
        {
            default: return string.Empty;
        }
    }

    static string GetLockedLineForRoom(string roomName)
    {
        switch (roomName)
        {
            default:
                return "This district is not ready yet. Check another room first.";
        }
    }

    static int GetMinDayForRoom(string roomName)
    {
        switch (roomName)
        {
            case "Transit Hub": return 3;
            case "Cooling Center": return 4;
            default: return 1;
        }
    }

    static string GetDayLockedLineForRoom(string roomName)
    {
        switch (roomName)
        {
            case "Transit Hub":
                return "Transit planning starts on Day 3. Handle neighborhood tasks first.";
            case "Cooling Center":
                return "Urban shade planning starts on Day 4. Build field evidence first.";
            default:
                return "This task opens on a later day.";
        }
    }

    Sprite GetNpcSpriteForRoom(string roomName)
    {
        if (npcSpritesByRoom.TryGetValue(roomName, out var cached) && cached != null)
        {
            return cached;
        }

        var externalSprite = TryGetExternalNpcSprite(roomName);
        if (externalSprite != null)
        {
            npcSpritesByRoom[roomName] = externalSprite;
            return externalSprite;
        }

        int row;
        int col;
        switch (roomName)
        {
            case "Old Quarter":
                row = 0; col = 6; // Mrs. Alvarez, elder resident
                break;
            case "Library Block":
                row = 2; col = 8; // Maya
                break;
            case "Cooling Center":
                row = 2; col = 0; // Nurse/doctor
                break;
            case "South District":
                row = 1; col = 0; // Daniel engineer
                break;
            case "Transit Hub":
                row = 4; col = 7; // Jae worker
                break;
            case "Mayor Plaza":
                row = 3; col = 0; // Rowan clerk
                break;
            default:
                row = 0; col = 7;
                break;
        }

        var sprite = CreateCharacterSprite(row, col);
        if (sprite == null)
        {
            sprite = GenerateCitizenSprite(
                new Color(0.30f + ((col % 4) * 0.09f), 0.48f, 0.72f, 1f),
                new Color(0.96f, 0.84f, 0.56f, 1f)
            );
        }
        npcSpritesByRoom[roomName] = sprite;
        return sprite != null ? sprite : defaultNpcSprite;
    }

    static Sprite TryGetExternalNpcSprite(string roomName)
    {
        switch (roomName)
        {
            case "Old Quarter":
                return LoadExternalPngAsSprite(
                    "npc_old_quarter_alvarez",
                    "Assets/ExternalAssets/Kenney/new-platformer-pack/Sprites/Characters/Default/character_yellow_idle.png",
                    96f);
            case "Library Block":
                return LoadExternalPngAsSprite(
                    "npc_library_maya",
                    "Assets/ExternalAssets/Kenney/new-platformer-pack/Sprites/Characters/Default/character_pink_idle.png",
                    96f);
            case "Cooling Center":
                return LoadExternalPngAsSprite(
                    "npc_cooling_nurse",
                    "Assets/ExternalAssets/Kenney/new-platformer-pack/Sprites/Characters/Default/character_beige_front.png",
                    96f);
            case "South District":
                return LoadExternalPngAsSprite(
                    "npc_south_daniel",
                    "Assets/ExternalAssets/Kenney/new-platformer-pack/Sprites/Characters/Default/character_green_idle.png",
                    96f);
            case "Transit Hub":
                return LoadExternalPngAsSprite(
                    "npc_transit_jae",
                    "Assets/ExternalAssets/Kenney/new-platformer-pack/Sprites/Characters/Default/character_purple_idle.png",
                    96f);
            case "Mayor Plaza":
                return LoadExternalPngAsSprite(
                    "npc_mayor_plaza_rowan",
                    "Assets/ExternalAssets/Kenney/new-platformer-pack/Sprites/Characters/Default/character_yellow_front.png",
                    96f);
            default:
                return null;
        }
    }

    static Sprite LoadExternalPngAsSprite(string cacheKey, string projectRelativePath, float pixelsPerUnit)
    {
        if (externalNpcSpriteCache.TryGetValue(cacheKey, out var cached) && cached != null)
        {
            return cached;
        }

        var fromResources = LoadSpriteFromResourcesMirror(projectRelativePath);
        if (fromResources != null)
        {
            externalNpcSpriteCache[cacheKey] = fromResources;
            return fromResources;
        }

#if UNITY_EDITOR
        var imported = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(projectRelativePath);
        if (imported != null)
        {
            externalNpcSpriteCache[cacheKey] = imported;
            return imported;
        }
#endif

        string relativeFromAssets = projectRelativePath.StartsWith("Assets/")
            ? projectRelativePath.Substring("Assets/".Length)
            : projectRelativePath;
        string diskPath = Path.Combine(Application.dataPath, relativeFromAssets);
        if (!File.Exists(diskPath))
        {
            return null;
        }

        try
        {
            byte[] bytes = File.ReadAllBytes(diskPath);
            if (bytes == null || bytes.Length == 0) return null;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            if (!tex.LoadImage(bytes, false))
            {
                Object.Destroy(tex);
                return null;
            }

            tex.name = cacheKey;
            var sprite = Sprite.Create(
                tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0f),
                pixelsPerUnit,
                0,
                SpriteMeshType.FullRect);
            externalNpcSpriteCache[cacheKey] = sprite;
            return sprite;
        }
        catch
        {
            return null;
        }
    }

    Sprite CreateCharacterSprite(int rowFromTop, int colFromLeft)
    {
        if (characterSheetTexture == null)
        {
            characterSheetTexture = FindTextureByName("Character_SpriteSheet");
            if (characterSheetTexture == null)
            {
                Debug.LogWarning("Heatwave: Character_SpriteSheet not found; NPC will use fallback sprite.");
                return null;
            }
        }

        string key = $"char_{rowFromTop}_{colFromLeft}";
        if (npcSpritesByRoom.TryGetValue(key, out var cached) && cached != null)
        {
            return cached;
        }

        var sprite = CreateAtlasSprite(characterSheetTexture, colFromLeft, rowFromTop);
        if (sprite != null)
        {
            npcSpritesByRoom[key] = sprite;
        }

        return sprite;
    }

    static Sprite GetSolidSprite()
    {
        if (solidSpriteCache != null) return solidSpriteCache;

        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.filterMode = FilterMode.Point;
        tex.Apply();
        solidSpriteCache = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 16f);
        return solidSpriteCache;
    }

    static Sprite GenerateCitizenSprite(Color shirtColor, Color accentColor)
    {
        var tex = new Texture2D(24, 24);
        var clear = new Color(0f, 0f, 0f, 0f);
        for (int x = 0; x < 24; x++)
        {
            for (int y = 0; y < 24; y++)
            {
                tex.SetPixel(x, y, clear);
            }
        }

        var skin = new Color(0.93f, 0.80f, 0.64f, 1f);
        var hair = new Color(0.22f, 0.16f, 0.12f, 1f);
        var pants = new Color(0.29f, 0.25f, 0.23f, 1f);
        var outline = new Color(0.13f, 0.10f, 0.08f, 1f);
        var shirtShadow = Color.Lerp(shirtColor, outline, 0.35f);
        var shoes = new Color(0.11f, 0.10f, 0.10f, 1f);

        for (int x = 8; x <= 15; x++)
        {
            for (int y = 13; y <= 18; y++) tex.SetPixel(x, y, skin);
        }
        for (int x = 8; x <= 15; x++)
        {
            tex.SetPixel(x, 19, hair);
            tex.SetPixel(x, 20, hair);
        }
        for (int x = 7; x <= 16; x++)
        {
            for (int y = 7; y <= 12; y++) tex.SetPixel(x, y, shirtColor);
        }
        for (int x = 7; x <= 16; x++)
        {
            tex.SetPixel(x, 6, shirtShadow);
        }
        for (int x = 9; x <= 14; x++)
        {
            for (int y = 3; y <= 5; y++) tex.SetPixel(x, y, pants);
        }
        for (int x = 8; x <= 10; x++)
        {
            for (int y = 1; y <= 2; y++) tex.SetPixel(x, y, shoes);
        }
        for (int x = 13; x <= 15; x++)
        {
            for (int y = 1; y <= 2; y++) tex.SetPixel(x, y, shoes);
        }
        for (int x = 10; x <= 13; x++)
        {
            tex.SetPixel(x, 21, accentColor);
        }

        for (int x = 7; x <= 16; x++)
        {
            tex.SetPixel(x, 0, outline);
            tex.SetPixel(x, 22, outline);
        }
        for (int y = 1; y <= 21; y++)
        {
            tex.SetPixel(6, y, outline);
            tex.SetPixel(17, y, outline);
        }

        tex.filterMode = FilterMode.Point;
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 24, 24), new Vector2(0.5f, 0f), 24f);
    }

    static Sprite LoadSpriteFromResourcesMirror(string projectRelativePath)
    {
        if (string.IsNullOrWhiteSpace(projectRelativePath)) return null;
        string relativeFromAssets = projectRelativePath.StartsWith("Assets/")
            ? projectRelativePath.Substring("Assets/".Length)
            : projectRelativePath;
        string resourceKey = Path.ChangeExtension(relativeFromAssets, null).Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(resourceKey)) return null;
        return Resources.Load<Sprite>($"Mirror/{resourceKey}");
    }

    static void AddWorldLabel(Transform target, string textValue, float yOffset, Color textColor, float scale, int sortingOrder)
    {
        var tag = new GameObject("NameTag");
        tag.transform.SetParent(target, false);
        tag.transform.localPosition = new Vector3(0f, yOffset, 0f);
        tag.transform.localScale = new Vector3(scale, scale, scale);

        float plateWidth = Mathf.Clamp(textValue.Length * 0.42f, 2.1f, 8.8f);
        var back = new GameObject("LabelBack");
        back.transform.SetParent(tag.transform, false);
        back.transform.localPosition = new Vector3(0f, -0.04f, 0.01f);
        var backSr = back.AddComponent<SpriteRenderer>();
        backSr.sprite = GetSolidSprite();
        backSr.color = new Color(0.05f, 0.06f, 0.06f, 0.80f);
        backSr.sortingOrder = sortingOrder - 1;
        back.transform.localScale = new Vector3(plateWidth, 0.68f, 1f);

        var text = tag.AddComponent<TextMeshPro>();
        text.text = textValue;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        HeatwaveUIFontKit.ApplyReadableTMP(
            text,
            10.6f,
            textColor,
            new Color(0.03f, 0.03f, 0.03f, 1f),
            0.30f
        );

        var renderer = tag.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sortingOrder = sortingOrder;
        }
    }

    static void AddNpcNameTag(Transform npcTransform, string npcName)
    {
        AddWorldLabel(npcTransform, npcName, 1.78f, new Color(1f, 0.92f, 0.62f, 1f), 0.64f, 12);
    }

    bool TryFindSpawnWorld(RoomData room, out Vector3 spawnWorld)
    {
        spawnWorld = Vector3.zero;
        if (groundTilemap == null) return false;

        const int maxTries = 40;
        for (int i = 0; i < maxTries; i++)
        {
            int x = Random.Range(room.CellRect.xMin, room.CellRect.xMax);
            int y = Random.Range(room.CellRect.yMin, room.CellRect.yMax);
            var cell = new Vector3Int(x, y, 0);

            if (!groundTilemap.HasTile(cell)) continue;
            if (wallsTilemap != null && wallsTilemap.HasTile(cell)) continue;

            spawnWorld = groundTilemap.GetCellCenterWorld(cell);
            return true;
        }

        var centerCell = new Vector3Int(
            room.CellRect.xMin + room.CellRect.width / 2,
            room.CellRect.yMin + room.CellRect.height / 2,
            0
        );

        spawnWorld = groundTilemap.GetCellCenterWorld(centerCell);
        return true;
    }

    static Vector3Int? FindAnyTile(Tilemap tilemap)
    {
        if (tilemap == null) return null;

        var bounds = tilemap.cellBounds;
        foreach (var cell in bounds.allPositionsWithin)
        {
            if (tilemap.HasTile(cell)) return cell;
        }

        return null;
    }

    static TileBase GetAnyTileBase(Tilemap tilemap)
    {
        var cell = FindAnyTile(tilemap);
        if (cell == null) return null;
        return tilemap.GetTile(cell.Value);
    }
}
