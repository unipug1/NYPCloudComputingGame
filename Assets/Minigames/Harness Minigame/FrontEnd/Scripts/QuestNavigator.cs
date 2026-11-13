using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

// Lives on GameSystems (survives scene changes).
//  - Quest panel click  -> teleport the player to that quest's MapArea in the map scene
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
            if (ScreenFader.Instance != null) ScreenFader.Instance.Teleport(() => TeleportTo(questName));
            else TeleportTo(questName);
        }
        else { pendingQuest = questName; LoadScene(mapSceneName, "Back to the map"); }
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
        if (Instance != null) Instance.ReturnToMap(questName);
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
        if (s.name != mapSceneName || string.IsNullOrEmpty(pendingQuest)) return;
        var q = pendingQuest; pendingQuest = null;
        StartCoroutine(TeleportNextFrame(q));   // let the map's objects finish Awake/OnEnable first
    }

    IEnumerator TeleportNextFrame(string q)
    {
        yield return null;
        TeleportTo(q);
    }

    void TeleportTo(string questName)
    {
        var area = MapArea.ForQuest(questName);
        if (area == null) { ShowBanner(questName + " - area not placed yet"); return; }

        var player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            var rb = player.GetComponent<Rigidbody2D>();
            if (rb != null) { rb.position = area.SpawnPosition; rb.linearVelocity = Vector2.zero; }
            player.transform.position = area.SpawnPosition;
            var cam = Camera.main;   // jump the camera too, so it doesn't glide across the map
            if (cam != null) cam.transform.position = new Vector3(area.SpawnPosition.x, area.SpawnPosition.y, cam.transform.position.z);
        }
        else Debug.LogWarning("[Navigator] No object tagged 'Player' in the map.");

        ShowBanner(area.areaName);
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
