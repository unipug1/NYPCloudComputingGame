using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LLM_Minigame_DeadEndModal : MonoBehaviour
{
    [Header("ui references")]
    [SerializeField] private RectTransform modalRect;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TextMeshProUGUI reasonText;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button restartButton;

    [Header("animation")]
    [SerializeField] private float fadeDuration = 0.2f;

    private Action onContinueChosen;
    private Action onRestartChosen;
    private Coroutine fadeRoutine;

    private void Awake()
    {
        if (modalRect == null) modalRect = GetComponent<RectTransform>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();

        if (continueButton != null) continueButton.onClick.AddListener(() => onContinueChosen?.Invoke());
        if (restartButton != null) restartButton.onClick.AddListener(() => onRestartChosen?.Invoke());

        SnapClosed();
    }

    public void ShowModal(string deadEndExplanation, Action onContinue, Action onRestart)
    {
        onContinueChosen = onContinue;
        onRestartChosen = onRestart;

        if (reasonText != null)
        {
            reasonText.text = deadEndExplanation;
        }

        gameObject.SetActive(true);
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeRoutine(true));
    }

    public void HideModal(Action onFinished = null)
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