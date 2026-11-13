using System;
using System.Collections.Generic;
using UnityEngine;

public enum QuestType { Main, Side }

[Serializable]
public class Quest
{
    public string questName;
    public QuestType type;
    [TextArea] public string description;
    public bool completed;

    [Tooltip("Scene to load when this quest is clicked in the quest panel (must be in Build Settings). Leave empty if not ready yet.")]
    public string sceneName;

    [Tooltip("Locked until every OTHER main quest is completed (e.g. the Harness game).")]
    public bool unlockAfterOthers;

    // Drag objects with QuestCompleteTrigger here (same scene only).
    // Mini-games in other scenes: call QuestManager.Instance.CompleteQuest("Quest Name") instead.
    public List<QuestCompleteTrigger> triggers = new List<QuestCompleteTrigger>();
}

// Lives under GameSystems, which survives scene changes (DontDestroyOnLoad via PersistentRoot).
// Progress is saved per player, so it's kept across scenes and reloads.
public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance;

    public List<Quest> quests = new List<Quest>();

    // The CloudNetworkMeter (progress bar)
    public CloudNetworkMeter meter;

    // Fired whenever a quest is completed or progress is loaded (quest panel listens to this)
    public event Action OnQuestsChanged;

    string saveKey = "nyp_oh_progress_guest";

    void Awake()
    {
        if (Instance != null && Instance != this) { enabled = false; return; }   // duplicate copy is removed by PersistentRoot
        Instance = this;

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

    // ---------------- completing ----------------
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

        Save();
        UpdateMeter();
        OnQuestsChanged?.Invoke();

        if (AllMainQuestsDone())
            Debug.Log("All main quests done - NYP Cloud Network restored!");
    }

    public bool IsCompleted(string questName)
    {
        var q = quests.Find(x => x.questName == questName);
        return q != null && q.completed;
    }

    // A quest marked "unlockAfterOthers" opens only when every other MAIN quest is done (any order)
    public bool IsUnlocked(Quest q)
    {
        if (q == null || !q.unlockAfterOthers) return true;
        return quests.TrueForAll(o => o == q || o.type != QuestType.Main || o.completed);
    }

    public bool IsUnlocked(string questName) => IsUnlocked(quests.Find(x => x.questName == questName));

    public float Progress => quests.Count == 0 ? 0f : (float)quests.FindAll(q => q.completed).Count / quests.Count;

    void UpdateMeter()
    {
        if (meter == null || quests.Count == 0) return;
        meter.SetValue(Progress);
    }

    public bool AllMainQuestsDone()
    {
        List<Quest> mains = quests.FindAll(q => q.type == QuestType.Main);
        return mains.Count > 0 && mains.TrueForAll(q => q.completed);
    }

    // ---------------- save / load (per player) ----------------
    [Serializable] class SaveData { public List<string> done = new List<string>(); }

    // Called by the front end after login / sign up / guest. playerId = email, or "guest".
    public void LoadProgress(string playerId)
    {
        saveKey = "nyp_oh_progress_" + (string.IsNullOrEmpty(playerId) ? "guest" : playerId);
        var data = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(saveKey, "{}")) ?? new SaveData();
        foreach (var q in quests) q.completed = data.done.Contains(q.questName);
        UpdateMeter();
        OnQuestsChanged?.Invoke();
    }

    public List<string> CompletedNames()
    {
        var list = new List<string>();
        foreach (var q in quests) if (q.completed) list.Add(q.questName);
        return list;
    }

    // Adds progress saved online (Firebase) on top of what this device has
    public void MergeCompleted(List<string> names)
    {
        if (names == null) return;
        bool changed = false;
        foreach (var q in quests)
            if (!q.completed && names.Contains(q.questName)) { q.completed = true; changed = true; }
        if (!changed) return;
        Save();
        UpdateMeter();
        OnQuestsChanged?.Invoke();
    }

    void Save()
    {
        var data = new SaveData { done = CompletedNames() };
        PlayerPrefs.SetString(saveKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();

        // logged-in players also save online
        if (AccountManager.LoggedIn && FirebaseService.Instance != null && FirebaseService.Instance.IsConfigured)
            FirebaseService.Instance.SaveProgress(AccountManager.Current.email, data.done);
    }

    // For testing: right-click the QuestManager component > Reset Progress
    [ContextMenu("Reset Progress")]
    public void ResetProgress()
    {
        foreach (var q in quests) q.completed = false;
        Save();
        UpdateMeter();
        OnQuestsChanged?.Invoke();
    }
}
