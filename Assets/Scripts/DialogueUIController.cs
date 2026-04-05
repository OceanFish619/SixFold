using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class DialogueUIController : MonoBehaviour
{
    const float ScreenMargin = 26f;
    const float PanelVerticalMargin = 18f;
    const float PanelBottomHeight = 0.46f;

    [SerializeField] GameObject dialogueUIRoot;
    [SerializeField] bool autoFindDialoguePanel = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void OnAfterSceneLoad()
    {
        ApplyDialogueLayoutNow();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyDialogueLayoutNow();
    }

    void Awake()
    {
        if (autoFindDialoguePanel && dialogueUIRoot == null)
        {
            var panel = GameObject.Find("DialoguePanel");
            if (panel != null) dialogueUIRoot = panel;
        }

        ApplyDialogueLayoutNow();

        if (dialogueUIRoot != null) dialogueUIRoot.SetActive(false);
    }

    public void Show()
    {
        if (dialogueUIRoot == null) return;
        ApplyDialogueLayoutNow();
        dialogueUIRoot.SetActive(true);
    }

    public void Hide()
    {
        if (dialogueUIRoot == null) return;
        dialogueUIRoot.SetActive(false);
    }

    public static void ApplyDialogueLayoutNow()
    {
        var panelGO = GameObject.Find("DialoguePanel");
        if (panelGO == null) return;

        var panel = panelGO.GetComponent<RectTransform>();
        if (panel == null) return;

        ConfigureCanvas(panel);
        ConfigurePanel(panel);

        var titleText = FindTMP(panel, "TitleText");
        var bodyText = FindTMP(panel, "BodyText");
        var optionA = FindRect(panel, "OptionAButton");
        var optionB = FindRect(panel, "OptionBButton");
        var closeButton = FindRect(panel, "CloseButton");

        var leftBox = EnsureBox(panel, "LeftDialogueBox");
        var rightBox = EnsureBox(panel, "RightDialogueBox");

        LayoutBoxes(leftBox, rightBox);
        StyleBox(leftBox, new Color(0.10f, 0.13f, 0.20f, 0.94f));
        StyleBox(rightBox, new Color(0.09f, 0.09f, 0.11f, 0.94f));

        if (titleText != null)
        {
            AttachToBox(titleText.rectTransform, leftBox, 16f, 14f, 16f, 14f);
            StyleTitle(titleText);
        }

        if (bodyText != null)
        {
            AttachToBox(bodyText.rectTransform, rightBox, 18f, 16f, 18f, 82f);
            StyleBody(bodyText);
        }

        if (optionA != null)
        {
            optionA.SetParent(rightBox, false);
            PlaceOption(optionA, 0.03f, 0.04f, 0.47f, 0.25f);
            StyleOption(optionA);
        }

        if (optionB != null)
        {
            optionB.SetParent(rightBox, false);
            PlaceOption(optionB, 0.53f, 0.04f, 0.97f, 0.25f);
            StyleOption(optionB);
        }

        if (closeButton != null)
        {
            closeButton.SetParent(panel, true);
            closeButton.anchorMin = new Vector2(1f, 1f);
            closeButton.anchorMax = new Vector2(1f, 1f);
            closeButton.pivot = new Vector2(1f, 1f);
            closeButton.anchoredPosition = new Vector2(-8f, -8f);
            closeButton.sizeDelta = new Vector2(108f, 42f);
            StyleClose(closeButton);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
    }

    static void ConfigureCanvas(RectTransform panel)
    {
        var canvas = panel.GetComponentInParent<Canvas>();
        if (canvas == null) return;

        if (canvas.transform.localScale.sqrMagnitude < 0.1f)
        {
            canvas.transform.localScale = Vector3.one;
        }

        var scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null) return;

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
    }

    static void ConfigurePanel(RectTransform panel)
    {
        panel.anchorMin = new Vector2(0f, 0f);
        panel.anchorMax = new Vector2(1f, PanelBottomHeight);
        panel.pivot = new Vector2(0.5f, 0f);
        panel.offsetMin = new Vector2(ScreenMargin, PanelVerticalMargin);
        panel.offsetMax = new Vector2(-ScreenMargin, -PanelVerticalMargin);

        var image = panel.GetComponent<Image>();
        if (image != null)
        {
            image.color = new Color(0f, 0f, 0f, 0.16f);
            image.raycastTarget = true;
        }
    }

    static void LayoutBoxes(RectTransform leftBox, RectTransform rightBox)
    {
        leftBox.anchorMin = new Vector2(0f, 0f);
        leftBox.anchorMax = new Vector2(0.34f, 1f);
        leftBox.offsetMin = new Vector2(8f, 10f);
        leftBox.offsetMax = new Vector2(-8f, -10f);
        leftBox.pivot = new Vector2(0f, 0f);

        rightBox.anchorMin = new Vector2(0.34f, 0f);
        rightBox.anchorMax = new Vector2(1f, 1f);
        rightBox.offsetMin = new Vector2(8f, 10f);
        rightBox.offsetMax = new Vector2(-8f, -10f);
        rightBox.pivot = new Vector2(0f, 0f);
    }

    static RectTransform EnsureBox(RectTransform parent, string name)
    {
        var existing = FindRect(parent, name);
        if (existing != null) return existing;

        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    static void StyleBox(RectTransform rect, Color color)
    {
        HeatwaveUIFontKit.ApplyPixelFrame(
            rect,
            color,
            new Color(0.70f, 0.53f, 0.31f, 1f),
            new Color(1f, 0.86f, 0.58f, 1f),
            5f,
            12f
        );
        var image = rect.GetComponent<Image>();
        if (image != null) image.raycastTarget = false;
    }

    static void AttachToBox(RectTransform target, RectTransform box, float left, float top, float right, float bottom)
    {
        target.SetParent(box, false);
        target.anchorMin = new Vector2(0f, 0f);
        target.anchorMax = new Vector2(1f, 1f);
        target.offsetMin = new Vector2(left, bottom);
        target.offsetMax = new Vector2(-right, -top);
        target.pivot = new Vector2(0.5f, 0.5f);
    }

    static void PlaceOption(RectTransform optionRect, float xMin, float yMin, float xMax, float yMax)
    {
        optionRect.anchorMin = new Vector2(xMin, yMin);
        optionRect.anchorMax = new Vector2(xMax, yMax);
        optionRect.offsetMin = Vector2.zero;
        optionRect.offsetMax = Vector2.zero;
        optionRect.pivot = new Vector2(0.5f, 0.5f);
    }

    static void StyleTitle(TMP_Text text)
    {
        text.textWrappingMode = TextWrappingModes.Normal;
        text.alignment = TextAlignmentOptions.Left;
        HeatwaveUIFontKit.ApplyReadableTMP(
            text,
            48f,
            new Color(1f, 0.95f, 0.74f, 1f),
            new Color(0.06f, 0.04f, 0.03f, 1f),
            0.29f
        );
        text.text = text.text == "New Text" ? "SPEAKER" : text.text;
    }

    static void StyleBody(TMP_Text text)
    {
        text.textWrappingMode = TextWrappingModes.Normal;
        text.alignment = TextAlignmentOptions.TopLeft;
        HeatwaveUIFontKit.ApplyReadableTMP(
            text,
            38f,
            new Color(0.98f, 0.98f, 1f, 1f),
            new Color(0.05f, 0.05f, 0.05f, 1f),
            0.22f
        );
        text.text = text.text == "New Text" ? "Dialogue appears here..." : text.text;
    }

    static void StyleOption(RectTransform optionRect)
    {
        var image = optionRect.GetComponent<Image>();
        if (image != null)
        {
            image.color = new Color(0.13f, 0.14f, 0.17f, 0.98f);
            image.raycastTarget = true;
        }
        HeatwaveUIFontKit.ApplyPixelFrame(
            optionRect,
            new Color(0.13f, 0.14f, 0.17f, 0.98f),
            new Color(0.67f, 0.50f, 0.30f, 1f),
            new Color(0.98f, 0.84f, 0.54f, 1f),
            4f,
            10f
        );

        var button = optionRect.GetComponent<Button>();
        if (button != null)
        {
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.96f, 0.84f, 1f);
            colors.pressedColor = new Color(0.90f, 0.83f, 0.68f, 1f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
        }

        var label = optionRect.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.alignment = TextAlignmentOptions.Center;
            HeatwaveUIFontKit.ApplyReadableTMP(
                label,
                30f,
                new Color(1f, 0.95f, 0.82f, 1f),
                new Color(0.05f, 0.04f, 0.03f, 1f),
                0.25f
            );
            if (label.text == "Button") label.text = "Choice";
        }
    }

    static void StyleClose(RectTransform closeRect)
    {
        var image = closeRect.GetComponent<Image>();
        if (image != null)
        {
            image.color = new Color(0.26f, 0.12f, 0.12f, 0.98f);
        }
        HeatwaveUIFontKit.ApplyPixelFrame(
            closeRect,
            new Color(0.26f, 0.12f, 0.12f, 0.98f),
            new Color(0.80f, 0.52f, 0.42f, 1f),
            new Color(1f, 0.80f, 0.74f, 1f),
            4f,
            10f
        );

        var label = closeRect.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.alignment = TextAlignmentOptions.Center;
            HeatwaveUIFontKit.ApplyReadableTMP(
                label,
                24f,
                new Color(1f, 0.95f, 0.95f, 1f),
                new Color(0.12f, 0.03f, 0.03f, 1f),
                0.26f
            );
            if (label.text == "Button") label.text = "SKIP";
        }
    }

    static RectTransform FindRect(RectTransform root, string childName)
    {
        var child = root.Find(childName);
        return child != null ? child.GetComponent<RectTransform>() : null;
    }

    static TMP_Text FindTMP(RectTransform root, string childName)
    {
        var child = root.Find(childName);
        return child != null ? child.GetComponent<TMP_Text>() : null;
    }
}
