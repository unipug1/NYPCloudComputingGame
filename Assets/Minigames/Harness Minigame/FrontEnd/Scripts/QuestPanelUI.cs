using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

// Quest list on the left of the screen. Reads QuestManager.Instance.quests, one row per quest:
//   normal  = available        -> click tracks it: a path on the floor leads you there (click again to stop)
//   done    = completed (blue, crossed out)
//   locked  = greyed out until the other quests are done (e.g. Harness)
// Lives under GameSystems, so it survives scene changes and always shows current progress.
public class QuestPanelUI : MonoBehaviour
{
    [System.Serializable]
    public class QuestAction
    {
        public string questName;        // must match the name in QuestManager
        public UnityEvent onSelect;     // optional: runs INSTEAD of loading the quest's scene
    }

    [Header("Layout")]
    public RectTransform slider;        // moves left/right when collapsing (holds panel + tabs)
    public RectTransform panel;         // the quest panel image (height grows with quest count)
    public RectTransform rowContainer;
    public GameObject rowTemplate;      // inactive template row
    public GameObject collapseTab;
    public GameObject expandTab;
    public float rowSpacing = 76f;
    public float panelTopPadding = 136f;
    public float panelBottomPadding = 32f;
    public float collapsedOffset = -376f;

    [Header("Sprites")]
    public Sprite rowNormal;
    public Sprite rowDone;
    public Color lockedTint = new Color(0.55f, 0.55f, 0.6f, 1f);

    [Header("Actions")]
    public bool collapseWhenQuestStarts = true;
    public List<QuestAction> actions = new List<QuestAction>();

    readonly List<Row> rows = new List<Row>();
    float targetX, shownX;
    bool built;

    class Row { public Quest quest; public Image img; public TMP_Text label; public GameObject cursor; public Coroutine flash; }

    void Start()
    {
        shownX = slider.anchoredPosition.x;
        targetX = shownX;
        rowTemplate.SetActive(false);
        Build();
        SetCollapsed(!InMap(), true);     // open on the map, tucked away inside minigames
        if (QuestManager.Instance != null) QuestManager.Instance.OnQuestsChanged += Refresh;
        if (QuestNavigator.Instance != null) QuestNavigator.Instance.OnTrackChanged += Refresh;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static bool IsTracked(Row r) => QuestNavigator.Instance != null && QuestNavigator.Instance.TrackedQuest == r.quest.questName;

    void OnDestroy()
    {
        if (QuestManager.Instance != null) QuestManager.Instance.OnQuestsChanged -= Refresh;
        if (QuestNavigator.Instance != null) QuestNavigator.Instance.OnTrackChanged -= Refresh;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    static bool InMap() =>
        QuestNavigator.Instance == null || SceneManager.GetActiveScene().name == QuestNavigator.Instance.mapSceneName;

    void OnSceneLoaded(Scene s, LoadSceneMode m) => SetCollapsed(!InMap(), true);

    void OnEnable() { if (built) Refresh(); }

    void Build()
    {
        var qm = QuestManager.Instance;
        if (qm == null) { Debug.LogWarning("[QuestPanel] No QuestManager found."); return; }

        for (int i = 0; i < qm.quests.Count; i++)
        {
            var q = qm.quests[i];
            var go = Instantiate(rowTemplate, rowContainer);
            go.name = "Quest_" + q.questName;
            go.SetActive(true);
            var rt = (RectTransform)go.transform;
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -i * rowSpacing);

            var row = new Row
            {
                quest = q,
                img = go.GetComponent<Image>(),
                label = go.GetComponentInChildren<TMP_Text>(true),
                cursor = go.transform.Find("Cursor") ? go.transform.Find("Cursor").gameObject : null
            };
            if (row.cursor) row.cursor.SetActive(false);

            var trig = go.AddComponent<EventTrigger>();
            AddTrigger(trig, EventTriggerType.PointerEnter, () => { if (row.cursor && qm.IsUnlocked(row.quest)) row.cursor.SetActive(true); });
            AddTrigger(trig, EventTriggerType.PointerExit, () => { if (row.cursor) row.cursor.SetActive(IsTracked(row)); });

            go.GetComponent<Button>().onClick.AddListener(() => Select(row));
            rows.Add(row);
        }

        panel.sizeDelta = new Vector2(panel.sizeDelta.x, panelTopPadding + qm.quests.Count * rowSpacing + panelBottomPadding);
        built = true;
        Refresh();
    }

    static void AddTrigger(EventTrigger t, EventTriggerType type, UnityAction a)
    {
        var e = new EventTrigger.Entry { eventID = type };
        e.callback.AddListener(_ => a());
        t.triggers.Add(e);
    }

    void Select(Row row)
    {
        var qm = QuestManager.Instance;
        if (qm == null) return;

        if (!qm.IsUnlocked(row.quest)) { Flash(row, "Clear the other quests first!"); return; }

        var act = actions.Find(a => a.questName == row.quest.questName);
        if (act != null && act.onSelect != null && act.onSelect.GetPersistentEventCount() > 0)
        {
            if (collapseWhenQuestStarts) SetCollapsed(true, false);
            act.onSelect.Invoke();
            return;
        }

        // track it (path guide) - or teleport, if QuestNavigator.teleportOnSelect is on
        if (collapseWhenQuestStarts) SetCollapsed(true, false);
        if (QuestNavigator.Instance != null) QuestNavigator.Instance.GoToQuest(row.quest.questName);
    }

    void Flash(Row row, string msg)
    {
        if (row.flash != null) StopCoroutine(row.flash);
        row.flash = StartCoroutine(FlashRoutine(row, msg));
    }

    IEnumerator FlashRoutine(Row row, string msg)
    {
        row.label.text = msg;
        yield return new WaitForSecondsRealtime(1.5f);
        row.flash = null;
        Refresh();
    }

    public void Refresh()
    {
        var qm = QuestManager.Instance;
        if (qm == null) return;
        foreach (var r in rows)
        {
            bool done = r.quest.completed;
            bool locked = !qm.IsUnlocked(r.quest);
            r.img.sprite = done ? rowDone : rowNormal;
            r.img.color = locked ? lockedTint : Color.white;
            bool tracked = IsTracked(r);
            if (r.cursor) r.cursor.SetActive(tracked);      // arrow stays on the quest you're following
            if (r.flash == null)
                r.label.text = done ? "<s>" + r.quest.questName + "</s>"
                             : tracked ? "<b>" + r.quest.questName + "</b>"
                                                          : r.quest.questName;
            r.label.color = done ? new Color32(8, 47, 125, 200) : locked ? new Color32(60, 62, 75, 255) : new Color32(20, 24, 40, 255);
        }
    }

    // ---------------- collapse / expand ----------------
    public void Collapse() => SetCollapsed(true, false);
    public void Expand() => SetCollapsed(false, false);

    void SetCollapsed(bool collapsed, bool instant)
    {
        targetX = collapsed ? shownX + collapsedOffset : shownX;
        collapseTab.SetActive(!collapsed);
        expandTab.SetActive(collapsed);
        if (instant) slider.anchoredPosition = new Vector2(targetX, slider.anchoredPosition.y);
    }

    void Update()
    {
        var p = slider.anchoredPosition;
        if (!Mathf.Approximately(p.x, targetX))
            slider.anchoredPosition = new Vector2(Mathf.MoveTowards(p.x, targetX, 2400f * Time.unscaledDeltaTime), p.y);
    }
}
