using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class HeatwaveUIFontKit
{
    static TMP_FontAsset cachedCuteFont;

    public static TMP_FontAsset GetCuteFont()
    {
        if (cachedCuteFont != null) return cachedCuteFont;

        cachedCuteFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/Bangers SDF");
        if (cachedCuteFont == null)
        {
            cachedCuteFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/Oswald Bold SDF");
        }
        if (cachedCuteFont == null)
        {
            cachedCuteFont = TMP_Settings.defaultFontAsset;
        }

        return cachedCuteFont;
    }

    public static void ApplyReadableTMP(
        TMP_Text text,
        float fontSize,
        Color textColor,
        Color? outlineColor = null,
        float outlineWidth = 0.22f)
    {
        if (text == null) return;

        var font = GetCuteFont();
        if (font != null)
        {
            text.font = font;
        }

        text.fontSize = fontSize;
        text.color = textColor;
        text.fontStyle = FontStyles.Bold;
        text.outlineColor = outlineColor ?? new Color(0.03f, 0.03f, 0.03f, 1f);
        text.outlineWidth = outlineWidth;
        text.enableAutoSizing = false;

        if (text is TextMeshProUGUI)
        {
            var uiOutline = text.GetComponent<Outline>();
            if (uiOutline == null) uiOutline = text.gameObject.AddComponent<Outline>();
            uiOutline.effectColor = new Color(0f, 0f, 0f, 0.82f);
            uiOutline.effectDistance = new Vector2(1.8f, -1.8f);

            var uiShadow = text.GetComponent<Shadow>();
            if (uiShadow == null) uiShadow = text.gameObject.AddComponent<Shadow>();
            uiShadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
            uiShadow.effectDistance = new Vector2(0f, -2.2f);
        }
    }

    public static void ApplyPixelFrame(
        RectTransform panel,
        Color fill,
        Color border,
        Color corner,
        float borderThickness = 5f,
        float cornerSize = 12f)
    {
        if (panel == null) return;

        var panelImage = panel.GetComponent<Image>();
        if (panelImage != null)
        {
            panelImage.color = fill;
        }

        EnsureEdge(panel, "PxTop", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, borderThickness), new Vector2(0f, 0f), border);
        EnsureEdge(panel, "PxBottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, borderThickness), new Vector2(0f, 0f), border);
        EnsureEdge(panel, "PxLeft", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(borderThickness, 0f), new Vector2(0f, 0f), border);
        EnsureEdge(panel, "PxRight", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(borderThickness, 0f), new Vector2(0f, 0f), border);

        EnsureEdge(panel, "PxCornerTL", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(cornerSize, cornerSize), new Vector2(cornerSize * 0.5f, -cornerSize * 0.5f), corner);
        EnsureEdge(panel, "PxCornerTR", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(cornerSize, cornerSize), new Vector2(-cornerSize * 0.5f, -cornerSize * 0.5f), corner);
        EnsureEdge(panel, "PxCornerBL", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(cornerSize, cornerSize), new Vector2(cornerSize * 0.5f, cornerSize * 0.5f), corner);
        EnsureEdge(panel, "PxCornerBR", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(cornerSize, cornerSize), new Vector2(-cornerSize * 0.5f, cornerSize * 0.5f), corner);
    }

    static void EnsureEdge(
        RectTransform panel,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 sizeDelta,
        Vector2 anchoredPosition,
        Color color)
    {
        var existing = panel.Find(name);
        RectTransform rect;
        Image image;
        if (existing == null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            rect = go.GetComponent<RectTransform>();
            rect.SetParent(panel, false);
            image = go.GetComponent<Image>();
            image.raycastTarget = false;
        }
        else
        {
            rect = existing.GetComponent<RectTransform>();
            image = existing.GetComponent<Image>();
        }

        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = sizeDelta;
        rect.anchoredPosition = anchoredPosition;

        if (image != null)
        {
            image.color = color;
        }
    }
}
