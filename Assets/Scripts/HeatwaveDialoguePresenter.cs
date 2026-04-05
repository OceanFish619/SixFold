using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Yarn.Unity;

public class HeatwaveDialoguePresenter : DialoguePresenterBase
{
    [Header("Bindings")]
    [SerializeField] GameObject dialogueRoot;
    [SerializeField] TMP_Text speakerText;
    [SerializeField] TMP_Text bodyText;
    [SerializeField] Button optionAButton;
    [SerializeField] Button optionBButton;
    [SerializeField] Button closeButton;

    [Header("Input")]
    [SerializeField] KeyCode advanceKey = KeyCode.E;

    readonly List<Button> optionButtons = new List<Button>();
    DialogueOption[] currentOptions = System.Array.Empty<DialogueOption>();
    DialogueOption selectedOption;
    Button boundCloseButton;
    bool waitingForLineAdvance;
    bool waitingForOptionSelect;

    public void AutoBindFromScene()
    {
        DialogueUIController.ApplyDialogueLayoutNow();

        if (dialogueRoot == null)
        {
            dialogueRoot = GameObject.Find("DialoguePanel");
        }

        if (dialogueRoot == null) return;

        if (speakerText == null)
        {
            var title = dialogueRoot.transform.Find("LeftDialogueBox/TitleText");
            if (title != null) speakerText = title.GetComponent<TMP_Text>();
        }

        if (bodyText == null)
        {
            var body = dialogueRoot.transform.Find("RightDialogueBox/BodyText");
            if (body != null) bodyText = body.GetComponent<TMP_Text>();
        }

        if (optionAButton == null)
        {
            var optionA = dialogueRoot.transform.Find("RightDialogueBox/OptionAButton");
            if (optionA != null) optionAButton = optionA.GetComponent<Button>();
        }

        if (optionBButton == null)
        {
            var optionB = dialogueRoot.transform.Find("RightDialogueBox/OptionBButton");
            if (optionB != null) optionBButton = optionB.GetComponent<Button>();
        }

        if (closeButton == null)
        {
            var close = dialogueRoot.transform.Find("CloseButton");
            if (close != null) closeButton = close.GetComponent<Button>();
        }

        optionButtons.Clear();
        if (optionAButton != null) optionButtons.Add(optionAButton);
        if (optionBButton != null && optionBButton != optionAButton) optionButtons.Add(optionBButton);
        BindCloseButton();
    }

    public override YarnTask OnDialogueStartedAsync()
    {
        AutoBindFromScene();

        if (dialogueRoot != null) dialogueRoot.SetActive(true);
        if (speakerText != null) speakerText.text = "MAYOR";
        if (bodyText != null) bodyText.text = string.Empty;
        HideAllOptionButtons();

        return YarnTask.CompletedTask;
    }

    public void ShowSystemMessage(string message)
    {
        AutoBindFromScene();
        if (dialogueRoot != null) dialogueRoot.SetActive(true);
        if (speakerText != null) speakerText.text = "SYSTEM";
        if (bodyText != null) bodyText.text = message;
        HideAllOptionButtons();
    }

    public override YarnTask OnDialogueCompleteAsync()
    {
        waitingForLineAdvance = false;
        waitingForOptionSelect = false;

        HideAllOptionButtons();
        if (dialogueRoot != null) dialogueRoot.SetActive(false);

        return YarnTask.CompletedTask;
    }

    void OnDisable()
    {
        if (boundCloseButton != null)
        {
            boundCloseButton.onClick.RemoveListener(OnCloseRequested);
        }
    }

