using System;
using System.Collections;
using TMPro;
using UnityEngine;

public class LLM_Minigame_PlayerBubble : MonoBehaviour
{
    [Header("components")]
    [SerializeField] private RectTransform bubbleRect;
    [SerializeField] private CanvasGroup bubbleCanvasGroup;
    [SerializeField] private TextMeshProUGUI bubbleText;

    [Header("continue arrow (up down loop)")]
    [SerializeField] private RectTransform continueArrowRect;
    [SerializeField] private float arrowBobDistance = 8f;
    [SerializeField] private float arrowBobSpeed = 4f;

    [Header("animation settings")]
    [SerializeField] private float popDuration = 0.2f;
    [SerializeField] private float shakeMagnitude = 4f;
    [SerializeField] private float shakeDuration = 0.15f;
    [SerializeField] private AnimationCurve popCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private Vector2 defaultPos;
    private Vector2 arrowDefaultPos;
    private Coroutine animRoutine;
    private Coroutine arrowRoutine;
    private Action onDismissCallback;
    private bool isWaitingForClick = false;
    private string lastSpokenText = "";

    public bool IsAnimating() => animRoutine != null;
    public bool IsOpen() => gameObject.activeInHierarchy && bubbleCanvasGroup != null && bubbleCanvasGroup.alpha > 0.1f;

    private void Awake()
    {
        if (bubbleRect == null) bubbleRect = GetComponent<RectTransform>();
        if (bubbleCanvasGroup == null) bubbleCanvasGroup = GetComponent<CanvasGroup>();

        if (bubbleRect != null) defaultPos = bubbleRect.anchoredPosition;
        if (continueArrowRect != null)
        {
            arrowDefaultPos = continueArrowRect.anchoredPosition;
            continueArrowRect.gameObject.SetActive(false);
        }

        SnapClosed();
    }

    private void Update()
    {
        if (isWaitingForClick && (Input.anyKeyDown || Input.GetMouseButtonDown(0)))
        {
            DismissBubble();
        }
    }

    public void ShowPlayerDialogue(string dialogue, bool withShake = true, Action onSettled = null)
    {
        LLM_Minigame_AudioManager.Instance?.PlayPlayerBubbleOpen();

        lastSpokenText = dialogue;
        bubbleText.text = dialogue;

        if (animRoutine != null) StopCoroutine(animRoutine);
        gameObject.SetActive(true);

        animRoutine = StartCoroutine(MidToMaxPopRoutine(withShake, onSettled));
    }

    private IEnumerator MidToMaxPopRoutine(bool withShake, Action onSettled)
    {
        isWaitingForClick = false;
        if (continueArrowRect != null) continueArrowRect.gameObject.SetActive(false);

        bubbleRect.anchoredPosition = defaultPos;
        bubbleRect.localScale = Vector3.one * 0.75f;
        bubbleCanvasGroup.alpha = 0f;

        float timer = 0f;
        while (timer < popDuration)
        {
            timer += Time.deltaTime;
            float progress = Mathf.Clamp01(timer / popDuration);
            float curveVal = popCurve.Evaluate(progress);

            float scale = Mathf.Lerp(0.75f, 1.05f, curveVal);
            if (progress > 0.8f)
            {
                scale = Mathf.Lerp(1.05f, 1.0f, (progress - 0.8f) / 0.2f);
            }

            bubbleRect.localScale = Vector3.one * scale;
            bubbleCanvasGroup.alpha = progress;
            yield return null;
        }

        bubbleRect.localScale = Vector3.one;
        bubbleCanvasGroup.alpha = 1f;

        if (withShake)
        {
            timer = 0f;
            while (timer < shakeDuration)
            {
                timer += Time.deltaTime;
                float offset = UnityEngine.Random.Range(-shakeMagnitude, shakeMagnitude);
                bubbleRect.anchoredPosition = new Vector2(defaultPos.x + offset, defaultPos.y + offset);
                yield return null;
            }
        }

        bubbleRect.anchoredPosition = defaultPos;
        animRoutine = null;
        onSettled?.Invoke();
    }

    public void EnableContinuePrompt(Action onDismissed)
    {
        onDismissCallback = onDismissed;
        isWaitingForClick = true;

        if (continueArrowRect != null)
        {
            continueArrowRect.gameObject.SetActive(true);
            if (arrowRoutine != null) StopCoroutine(arrowRoutine);
            arrowRoutine = StartCoroutine(ArrowUpDownRoutine());
        }
    }

    private IEnumerator ArrowUpDownRoutine()
    {
        float timer = 0f;
        while (true)
        {
            timer += Time.deltaTime * arrowBobSpeed;
            float offset = Mathf.Sin(timer) * arrowBobDistance;
            continueArrowRect.anchoredPosition = new Vector2(arrowDefaultPos.x, arrowDefaultPos.y + offset);
            yield return null;
        }
    }

    public void DismissBubble()
    {
        // do not start dismiss if already closed or inactive
        if (!gameObject.activeInHierarchy)
        {
            SnapClosed();
            return;
        }

        LLM_Minigame_AudioManager.Instance?.PlayPlayerBubbleClose();
        isWaitingForClick = false;
        if (arrowRoutine != null) StopCoroutine(arrowRoutine);
        if (continueArrowRect != null) continueArrowRect.gameObject.SetActive(false);

        if (animRoutine != null) StopCoroutine(animRoutine);
        animRoutine = StartCoroutine(DismissRoutine());
    }

    private IEnumerator DismissRoutine()
    {
        float timer = 0f;
        float duration = 0.16f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float progress = timer / duration;

            bubbleRect.localScale = Vector3.one * Mathf.Lerp(1f, 0.75f, progress);
            bubbleCanvasGroup.alpha = 1f - progress;
            yield return null;
        }

        Action callback = onDismissCallback;
        onDismissCallback = null; // clear callback so it cannot fire twice

        SnapClosed();
        animRoutine = null;

        callback?.Invoke();
    }

    public void ReopenForReview(Action onClosed = null, Action onSettled = null)
    {
        if (string.IsNullOrEmpty(lastSpokenText)) return;
        ShowPlayerDialogue(lastSpokenText, false, onSettled);
        EnableContinuePrompt(onClosed);
    }

    public void SnapClosed()
    {
        isWaitingForClick = false;
        animRoutine = null;
        onDismissCallback = null;
        if (bubbleRect != null) bubbleRect.localScale = Vector3.one * 0.75f;
        if (bubbleCanvasGroup != null) bubbleCanvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }
}