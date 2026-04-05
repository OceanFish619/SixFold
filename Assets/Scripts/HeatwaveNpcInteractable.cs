using UnityEngine;

public class HeatwaveNpcInteractable : MonoBehaviour
{
    public string npcName = "Resident";
    [Header("Yarn Routing")]
    [Tooltip("Primary storyline node for this room NPC.")]
    public string primaryYarnNode;
    [Tooltip("Side/follow-up node used after completion variable becomes true.")]
    public string secondaryYarnNode;
    [Tooltip("Completion variable name, e.g. $quest_petition_done.")]
    public string completionVariable;
    [Tooltip("Optional gate variable required before this NPC can start Yarn routing.")]
    public string unlockVariable;
    [Tooltip("Earliest day this NPC can be fully engaged.")]
    public int minDay = 1;
    [TextArea(1, 3)]
    public string lockedLine = "This district is not ready yet. Check another room first.";
    [TextArea(1, 3)]
    public string dayLockedLine = "This issue opens on a later day. Check your objective panel.";
    [TextArea(2, 5)]
    public string[] lines;
    [Header("Orientation Intro")]
    [Tooltip("Set when this NPC is first met during orientation, e.g. $met_maya.")]
    public string firstMeetVariable;
    [TextArea(2, 6)]
    public string[] firstMeetLines;
    public float interactionDistance = 1.45f;

    Transform player;

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

        float distance = Vector2.Distance(player.position, transform.position);
        bool inRange = distance <= interactionDistance;
        var gameController = HeatwaveCityGameController.Instance;

        if (gameController != null && gameController.IsMainDialogueRunning)
        {
            HeatwaveNpcDialogueUI.Instance.ClearPrompt(this);
            return;
        }

        if (inRange)
        {
            HeatwaveNpcDialogueUI.Instance.SetPrompt(this, $"Press E to talk to {npcName}");

            if (!HeatwaveNpcDialogueUI.Instance.IsConversationOpen &&
                Input.GetKeyDown(KeyCode.E))
            {
                if (gameController != null)
                {
                    bool day1Started = gameController.IsVariableTrue("$day1_started");
                    bool firstMeetPending =
                        !day1Started &&
                        !string.IsNullOrWhiteSpace(firstMeetVariable) &&
                        !gameController.IsVariableTrue(firstMeetVariable);

                    if (firstMeetPending)
                    {
                        gameController.TrySetBoolVariable(firstMeetVariable, true);
                        HeatwaveNpcDialogueUI.Instance.StartConversation(npcName, BuildFirstMeetLines());
                        return;
                    }

                    if (day1Started && gameController.CurrentDay < Mathf.Max(1, minDay))
                    {
                        HeatwaveNpcDialogueUI.Instance.StartConversation(npcName, new[] { dayLockedLine });
                        return;
                    }

                    if (!string.IsNullOrWhiteSpace(unlockVariable) && !gameController.IsVariableTrue(unlockVariable))
                    {
                        HeatwaveNpcDialogueUI.Instance.StartConversation(npcName, new[] { lockedLine });
                        return;
                    }

                    string routeNode = ResolveRouteNode(gameController);
                    if (!string.IsNullOrWhiteSpace(routeNode))
                    {
                        bool started = gameController.TryStartNode(routeNode, allowFallback: false);
                        if (started)
                        {
                            HeatwaveNpcDialogueUI.Instance.ClearPrompt(this);
                            return;
                        }
                    }
                }

                HeatwaveNpcDialogueUI.Instance.StartConversation(npcName, BuildFallbackLines());
            }
        }
        else
        {
            HeatwaveNpcDialogueUI.Instance.ClearPrompt(this);
        }
    }

    string[] BuildFallbackLines()
    {
        if (lines != null && lines.Length > 0)
        {
            return lines;
        }

        return new[]
        {
            $"Hi Mayor. - {npcName}",
            "I have no report linked yet, but I am here."
        };
    }

    string[] BuildFirstMeetLines()
    {
        if (firstMeetLines != null && firstMeetLines.Length > 0)
        {
            return firstMeetLines;
        }

        return BuildFallbackLines();
    }

    string ResolveRouteNode(HeatwaveCityGameController gameController)
    {
        bool completed = !string.IsNullOrWhiteSpace(completionVariable) && gameController.IsVariableTrue(completionVariable);
        if (completed && !string.IsNullOrWhiteSpace(secondaryYarnNode))
        {
            return secondaryYarnNode;
        }

        if (!string.IsNullOrWhiteSpace(primaryYarnNode))
        {
            return primaryYarnNode;
        }

        if (!string.IsNullOrWhiteSpace(secondaryYarnNode))
        {
            return secondaryYarnNode;
        }

        return string.Empty;
    }
}
