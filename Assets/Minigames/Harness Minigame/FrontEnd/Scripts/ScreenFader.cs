using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

// Screen transitions. Lives on GameSystems (survives scene changes), drawn above everything.
//   Teleport on the map  -> quick fade to navy and back
//   Change scene         -> fade into the home-screen sky with a loading line, load, fade back in
public class ScreenFader : MonoBehaviour
{
    public static ScreenFader Instance;

    [Header("Built by Tools > Front End > Build")]
    public CanvasGroup group;          // whole overlay (alpha 0..1, blocks clicks while showing)
    public Image solid;                // navy fill (teleports)
    public RectTransform sky;          // home background (scene changes)
    public TMP_Text label;             // "Loading ..." line
    public GameObject labelBand;       // dark strip behind the line

    [Header("Timing (seconds)")]
    public float teleportOut = 0.15f;
    public float teleportIn = 0.25f;
    public float sceneOut = 0.35f;
    public float sceneIn = 0.4f;
    public float minHold = 0.35f;      // keep the sky up at least this long, so it never just flickers

    public bool Busy { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { enabled = false; return; }
        Instance = this;
        Hide();
    }

    void Hide()
    {
        if (group == null) return;
        group.alpha = 0; group.blocksRaycasts = false;
        group.gameObject.SetActive(false);
    }

    // ---------------- teleport (same scene) ----------------
    public void Teleport(Action move)
    {
        if (group == null) { move?.Invoke(); return; }
        if (Busy) return;
        StartCoroutine(TeleportRoutine(move));
    }

    IEnumerator TeleportRoutine(Action move)
    {
        Busy = true;
        Show(skyStyle: false, null);
        yield return Fade(0, 1, teleportOut);
        move?.Invoke();
        yield return null;                       // let the camera catch up behind the cover
        yield return Fade(1, 0, teleportIn);
        Hide();
        Busy = false;
    }

    // ---------------- cover / uncover without loading (e.g. home screen -> game) ----------------
    public void Cover(Action whileCovered)
    {
        if (group == null) { whileCovered?.Invoke(); return; }
        if (Busy) { whileCovered?.Invoke(); return; }
        StartCoroutine(CoverRoutine(whileCovered));
    }

    IEnumerator CoverRoutine(Action whileCovered)
    {
        Busy = true;
        Show(skyStyle: false, null);
        yield return Fade(0, 1, teleportOut);
        whileCovered?.Invoke();
        yield return null;
        yield return Fade(1, 0, sceneIn);
        Hide();
        Busy = false;
    }

    // ---------------- scene change ----------------
    public void LoadScene(string scene, string text)
    {
        if (group == null) { SceneManager.LoadScene(scene); return; }
        if (Busy) return;
        StartCoroutine(LoadRoutine(scene, text));
    }

    IEnumerator LoadRoutine(string scene, string text)
    {
        Busy = true;
        Show(skyStyle: true, text);
        yield return Fade(0, 1, sceneOut);

        float shown = Time.unscaledTime;
        var op = SceneManager.LoadSceneAsync(scene);
        while (!op.isDone) { Animate(); yield return null; }
        yield return null;                       // let the new scene run Awake/Start + any teleport
        while (Time.unscaledTime - shown < minHold) { Animate(); yield return null; }

        yield return Fade(1, 0, sceneIn);
        Hide();
        Busy = false;
    }

    // ---------------- pieces ----------------
    void Show(bool skyStyle, string text)
    {
        group.gameObject.SetActive(true);
        group.blocksRaycasts = true;            // no double clicks during a transition
        if (solid) solid.enabled = !skyStyle;
        if (sky) { sky.gameObject.SetActive(skyStyle); sky.localScale = Vector3.one; }
        baseText = text ?? "";
        bool showLabel = skyStyle && baseText.Length > 0;
        if (labelBand) labelBand.SetActive(showLabel);
        if (label) { label.gameObject.SetActive(showLabel); label.text = baseText; }
    }

    string baseText = "";

    // slow zoom on the sky + animated dots
    void Animate()
    {
        if (sky && sky.gameObject.activeSelf) sky.localScale += Vector3.one * (0.02f * Time.unscaledDeltaTime);
        if (label && label.gameObject.activeSelf)
            label.text = baseText + new string('.', 1 + (int)(Time.unscaledTime * 3f) % 3);
    }

    IEnumerator Fade(float from, float to, float seconds)
    {
        float t = 0;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / seconds);
            k = k * k * (3f - 2f * k);           // smoothstep
            group.alpha = Mathf.Lerp(from, to, k);
            Animate();
            yield return null;
        }
        group.alpha = to;
    }
}
