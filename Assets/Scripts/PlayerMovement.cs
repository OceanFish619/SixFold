using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float speed = 4f;
    [Header("Pixel Character")]
    [SerializeField] string characterSheetName = "Character_SpriteSheet";
    [SerializeField] int characterRow = 0;
    [SerializeField] float walkFps = 7f;
    [SerializeField] int sheetColumns = 11;
    [SerializeField] int sheetRows = 6;
    [Header("External Mayor Sprite (Web Asset)")]
    [SerializeField] bool useExternalMayorSprite = true;
    [SerializeField] string mayorSpritePoseDir = "Assets/ExternalAssets/Kenney/new-platformer-pack/Sprites/Characters/Default";
    [SerializeField] string mayorColorProfile = "purple";
    [Header("Visual")]
    [SerializeField] float playerVisualScale = 1.35f;

    Rigidbody2D rb;
    Vector2 move;
    SpriteRenderer spriteRenderer;
    Sprite[] downFrames;
    Sprite[] rightFrames;
    Sprite[] upFrames;
    float animTimer;
    int animIndex;
    Vector2 lastFacing = Vector2.down;
    static readonly Dictionary<string, Texture2D> textureCache = new Dictionary<string, Texture2D>();

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        transform.localScale = new Vector3(playerVisualScale, playerVisualScale, 1f);
        if (spriteRenderer != null)
        {
            // Scene file may keep an old tint; force neutral player color.
            spriteRenderer.color = Color.white;
        }
        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.linearDamping = 0f;
            rb.angularDamping = 0f;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        var col = GetComponent<Collider2D>();
        float safeScale = Mathf.Max(0.01f, playerVisualScale);
        float invScale = 1f / safeScale;
        if (col is BoxCollider2D box)
        {
            // Keep hitbox stable even when the visual is scaled up.
            box.size *= invScale;
            box.offset *= invScale;
        }
        else if (col is CircleCollider2D circle)
        {
            circle.radius *= invScale;
            circle.offset *= invScale;
        }
        else if (col is CapsuleCollider2D capsule)
        {
            capsule.size *= invScale;
            capsule.offset *= invScale;
        }

        if (col != null && col.sharedMaterial == null)
        {
            var noFriction = new PhysicsMaterial2D("PlayerNoFriction");
            noFriction.friction = 0f;
            noFriction.bounciness = 0f;
            col.sharedMaterial = noFriction;
        }

        LoadCharacterFrames();
    }

    void Update()
    {
        move = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        ).normalized;
        UpdateAnimation(Time.deltaTime);
    }

    void FixedUpdate()
    {
        if (rb == null) return;
        rb.MovePosition(rb.position + move * speed * Time.fixedDeltaTime);
    }

    void LoadCharacterFrames()
    {
        if (useExternalMayorSprite && TryLoadExternalMayorFrames())
        {
            return;
        }

        var texture = FindTextureByName(characterSheetName);
        if (texture == null) return;

        // Sheet layout assumption (from provided pack):
        // col 0-1: front/down, col 2-3: side/right, col 4-5: back/up.
        downFrames = new[]
        {
            CreateSprite(texture, 0, characterRow, sheetColumns, sheetRows),
            CreateSprite(texture, 1, characterRow, sheetColumns, sheetRows)
        };
        rightFrames = new[]
        {
            CreateSprite(texture, 2, characterRow, sheetColumns, sheetRows),
            CreateSprite(texture, 3, characterRow, sheetColumns, sheetRows)
        };
        upFrames = new[]
        {
            CreateSprite(texture, 4, characterRow, sheetColumns, sheetRows),
            CreateSprite(texture, 5, characterRow, sheetColumns, sheetRows)
        };

        if (spriteRenderer != null && downFrames[0] != null)
        {
            spriteRenderer.sprite = downFrames[0];
            spriteRenderer.sortingOrder = 6;
            return;
        }

        BuildFallbackMayorFrames();
    }

    bool TryLoadExternalMayorFrames()
    {
        string baseDir = mayorSpritePoseDir;
        if (string.IsNullOrWhiteSpace(baseDir)) return false;

        string color = string.IsNullOrWhiteSpace(mayorColorProfile) ? "purple" : mayorColorProfile.Trim().ToLowerInvariant();
        string prefix = $"character_{color}";
        var idle = LoadSpriteFromProjectPath($"{baseDir}/{prefix}_idle.png", $"mayor_{color}_idle", 96f);
        var front = LoadSpriteFromProjectPath($"{baseDir}/{prefix}_front.png", $"mayor_{color}_front", 96f);
        var walkA = LoadSpriteFromProjectPath($"{baseDir}/{prefix}_walk_a.png", $"mayor_{color}_walk_a", 96f);
        var walkB = LoadSpriteFromProjectPath($"{baseDir}/{prefix}_walk_b.png", $"mayor_{color}_walk_b", 96f);

        if ((idle == null || walkA == null || walkB == null) && color != "beige")
        {
            prefix = "character_beige";
            idle = LoadSpriteFromProjectPath($"{baseDir}/{prefix}_idle.png", "mayor_beige_idle", 96f);
            front = LoadSpriteFromProjectPath($"{baseDir}/{prefix}_front.png", "mayor_beige_front", 96f);
            walkA = LoadSpriteFromProjectPath($"{baseDir}/{prefix}_walk_a.png", "mayor_beige_walk_a", 96f);
            walkB = LoadSpriteFromProjectPath($"{baseDir}/{prefix}_walk_b.png", "mayor_beige_walk_b", 96f);
        }

        if (idle == null || walkA == null || walkB == null)
        {
            return false;
        }

        downFrames = new[]
        {
            front != null ? front : idle,
            walkA
        };
        rightFrames = new[]
        {
            walkA,
            walkB
        };
        upFrames = new[]
        {
            idle,
            idle
        };

        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = downFrames[0];
            spriteRenderer.sortingOrder = 6;
        }

        return true;
    }

    void BuildFallbackMayorFrames()
    {
        var idle = CreateMayorFallbackSprite(new Color(0.25f, 0.57f, 0.84f, 1f), new Color(0.11f, 0.23f, 0.37f, 1f), false);
        var walk = CreateMayorFallbackSprite(new Color(0.27f, 0.61f, 0.90f, 1f), new Color(0.12f, 0.26f, 0.40f, 1f), true);
        downFrames = new[] { idle, walk };
        rightFrames = new[] { walk, idle };
        upFrames = new[] { idle, idle };
        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = idle;
            spriteRenderer.sortingOrder = 6;
        }
    }

    void UpdateAnimation(float dt)
    {
        if (spriteRenderer == null) return;
        if (downFrames == null || rightFrames == null || upFrames == null) return;

        bool moving = move.sqrMagnitude > 0.01f;
        if (moving) lastFacing = move;

        Sprite[] frames = downFrames;
        bool flipX = false;

        if (Mathf.Abs(lastFacing.x) > Mathf.Abs(lastFacing.y))
        {
            frames = rightFrames;
            flipX = lastFacing.x < 0f;
        }
        else if (lastFacing.y > 0f)
        {
            frames = upFrames;
        }
        else
        {
            frames = downFrames;
        }

        spriteRenderer.flipX = flipX;

        if (!moving)
        {
            spriteRenderer.sprite = frames[0] != null ? frames[0] : spriteRenderer.sprite;
            animTimer = 0f;
            animIndex = 0;
            return;
        }

        animTimer += dt * walkFps;
        if (animTimer >= 1f)
        {
            animTimer -= 1f;
            animIndex = (animIndex + 1) % 2;
        }

        var next = frames[animIndex];
        if (next != null) spriteRenderer.sprite = next;
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

    static Sprite LoadSpriteFromProjectPath(string projectRelativePath, string cacheKey, float pixelsPerUnit)
    {
        if (!string.IsNullOrWhiteSpace(cacheKey) &&
            textureCache.TryGetValue(cacheKey, out var cachedTex) &&
            cachedTex != null)
        {
            return Sprite.Create(
                cachedTex,
                new Rect(0, 0, cachedTex.width, cachedTex.height),
                new Vector2(0.5f, 0f),
                pixelsPerUnit,
                0,
                SpriteMeshType.FullRect);
        }

        var fromResources = LoadSpriteFromResourcesMirror(projectRelativePath);
        if (fromResources != null)
        {
            return fromResources;
        }

#if UNITY_EDITOR
        var importedSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(projectRelativePath);
        if (importedSprite != null)
        {
            return importedSprite;
        }
#endif

        string relativeFromAssets = projectRelativePath.StartsWith("Assets/")
            ? projectRelativePath.Substring("Assets/".Length)
            : projectRelativePath;
        string diskPath = Path.Combine(Application.dataPath, relativeFromAssets);
        if (!File.Exists(diskPath)) return null;

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

        if (!string.IsNullOrWhiteSpace(cacheKey))
        {
            tex.name = cacheKey;
            textureCache[cacheKey] = tex;
        }

        return Sprite.Create(
            tex,
            new Rect(0, 0, tex.width, tex.height),
            new Vector2(0.5f, 0f),
            pixelsPerUnit,
            0,
            SpriteMeshType.FullRect);
    }

    static Sprite LoadSpriteFromResourcesMirror(string projectRelativePath)
    {
        if (string.IsNullOrWhiteSpace(projectRelativePath)) return null;
        string relativeFromAssets = projectRelativePath.StartsWith("Assets/")
            ? projectRelativePath.Substring("Assets/".Length)
            : projectRelativePath;
        string key = Path.ChangeExtension(relativeFromAssets, null).Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(key)) return null;
        return Resources.Load<Sprite>($"Mirror/{key}");
    }

    static Sprite CreateMayorFallbackSprite(Color shirt, Color accent, bool step)
    {
        var tex = new Texture2D(24, 24, TextureFormat.RGBA32, false);
        var clear = new Color(0f, 0f, 0f, 0f);
        for (int x = 0; x < 24; x++)
        {
            for (int y = 0; y < 24; y++) tex.SetPixel(x, y, clear);
        }

        var skin = new Color(0.94f, 0.80f, 0.64f, 1f);
        var hair = new Color(0.19f, 0.14f, 0.11f, 1f);
        var pants = new Color(0.17f, 0.19f, 0.24f, 1f);
        var outline = new Color(0.07f, 0.07f, 0.08f, 1f);
        var shoes = new Color(0.08f, 0.08f, 0.09f, 1f);

        for (int x = 8; x <= 15; x++) for (int y = 13; y <= 18; y++) tex.SetPixel(x, y, skin);
        for (int x = 8; x <= 15; x++) { tex.SetPixel(x, 19, hair); tex.SetPixel(x, 20, hair); }
        for (int x = 7; x <= 16; x++) for (int y = 7; y <= 12; y++) tex.SetPixel(x, y, shirt);
        for (int x = 9; x <= 14; x++) for (int y = 3; y <= 5; y++) tex.SetPixel(x, y, pants);
        if (step)
        {
            for (int x = 8; x <= 10; x++) for (int y = 1; y <= 2; y++) tex.SetPixel(x, y, shoes);
            for (int x = 14; x <= 16; x++) for (int y = 2; y <= 3; y++) tex.SetPixel(x, y, shoes);
        }
        else
        {
            for (int x = 8; x <= 10; x++) for (int y = 1; y <= 2; y++) tex.SetPixel(x, y, shoes);
            for (int x = 13; x <= 15; x++) for (int y = 1; y <= 2; y++) tex.SetPixel(x, y, shoes);
        }
        for (int x = 10; x <= 13; x++) tex.SetPixel(x, 21, accent);

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
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, 24, 24), new Vector2(0.5f, 0f), 24f, 0, SpriteMeshType.FullRect);
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

    static Sprite CreateSprite(Texture2D texture, int colFromLeft, int rowFromTop, int cols, int rows)
    {
        if (texture == null || cols <= 0 || rows <= 0) return null;

        int cellW = texture.width / cols;
        int cellH = texture.height / rows;
        if (cellW <= 0 || cellH <= 0) return null;

        int pad = Mathf.Clamp(2, 0, Mathf.Min(cellW, cellH) / 8);
        int useW = cellW - pad * 2;
        int useH = cellH - pad * 2;
        if (useW <= 0 || useH <= 0) return null;

        int x = colFromLeft * cellW + pad;
        int y = texture.height - ((rowFromTop + 1) * cellH) + pad;
        if (x < 0 || y < 0 || x + useW > texture.width || y + useH > texture.height) return null;

        int ppu = Mathf.Max(1, Mathf.Min(useW, useH));
        return Sprite.Create(texture, new Rect(x, y, useW, useH), new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect);
    }
}
