using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class LLM_Minigame_SlideModal : MonoBehaviour
{
    [Header("Transforms")]
    [SerializeField] private RectTransform modalRect;
    [SerializeField] private Vector2 shownPos = Vector2.zero;
    [SerializeField] private Vector2 hiddenPos = new Vector2(0f, 1200f);

    [Header("Screen Darken Backdrop")]
    [SerializeField] private UnityEngine.UI.Image backdropOverlay;
    [SerializeField] private float targetBackdropAlpha = 0.65f;

    [Header("Canvas Group")]
    [SerializeField] private CanvasGroup modalCanvasGroup;

    [Header("Animation Settings")]
    [SerializeField] private float dropDuration = 0.75f;

    private Coroutine animRoutine;
    private bool isShown = false;

    private void Awake()
    {
        if (modalRect == null)
        {
            modalRect = GetComponent<RectTransform>();
        }

        if (modalCanvasGroup == null)
        {
            modalCanvasGroup = GetComponent<CanvasGroup>();
        }

        SnapHidden();
    }

    public bool IsShown()
    {
        return isShown;
    }

    public void SnapHidden()
    {
        if (animRoutine != null)
        {
            StopCoroutine(animRoutine);
        }

        isShown = false;

        if (modalRect != null)
        {
            modalRect.anchoredPosition = hiddenPos;
        }

        if (modalCanvasGroup != null)
        {
            modalCanvasGroup.interactable = false;
            modalCanvasGroup.blocksRaycasts = false;
        }

        if (backdropOverlay != null)
        {
            Color c = backdropOverlay.color;
            c.a = 0f;
            backdropOverlay.color = c;
            backdropOverlay.raycastTarget = false;
        }

        gameObject.SetActive(false);
    }

    public void SlideIn(Action onComplete = null)
    {
        gameObject.SetActive(true);
        isShown = true;

        if (animRoutine != null)
        {
            StopCoroutine(animRoutine);
        }

        animRoutine = StartCoroutine(BounceDropRoutine(onComplete));
    }

    public void SlideOut(Action onComplete = null)
    {
        isShown = false;

        if (animRoutine != null)
        {
            StopCoroutine(animRoutine);
        }

        animRoutine = StartCoroutine(SlideUpOutRoutine(onComplete));
    }

    // drops from top and bounces multiple times like a real ball
    private IEnumerator BounceDropRoutine(Action onComplete)
    {
        if (modalCanvasGroup != null)
        {
            modalCanvasGroup.interactable = false;
            modalCanvasGroup.blocksRaycasts = false;
        }

        if (backdropOverlay != null)
        {
            backdropOverlay.raycastTarget = true;
        }

        float timer = 0f;
        while (timer < dropDuration)
        {
            timer += Time.deltaTime;
            float progress = Mathf.Clamp01(timer / dropDuration);

            // classic physics ball bounce equation
            float bounceProgress = EvaluateBounce(progress);
            modalRect.anchoredPosition = Vector2.LerpUnclamped(hiddenPos, shownPos, bounceProgress);

            // fade in the dark background
            if (backdropOverlay != null)
            {
                Color c = backdropOverlay.color;
                c.a = Mathf.Lerp(0f, targetBackdropAlpha, progress);
                backdropOverlay.color = c;
            }

            yield return null;
        }

        modalRect.anchoredPosition = shownPos;

        if (backdropOverlay != null)
        {
            Color c = backdropOverlay.color;
            c.a = targetBackdropAlpha;
            backdropOverlay.color = c;
        }

        if (modalCanvasGroup != null)
        {
            modalCanvasGroup.interactable = true;
            modalCanvasGroup.blocksRaycasts = true;
        }

        onComplete?.Invoke();
    }

    private IEnumerator SlideUpOutRoutine(Action onComplete)
    {
        if (modalCanvasGroup != null)
        {
            modalCanvasGroup.interactable = false;
            modalCanvasGroup.blocksRaycasts = false;
        }

        float timer = 0f;
        float exitDuration = 0.3f;
        Vector2 startPos = modalRect.anchoredPosition;

        while (timer < exitDuration)
        {
            timer += Time.deltaTime;
            float progress = Mathf.Clamp01(timer / exitDuration);

            modalRect.anchoredPosition = Vector2.Lerp(startPos, hiddenPos, progress);

            if (backdropOverlay != null)
            {
                Color c = backdropOverlay.color;
                c.a = Mathf.Lerp(targetBackdropAlpha, 0f, progress);
                backdropOverlay.color = c;
            }

            yield return null;
        }

        SnapHidden();
        onComplete?.Invoke();
    }

    // ball bounce equation
    private float EvaluateBounce(float t)
    {
        if (t < (1f / 2.75f))
        {
            return 7.5625f * t * t;
        }
        else if (t < (2f / 2.75f))
        {
            t -= (1.5f / 2.75f);
            return 7.5625f * t * t + 0.75f;
        }
        else if (t < (2.5f / 2.75f))
        {
            t -= (2.25f / 2.75f);
            return 7.5625f * t * t + 0.9375f;
        }
        else
        {
            t -= (2.625f / 2.75f);
            return 7.5625f * t * t + 0.984375f;
        }
    }
}