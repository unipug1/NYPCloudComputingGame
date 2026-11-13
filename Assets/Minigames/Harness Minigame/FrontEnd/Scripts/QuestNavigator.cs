using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

// Lives on GameSystems (survives scene changes).
//  - Quest panel click  -> track that quest: a path on the floor (PathGuide) leads the player to it
//                          (tick "Teleport On Select" to jump there instead)
//  - Minigame finished  -> QuestNavigator.FinishQuest("LLM Game") completes it and returns to the map
public class QuestNavigator : MonoBehaviour
{
    public static QuestNavigator Instance;

    [Tooltip("The scene with the map + home screen")]
    public string mapSceneName = "Main_Map";

    [Header("Area name banner")]
    public GameObject banner;
    public TMP_Text bannerText;
    public float bannerSeconds = 2.5f;

    [Header("Quest panel")]
    [Tooltip("Off: clicking a quest shows a path to walk. On: clicking a quest teleports you there.")]
    public bool teleportOnSelect = false;

    public string TrackedQuest { get; private set; }
    public event System.Action OnTrackChanged;

    string pendingTrack;       // quest to track once the map scene has loaded
    string pendingQuest;       // teleport target once the map scene has loaded
    string lastQuest;          // the quest whose minigame was opened last
    Coroutine bannerRoutine;

    void Awake()
    {
        if (Instance != null && Instance != this) { enabled = false; return; }
        Instance = this;
        SceneManager.sceneLoaded += OnSceneLoaded;
        if (banner) banner.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this) SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // ---------------- from the quest panel ----------------
    public void GoToQuest(string questName)
    {
        if (SceneManager.GetActiveScene().name == mapSceneName)
        {
            if (teleportOnSelect) TeleportTo(questName, animate: true);   // chevron wipe / fade
            else Track(TrackedQuest == questName ? null : questName);     // click again to stop tracking
        }
        else
        {
            // back to the map (next to the minigame you were in), then show the path
            if (teleportOnSelect) pendingQuest = questName;
            else { pendingQuest = lastQuest; pendingTrack = questName; }
            LoadScene(mapSceneName, "Back to the map");
        }
    }

    // show / hide the walking path to a quest (null = none)
    public void Track(string questName)
    {
        TrackedQuest = string.IsNullOrEmpty(questName) ? null : questName;
        OnTrackChanged?.Invoke();
        if (TrackedQuest == null) return;
        var area = MapArea.ForQuest(TrackedQuest);
        ShowBanner(area != null ? "Follow the path to the " + area.areaName : TrackedQuest + " - area not placed yet");
    }

    // ---------------- from an Interactable in a map area ----------------
    public void OpenMinigame(string questName)
    {
        var qm = QuestManager.Instance;
        var q = qm != null ? qm.quests.Find(x => x.questName == questName) : null;
        if (q == null) { Debug.LogWarning("[Navigator] Unknown quest " + questName); return; }
        if (!qm.IsUnlocked(q)) { ShowBanner("Clear the other quests first!"); return; }
        if (string.IsNullOrEmpty(q.sceneName)) { ShowBanner("Coming soon!"); return; }
        lastQuest = questName;
        LoadScene(q.sceneName, "Loading " + questName);
    }

    // ---------------- from a minigame ----------------
    // One line at the end of each minigame:  QuestNavigator.FinishQuest("LLM Game");
    public static void FinishQuest(string questName)
    {
        if (QuestManager.Instance != null) QuestManager.Instance.CompleteQuest(questName);
        if (Instance != null)
        {
            if (Instance.TrackedQuest == questName) Instance.Track(null);   // done: stop showing its path
            Instance.ReturnToMap(questName);
        }
    }

    // Leave a minigame without finishing it (e.g. a "Back" button)
    public static void ExitToMap()
    {
        if (Instance != null) Instance.ReturnToMap(Instance.lastQuest);
    }

    void ReturnToMap(string questName)
    {
        pendingQuest = questName;   // come back next to the area you left from
        LoadScene(mapSceneName, "Back to the map");
    }

    void LoadScene(string scene, string loadingText)
    {
        if (!Application.CanStreamedLevelBeLoaded(scene))
        {
            Debug.LogWarning("[Navigator] Scene '" + scene + "' isn't in Build Settings.");
            ShowBanner("Scene not added yet");
            return;
        }
        if (ScreenFader.Instance != null) ScreenFader.Instance.LoadScene(scene, loadingText);   // fade through the sky
        else SceneManager.LoadScene(scene);
    }

    void OnSceneLoaded(Scene s, LoadSceneMode m)
    {
        if (s.name != mapSceneName) return;
        var q = pendingQuest; pendingQuest = null;
        var t = pendingTrack; pendingTrack = null;
        if (!string.IsNullOrEmpty(q) || !string.IsNullOrEmpty(t))
            StartCoroutine(AfterMapLoaded(q, t));   // let the map's objects finish Awake/OnEnable first
    }

    IEnumerator AfterMapLoaded(string teleportTo, string track)
    {
        yield return null;
        if (!string.IsNullOrEmpty(teleportTo)) TeleportTo(teleportTo);
        if (!string.IsNullOrEmpty(track)) Track(track);
    }

    void TeleportTo(string questName, bool animate = false)
    {
        var area = MapArea.ForQuest(questName);
        if (area == null) { ShowBanner(questName + " - area not placed yet"); return; }
        // chevron wipe (DoorTransition) or fade; arriving from another scene is already covered by the sky fade
        PlayerTeleport.To(area.SpawnPosition, area.areaName, animate);
    }

    public void ShowBanner(string text)
    {
        if (banner == null || bannerText == null) { Debug.Log("[Navigator] " + text); return; }
        if (bannerRoutine != null) StopCoroutine(bannerRoutine);
        bannerRoutine = StartCoroutine(BannerRoutine(text));
    }

    IEnumerator BannerRoutine(string text)
    {
        bannerText.text = text;
        banner.SetActive(true);
        yield return new WaitForSecondsRealtime(bannerSeconds);
        banner.SetActive(false);
    }
}
