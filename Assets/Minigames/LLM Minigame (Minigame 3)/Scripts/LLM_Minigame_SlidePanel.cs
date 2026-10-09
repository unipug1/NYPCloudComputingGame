using System;
using System.Collections;
using UnityEngine;

public class LLM_Minigame_SlidePanel : MonoBehaviour
{
    [SerializeField] private RectTransform targetRect;
    [SerializeField] private Vector2 shownPosition = Vector2.zero;
    [SerializeField] private Vector2 hiddenPosition = new Vector2(0f, -800f);
    [SerializeField] private float duration = 0.25f;

    private Coroutine slideRoutine;
    private bool isShown = false;

    private void Awake()
    {
        if (targetRect == null)
        {
            targetRect = GetComponent<RectTransform>();
        }
    }

    public bool IsShown()
    {
        return isShown;
    }

    // snap immediately to hidden or shown without animating (used at game start)
    public void SnapInstant(bool show)
    {
        if (slideRoutine != null)
        {
            StopCoroutine(slideRoutine);
        }

        isShown = show;
        gameObject.SetActive(show);

        if (targetRect != null)
        {
            targetRect.anchoredPosition = show ? shownPosition : hiddenPosition;
        }
    }

    public void SlideIn(Action onComplete = null)
    {
        gameObject.SetActive(true);
        isShown = true;
        StartSlide(shownPosition, onComplete);
    }

    public void SlideOut(Action onComplete = null)
    {
        isShown = false;
        StartSlide(hiddenPosition, () =>
        {
            gameObject.SetActive(false);
            onComplete?.Invoke();
        });
    }

    private void StartSlide(Vector2 targetPos, Action onComplete)
    {
        if (slideRoutine != null)
        {
            StopCoroutine(slideRoutine);
        }
        slideRoutine = StartCoroutine(SlideRoutine(targetPos, onComplete));
    }

    // cubic ease out coroutine so we do not need external tween packages
    private IEnumerator SlideRoutine(Vector2 targetPos, Action onComplete)
    {
        Vector2 startPos = targetRect.anchoredPosition;
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float progress = Mathf.Clamp01(timer / duration);

            // cubic ease out curve gives a snappy pop at the start and smooth stop
            float curve = 1f - Mathf.Pow(1f - progress, 3f);

            targetRect.anchoredPosition = Vector2.LerpUnclamped(startPos, targetPos, curve);
            yield return null;
        }

        targetRect.anchoredPosition = targetPos;
        onComplete?.Invoke();
    }
}