using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Background music + UI / interact / teleport / quest-complete sounds.
// Lives on GameSystems, so it keeps playing across scenes. Built by Tools > Front End > Build.
public class GameAudio : MonoBehaviour
{
    public static GameAudio Instance;

    [Header("Clips")]
    public AudioClip music;            // hub / home screen loop
    public AudioClip uiClick;          // any UI button
    public AudioClip interact;         // pressing E on an NPC / station / door
    public AudioClip dialogueBlip;     // advancing dialogue
    public AudioClip teleport;         // quest-panel jumps, TP pads, doors
    public AudioClip questComplete;

    [Header("Volume")]
    [Range(0, 1)] public float musicVolume = 0.35f;
    [Range(0, 1)] public float sfxVolume = 0.8f;
    public float musicFadeSeconds = 0.8f;

    [Header("Scenes with their own audio")]
    public string[] musicOffInScenes = { "LLM_Minigame" };
    public string[] clickOffInScenes = { "LLM_Minigame" };

    AudioSource musicSource, sfxSource;
    Coroutine fade;
    int lastDone = -1;
    readonly List<RaycastResult> hits = new List<RaycastResult>();

    void Awake()
    {
        if (Instance != null && Instance != this) { enabled = false; return; }
        Instance = this;
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.loop = true; musicSource.playOnAwake = false; musicSource.volume = 0;
        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        PlayerTeleport.OnTeleportStarted += OnTeleportStarted;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        PlayerTeleport.OnTeleportStarted -= OnTeleportStarted;
        if (QuestManager.Instance != null) QuestManager.Instance.OnQuestsChanged -= OnQuestsChanged;
    }

    void Start()
    {
        if (QuestManager.Instance != null) QuestManager.Instance.OnQuestsChanged += OnQuestsChanged;
        UpdateMusic(SceneManager.GetActiveScene().name);
    }

    void OnSceneLoaded(Scene s, LoadSceneMode m) => UpdateMusic(s.name);

    // ---------------- music ----------------
    void UpdateMusic(string scene)
    {
        if (music == null) return;
        bool want = System.Array.IndexOf(musicOffInScenes, scene) < 0;
        if (want && !musicSource.isPlaying) { musicSource.clip = music; musicSource.Play(); }
        FadeMusic(want ? musicVolume : 0f);
    }

    void FadeMusic(float to)
    {
        if (fade != null) StopCoroutine(fade);
        fade = StartCoroutine(FadeRoutine(to));
    }

    IEnumerator FadeRoutine(float to)
    {
        float from = musicSource.volume, t = 0;
        while (t < musicFadeSeconds)
        {
            t += Time.unscaledDeltaTime;
            musicSource.volume = Mathf.Lerp(from, to, t / musicFadeSeconds);
            yield return null;
        }
        musicSource.volume = to;
        if (to <= 0.001f) musicSource.Pause();
    }

    public void SetMusicVolume(float v) { musicVolume = Mathf.Clamp01(v); if (musicSource.isPlaying) musicSource.volume = musicVolume; }

    // ---------------- sfx ----------------
    public void Play(AudioClip clip, float volume = 1f)
    {
        if (clip != null) sfxSource.PlayOneShot(clip, sfxVolume * volume);
    }

    public static void PlayClick() { if (Instance) Instance.Play(Instance.uiClick, 0.8f); }
    public static void PlayInteract() { if (Instance) Instance.Play(Instance.interact); }

    void OnTeleportStarted() => Play(teleport, 0.7f);

    void OnQuestsChanged()
    {
        var qm = QuestManager.Instance;
        if (qm == null) return;
        int done = qm.CompletedNames().Count;
        if (lastDone >= 0 && done > lastDone) Play(questComplete);
        lastDone = done;
    }

    void Update()
    {
        string scene = SceneManager.GetActiveScene().name;

        // UI button clicks (mouse or touch)
        if (PointerPressed(out Vector2 pos) && System.Array.IndexOf(clickOffInScenes, scene) < 0 && OverButton(pos))
            PlayClick();

        // E: interact with something / advance dialogue
        if (InteractPressed())
        {
            var dm = DialogueManager.Instance;
            if (dm != null && dm.IsDialogueRunning) Play(dialogueBlip, 0.6f);
            else
            {
                var player = GameObject.FindWithTag("Player");
                var pi = player != null ? player.GetComponent<PlayerInteraction>() : null;
                if (pi != null && pi.isActiveAndEnabled && pi.CurrentTarget != null) PlayInteract();
            }
        }
    }

    bool OverButton(Vector2 pos)
    {
        var es = EventSystem.current;
        if (es == null) return false;
        hits.Clear();
        es.RaycastAll(new PointerEventData(es) { position = pos }, hits);
        if (hits.Count == 0) return false;
        var sel = hits[0].gameObject.GetComponentInParent<Selectable>();
        return sel != null && sel.IsInteractable() && (sel is Button || sel is Toggle || sel is TMPro.TMP_Dropdown);
    }

    static bool PointerPressed(out Vector2 pos)
    {
        pos = default;
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) { pos = Mouse.current.position.ReadValue(); return true; }
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        { pos = Touchscreen.current.primaryTouch.position.ReadValue(); return true; }
        return false;
#else
        if (Input.GetMouseButtonDown(0)) { pos = Input.mousePosition; return true; }
        return false;
#endif
    }

    static bool InteractPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.E);
#endif
    }
}
