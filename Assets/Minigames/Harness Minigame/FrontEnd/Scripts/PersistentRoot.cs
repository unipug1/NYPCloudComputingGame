using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

// Keeps GameSystems (quest manager, progress bar, quest panel, home screen) alive across scene changes.
// If a scene you load also contains a GameSystems copy, that copy switches off and deletes itself.
// Also makes sure every scene has an EventSystem so the HUD buttons keep working.
[DefaultExecutionOrder(-1000)]
public class PersistentRoot : MonoBehaviour
{
    static PersistentRoot instance;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            gameObject.SetActive(false);   // stop its scripts from running
            Destroy(gameObject);
            return;
        }
        instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        if (instance == this) SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;
        var es = new GameObject("EventSystem (auto)", typeof(EventSystem));
        var t = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (t != null) es.AddComponent(t); else es.AddComponent<StandaloneInputModule>();
    }
}
