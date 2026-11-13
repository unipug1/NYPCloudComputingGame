using UnityEngine;
using UnityEngine.SceneManagement;

// "BACK TO MAP" button on the shared HUD: only shows inside minigame scenes.
public class BackToMapButton : MonoBehaviour
{
    public GameObject button;
    [Tooltip("Scenes that already have their own way back (or where the button would cover something)")]
    public string[] hideInScenes = { "Harness_Minigame" };

    void OnEnable() { SceneManager.sceneLoaded += OnLoaded; Refresh(); }
    void OnDisable() { SceneManager.sceneLoaded -= OnLoaded; }
    void OnLoaded(Scene s, LoadSceneMode m) => Refresh();

    void Refresh()
    {
        var nav = QuestNavigator.Instance;
        var scene = SceneManager.GetActiveScene().name;
        if (button != null) button.SetActive(nav != null && scene != nav.mapSceneName && System.Array.IndexOf(hideInScenes, scene) < 0);
    }

    public void Click() => QuestNavigator.ExitToMap();
}
