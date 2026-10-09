using System.Collections;
using UnityEngine;

public class LLM_Minigame_ManpuEffect : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private RectTransform groupRect;
    [SerializeField] private CanvasGroup groupCanvasGroup;

    [Header("Scale Settings")]
    [SerializeField] private float peakScale = 1.15f;
    [SerializeField] private float restingScale = 0.9f;

    [Header("Timing Settings")]
    [SerializeField] private float popDuration = 0.14f;
    [SerializeField] private float holdDuration = 0.25f;
    [SerializeField] private float fadeDuration = 0.2f;
    [SerializeField] private float floatUpDistance = 20f;

    private Vector2 defaultAnchoredPos;
    private Coroutine playRoutine;

    private void Awake()
    {
        if (groupRect == null)
        {
            groupRect = GetComponent<RectTransform>();
        }

        if (groupCanvasGroup == null)
        {
            groupCanvasGroup = GetComponent<CanvasGroup>();
        }

        if (groupRect != null)
        {
            defaultAnchoredPos = groupRect.anchoredPosition;
            groupRect.localScale = Vector3.zero;
        }

        // hide completely at start without disabling the gameobject
        if (groupCanvasGroup != null)
        {
            groupCanvasGroup.alpha = 0f;
            groupCanvasGroup.blocksRaycasts = false;
        }
    }

    public void PlayPop()
    {
        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
        }

        playRoutine = StartCoroutine(PopRoutine());
    }

    private IEnumerator PopRoutine()
    {
        // reset position, scale, and show via canvas group
        groupRect.anchoredPosition = defaultAnchoredPos;
        groupRect.localScale = Vector3.zero;

        if (groupCanvasGroup != null)
        {
            groupCanvasGroup.alpha = 1f;
        }

        // phase 1: scale up from 0 to peak scale (overshoot)
        float timer = 0f;
        float upDuration = popDuration * 0.65f;
        while (timer < upDuration)
        {
            timer += Time.deltaTime;
            float progress = Mathf.Clamp01(timer / upDuration);
            groupRect.localScale = Vector3.LerpUnclamped(Vector3.zero, Vector3.one * peakScale, progress);
            yield return null;
        }

        // phase 2: settle down from peak scale to resting scale
        timer = 0f;
        float settleDuration = popDuration - upDuration;
        while (timer < settleDuration)
        {
            timer += Time.deltaTime;
            float progress = Mathf.Clamp01(timer / settleDuration);
            groupRect.localScale = Vector3.LerpUnclamped(Vector3.one * peakScale, Vector3.one * restingScale, progress);
            yield return null;
        }

        groupRect.localScale = Vector3.one * restingScale;

        // phase 3: hold in place
        yield return new WaitForSeconds(holdDuration);

        // phase 4: fade out canvas group while drifting slightly upward
        timer = 0f;
        Vector2 startPos = defaultAnchoredPos;
        Vector2 endPos = new Vector2(defaultAnchoredPos.x, defaultAnchoredPos.y + floatUpDistance);

        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float progress = Mathf.Clamp01(timer / fadeDuration);

            if (groupCanvasGroup != null)
            {
                groupCanvasGroup.alpha = Mathf.Lerp(1f, 0f, progress);
            }

            groupRect.anchoredPosition = Vector2.Lerp(startPos, endPos, progress);
            yield return null;
        }

        // return to invisible resting state
        if (groupCanvasGroup != null)
        {
            groupCanvasGroup.alpha = 0f;
        }

        groupRect.localScale = Vector3.zero;
        groupRect.anchoredPosition = defaultAnchoredPos;
    }
}