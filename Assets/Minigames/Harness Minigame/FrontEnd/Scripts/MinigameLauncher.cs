using UnityEngine;

// Put on the object in a map area that starts the minigame (e.g. the computer / NPC).
// Hook it up to your Interactable:  On Interact () -> MinigameLauncher.Launch
public class MinigameLauncher : MonoBehaviour
{
    [Tooltip("Must match the quest name in QuestManager, e.g. \"LLM Game\"")]
    public string questName;

    public void Launch()
    {
        if (QuestNavigator.Instance != null) QuestNavigator.Instance.OpenMinigame(questName);
        else Debug.LogWarning("[MinigameLauncher] No GameSystems in the scene.");
    }
}
