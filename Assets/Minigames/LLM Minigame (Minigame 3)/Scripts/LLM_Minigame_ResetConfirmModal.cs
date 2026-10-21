using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class LLM_Minigame_ResetConfirmModal : MonoBehaviour
{
    [Header("ui references")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Button yesButton;
    [SerializeField] private Button noButton;

    [Header("animation")]
    [SerializeField] private float fadeDuration = 0.15f;

    private Action onConfirmed;
    private Action onCancelled;
    private Coroutine fadeRoutine;

    private void Awake()
    {
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();

        if (yesButton != null) yesButton.onClick.AddListener(() => onConfirmed?.Invoke());
        if (noButton != null) noButton.onClick.AddListener(() => onCancelled?.Invoke());

        SnapClosed();
    }

    public void Show(Action confirmCallback, Action cancelCallback)
    {
        onConfirmed = confirmCallback;
        onCancelled = cancelCallback;

        gameObject.SetActive(true);
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeRoutine(true));
    }

    public void Hide(Action onFinished = null)
    {
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeRoutine(false, onFinished));
    }

    private IEnumerator FadeRoutine(bool show, Action onComplete = null)
    {
        float timer = 0f;
        float startAlpha = canvasGroup != null ? canvasGroup.alpha : (show ? 0f : 1f);
        float targetAlpha = show ? 1f : 0f;

        if (canvasGroup != null)
        {
            canvasGroup.interactable = show;
            canvasGroup.blocksRaycasts = show;
        }

        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float progress = Mathf.Clamp01(timer / fadeDuration);
            if (canvasGroup != null) canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, progress);
            yield return null;
        }

        if (canvasGroup != null) canvasGroup.alpha = targetAlpha;

        if (!show)
        {
            SnapClosed();
        }

        onComplete?.Invoke();
    }

    public void SnapClosed()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
        gameObject.SetActive(false);
    }
}