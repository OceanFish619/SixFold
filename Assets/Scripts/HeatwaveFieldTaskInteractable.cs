using TMPro;
using UnityEngine;

public class HeatwaveFieldTaskInteractable : MonoBehaviour
{
    public string taskKey = "TASK";
    public string taskName = "Field Task";
    public float interactionDistance = 1.35f;

    [Header("Visual")]
    [SerializeField] Color activeColor = new Color(0.89f, 0.72f, 0.30f, 1f);
    [SerializeField] Color doneColor = new Color(0.56f, 0.78f, 0.52f, 1f);
    [SerializeField] Color inactiveColor = new Color(0.36f, 0.34f, 0.30f, 0.24f);

    Transform player;
    SpriteRenderer markerRenderer;
    Collider2D markerCollider;
    TextMeshPro[] worldLabels;

    void Awake()
    {
        markerRenderer = GetComponent<SpriteRenderer>();
        markerCollider = GetComponent<Collider2D>();
        worldLabels = GetComponentsInChildren<TextMeshPro>(true);
    }

    void Update()
    {
        if (HeatwaveNpcDialogueUI.Instance == null)
        {
            return;
        }

        if (HeatwaveCityGameController.IsInputLocked)
        {
            HeatwaveNpcDialogueUI.Instance.ClearPrompt(this);
            return;
        }

        if (player == null)
        {
            var mover = FindFirstObjectByType<PlayerMovement>();
            if (mover != null) player = mover.transform;
            if (player == null) return;
        }

        var gameController = HeatwaveCityGameController.Instance;
        bool visible = true;
        bool interactable = true;
        bool completed = false;
        string hint = "Unavailable.";

        if (gameController != null)
        {
            gameController.GetTaskSiteState(taskKey, out visible, out interactable, out completed, out hint);
        }

        ApplyVisualState(visible, interactable, completed);
        if (!visible)
        {
            HeatwaveNpcDialogueUI.Instance.ClearPrompt(this);
            return;
        }

        if (gameController != null && gameController.IsMainDialogueRunning)
        {
            HeatwaveNpcDialogueUI.Instance.ClearPrompt(this);
            return;
        }

        float distance = Vector2.Distance(player.position, transform.position);
        bool inRange = distance <= interactionDistance;

        if (inRange)
        {
            if (completed)
            {
                HeatwaveNpcDialogueUI.Instance.SetPrompt(this, $"{taskName}: completed");
                return;
            }

            if (!interactable)
            {
                HeatwaveNpcDialogueUI.Instance.SetPrompt(this, hint);
                return;
            }

            HeatwaveNpcDialogueUI.Instance.SetPrompt(this, $"Press E to inspect {taskName}");
            if (Input.GetKeyDown(KeyCode.E) && interactable)
            {
                if (gameController == null)
                {
                    HeatwaveNpcDialogueUI.Instance.StartConversation("SYSTEM", new[] { "Game controller missing." });
                    return;
                }

                var report = gameController.CompleteFieldTask(taskKey);
                HeatwaveNpcDialogueUI.Instance.StartConversation(taskName, report);
            }
        }
        else
        {
            HeatwaveNpcDialogueUI.Instance.ClearPrompt(this);
        }
    }

    void ApplyVisualState(bool visible, bool interactable, bool completed)
    {
        if (markerRenderer != null)
        {
            markerRenderer.enabled = visible;
            markerRenderer.color = completed
                ? doneColor
                : (interactable ? activeColor : inactiveColor);
        }

        if (markerCollider != null)
        {
            markerCollider.enabled = visible;
        }

        if (worldLabels == null) return;
        for (int i = 0; i < worldLabels.Length; i++)
        {
            if (worldLabels[i] == null) continue;
            worldLabels[i].gameObject.SetActive(visible);
            worldLabels[i].color = completed
                ? new Color(0.80f, 0.96f, 0.74f, 1f)
                : (interactable ? new Color(1f, 0.89f, 0.48f, 1f) : new Color(0.82f, 0.75f, 0.64f, 0.86f));
        }
    }
}