    public override async YarnTask RunLineAsync(LocalizedLine line, LineCancellationToken token)
    {
        AutoBindFromScene();

        if (dialogueRoot != null && !dialogueRoot.activeSelf) dialogueRoot.SetActive(true);

        if (speakerText != null)
        {
            var name = line.CharacterName;
            speakerText.text = string.IsNullOrWhiteSpace(name) ? "CITY FEED" : name.ToUpperInvariant();
        }

        if (bodyText != null)
        {
            bodyText.text = line.TextWithoutCharacterName.Text;
        }

        HideAllOptionButtons();

        waitingForLineAdvance = true;
        while (waitingForLineAdvance && !token.IsNextContentRequested)
        {
            if (token.IsHurryUpRequested || Input.GetKeyDown(advanceKey) || Input.GetMouseButtonDown(0))
            {
                waitingForLineAdvance = false;
                break;
            }

            await YarnTask.Yield();
        }
    }

#pragma warning disable CS8632
    public override async YarnTask<DialogueOption?> RunOptionsAsync(DialogueOption[] dialogueOptions, LineCancellationToken cancellationToken)
#pragma warning restore CS8632
    {
        AutoBindFromScene();

        if (dialogueOptions == null || dialogueOptions.Length == 0)
        {
            return null;
        }

        EnsureOptionButtons(dialogueOptions.Length);
        LayoutOptionButtons(dialogueOptions.Length);
        ClearOptionCallbacks();

        currentOptions = dialogueOptions;
        selectedOption = null;
        waitingForOptionSelect = true;

        for (int i = 0; i < optionButtons.Count; i++)
        {
            var button = optionButtons[i];
            if (i < dialogueOptions.Length)
            {
                var option = dialogueOptions[i];
                button.gameObject.SetActive(true);
                button.interactable = option.IsAvailable;

                var label = button.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                {
                    label.text = option.Line.TextWithoutCharacterName.Text;
                }

                int capturedIndex = i;
                button.onClick.AddListener(() => SelectOption(capturedIndex));
            }
            else
            {
                button.gameObject.SetActive(false);
            }
        }

        while (waitingForOptionSelect && !cancellationToken.IsNextContentRequested)
        {
            await YarnTask.Yield();
        }

        ClearOptionCallbacks();
        HideAllOptionButtons();

        if (cancellationToken.IsNextContentRequested) return null;
        return selectedOption;
    }

    void SelectOption(int index)
    {
        if (!waitingForOptionSelect) return;
        if (index < 0 || index >= currentOptions.Length) return;

        selectedOption = currentOptions[index];
        waitingForOptionSelect = false;
    }

    void BindCloseButton()
    {
        if (boundCloseButton == closeButton) return;

        if (boundCloseButton != null)
        {
            boundCloseButton.onClick.RemoveListener(OnCloseRequested);
        }

        boundCloseButton = closeButton;
        if (boundCloseButton == null) return;

        boundCloseButton.onClick.RemoveListener(OnCloseRequested);
        boundCloseButton.onClick.AddListener(OnCloseRequested);
    }

    void OnCloseRequested()
    {
        waitingForLineAdvance = false;
        waitingForOptionSelect = false;
        HideAllOptionButtons();

        var runner = FindFirstObjectByType<DialogueRunner>();
        if (runner != null && runner.IsDialogueRunning)
        {
            runner.Stop().Forget();
            return;
        }

        if (dialogueRoot != null)
        {
            dialogueRoot.SetActive(false);
        }
    }

    void EnsureOptionButtons(int requiredCount)
    {
        if (optionButtons.Count == 0)
        {
            AutoBindFromScene();
        }

        if (optionButtons.Count == 0) return;

        var template = optionButtons[optionButtons.Count - 1];
        var parent = template.transform.parent;

        while (optionButtons.Count < requiredCount)
        {
            var clone = Instantiate(template, parent);
            clone.name = $"Option{optionButtons.Count + 1}Button";
            clone.gameObject.SetActive(false);
            clone.onClick.RemoveAllListeners();

            var label = clone.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = "Choice";

            optionButtons.Add(clone);
        }
    }

    void LayoutOptionButtons(int optionCount)
    {
        if (optionCount <= 0) return;

        if (optionCount == 1)
        {
            SetAnchors(optionButtons[0], 0.03f, 0.04f, 0.97f, 0.24f);
            return;
        }

        if (optionCount == 2)
        {
            SetAnchors(optionButtons[0], 0.03f, 0.04f, 0.47f, 0.25f);
            SetAnchors(optionButtons[1], 0.53f, 0.04f, 0.97f, 0.25f);
            return;
        }

        float top = 0.44f;
        float rowHeight = 0.12f;
        float gap = 0.03f;
        for (int i = 0; i < optionCount; i++)
        {
            float yMax = top - (rowHeight + gap) * i;
            float yMin = yMax - rowHeight;
            SetAnchors(optionButtons[i], 0.03f, yMin, 0.97f, yMax);
        }
    }

    static void SetAnchors(Button button, float xMin, float yMin, float xMax, float yMax)
    {
        if (button == null) return;
        var rect = button.GetComponent<RectTransform>();
        if (rect == null) return;

        rect.anchorMin = new Vector2(xMin, yMin);
        rect.anchorMax = new Vector2(xMax, yMax);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    void HideAllOptionButtons()
    {
        for (int i = 0; i < optionButtons.Count; i++)
        {
            if (optionButtons[i] != null)
            {
                optionButtons[i].gameObject.SetActive(false);
            }
        }
    }

    void ClearOptionCallbacks()
    {
        for (int i = 0; i < optionButtons.Count; i++)
        {
            if (optionButtons[i] != null)
            {
                optionButtons[i].onClick.RemoveAllListeners();
            }
        }
    }
}
