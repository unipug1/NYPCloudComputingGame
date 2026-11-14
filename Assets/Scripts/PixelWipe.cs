using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Full-screen pixel wipe: blocks sweep in from the left to cover the screen,
/// an action runs while everything is hidden, then the blocks sweep away.
///
/// Usage from any UI script:  PixelWipe.Play(this, () => { /* swap windows */ });
/// No scene setup is needed. To tweak the settings in the Inspector, add this
/// component to the root Canvas yourself and it will be used instead.
/// </summary>
public class PixelWipe : MonoBehaviour
{
    [Header("Look")]
    [Tooltip("Size of each block in canvas units (the canvas is 1920 x 1080).")]
    public float blockSize = 120f;
    public Color blockColor = new Color32(0x0B, 0x0F, 0x1C, 0xFF);
    [Tooltip("How ragged the moving edge is, in blocks. 0 = a straight line.")]
    public float edgeJitter = 2.5f;

    [Header("Timing (seconds)")]
    public float coverTime = 0.3f;
    public float holdTime = 0.12f;
    public float revealTime = 0.3f;

    public bool IsPlaying { get; private set; }

    RectTransform overlay;
    Image[] blocks;
    float[] thresholds;
    Vector2 builtForSize;
    float builtForBlockSize;

    /// <summary>Plays the wipe on the canvas that <paramref name="from"/> lives in.</summary>
    public static void Play(Component from, Action atMidpoint)
    {
        Canvas canvas = from != null ? from.GetComponentInParent<Canvas>() : null;
        if (canvas == null)
        {
            if (atMidpoint != null) atMidpoint();
            return;
        }
        canvas = canvas.rootCanvas;
        PixelWipe wipe = canvas.GetComponent<PixelWipe>();
        if (wipe == null) wipe = canvas.gameObject.AddComponent<PixelWipe>();
        wipe.Run(atMidpoint);
    }

    public void Run(Action atMidpoint)
    {
        if (IsPlaying || !isActiveAndEnabled)
        {
            if (atMidpoint != null) atMidpoint();
            return;
        }
        StartCoroutine(Routine(atMidpoint));
    }

    IEnumerator Routine(Action atMidpoint)
    {
        IsPlaying = true;
        Build();
        overlay.SetAsLastSibling();
        overlay.gameObject.SetActive(true);
        SetAll(false);

        // Cover
        yield return Sweep(coverTime, true);
        SetAll(true);

        // Swap while the screen is hidden. Never leave the screen covered if this throws.
        try
        {
            if (atMidpoint != null) atMidpoint();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
        overlay.SetAsLastSibling();
        if (holdTime > 0f) yield return new WaitForSecondsRealtime(holdTime);

        // Reveal
        yield return Sweep(revealTime, false);
        SetAll(false);

        overlay.gameObject.SetActive(false);
        IsPlaying = false;
    }

    IEnumerator Sweep(float duration, bool show)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float progress = duration > 0f ? Mathf.Clamp01(t / duration) : 1f;
            for (int i = 0; i < blocks.Length; i++)
            {
                bool passed = progress >= thresholds[i];
                blocks[i].enabled = show ? passed : !passed;
            }
            yield return null;
        }
    }

    void SetAll(bool visible)
    {
        for (int i = 0; i < blocks.Length; i++) blocks[i].enabled = visible;
    }

    void Build()
    {
        Vector2 size = ((RectTransform)transform).rect.size;
        if (overlay != null && size == builtForSize && Mathf.Approximately(blockSize, builtForBlockSize)) return;
        if (overlay != null) Destroy(overlay.gameObject);

        float cell = Mathf.Max(8f, blockSize);
        int cols = Mathf.CeilToInt(size.x / cell);
        int rows = Mathf.CeilToInt(size.y / cell);

        GameObject root = new GameObject("PixelWipeOverlay", typeof(RectTransform));
        root.layer = gameObject.layer;
        overlay = root.GetComponent<RectTransform>();
        overlay.SetParent(transform, false);
        overlay.anchorMin = Vector2.zero;
        overlay.anchorMax = Vector2.one;
        overlay.offsetMin = Vector2.zero;
        overlay.offsetMax = Vector2.zero;

        // Invisible full-screen image so clicks cannot reach the UI mid-wipe.
        Image blocker = root.AddComponent<Image>();
        blocker.color = Color.clear;
        blocker.raycastTarget = true;

        blocks = new Image[cols * rows];
        thresholds = new float[cols * rows];
        float span = cols + Mathf.Max(0f, edgeJitter);
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                GameObject go = new GameObject("Block", typeof(RectTransform));
                go.layer = gameObject.layer;
                RectTransform rt = go.GetComponent<RectTransform>();
                rt.SetParent(overlay, false);
                rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
                rt.anchoredPosition = new Vector2(x * cell, y * cell);
                rt.sizeDelta = new Vector2(cell + 1f, cell + 1f);   // +1 hides seams between blocks

                Image img = go.AddComponent<Image>();
                img.color = blockColor;
                img.raycastTarget = false;
                img.enabled = false;

                int i = y * cols + x;
                blocks[i] = img;
                thresholds[i] = (x + UnityEngine.Random.Range(0f, Mathf.Max(0f, edgeJitter))) / span;
            }
        }

        builtForSize = size;
        builtForBlockSize = blockSize;
        overlay.gameObject.SetActive(false);
    }
}
