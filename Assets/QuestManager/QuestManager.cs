using System.Collections.Generic;
using UnityEngine;

public enum QuestType { Main, Side }

[System.Serializable]
public class Quest
{
    public string questName;
    public QuestType type;
    [TextArea] public string description;
    public bool completed;

    // Drag objects with QuestCompleteTrigger here
    public List<QuestCompleteTrigger> triggers = new List<QuestCompleteTrigger>();
}

public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance;

    public List<Quest> quests = new List<Quest>();

    // Drag the CloudNetworkMeter object here
    public CloudNetworkMeter meter;

    void Awake()
    {
        Instance = this;

        // Link each dragged-in trigger to its quest
        foreach (Quest q in quests)
            foreach (QuestCompleteTrigger t in q.triggers)
                if (t != null) t.questName = q.questName;

        int mainCount = quests.FindAll(q => q.type == QuestType.Main).Count;
        if (mainCount > 4) Debug.LogWarning("More than 4 Main quests! Max is 4.");
    }

    void Start()
    {
        UpdateMeter();
    }

    public void CompleteQuest(string questName)
    {
        Quest q = quests.Find(x => x.questName == questName);

        if (q == null)
        {
            Debug.LogWarning("Quest not found: " + questName);
            return;
        }

        if (q.completed) return;

        q.completed = true;
        Debug.Log("Quest completed: " + questName);

        UpdateMeter();

        if (AllMainQuestsDone())
            Debug.Log("All main quests done - NYP Cloud Network restored!");
    }

    void UpdateMeter()
    {
        if (meter == null || quests.Count == 0) return;

        int done = quests.FindAll(q => q.completed).Count;
        meter.SetValue((float)done / quests.Count);
    }

    public bool AllMainQuestsDone()
    {
        List<Quest> mains = quests.FindAll(q => q.type == QuestType.Main);
        return mains.Count > 0 && mains.TrueForAll(q => q.completed);
    }
}
