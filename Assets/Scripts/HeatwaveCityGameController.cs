using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Yarn.Unity;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class HeatwaveCityGameController : MonoBehaviour
{
    public static HeatwaveCityGameController Instance { get; private set; }

    [Header("Dialogue Setup")]
    [SerializeField] YarnProject yarnProject;
    [SerializeField] string startNode = "C1_BOOTSTRAP";
    [SerializeField] KeyCode startDialogueKey = KeyCode.T;
    [SerializeField] bool allowManualStart = false;
    [SerializeField] bool autoStartDialogue = true;

    [Header("HUD")]
    [SerializeField] bool showRuntimeHUD = true;
    [SerializeField] Vector2 hudMargin = new Vector2(24f, 24f);
    [SerializeField] bool showRoomObjectives = true;
    [Header("Opening Fallback")]
    [SerializeField] bool forceOpeningNarrativeFallback = true;
    [Header("Flow UI")]
    [SerializeField] bool showTitleCoverOnLaunch = true;
    [SerializeField] string titleSceneName = "TitleScene";

    DialogueRunner dialogueRunner;
    HeatwaveDialoguePresenter presenter;
    TMP_Text statusText;
    TMP_Text objectiveText;
    TMP_Text guideText;
    float hudTick;
    [SerializeField] int maxDays = 7;
    int currentDay = 1;
    bool finalResultEvaluated;
    bool openingFallbackPlayed;
    bool runStarted;
    bool failureOverlayShown;
    GameObject titleOverlay;
    GameObject failureOverlay;
    Button startRunButton;
    Button retryButton;
    bool smokeQuitScheduled;
    static readonly Dictionary<string, Sprite> coverSpriteCache = new Dictionary<string, Sprite>();
    static readonly Dictionary<string, string> coverFileIndex = new Dictionary<string, string>();
    static bool coverIndexBuilt;
    const string CoverAssetsFolder = "CoverAssets";

    public static bool IsInputLocked { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        DialogueUIController.ApplyDialogueLayoutNow();
        EnsureRunner();
        EnsurePresenter();
        WireRunner();
        TryAssignYarnProject();
        EnsureHUD();
    }

    void Start()
    {
        if (dialogueRunner != null && dialogueRunner.YarnProject == null && presenter != null)
        {
            Debug.LogError("Heatwave: No YarnProject assigned at startup.");
            presenter.ShowSystemMessage(
                "No YarnProject assigned.\n" +
                "Create one via Assets > Create > Yarn Spinner > Yarn Project,\n" +
                "then add HeatwaveCity_Main.yarn as a source."
            );
        }

        InitializeCoreWeekVariables();
        forceOpeningNarrativeFallback = true;
        showTitleCoverOnLaunch = true;

        BuildFlowUI();

        if (showTitleCoverOnLaunch)
        {
            ShowTitleCover();
        }
        else
        {
            BeginRun();
        }

        ScheduleSmokeTestQuitIfRequested();
    }

    void Update()
    {
        if (allowManualStart && Input.GetKeyDown(startDialogueKey))
        {
            StartHeatwaveDialogue();
        }

        if (showRuntimeHUD)
        {
            hudTick += Time.deltaTime;
            if (hudTick >= 0.15f)
            {
                hudTick = 0f;
                RefreshHUD();
            }
        }
    }

    public void StartHeatwaveDialogue()
    {
        bool started = TryStartOpeningDialogue();
        if (!started && presenter != null)
        {
            presenter.ShowSystemMessage(
                "Cannot start opening briefing.\n" +
                "Missing node: C1_BOOTSTRAP.\n" +
                "Please reimport HeatwaveCity.yarnproject."
            );
        }
    }

    public bool TryStartNode(string requestedNode, bool allowFallback = true)
    {
        if (dialogueRunner == null)
        {
            Debug.LogWarning("Heatwave: DialogueRunner not ready.");
            return false;
        }

        if (dialogueRunner.IsDialogueRunning) return false;

        TryAssignYarnProject();

        if (dialogueRunner.YarnProject == null)
        {
            Debug.LogWarning("Heatwave: No YarnProject assigned. Create a .yarnproject asset and assign it to HeatwaveCityGameController.");
            if (presenter != null)
            {
                presenter.ShowSystemMessage(
                    "Cannot start dialogue.\n" +
                    "No YarnProject assigned to HeatwaveCityGameController."
                );
            }
            return false;
        }

        string nodeToStart = ResolveStartNode(dialogueRunner.YarnProject, requestedNode, allowFallback);
        if (string.IsNullOrEmpty(nodeToStart))
        {
            Debug.LogWarning("Heatwave: Assigned YarnProject contains no playable nodes.");
            if (presenter != null)
            {
                presenter.ShowSystemMessage(
                    "Cannot start dialogue.\n" +
                    "YarnProject has no compiled nodes."
                );
            }
            return false;
        }

        dialogueRunner.StartDialogue(nodeToStart).Forget();
        return true;
    }

    public bool IsMainDialogueRunning => dialogueRunner != null && dialogueRunner.IsDialogueRunning;
    public int CurrentDay => currentDay;

    public bool IsVariableTrue(string variableName)
    {
        if (string.IsNullOrWhiteSpace(variableName) || dialogueRunner == null) return false;
        var storage = dialogueRunner.VariableStorage;
        if (storage == null) return false;
        return storage.TryGetValue<bool>(variableName, out var value) && value;
    }

    public bool TrySetBoolVariable(string variableName, bool value)
    {
        if (string.IsNullOrWhiteSpace(variableName) || dialogueRunner == null) return false;
        var storage = dialogueRunner.VariableStorage;
        if (storage == null) return false;
        storage.SetValue(variableName, value);
        return true;
    }

    public string[] CompleteFieldTask(string taskKey)
    {
        var storage = dialogueRunner != null ? dialogueRunner.VariableStorage : null;
        if (storage == null)
        {
            return new[] { "System not ready yet.", "Try again in a moment." };
        }

        if (!IsDay1Started(storage))
        {
            return new[]
            {
                "Field work is locked during orientation.",
                "Meet all NPC leaders first, then return to Maya to start Day 1."
            };
        }

        if (IsWeekFailed(storage))
        {
            return new[]
            {
                "Day 7 deadline reached: mission failed.",
                "Heatwave City did not meet stability targets this week."
            };
        }

        if (IsWeekFinalized(storage))
        {
            return new[]
            {
                "Week report already finalized.",
                "Start a new run if you want a different outcome."
            };
        }

        switch (taskKey)
        {
            case "TASK_MAYA":
                return CompleteMayaTask(storage);
            case "TASK_DANIEL":
                return CompleteDanielTask(storage);
            case "TASK_ALVAREZ":
                return CompleteAlvarezTask(storage);
            case "TASK_TRANSIT":
                return CompleteTransitTask(storage);
            case "TASK_KAI":
                return CompleteKaiTask(storage);
            case "TASK_ROWAN":
                return CompleteRowanTask(storage);
            case "TASK_FINAL":
                return CompleteFinalAudit(storage);
            default:
                return new[] { "No field objective is linked here yet." };
        }
    }

    void EnsureRunner()
    {
        dialogueRunner = FindFirstObjectByType<DialogueRunner>();
        if (dialogueRunner != null) return;

        var go = new GameObject("HeatwaveDialogueRunner");
        dialogueRunner = go.AddComponent<DialogueRunner>();
        dialogueRunner.autoStart = false;
        dialogueRunner.verboseLogging = false;
    }

    void EnsurePresenter()
    {
        presenter = FindFirstObjectByType<HeatwaveDialoguePresenter>();
        if (presenter != null) return;

        var panel = DialogueUIController.FindDialoguePanel();
        if (panel == null)
        {
            Debug.LogWarning("Heatwave: DialoguePanel not found in scene.");
            return;
        }

        presenter = panel.GetComponent<HeatwaveDialoguePresenter>();
        if (presenter == null)
        {
            presenter = panel.AddComponent<HeatwaveDialoguePresenter>();
        }

        presenter.AutoBindFromScene();
    }

    void WireRunner()
    {
        if (dialogueRunner == null || presenter == null) return;
        dialogueRunner.DialoguePresenters = new DialoguePresenterBase[] { presenter };
    }

    void TryAssignYarnProject()
    {
        if (dialogueRunner == null) return;

        if (yarnProject == null)
        {
            yarnProject = Resources.Load<YarnProject>("Yarn/HeatwaveCity")
                ?? Resources.Load<YarnProject>("HeatwaveCity");

#if UNITY_EDITOR
            TryAssignProjectFromKnownPath();
#endif

            var loadedProjects = Resources.FindObjectsOfTypeAll<YarnProject>();

            for (int i = 0; i < loadedProjects.Length; i++)
            {
                var project = loadedProjects[i];
                if (project == null) continue;
                if (project.name != "HeatwaveCity") continue;
                if (!TryGetNodeNames(project, out var nodeNames) || nodeNames.Length == 0) continue;
                yarnProject = project;
                break;
            }

            for (int i = 0; i < loadedProjects.Length; i++)
            {
                var project = loadedProjects[i];
                if (project == null || yarnProject != null) continue;
                if (!project.name.Contains("Heatwave")) continue;
                if (!TryGetNodeNames(project, out var nodeNames) || nodeNames.Length == 0) continue;
                yarnProject = project;
                break;
            }

            for (int i = 0; i < loadedProjects.Length; i++)
            {
                var project = loadedProjects[i];
                if (project == null || yarnProject != null) continue;
                if (!TryGetNodeNames(project, out var nodeNames) || nodeNames.Length == 0) continue;
                yarnProject = project;
                break;
            }

            if (yarnProject == null)
            {
                for (int i = 0; i < loadedProjects.Length; i++)
                {
                    if (loadedProjects[i] != null)
                    {
                        yarnProject = loadedProjects[i];
                        break;
                    }
                }
            }
        }

        if (yarnProject != null && !dialogueRunner.IsDialogueRunning)
        {
            try
            {
                dialogueRunner.SetProject(yarnProject);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Heatwave: failed to set YarnProject: {ex.Message}");
            }
        }
    }

#if UNITY_EDITOR
    void TryAssignProjectFromKnownPath()
    {
        const string projectPath = "Assets/Resources/Yarn/HeatwaveCity.yarnproject";

        yarnProject = AssetDatabase.LoadAssetAtPath<YarnProject>(projectPath);
        if (yarnProject != null && TryGetNodeNames(yarnProject, out var existingNodes) && existingNodes.Length > 0)
        {
            return;
        }

        var project = new Yarn.Compiler.Project();
        project.ExcludeFilePatterns = new[]
        {
            "**/*~/*",
            "./Samples/Yarn Spinner*/*",
            "**/HeatwaveCity_SimpleEnglish.yarn",
        };
        project.SourceFilePatterns = new[]
        {
            "HeatwaveCity_Main.yarn",
        };

        project.SaveToFile(projectPath);
        AssetDatabase.ImportAsset(projectPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

        yarnProject = AssetDatabase.LoadAssetAtPath<YarnProject>(projectPath);
    }
#endif

    void EnsureHUD()
    {
        if (!showRuntimeHUD) return;

        var canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        statusText = CreateHUDPanel(
            canvas.transform,
            "HeatwaveHUD_Status",
            new Vector2(470f, 198f),
            new Vector2(hudMargin.x, -hudMargin.y),
            new Color(0.11f, 0.10f, 0.08f, 0.95f),
            new Color(1f, 0.93f, 0.78f, 1f),
            25f
        );

        objectiveText = CreateHUDPanel(
            canvas.transform,
            "HeatwaveHUD_Objectives",
            new Vector2(560f, 418f),
            new Vector2(hudMargin.x, -(hudMargin.y + 222f)),
            new Color(0.10f, 0.10f, 0.11f, 0.95f),
            new Color(0.96f, 0.98f, 1f, 1f),
            22f
        );
        if (objectiveText != null)
        {
            objectiveText.enableAutoSizing = true;
            objectiveText.fontSizeMin = 17f;
            objectiveText.fontSizeMax = 24f;
            objectiveText.lineSpacing = 5f;
        }

        guideText = CreateGuideBanner(canvas.transform);

        RefreshHUD();
    }

    static TMP_Text CreateHUDPanel(
        Transform parent,
        string panelName,
        Vector2 size,
        Vector2 anchoredPosition,
        Color panelColor,
        Color textColor,
        float fontSize)
    {
        var existing = parent.Find(panelName);
        GameObject panel = existing != null
            ? existing.gameObject
            : new GameObject(panelName, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);

        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = anchoredPosition;
        panelRect.sizeDelta = size;

        HeatwaveUIFontKit.ApplyPixelFrame(
            panelRect,
            panelColor,
            new Color(0.70f, 0.52f, 0.28f, 1f),
            new Color(1f, 0.86f, 0.56f, 1f),
            5f,
            14f
        );

        var textNode = panel.transform.Find($"{panelName}_Text");
        GameObject textGO = textNode != null
            ? textNode.gameObject
            : new GameObject($"{panelName}_Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGO.transform.SetParent(panel.transform, false);

        var textRect = textGO.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(18f, 14f);
        textRect.offsetMax = new Vector2(-18f, -14f);

        var text = textGO.GetComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.TopLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        HeatwaveUIFontKit.ApplyReadableTMP(
            text,
            fontSize,
            textColor,
            new Color(0.04f, 0.03f, 0.02f, 1f),
            0.26f
        );
        return text;
    }

    static TMP_Text CreateGuideBanner(Transform parent)
    {
        var existing = parent.Find("HeatwaveHUD_Guide");
        GameObject panel = existing != null
            ? existing.gameObject
            : new GameObject("HeatwaveHUD_Guide", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);

        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 1f);
        panelRect.anchorMax = new Vector2(0.5f, 1f);
        panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.anchoredPosition = new Vector2(0f, -22f);
        panelRect.sizeDelta = new Vector2(900f, 76f);

        HeatwaveUIFontKit.ApplyPixelFrame(
            panelRect,
            new Color(0.09f, 0.08f, 0.07f, 0.92f),
            new Color(0.62f, 0.46f, 0.28f, 1f),
            new Color(0.95f, 0.78f, 0.46f, 1f),
            4f,
            10f
        );

        var textNode = panel.transform.Find("HeatwaveHUD_GuideText");
        GameObject textGO = textNode != null
            ? textNode.gameObject
            : new GameObject("HeatwaveHUD_GuideText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGO.transform.SetParent(panel.transform, false);

        var textRect = textGO.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(18f, 14f);
        textRect.offsetMax = new Vector2(-18f, -14f);

        var text = textGO.GetComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        HeatwaveUIFontKit.ApplyReadableTMP(
            text,
            27f,
            new Color(1f, 0.95f, 0.76f, 1f),
            new Color(0.08f, 0.06f, 0.04f, 1f),
            0.25f
        );
        return text;
    }

    void RefreshHUD()
    {
        if (statusText == null || objectiveText == null || dialogueRunner == null)
        {
            return;
        }

        var storage = dialogueRunner.VariableStorage;
        if (storage != null)
        {
            storage.SetValue("$current_day", currentDay);
            storage.SetValue("$days_remaining", Mathf.Max(0, maxDays - currentDay));
        }

        string heatSafety = ReadFloat(storage, "$heat_safety");
        string trust = ReadFloat(storage, "$community_trust");
        string infra = ReadFloat(storage, "$infrastructure_stability");
        string vulnerable = ReadFloat(storage, "$vulnerable_risk");
        string budget = ReadFloat(storage, "$budget_reserve");

        statusText.text =
            "HEATWAVE CITY\n" +
            $"Day: {currentDay}/{maxDays}\n" +
            $"Heat Safety: {heatSafety}\n" +
            $"Community Trust: {trust}\n" +
            $"Infrastructure: {infra}\n" +
            $"Vulnerable Risk: {vulnerable}\n" +
            $"Budget Reserve: {budget}";

        objectiveText.text = showRoomObjectives ? BuildRoomObjectiveText(storage) : "OBJECTIVES\nOff";
        if (guideText != null)
        {
            guideText.text = BuildLiveGuidance(storage);
        }

        UpdateFailureOverlay(storage);
    }

    void BuildFlowUI()
    {
        var canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        titleOverlay = EnsureFullscreenPanel(
            canvas.transform,
            "Heatwave_TitleOverlay",
            new Color(0.08f, 0.07f, 0.06f, 0.94f),
            out var titleText,
            out var subtitleText,
            out startRunButton
        );

        if (titleText != null)
        {
            titleText.text = "HEATWAVE MAYOR";
        }
        if (subtitleText != null)
        {
            subtitleText.text =
                "You crossed into a collapsing city.\n" +
                "Lead a nonviolent 7-day heat response.\n" +
                "Balance heat safety, trust, infrastructure, and justice.";
        }
        if (startRunButton != null)
        {
            startRunButton.onClick.RemoveAllListeners();
            startRunButton.onClick.AddListener(BeginRun);
            var startLabel = startRunButton.GetComponentInChildren<TMP_Text>(true);
            if (startLabel != null) startLabel.text = "START RUN";
        }

        failureOverlay = EnsureFullscreenPanel(
            canvas.transform,
            "Heatwave_FailureOverlay",
            new Color(0.14f, 0.04f, 0.04f, 0.90f),
            out var failTitle,
            out var failBody,
            out retryButton
        );

        if (failTitle != null) failTitle.text = "WEEK FAILED";
        if (failBody != null)
        {
            failBody.text =
                "Day 7 deadline reached without stable targets.\n" +
                "Retry and rebalance your policy trade-offs.";
        }
        if (retryButton != null)
        {
            retryButton.onClick.RemoveAllListeners();
            retryButton.onClick.AddListener(RetryRunFromCover);
            var retryLabel = retryButton.GetComponentInChildren<TMP_Text>(true);
            if (retryLabel != null) retryLabel.text = "RETRY";
        }

        ApplyCoverSkin();

        if (titleOverlay != null) titleOverlay.SetActive(false);
        if (failureOverlay != null) failureOverlay.SetActive(false);
    }

    static GameObject EnsureFullscreenPanel(
        Transform canvasRoot,
        string panelName,
        Color overlayColor,
        out TMP_Text titleText,
        out TMP_Text bodyText,
        out Button actionButton)
    {
        titleText = null;
        bodyText = null;
        actionButton = null;
        if (canvasRoot == null) return null;

        var panelNode = canvasRoot.Find(panelName);
        GameObject panel = panelNode != null
            ? panelNode.gameObject
            : new GameObject(panelName, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasRoot, false);
        panel.transform.SetAsLastSibling();

        var rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var image = panel.GetComponent<Image>();
        image.color = overlayColor;
        image.raycastTarget = true;

        var frameRoot = panel.transform.Find("FrameRoot");
        GameObject frame = frameRoot != null
            ? frameRoot.gameObject
            : new GameObject("FrameRoot", typeof(RectTransform), typeof(Image));
        frame.transform.SetParent(panel.transform, false);
        var frameRect = frame.GetComponent<RectTransform>();
        frameRect.anchorMin = new Vector2(0.5f, 0.5f);
        frameRect.anchorMax = new Vector2(0.5f, 0.5f);
        frameRect.pivot = new Vector2(0.5f, 0.5f);
        frameRect.sizeDelta = new Vector2(980f, 520f);
        HeatwaveUIFontKit.ApplyPixelFrame(
            frameRect,
            new Color(0.12f, 0.10f, 0.08f, 0.97f),
            new Color(0.70f, 0.52f, 0.28f, 1f),
            new Color(1f, 0.86f, 0.56f, 1f),
            6f,
            16f
        );
        EnsureFrameBackdrop(frame.transform);

        titleText = EnsurePanelText(
            frame.transform,
            "TitleText",
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -60f),
            new Vector2(880f, 96f),
            72f,
            TextAlignmentOptions.Center,
            new Color(1f, 0.93f, 0.74f, 1f)
        );

        bodyText = EnsurePanelText(
            frame.transform,
            "BodyText",
            new Vector2(0.5f, 0.62f),
            new Vector2(0.5f, 0.62f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, -10f),
            new Vector2(860f, 210f),
            31f,
            TextAlignmentOptions.Center,
            new Color(0.95f, 0.96f, 1f, 1f)
        );
        if (bodyText != null)
        {
            bodyText.textWrappingMode = TextWrappingModes.Normal;
        }

        actionButton = EnsurePanelButton(frame.transform, "ActionButton", new Vector2(0f, -190f), new Vector2(360f, 112f));
        return panel;
    }

    static void EnsureFrameBackdrop(Transform frameRoot)
    {
        if (frameRoot == null) return;

        var backdropNode = frameRoot.Find("ContentBackdrop");
        GameObject backdrop = backdropNode != null
            ? backdropNode.gameObject
            : new GameObject("ContentBackdrop", typeof(RectTransform), typeof(Image));
        backdrop.transform.SetParent(frameRoot, false);
        backdrop.transform.SetSiblingIndex(0);

        var rect = backdrop.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.14f, 0.22f);
        rect.anchorMax = new Vector2(0.86f, 0.78f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var image = backdrop.GetComponent<Image>();
        image.color = new Color(0.16f, 0.12f, 0.08f, 0.98f);
        image.raycastTarget = false;
    }

    static TMP_Text EnsurePanelText(
        Transform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 size,
        float fontSize,
        TextAlignmentOptions alignment,
        Color color)
    {
        if (parent == null) return null;
        var node = parent.Find(name);
        GameObject go = node != null
            ? node.gameObject
            : new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        var text = go.GetComponent<TextMeshProUGUI>();
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        HeatwaveUIFontKit.ApplyReadableTMP(
            text,
            fontSize,
            color,
            new Color(0.03f, 0.03f, 0.03f, 1f),
            0.30f
        );
        return text;
    }

    static Button EnsurePanelButton(Transform parent, string name, Vector2 anchoredPosition, Vector2 size)
    {
        if (parent == null) return null;
        var node = parent.Find(name);
        GameObject go = node != null
            ? node.gameObject
            : new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        HeatwaveUIFontKit.ApplyPixelFrame(
            rect,
            new Color(0.22f, 0.33f, 0.24f, 1f),
            new Color(0.76f, 0.89f, 0.54f, 1f),
            new Color(0.93f, 1f, 0.78f, 1f),
            5f,
            14f
        );

        var img = go.GetComponent<Image>();
        img.raycastTarget = true;
        var button = go.GetComponent<Button>();

        var textNode = go.transform.Find("Label");
        GameObject textGo = textNode != null
            ? textNode.gameObject
            : new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(go.transform, false);
        var textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        var label = textGo.GetComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        HeatwaveUIFontKit.ApplyReadableTMP(
            label,
            46f,
            new Color(0.94f, 1f, 0.90f, 1f),
            new Color(0.06f, 0.08f, 0.05f, 1f),
            0.24f
        );
        label.text = "START";

        return button;
    }

    void ApplyCoverSkin()
    {
        var titleBg = TryLoadCoverSpriteAny(
            "title_bg.png",
            "TitleCoverBackground.png",
            "TitleCoverBackground");
        var failureBg = TryLoadCoverSpriteAny(
            "title_bg_failure.png",
            "TitleCoverBackgounrdVarient.png",
            "TitleCoverBackgroundVarient.png",
            "TitleCoverBackgroundVariant.png",
            "TitleCoverBackgounrdVarient");

        ApplyOverlayBackground(titleOverlay, titleBg, false);
        ApplyOverlayBackground(failureOverlay, failureBg, true);

        ApplyLogo(titleOverlay,
            TryLoadCoverSpriteAny("logo_emblem.png", "LogoEmblem.png", "LogoEmblem"));

        var frameSprite = TryLoadCoverSpriteAny("ui_frame.png", "PixelUIFrame.png", "PixelUIFrame");
        ApplyFrameSprite(titleOverlay, frameSprite);
        ApplyFrameSprite(failureOverlay, frameSprite);

        ApplyButtonSkin(
            startRunButton,
            new[] { "btn_start_normal.png" },
            new[] { "btn_start_hover.png" },
            new[] { "btn_start_pressed.png" });
        ApplyButtonSkin(
            retryButton,
            new[] { "btn_retry_normal.png" },
            new[] { "btn_retry_hover.png" },
            new[] { "btn_retry_pressed.png" });
    }

    void ApplyOverlayBackground(GameObject overlay, Sprite sourceSprite, bool failure)
    {
        if (overlay == null) return;

        var bgNode = overlay.transform.Find("BackgroundArt");
        GameObject bg = bgNode != null
            ? bgNode.gameObject
            : new GameObject("BackgroundArt", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(overlay.transform, false);
        bg.transform.SetAsFirstSibling();

        var rect = bg.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var image = bg.GetComponent<Image>();
        image.raycastTarget = false;
        image.sprite = sourceSprite != null ? sourceSprite : GetCoverOrFallback(failure);
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.color = failure ? new Color(1f, 0.84f, 0.84f, 0.80f) : new Color(1f, 1f, 1f, 0.78f);

        EnsureOverlayScrim(overlay.transform, failure);
    }

    static void EnsureOverlayScrim(Transform overlayRoot, bool failure)
    {
        if (overlayRoot == null) return;

        var scrimNode = overlayRoot.Find("BottomScrim");
        GameObject scrim = scrimNode != null
            ? scrimNode.gameObject
            : new GameObject("BottomScrim", typeof(RectTransform), typeof(Image));
        scrim.transform.SetParent(overlayRoot, false);
        scrim.transform.SetSiblingIndex(1);

        var rect = scrim.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0.36f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var image = scrim.GetComponent<Image>();
        image.raycastTarget = false;
        image.color = failure
            ? new Color(0.15f, 0.03f, 0.03f, 0.54f)
            : new Color(0.07f, 0.06f, 0.05f, 0.62f);
    }

    void ApplyLogo(GameObject overlay, Sprite logoSprite)
    {
        if (overlay == null) return;

        var frame = overlay.transform.Find("FrameRoot");
        if (frame == null) return;

        var logoNode = frame.Find("Logo");
        GameObject logo = logoNode != null
            ? logoNode.gameObject
            : new GameObject("Logo", typeof(RectTransform), typeof(Image));
        logo.transform.SetParent(frame, false);

        var rect = logo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(24f, -24f);
        rect.sizeDelta = new Vector2(88f, 88f);

        var image = logo.GetComponent<Image>();
        image.raycastTarget = false;
        image.preserveAspect = true;
        if (logoSprite == null)
        {
            logo.SetActive(false);
            return;
        }

        image.sprite = logoSprite;
        image.color = Color.white;
        logo.SetActive(true);
    }

    void ApplyFrameSprite(GameObject overlay, Sprite frameSprite)
    {
        if (overlay == null) return;
        var frame = overlay.transform.Find("FrameRoot");
        if (frame == null) return;

        var image = frame.GetComponent<Image>();
        if (image == null) return;

        if (frameSprite == null) return;

        image.sprite = frameSprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.color = Color.white;
    }

    void ApplyButtonSkin(Button button, string[] normalCandidates, string[] hoverCandidates, string[] pressedCandidates)
    {
        if (button == null) return;

        var image = button.GetComponent<Image>();
        if (image == null) return;

        var normal = TryLoadCoverSpriteAny(normalCandidates);
        var hover = TryLoadCoverSpriteAny(hoverCandidates);
        var pressed = TryLoadCoverSpriteAny(pressedCandidates);

        if (normal != null)
        {
            image.sprite = normal;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.color = Color.white;

            var state = button.spriteState;
            state.highlightedSprite = hover != null ? hover : normal;
            state.selectedSprite = hover != null ? hover : normal;
            state.pressedSprite = pressed != null ? pressed : normal;
            button.spriteState = state;
            button.transition = Selectable.Transition.SpriteSwap;
        }
        else
        {
            var colors = button.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 1f);
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            colors.pressedColor = new Color(0.84f, 0.84f, 0.84f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.65f, 0.65f, 0.65f, 1f);
            colors.colorMultiplier = 1f;
            button.colors = colors;
            button.transition = Selectable.Transition.ColorTint;
        }
    }

    static Sprite GetCoverOrFallback(bool failure)
    {
        string fallbackKey = failure ? "__fallback_failure_bg__" : "__fallback_title_bg__";
        if (coverSpriteCache.TryGetValue(fallbackKey, out var cached) && cached != null)
        {
            return cached;
        }

        Color top = failure ? new Color(0.43f, 0.13f, 0.12f, 1f) : new Color(0.47f, 0.35f, 0.22f, 1f);
        Color bottom = failure ? new Color(0.16f, 0.05f, 0.05f, 1f) : new Color(0.20f, 0.14f, 0.10f, 1f);
        Color tile = failure ? new Color(0.50f, 0.20f, 0.18f, 1f) : new Color(0.56f, 0.42f, 0.24f, 1f);

        var texture = new Texture2D(320, 180, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < texture.height; y++)
        {
            float t = (float)y / (texture.height - 1);
            Color grad = Color.Lerp(bottom, top, t);
            for (int x = 0; x < texture.width; x++)
            {
                int block = ((x / 8) + (y / 8)) % 2;
                Color c = block == 0 ? grad : Color.Lerp(grad, tile, 0.14f);
                texture.SetPixel(x, y, c);
            }
        }

        texture.Apply(false, false);
        var generated = Sprite.Create(
            texture,
            new Rect(0, 0, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect);
        generated.name = fallbackKey;
        coverSpriteCache[fallbackKey] = generated;
        return generated;
    }

    static Sprite TryLoadCoverSpriteAny(params string[] candidates)
    {
        if (candidates == null || candidates.Length == 0) return null;
        for (int i = 0; i < candidates.Length; i++)
        {
            var sprite = TryLoadCoverSprite(candidates[i]);
            if (sprite != null) return sprite;
        }

        return null;
    }

    static Sprite TryLoadCoverSprite(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return null;
        if (coverSpriteCache.TryGetValue(fileName, out var cached) && cached != null)
        {
            return cached;
        }

        string noExtName = Path.GetFileNameWithoutExtension(fileName);
        var resourceSprite =
            Resources.Load<Sprite>($"CoverAssets/{noExtName}")
            ?? Resources.Load<Sprite>($"Mirror/CoverAssets/{noExtName}")
            ?? Resources.Load<Sprite>($"Mirror/Tile Pixels/{noExtName}")
            ?? Resources.Load<Sprite>($"Mirror/TilePixels/{noExtName}");
        if (resourceSprite != null)
        {
            coverSpriteCache[fileName] = resourceSprite;
            return resourceSprite;
        }

        EnsureCoverFileIndex();

        string diskPath = null;
        if (fileName.Contains("/") || fileName.Contains("\\"))
        {
            string projectPath = fileName.StartsWith("Assets/")
                ? fileName.Substring("Assets/".Length)
                : Path.Combine(CoverAssetsFolder, fileName);
            diskPath = Path.Combine(Application.dataPath, projectPath);
            if (!File.Exists(diskPath))
            {
                diskPath = null;
            }
        }

        if (diskPath == null)
        {
            string keyLower = fileName.ToLowerInvariant();
            if (!coverFileIndex.TryGetValue(keyLower, out diskPath))
            {
                string noExt = Path.GetFileNameWithoutExtension(keyLower);
                coverFileIndex.TryGetValue(noExt, out diskPath);
            }
        }

        if (string.IsNullOrWhiteSpace(diskPath) || !File.Exists(diskPath))
        {
            return null;
        }

        try
        {
            byte[] bytes = File.ReadAllBytes(diskPath);
            if (bytes == null || bytes.Length == 0) return null;

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            if (!texture.LoadImage(bytes, false))
            {
                Destroy(texture);
                return null;
            }

            texture.name = fileName;
            var sprite = Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect);
            sprite.name = fileName;
            coverSpriteCache[fileName] = sprite;
            return sprite;
        }
        catch
        {
            return null;
        }
    }

    static void EnsureCoverFileIndex()
    {
        if (coverIndexBuilt) return;
        coverIndexBuilt = true;
        coverFileIndex.Clear();

        string[] roots =
        {
            Path.Combine(Application.dataPath, "CoverAssets"),
            Path.Combine(Application.dataPath, "Tile Pixels"),
            Path.Combine(Application.dataPath, "tile pixels"),
            Path.Combine(Application.dataPath, "TilePixels"),
        };

        for (int i = 0; i < roots.Length; i++)
        {
            string root = roots[i];
            if (!Directory.Exists(root)) continue;

            string[] files = Directory.GetFiles(root, "*.*", SearchOption.AllDirectories);
            for (int f = 0; f < files.Length; f++)
            {
                string path = files[f];
                string ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext != ".png" && ext != ".jpg" && ext != ".jpeg") continue;

                string fileName = Path.GetFileName(path).ToLowerInvariant();
                if (!coverFileIndex.ContainsKey(fileName))
                {
                    coverFileIndex[fileName] = path;
                }

                string noExt = Path.GetFileNameWithoutExtension(fileName);
                if (!coverFileIndex.ContainsKey(noExt))
                {
                    coverFileIndex[noExt] = path;
                }
            }
        }
    }

    void ShowTitleCover()
    {
        SetInputLock(true);
        if (failureOverlay != null) failureOverlay.SetActive(false);
        if (titleOverlay != null) titleOverlay.SetActive(true);
    }

    void BeginRun()
    {
        if (runStarted) return;
        runStarted = true;
        failureOverlayShown = false;

        if (titleOverlay != null) titleOverlay.SetActive(false);
        if (failureOverlay != null) failureOverlay.SetActive(false);
        SetInputLock(false);

        if (autoStartDialogue)
        {
            if (!TryStartOpeningDialogue() && forceOpeningNarrativeFallback)
            {
                StartCoroutine(PlayGuaranteedOpeningRoutine());
            }
        }
        else if (forceOpeningNarrativeFallback)
        {
            StartCoroutine(PlayGuaranteedOpeningRoutine());
        }
    }

    void RetryRunFromCover()
    {
        SetInputLock(false);
        string sceneToLoad = ResolveRestartSceneName();
        SceneManager.LoadScene(sceneToLoad, LoadSceneMode.Single);
    }

    string ResolveRestartSceneName()
    {
        if (!string.IsNullOrWhiteSpace(titleSceneName) && SceneExistsInBuild(titleSceneName))
        {
            return titleSceneName;
        }

        return SceneManager.GetActiveScene().name;
    }

    static bool SceneExistsInBuild(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName)) return false;
        int sceneCount = SceneManager.sceneCountInBuildSettings;
        for (int i = 0; i < sceneCount; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            if (string.IsNullOrWhiteSpace(path)) continue;
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (name == sceneName) return true;
        }

        return false;
    }

    void SetInputLock(bool locked)
    {
        IsInputLocked = locked;
        var mover = FindFirstObjectByType<PlayerMovement>();
        if (mover != null)
        {
            mover.enabled = !locked;
            var rb = mover.GetComponent<Rigidbody2D>();
            if (locked && rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
        }
    }

    void UpdateFailureOverlay(VariableStorageBehaviour storage)
    {
        if (failureOverlay == null || storage == null) return;

        bool failed = IsWeekFailed(storage);
        if (!failed)
        {
            if (failureOverlayShown)
            {
                failureOverlayShown = false;
                failureOverlay.SetActive(false);
            }
            return;
        }

        if (failureOverlayShown) return;

        failureOverlayShown = true;
        if (titleOverlay != null) titleOverlay.SetActive(false);
        failureOverlay.SetActive(true);
        failureOverlay.transform.SetAsLastSibling();
        SetInputLock(true);
    }

    void ScheduleSmokeTestQuitIfRequested()
    {
        if (smokeQuitScheduled) return;
        var args = System.Environment.GetCommandLineArgs();
        if (args == null || args.Length == 0) return;

        bool smoke = false;
        float seconds = 7f;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "-smoketest")
            {
                smoke = true;
                continue;
            }

            if (args[i] == "-autoQuitSeconds" && i + 1 < args.Length && float.TryParse(args[i + 1], out var parsed))
            {
                seconds = Mathf.Clamp(parsed, 2f, 45f);
            }
        }

        if (!smoke) return;
        smokeQuitScheduled = true;
        StartCoroutine(SmokeQuitRoutine(seconds));
    }

    IEnumerator SmokeQuitRoutine(float seconds)
    {
        Debug.Log($"Heatwave smoke test: auto quit in {seconds:0.0}s");
        yield return new WaitForSeconds(seconds);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit(0);
#endif
    }

    IEnumerator PlayGuaranteedOpeningRoutine()
    {
        if (openingFallbackPlayed) yield break;
        if (dialogueRunner == null) yield break;

        var storage = dialogueRunner.VariableStorage;
        if (storage != null && IsDay1Started(storage)) yield break;

        // Prevent hidden/stuck Yarn line state from eating the opening.
        if (dialogueRunner.IsDialogueRunning)
        {
            dialogueRunner.Stop().Forget();
            float stopWait = 1.2f;
            while (dialogueRunner.IsDialogueRunning && stopWait > 0f)
            {
                stopWait -= Time.deltaTime;
                yield return null;
            }
        }

        float timeout = 4f;
        while (HeatwaveNpcDialogueUI.Instance == null && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        if (HeatwaveNpcDialogueUI.Instance == null)
        {
            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas != null)
            {
                canvas.gameObject.AddComponent<HeatwaveNpcDialogueUI>();
            }
        }

        timeout = 1.5f;
        while (HeatwaveNpcDialogueUI.Instance == null && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        var ui = HeatwaveNpcDialogueUI.Instance;
        if (ui == null) yield break;

        openingFallbackPlayed = true;

        yield return RunFallbackConversation(ui, "NARRATOR", new[]
        {
            "Darkness. Then a fan hums overhead.",
            "You wake up in an unfamiliar mayor office.",
            "Outside, heat haze blurs the streets.",
            "Today is 112F. This city gives you seven days."
        });

        yield return RunFallbackConversation(ui, "MAYA", new[]
        {
            "Easy, Mayor. Sit up slowly.",
            "You crossed worlds last night. You now lead Heatwave City.",
            "This is a nonviolent crisis response game: policy, coordination, and fairness.",
            "Meet Daniel, Mrs. Alvarez, Jae, Kai, and Rowan first.",
            "Then return to me to begin Day 1."
        });

        TrySetBoolVariable("$met_maya", true);
    }

    bool TryStartOpeningDialogue()
    {
        return TryStartNode(startNode, allowFallback: false);
    }

    void InitializeCoreWeekVariables()
    {
        if (dialogueRunner == null) return;
        var storage = dialogueRunner.VariableStorage;
        if (storage == null) return;

        EnsureFloat(storage, "$heat_safety", 45f);
        EnsureFloat(storage, "$community_trust", 40f);
        EnsureFloat(storage, "$infrastructure_stability", 42f);
        EnsureFloat(storage, "$vulnerable_risk", 68f);
        EnsureFloat(storage, "$budget_reserve", 100f);

        EnsureInt(storage, "$current_day", 1);
        EnsureInt(storage, "$days_remaining", Mathf.Max(0, maxDays - 1));

        EnsureBool(storage, "$heatwave_week_survived", false);
        EnsureBool(storage, "$heatwave_week_failed", false);
        EnsureBool(storage, "$day1_started", false);
        EnsureBool(storage, "$quest_petition_done", false);
        EnsureBool(storage, "$quest_grid_done", false);
        EnsureBool(storage, "$quest_buddy_done", false);
        EnsureBool(storage, "$quest_transit_done", false);
        EnsureBool(storage, "$quest_shade_done", false);
        EnsureBool(storage, "$quest_rowan_done", false);
        EnsureBool(storage, "$quest_final_done", false);
    }

    static void EnsureFloat(VariableStorageBehaviour storage, string key, float fallback)
    {
        if (storage == null || string.IsNullOrWhiteSpace(key)) return;
        if (!storage.TryGetValue<float>(key, out _))
        {
            storage.SetValue(key, fallback);
        }
    }

    static void EnsureInt(VariableStorageBehaviour storage, string key, int fallback)
    {
        if (storage == null || string.IsNullOrWhiteSpace(key)) return;
        if (!storage.TryGetValue<float>(key, out _))
        {
            storage.SetValue(key, fallback);
        }
    }

    static void EnsureBool(VariableStorageBehaviour storage, string key, bool fallback)
    {
        if (storage == null || string.IsNullOrWhiteSpace(key)) return;
        if (!storage.TryGetValue<bool>(key, out _))
        {
            storage.SetValue(key, fallback);
        }
    }

    static IEnumerator RunFallbackConversation(HeatwaveNpcDialogueUI ui, string speaker, string[] lines)
    {
        if (ui == null || lines == null || lines.Length == 0) yield break;

        while (ui.IsConversationOpen)
        {
            yield return null;
        }

        ui.StartConversation(speaker, lines);
        while (ui.IsConversationOpen)
        {
            yield return null;
        }
    }

    string ResolveStartNode(YarnProject project, string requestedNode, bool allowFallback)
    {
        if (!TryGetNodeNames(project, out var nodeNames) || nodeNames.Length == 0)
        {
            return null;
        }

        if (ContainsNode(nodeNames, requestedNode)) return requestedNode;
        if (!allowFallback) return null;
        if (ContainsNode(nodeNames, "C1_BOOTSTRAP")) return "C1_BOOTSTRAP";
        if (ContainsNode(nodeNames, startNode)) return startNode;
        if (ContainsNode(nodeNames, "C1_START")) return "C1_START";

        return nodeNames[0];
    }

    static bool ContainsNode(string[] nodeNames, string target)
    {
        if (string.IsNullOrWhiteSpace(target) || nodeNames == null) return false;
        for (int i = 0; i < nodeNames.Length; i++)
        {
            if (nodeNames[i] == target) return true;
        }

        return false;
    }

    static bool TryGetNodeNames(YarnProject project, out string[] nodeNames)
    {
        nodeNames = System.Array.Empty<string>();
        if (project == null) return false;

        try
        {
            nodeNames = project.NodeNames;
            return nodeNames != null;
        }
        catch
        {
            return false;
        }
    }

    string[] CompleteMayaTask(VariableStorageBehaviour storage)
    {
        if (!GetBool(storage, "$maya_assigned")) return new[] { "No active Maya assignment.", "Talk to Maya first in Library Block." };
        if (GetBool(storage, "$quest_petition_done")) return new[] { "Cooling center rollout already verified." };

        int choice = Mathf.RoundToInt(GetFloat(storage, "$maya_choice", 0f));
        if (choice <= 0) return new[] { "Maya has no confirmed policy from you yet." };

        switch (choice)
        {
            case 1:
                AddFloat(storage, "$heat_safety", 10f);
                AddFloat(storage, "$community_trust", 8f);
                AddFloat(storage, "$infrastructure_stability", -5f);
                AddFloat(storage, "$budget_reserve", -15f);
                break;
            case 2:
                AddFloat(storage, "$community_trust", -8f);
                AddFloat(storage, "$vulnerable_risk", 7f);
                break;
            case 3:
                AddFloat(storage, "$heat_safety", 5f);
                AddFloat(storage, "$community_trust", 3f);
                AddFloat(storage, "$budget_reserve", -10f);
                break;
        }

        storage.SetValue("$quest_petition_done", true);
        return AdvanceDay(storage,
            "You inspected the cooling hall in person.",
            "Residents recognized your policy as real action.");
    }

    string[] CompleteDanielTask(VariableStorageBehaviour storage)
    {
        if (!GetBool(storage, "$daniel_assigned")) return new[] { "No active Daniel assignment.", "Talk to Daniel first in South District." };
        if (GetBool(storage, "$quest_grid_done")) return new[] { "Grid inspection already completed." };

        int choice = Mathf.RoundToInt(GetFloat(storage, "$daniel_choice", 0f));
        if (choice <= 0) return new[] { "Daniel still needs a confirmed plan." };

        switch (choice)
        {
            case 1:
                AddFloat(storage, "$infrastructure_stability", 12f);
                AddFloat(storage, "$community_trust", -6f);
                break;
            case 2:
                AddFloat(storage, "$infrastructure_stability", 9f);
                AddFloat(storage, "$budget_reserve", -18f);
                AddFloat(storage, "$community_trust", 4f);
                break;
            case 3:
                AddFloat(storage, "$infrastructure_stability", -14f);
                AddFloat(storage, "$vulnerable_risk", 9f);
                break;
        }

        storage.SetValue("$quest_grid_done", true);
        return AdvanceDay(storage,
            "You walked the transformer blocks with Daniel.",
            "The grid policy is now active citywide.");
    }

    string[] CompleteAlvarezTask(VariableStorageBehaviour storage)
    {
        if (!GetBool(storage, "$alvarez_assigned")) return new[] { "No active resident-care assignment.", "Talk to Mrs. Alvarez first in Old Quarter." };
        if (GetBool(storage, "$quest_buddy_done")) return new[] { "Neighborhood check route already completed." };

        int choice = Mathf.RoundToInt(GetFloat(storage, "$alvarez_choice", 0f));
        if (choice <= 0) return new[] { "No resident-care plan confirmed yet." };

        switch (choice)
        {
            case 1:
                AddFloat(storage, "$heat_safety", 9f);
                AddFloat(storage, "$community_trust", 9f);
                AddFloat(storage, "$budget_reserve", -11f);
                AddFloat(storage, "$vulnerable_risk", -10f);
                break;
            case 2:
                AddFloat(storage, "$heat_safety", 3f);
                AddFloat(storage, "$community_trust", -2f);
                break;
            case 3:
                AddFloat(storage, "$community_trust", -8f);
                AddFloat(storage, "$vulnerable_risk", 8f);
                break;
        }

        storage.SetValue("$quest_buddy_done", true);
        return AdvanceDay(storage,
            "You visited apartments and checked heat exposure directly.",
            "Resident-care policy is now verified on the ground.");
    }

    string[] CompleteTransitTask(VariableStorageBehaviour storage)
    {
        if (currentDay < 3)
        {
            return new[] { "Transit operations open from Day 3.", "Focus on neighborhood safety first." };
        }

        if (!GetBool(storage, "$transit_assigned")) return new[] { "No active transit assignment.", "Talk to Station Worker Jae first." };
        if (GetBool(storage, "$quest_transit_done")) return new[] { "Transit heat mitigation already completed." };

        int choice = Mathf.RoundToInt(GetFloat(storage, "$transit_choice", 0f));
        if (choice <= 0) return new[] { "No transit plan selected yet." };

        switch (choice)
        {
            case 1:
                AddFloat(storage, "$infrastructure_stability", 8f);
                AddFloat(storage, "$heat_safety", 4f);
                break;
            case 2:
                AddFloat(storage, "$heat_safety", 5f);
                AddFloat(storage, "$budget_reserve", -8f);
                break;
            case 3:
                AddFloat(storage, "$community_trust", 2f);
                AddFloat(storage, "$infrastructure_stability", 2f);
                break;
        }

        storage.SetValue("$quest_transit_done", true);
        return AdvanceDay(storage,
            "You audited transit platforms at noon peak heat.",
            "Transit mitigation package is now deployed.");
    }

    string[] CompleteKaiTask(VariableStorageBehaviour storage)
    {
        if (currentDay < 4)
        {
            return new[] { "Urban redesign opens from Day 4.", "Stabilize early emergencies first." };
        }

        if (!GetBool(storage, "$kai_assigned")) return new[] { "No active shade assignment.", "Talk to Kai first at Cooling Center." };
        if (GetBool(storage, "$quest_shade_done")) return new[] { "Shade strategy site work already completed." };

        int choice = Mathf.RoundToInt(GetFloat(storage, "$kai_choice", 0f));
        if (choice <= 0) return new[] { "Kai still needs your final plan choice." };

        switch (choice)
        {
            case 1:
                AddFloat(storage, "$heat_safety", 12f);
                AddFloat(storage, "$community_trust", 6f);
                AddFloat(storage, "$infrastructure_stability", 4f);
                AddFloat(storage, "$budget_reserve", -14f);
                break;
            case 2:
                AddFloat(storage, "$heat_safety", 7f);
                AddFloat(storage, "$infrastructure_stability", -7f);
                AddFloat(storage, "$budget_reserve", -12f);
                break;
            case 3:
                AddFloat(storage, "$heat_safety", 9f);
                AddFloat(storage, "$infrastructure_stability", -1f);
                AddFloat(storage, "$community_trust", 3f);
                AddFloat(storage, "$budget_reserve", -10f);
                break;
        }

        storage.SetValue("$quest_shade_done", true);
        return AdvanceDay(storage,
            "You walked heat-map corridors with Kai.",
            "Shade and cooling-corridor works are now approved.");
    }

    string[] CompleteRowanTask(VariableStorageBehaviour storage)
    {
        if (currentDay < 2)
        {
            return new[] { "Citywide communication audit opens from Day 2.", "Finish your first district response first." };
        }

        if (!GetBool(storage, "$rowan_assigned")) return new[] { "No active Rowan assignment.", "Talk to City Clerk Rowan first at Mayor Plaza." };
        if (GetBool(storage, "$quest_rowan_done")) return new[] { "City communication audit already completed." };

        int choice = Mathf.RoundToInt(GetFloat(storage, "$rowan_choice", 0f));
        if (choice <= 0) return new[] { "Rowan still needs your final communication plan choice." };

        switch (choice)
        {
            case 1:
                AddFloat(storage, "$community_trust", 10f);
                AddFloat(storage, "$vulnerable_risk", -7f);
                AddFloat(storage, "$budget_reserve", -8f);
                break;
            case 2:
                AddFloat(storage, "$community_trust", 3f);
                AddFloat(storage, "$vulnerable_risk", -2f);
                break;
            case 3:
                AddFloat(storage, "$community_trust", -9f);
                AddFloat(storage, "$vulnerable_risk", 6f);
                break;
        }

        storage.SetValue("$quest_rowan_done", true);
        return AdvanceDay(storage,
            "You reviewed city heat alerts, multilingual coverage, and outreach blind spots.",
            "City communication protocol is now deployed.");
    }

    string[] CompleteFinalAudit(VariableStorageBehaviour storage)
    {
        if (GetBool(storage, "$quest_final_done"))
        {
            return new[] { "Final report already submitted.", BuildDayWarning(storage) };
        }

        bool coreDone = CoreTasksComplete(storage);

        if (!coreDone)
        {
            return new[]
            {
                "Final audit is locked.",
                "Complete all six field tasks before submitting the Day 7 report."
            };
        }

        storage.SetValue("$quest_final_done", true);
        EvaluateFinalStatus(storage);

        bool survived = GetBool(storage, "$heatwave_week_survived");
        return new[]
        {
            "You submitted the integrated heatwave response report.",
            survived
                ? "Final result: PASS. Heatwave City stabilized this week."
                : "Final result: FAIL. Targets were not met before the deadline."
        };
    }

    string[] AdvanceDay(VariableStorageBehaviour storage, string lineA, string lineB)
    {
        // Ongoing heatwave pressure each day.
        AddFloat(storage, "$heat_safety", -2f);
        AddFloat(storage, "$vulnerable_risk", 3f);
        AddFloat(storage, "$infrastructure_stability", -1f);

        currentDay = Mathf.Min(maxDays, currentDay + 1);
        storage.SetValue("$current_day", currentDay);
        storage.SetValue("$days_remaining", Mathf.Max(0, maxDays - currentDay));

        EvaluateFinalStatus(storage);

        return new[]
        {
            lineA,
            lineB,
            $"Day advanced to {currentDay}/{maxDays}.",
            BuildDayWarning(storage)
        };
    }

    void EvaluateFinalStatus(VariableStorageBehaviour storage)
    {
        if (finalResultEvaluated) return;

        bool coreDone = CoreTasksComplete(storage);
        bool finalSubmitted = GetBool(storage, "$quest_final_done");

        bool ranOutOfDays = currentDay >= maxDays;
        if (!finalSubmitted)
        {
            // On deadline day, fail only if core tasks are still incomplete.
            if (!(ranOutOfDays && !coreDone))
            {
                return;
            }
        }

        float heat = GetFloat(storage, "$heat_safety", 0f);
        float trust = GetFloat(storage, "$community_trust", 0f);
        float infra = GetFloat(storage, "$infrastructure_stability", 0f);
        float vulnerable = GetFloat(storage, "$vulnerable_risk", 100f);

        bool success = heat >= 65f && trust >= 55f && infra >= 55f && vulnerable <= 50f && coreDone;
        storage.SetValue("$heatwave_week_survived", success);
        storage.SetValue("$heatwave_week_failed", !success);
        finalResultEvaluated = true;
    }

    string BuildDayWarning(VariableStorageBehaviour storage)
    {
        if (currentDay >= maxDays && finalResultEvaluated)
        {
            bool survived = GetBool(storage, "$heatwave_week_survived");
            return survived
                ? "Final week report: City response is stable."
                : "Final week report: FAIL. City response missed critical targets.";
        }

        if (currentDay >= maxDays && !finalResultEvaluated)
        {
            return "Day 7 reached. Submit Final Audit at Mayor Plaza now.";
        }

        int daysLeft = Mathf.Max(0, maxDays - currentDay);
        return $"Days remaining: {daysLeft}.";
    }

    static float GetFloat(VariableStorageBehaviour storage, string variableName, float defaultValue)
    {
        if (storage != null && storage.TryGetValue<float>(variableName, out var value))
        {
            return value;
        }

        return defaultValue;
    }

    static bool GetBool(VariableStorageBehaviour storage, string variableName)
    {
        if (storage != null && storage.TryGetValue<bool>(variableName, out var value))
        {
            return value;
        }

        return false;
    }

    static void AddFloat(VariableStorageBehaviour storage, string variableName, float delta)
    {
        float current = GetFloat(storage, variableName, 0f);
        storage.SetValue(variableName, current + delta);
    }

    static string ReadFloat(VariableStorageBehaviour storage, string variableName)
    {
        if (storage != null && storage.TryGetValue<float>(variableName, out var value))
        {
            return Mathf.RoundToInt(value).ToString();
        }

        return "--";
    }

    static string Flag(bool done)
    {
        return done ? "DONE" : "OPEN";
    }

    bool IsWeekFinalized(VariableStorageBehaviour storage)
    {
        if (storage == null) return false;
        return finalResultEvaluated && GetBool(storage, "$quest_final_done");
    }

    bool IsWeekFailed(VariableStorageBehaviour storage)
    {
        if (storage == null) return false;
        if (!finalResultEvaluated) return false;
        return !GetBool(storage, "$heatwave_week_survived");
    }

    public void GetTaskSiteState(
        string taskKey,
        out bool visible,
        out bool interactable,
        out bool completed,
        out string hint)
    {
        visible = false;
        interactable = false;
        completed = false;
        hint = "No linked objective.";

        if (dialogueRunner == null || dialogueRunner.VariableStorage == null)
        {
            hint = "System not ready.";
            return;
        }

        var storage = dialogueRunner.VariableStorage;
        if (!IsDay1Started(storage))
        {
            hint = "Orientation phase: meet all NPCs first.";
            return;
        }

        bool day3 = currentDay >= 3;
        bool day4 = currentDay >= 4;
        bool coreDone = CoreTasksComplete(storage);
        bool weekFailed = IsWeekFailed(storage);
        bool weekFinalized = IsWeekFinalized(storage);

        switch (taskKey)
        {
            case "TASK_MAYA":
                completed = GetBool(storage, "$quest_petition_done");
                visible = GetBool(storage, "$maya_assigned") || completed;
                interactable = GetBool(storage, "$maya_assigned") && !completed;
                hint = completed ? "Cooling hall check already done." : "Talk to Maya first in Library Block.";
                break;

            case "TASK_DANIEL":
                completed = GetBool(storage, "$quest_grid_done");
                visible = GetBool(storage, "$daniel_assigned") || completed;
                interactable = GetBool(storage, "$daniel_assigned") && !completed;
                hint = completed ? "Grid audit already done." : "Talk to Daniel first in South District.";
                break;

            case "TASK_ALVAREZ":
                completed = GetBool(storage, "$quest_buddy_done");
                visible = GetBool(storage, "$alvarez_assigned") || completed;
                interactable = GetBool(storage, "$alvarez_assigned") && !completed;
                hint = completed ? "Resident home visit already done." : "Talk to Mrs. Alvarez first in Old Quarter.";
                break;

            case "TASK_TRANSIT":
                completed = GetBool(storage, "$quest_transit_done");
                visible = day3 && (GetBool(storage, "$transit_assigned") || completed);
                interactable = day3 && GetBool(storage, "$transit_assigned") && !completed;
                hint = completed
                    ? "Transit survey already done."
                    : (day3 ? "Talk to Jae first at Transit Hub." : "Opens on Day 3.");
                break;

            case "TASK_KAI":
                completed = GetBool(storage, "$quest_shade_done");
                visible = day4 && (GetBool(storage, "$kai_assigned") || completed);
                interactable = day4 && GetBool(storage, "$kai_assigned") && !completed;
                hint = completed
                    ? "Shade corridor survey already done."
                    : (day4 ? "Talk to Kai first at Cooling Center." : "Opens on Day 4.");
                break;

            case "TASK_ROWAN":
                completed = GetBool(storage, "$quest_rowan_done");
                visible = (currentDay >= 2) && (GetBool(storage, "$rowan_assigned") || completed);
                interactable = (currentDay >= 2) && GetBool(storage, "$rowan_assigned") && !completed;
                hint = completed
                    ? "City communication audit already done."
                    : (currentDay >= 2 ? "Talk to Rowan first at Mayor Plaza." : "Opens on Day 2.");
                break;

            case "TASK_FINAL":
                completed = GetBool(storage, "$quest_final_done");
                visible = completed || coreDone || currentDay >= 7;
                interactable = coreDone && !completed;
                hint = completed
                    ? "Final audit already submitted."
                    : (coreDone ? "Submit final report here." : "Complete all six field tasks first.");
                break;
        }

        if (weekFailed)
        {
            interactable = false;
            hint = "Day 7 deadline passed. This run failed; targets were not met.";
            return;
        }

        if (weekFinalized)
        {
            interactable = false;
            hint = "Week finalized. Start a new run to change outcomes.";
        }
    }

    bool CoreTasksComplete(VariableStorageBehaviour storage)
    {
        return
            GetBool(storage, "$quest_petition_done") &&
            GetBool(storage, "$quest_grid_done") &&
            GetBool(storage, "$quest_buddy_done") &&
            GetBool(storage, "$quest_transit_done") &&
            GetBool(storage, "$quest_shade_done") &&
            GetBool(storage, "$quest_rowan_done");
    }

    static bool IsDay1Started(VariableStorageBehaviour storage)
    {
        return GetBool(storage, "$day1_started");
    }

    static bool AreAllIntroNpcsMet(VariableStorageBehaviour storage)
    {
        return
            GetBool(storage, "$met_maya") &&
            GetBool(storage, "$met_daniel") &&
            GetBool(storage, "$met_alvarez") &&
            GetBool(storage, "$met_jae") &&
            GetBool(storage, "$met_kai") &&
            GetBool(storage, "$met_rowan");
    }

    string BuildRoomObjectiveText(VariableStorageBehaviour storage)
    {
        bool day1Started = IsDay1Started(storage);
        bool petitionDone = GetBool(storage, "$quest_petition_done");
        bool gridDone = GetBool(storage, "$quest_grid_done");
        bool buddyDone = GetBool(storage, "$quest_buddy_done");
        bool transitDone = GetBool(storage, "$quest_transit_done");
        bool shadeDone = GetBool(storage, "$quest_shade_done");
        bool rowanDone = GetBool(storage, "$quest_rowan_done");
        bool survived = GetBool(storage, "$heatwave_week_survived");

        int introMetCount =
            (GetBool(storage, "$met_maya") ? 1 : 0) +
            (GetBool(storage, "$met_daniel") ? 1 : 0) +
            (GetBool(storage, "$met_alvarez") ? 1 : 0) +
            (GetBool(storage, "$met_jae") ? 1 : 0) +
            (GetBool(storage, "$met_kai") ? 1 : 0) +
            (GetBool(storage, "$met_rowan") ? 1 : 0);

        if (!day1Started)
        {
            string introStep = GetNextStepGuide(storage);
            return
                "ORIENTATION\n" +
                "You just woke up as mayor of Heatwave City.\n" +
                $"Leaders met: {introMetCount}/6\n\n" +
                "REQUIRED BEFORE DAY 1\n" +
                "Talk to Maya, Daniel, Mrs. Alvarez, Jae, Kai, and Rowan.\n" +
                "Then return to Maya to officially begin Day 1.\n\n" +
                $"NEXT STEP\n{introStep}";
        }

        int completedCount =
            (petitionDone ? 1 : 0) +
            (gridDone ? 1 : 0) +
            (buddyDone ? 1 : 0) +
            (transitDone ? 1 : 0) +
            (shadeDone ? 1 : 0) +
            (rowanDone ? 1 : 0);
        string nextStep = GetNextStepGuide(storage);
        string winRule = "Day 7 win rule: Heat>=65, Trust>=55, Infra>=55, Vulnerable<=50, all 6 tasks DONE.";
        string finalState;
        if (currentDay < maxDays)
        {
            finalState = $"Days remaining: {Mathf.Max(0, maxDays - currentDay)}";
        }
        else if (!finalResultEvaluated)
        {
            finalState = "DAY 7: Submit Final Audit at Mayor Plaza.";
        }
        else
        {
            finalState = survived
                ? "FINAL RESULT: PASS"
                : "FINAL RESULT: FAIL (Day 7 deadline missed targets)";
        }

        return
            "DAY PLAN\n" +
            "Stabilize Heatwave City before Day 7.\n" +
            $"Field Tasks: {completedCount}/6\n" +
            winRule + "\n\n" +
            "TASK STATUS\n" +
            $"Maya (Cooling Petition): {Flag(petitionDone)}\n" +
            $"Daniel (Power Grid): {Flag(gridDone)}\n" +
            $"Mrs. Alvarez (Buddy System): {Flag(buddyDone)}\n" +
            $"Jae (Transit Heat Plan): {Flag(transitDone)}\n" +
            $"Kai (Shade Plan): {Flag(shadeDone)}\n" +
            $"Rowan (City Alert Audit): {Flag(rowanDone)}\n\n" +
            $"NEXT STEP\n{nextStep}\n\n" +
            finalState;
    }

    string BuildLiveGuidance(VariableStorageBehaviour storage)
    {
        if (TryGetCurrentObjective(out _, out var objectiveText))
        {
            return $"Current Objective: {objectiveText}";
        }

        return objectiveText;
    }

    string GetNextStepGuide(VariableStorageBehaviour storage)
    {
        if (TryGetCurrentObjective(out var objectiveCode, out var objectiveText))
        {
            if (objectiveCode.StartsWith("NPC_"))
            {
                return $"{objectiveText}. Pick a policy first.";
            }

            if (objectiveCode.StartsWith("TASK_"))
            {
                return $"{objectiveText}. Inspect on site for reward.";
            }

            return objectiveText;
        }

        return objectiveText;
    }

    public bool TryGetCurrentObjective(out string objectiveCode, out string objectiveText)
    {
        objectiveCode = string.Empty;
        objectiveText = "Explore freely.";

        if (dialogueRunner == null || dialogueRunner.VariableStorage == null)
        {
            objectiveText = "System is loading...";
            return false;
        }

        var storage = dialogueRunner.VariableStorage;
        bool day1Started = IsDay1Started(storage);

        if (!day1Started)
        {
            bool metMaya = GetBool(storage, "$met_maya");
            bool metDaniel = GetBool(storage, "$met_daniel");
            bool metAlvarez = GetBool(storage, "$met_alvarez");
            bool metJae = GetBool(storage, "$met_jae");
            bool metKai = GetBool(storage, "$met_kai");
            bool metRowan = GetBool(storage, "$met_rowan");

            if (!metMaya)
            {
                objectiveCode = "NPC_MAYA";
                objectiveText = "Talk to Maya in Library Block";
                return true;
            }
            if (!metDaniel)
            {
                objectiveCode = "NPC_DANIEL";
                objectiveText = "Talk to Daniel in South District";
                return true;
            }
            if (!metAlvarez)
            {
                objectiveCode = "NPC_ALVAREZ";
                objectiveText = "Talk to Mrs. Alvarez in Old Quarter";
                return true;
            }
            if (!metJae)
            {
                objectiveCode = "NPC_JAE";
                objectiveText = "Talk to Jae in Transit Hub";
                return true;
            }
            if (!metKai)
            {
                objectiveCode = "NPC_KAI";
                objectiveText = "Talk to Kai in Cooling Center";
                return true;
            }
            if (!metRowan)
            {
                objectiveCode = "NPC_ROWAN";
                objectiveText = "Talk to Rowan in Mayor Plaza";
                return true;
            }

            if (AreAllIntroNpcsMet(storage))
            {
                objectiveCode = "NPC_MAYA";
                objectiveText = "Return to Maya to begin Day 1";
                return true;
            }

            objectiveText = "Complete orientation by meeting all city leaders.";
            return false;
        }

        bool petitionDone = GetBool(storage, "$quest_petition_done");
        bool gridDone = GetBool(storage, "$quest_grid_done");
        bool buddyDone = GetBool(storage, "$quest_buddy_done");
        bool transitDone = GetBool(storage, "$quest_transit_done");
        bool shadeDone = GetBool(storage, "$quest_shade_done");
        bool rowanDone = GetBool(storage, "$quest_rowan_done");

        bool mayaAssigned = GetBool(storage, "$maya_assigned");
        bool danielAssigned = GetBool(storage, "$daniel_assigned");
        bool alvarezAssigned = GetBool(storage, "$alvarez_assigned");
        bool transitAssigned = GetBool(storage, "$transit_assigned");
        bool kaiAssigned = GetBool(storage, "$kai_assigned");

        if (!mayaAssigned)
        {
            objectiveCode = "NPC_MAYA";
            objectiveText = "Go to Library Block and talk to Maya";
            return true;
        }
        if (!petitionDone)
        {
            objectiveCode = "TASK_MAYA";
            objectiveText = "Inspect Cooling Hall Check in Library Block";
            return true;
        }

        if (!danielAssigned)
        {
            objectiveCode = "NPC_DANIEL";
            objectiveText = "Go to South District and talk to Daniel";
            return true;
        }
        if (!gridDone)
        {
            objectiveCode = "TASK_DANIEL";
            objectiveText = "Inspect Transformer Block Audit in South District";
            return true;
        }

        if (!alvarezAssigned)
        {
            objectiveCode = "NPC_ALVAREZ";
            objectiveText = "Go to Old Quarter and talk to Mrs. Alvarez";
            return true;
        }
        if (!buddyDone)
        {
            objectiveCode = "TASK_ALVAREZ";
            objectiveText = "Inspect Resident Home Visit in Old Quarter";
            return true;
        }

        if (currentDay < 3)
        {
            objectiveText = "Transit Hub mission unlocks on Day 3";
            return false;
        }

        if (!transitAssigned)
        {
            objectiveCode = "NPC_JAE";
            objectiveText = "Go to Transit Hub and talk to Jae";
            return true;
        }
        if (!transitDone)
        {
            objectiveCode = "TASK_TRANSIT";
            objectiveText = "Inspect Platform Heat Survey in Transit Hub";
            return true;
        }

        if (currentDay < 4)
        {
            objectiveText = "Cooling Center mission unlocks on Day 4";
            return false;
        }

        if (!kaiAssigned)
        {
            objectiveCode = "NPC_KAI";
            objectiveText = "Go to Cooling Center and talk to Kai";
            return true;
        }
        if (!shadeDone)
        {
            objectiveCode = "TASK_KAI";
            objectiveText = "Inspect Shade Corridor Survey in Cooling Center";
            return true;
        }

        bool rowanAssigned = GetBool(storage, "$rowan_assigned");
        if (!rowanAssigned)
        {
            objectiveCode = "NPC_ROWAN";
            objectiveText = "Go to Mayor Plaza and talk to Rowan";
            return true;
        }
        if (!rowanDone)
        {
            objectiveCode = "TASK_ROWAN";
            objectiveText = "Inspect City Alert Audit in Mayor Plaza";
            return true;
        }

        bool coreDone = CoreTasksComplete(storage);
        bool finalSubmitted = GetBool(storage, "$quest_final_done");
        if (coreDone && !finalSubmitted)
        {
            objectiveCode = "TASK_FINAL";
            objectiveText = "Go to Mayor Plaza and submit Final Audit";
            return true;
        }

        if (finalResultEvaluated)
        {
            bool survived = GetBool(storage, "$heatwave_week_survived");
            objectiveText = survived
                ? "Week report finalized: PASS. Free exploration available."
                : "Week report finalized: FAIL. Restart to retry the 7-day challenge.";
            return false;
        }

        objectiveText = "Complete remaining field tasks.";
        return false;
    }
}
