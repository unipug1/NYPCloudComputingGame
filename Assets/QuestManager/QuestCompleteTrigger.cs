using UnityEngine;

public class QuestCompleteTrigger : MonoBehaviour
{
    // Filled in automatically when you drag this object into QuestManager
    public string questName;

    // Tick this to complete the quest when the player walks into this object's trigger collider
    public bool completeOnPlayerTouch;

    // Call this from a button or when a mini-game is won
    public void Complete()
    {
        QuestManager.Instance.CompleteQuest(questName);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (completeOnPlayerTouch && other.CompareTag("Player"))
            Complete();
    }
}
