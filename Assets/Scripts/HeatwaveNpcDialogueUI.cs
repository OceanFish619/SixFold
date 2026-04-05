using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HeatwaveNpcDialogueUI : MonoBehaviour
{
    public static HeatwaveNpcDialogueUI Instance { get; private set; }

    TMP_Text promptText;
    TMP_Text speakerText;
    TMP_Text lineText;
    GameObject panelRoot;

    object promptSource;
    string[] activeLines;
    int lineIndex;

    public bool IsConversationOpen => panelRoot != null && panelRoot.activeSelf;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        BuildUI();
    }

    void Update()
    {
        if (!IsConversationOpen) return;

        if (Input.GetKeyDown(KeyCode.E) || Input.GetMouseButtonDown(0))
        {
            AdvanceConversation();
        }
    }

    public void SetPrompt(object source, string text)
    {
        if (IsConversationOpen || promptText == null) return;
        promptSource = source;
        promptText.text = text;
        promptText.gameObject.SetActive(true);
    }

    public void ClearPrompt(object source)
    {
        if (promptText == null) return;
        if (!ReferenceEquals(promptSource, source)) return;

        promptSource = null;
        promptText.gameObject.SetActive(false);
    }

    public void StartConversation(string speaker, string[] lines)
    {
        if (panelRoot == null || speakerText == null || lineText == null) return;

        activeLines = (lines == null || lines.Length == 0)
            ? new[] { "Stay hydrated. The heat is dangerous today." }
            : lines;

        lineIndex = 0;
        speakerText.text = speaker;
        lineText.text = activeLines[lineIndex];

        if (promptText != null)
        {
            promptText.gameObject.SetActive(false);
            promptSource = null;
        }

        panelRoot.SetActive(true);
    }

    void AdvanceConversation()
    {
        if (activeLines == null || activeLines.Length == 0)
        {
            EndConversation();
            return;
        }

        lineIndex++;
        if (lineIndex >= activeLines.Length)
        {
            EndConversation();
            return;
        }

        lineText.text = activeLines[lineIndex];
    }

    void EndConversation()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        activeLines = null;
        lineIndex = 0;
    }

    void BuildUI()
    {
        var canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = FindFirstObjectByType<Canvas>();
        }

        if (canvas == null) return;

        var existingPrompt = canvas.transform.Find("NpcPromptText");
        if (existingPrompt != null)
        {
            promptText = existingPrompt.GetComponent<TMP_Text>();
            HeatwaveUIFontKit.ApplyReadableTMP(
                promptText,
                34f,
                new Color(1f, 0.94f, 0.76f, 1f),
                new Color(0.05f, 0.04f, 0.03f, 1f),
                0.28f
            );
        }
        else
        {
            var promptGo = new GameObject("NpcPromptText", typeof(RectTransform), typeof(TextMeshProUGUI));
            promptGo.transform.SetParent(canvas.transform, false);

            var rect = promptGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 130f);
            rect.sizeDelta = new Vector2(920f, 66f);

            promptText = promptGo.GetComponent<TextMeshProUGUI>();
            promptText.alignment = TextAlignmentOptions.Center;
            promptText.textWrappingMode = TextWrappingModes.NoWrap;
            HeatwaveUIFontKit.ApplyReadableTMP(
                promptText,
                34f,
                new Color(1f, 0.94f, 0.76f, 1f),
                new Color(0.05f, 0.04f, 0.03f, 1f),
                0.28f
            );
            promptText.gameObject.SetActive(false);
        }

        var existingPanel = canvas.transform.Find("NpcDialoguePanel");
        if (existingPanel != null)
        {
            panelRoot = existingPanel.gameObject;
            speakerText = panelRoot.transform.Find("Speaker")?.GetComponent<TMP_Text>();
            lineText = panelRoot.transform.Find("Line")?.GetComponent<TMP_Text>();
            var existingPanelRect = panelRoot.GetComponent<RectTransform>();
            if (existingPanelRect != null)
            {
                HeatwaveUIFontKit.ApplyPixelFrame(
                    existingPanelRect,
                    new Color(0.10f, 0.10f, 0.11f, 0.96f),
                    new Color(0.72f, 0.53f, 0.30f, 1f),
                    new Color(1f, 0.86f, 0.56f, 1f),
                    5f,
                    12f
                );
            }
            if (speakerText != null)
            {
                HeatwaveUIFontKit.ApplyReadableTMP(
                    speakerText,
                    34f,
                    new Color(1f, 0.93f, 0.70f, 1f),
                    new Color(0.06f, 0.04f, 0.03f, 1f),
                    0.28f
                );
            }
            if (lineText != null)
            {
                HeatwaveUIFontKit.ApplyReadableTMP(
                    lineText,
                    30f,
                    new Color(0.96f, 0.97f, 1f, 1f),
                    new Color(0.05f, 0.05f, 0.05f, 1f),
                    0.22f
                );
            }
            return;
        }

        panelRoot = new GameObject("NpcDialoguePanel", typeof(RectTransform), typeof(Image));
        panelRoot.transform.SetParent(canvas.transform, false);

        var panelRect = panelRoot.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0f);
        panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = new Vector2(0f, 22f);
        panelRect.sizeDelta = new Vector2(1080f, 178f);

        HeatwaveUIFontKit.ApplyPixelFrame(
            panelRect,
            new Color(0.10f, 0.10f, 0.11f, 0.96f),
            new Color(0.72f, 0.53f, 0.30f, 1f),
            new Color(1f, 0.86f, 0.56f, 1f),
            5f,
            12f
        );

        var speakerGo = new GameObject("Speaker", typeof(RectTransform), typeof(TextMeshProUGUI));
        speakerGo.transform.SetParent(panelRoot.transform, false);
        var speakerRect = speakerGo.GetComponent<RectTransform>();
        speakerRect.anchorMin = new Vector2(0f, 1f);
        speakerRect.anchorMax = new Vector2(1f, 1f);
        speakerRect.pivot = new Vector2(0.5f, 1f);
        speakerRect.anchoredPosition = new Vector2(0f, -14f);
        speakerRect.sizeDelta = new Vector2(-36f, 40f);

        speakerText = speakerGo.GetComponent<TextMeshProUGUI>();
        speakerText.alignment = TextAlignmentOptions.Left;
        speakerText.textWrappingMode = TextWrappingModes.NoWrap;
        HeatwaveUIFontKit.ApplyReadableTMP(
            speakerText,
            34f,
            new Color(1f, 0.93f, 0.70f, 1f),
            new Color(0.06f, 0.04f, 0.03f, 1f),
            0.28f
        );

        var lineGo = new GameObject("Line", typeof(RectTransform), typeof(TextMeshProUGUI));
        lineGo.transform.SetParent(panelRoot.transform, false);
        var lineRect = lineGo.GetComponent<RectTransform>();
        lineRect.anchorMin = new Vector2(0f, 0f);
        lineRect.anchorMax = new Vector2(1f, 1f);
        lineRect.offsetMin = new Vector2(18f, 14f);
        lineRect.offsetMax = new Vector2(-18f, -52f);

        lineText = lineGo.GetComponent<TextMeshProUGUI>();
        lineText.alignment = TextAlignmentOptions.TopLeft;
        lineText.textWrappingMode = TextWrappingModes.Normal;
        HeatwaveUIFontKit.ApplyReadableTMP(
            lineText,
            30f,
            new Color(0.96f, 0.97f, 1f, 1f),
            new Color(0.05f, 0.05f, 0.05f, 1f),
            0.22f
        );

        panelRoot.SetActive(false);
    }
}
