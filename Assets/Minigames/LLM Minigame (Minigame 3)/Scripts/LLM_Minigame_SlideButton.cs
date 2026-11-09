using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LLM_Minigame_SlideButton : MonoBehaviour
{
    [Header("components")]
    [SerializeField] private RectTransform buttonRect;
    [SerializeField] private Button button;
    [SerializeField] private TextMeshProUGUI buttonLabel;

    [Header("positions")]
    [SerializeField] private float inPosX = 22f;
    [SerializeField] private float outPosX = 133f;

    [Header("animation durations")]
    [SerializeField] private float slideInDuration = 0.28f;
    [SerializeField] private float slideOutDuration = 0.18f;

    [Header("animation curves")]
    [SerializeField] private AnimationCurve slideInBounceCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.65f, 1.12f),
        new Keyframe(0.85f, 0.97f),
        new Keyframe(1f, 1f)
    );
    [SerializeField] private AnimationCurve slideOutCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private float defaultPosY;
    private Coroutine slideRoutine;
    private bool isShown = false;

    public bool IsShown() => isShown;
    public bool IsAnimating() => slideRoutine != null;

    private void Awake()
    {
        if (buttonRect == null) buttonRect = GetComponent<RectTransform>();
        if (button == null) button = GetComponent<Button>();
        if (buttonLabel == null) buttonLabel = GetComponentInChildren<TextMeshProUGUI>();

        if (buttonRect != null)
        {
            defaultPosY = buttonRect.anchoredPosition.y;
        }

        SnapOut();
    }

    public void AddClickListener(Action onClick)
    {
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick?.Invoke());
        }
    }

    public void SetText(string text)
    {
        if (buttonLabel != null)
        {
            buttonLabel.text = text;
        }
    }

    public void SlideIn(Action onComplete = null)
    {
        if (isShown && slideRoutine == null)
        {
            onComplete?.Invoke();
            return;
        }

        isShown = true;
        gameObject.SetActive(true);

        if (slideRoutine != null) StopCoroutine(slideRoutine);
        slideRoutine = StartCoroutine(SlideInRoutine(onComplete));
    }

    private IEnumerator SlideInRoutine(Action onComplete)
    {
        if (button != null) button.interactable = false;

        float timer = 0f;
        while (timer < slideInDuration)
        {
            timer += Time.deltaTime;
            float progress = Mathf.Clamp01(timer / slideInDuration);
            float curveVal = slideInBounceCurve.Evaluate(progress);

            float currentX = Mathf.LerpUnclamped(outPosX, inPosX, curveVal);
            buttonRect.anchoredPosition = new Vector2(currentX, defaultPosY);
            yield return null;
        }

        buttonRect.anchoredPosition = new Vector2(inPosX, defaultPosY);
        if (button != null) button.interactable = true;

        slideRoutine = null;
        onComplete?.Invoke();
    }

    public void SlideOut(Action onComplete = null)
    {
        if (!isShown && slideRoutine == null)
        {
            onComplete?.Invoke();
            return;
        }

        isShown = false;
        if (button != null) button.interactable = false;

        if (slideRoutine != null) StopCoroutine(slideRoutine);
        slideRoutine = StartCoroutine(SlideOutRoutine(onComplete));
    }

    private IEnumerator SlideOutRoutine(Action onComplete)
    {
        float timer = 0f;
        float startX = buttonRect.anchoredPosition.x;

        while (timer < slideOutDuration)
        {
            timer += Time.deltaTime;
            float progress = Mathf.Clamp01(timer / slideOutDuration);
            float curveVal = slideOutCurve.Evaluate(progress);

            float currentX = Mathf.Lerp(startX, outPosX, curveVal);
            buttonRect.anchoredPosition = new Vector2(currentX, defaultPosY);
            yield return null;
        }

        buttonRect.anchoredPosition = new Vector2(outPosX, defaultPosY);
        gameObject.SetActive(false);

        slideRoutine = null;
        onComplete?.Invoke();
    }

    // slides out, updates text, and bounces back in (used for P to C toggle)
    public void SwapTextWithAnimation(string newText, Action onComplete = null)
    {
        if (slideRoutine != null) StopCoroutine(slideRoutine);
        slideRoutine = StartCoroutine(SwapTextRoutine(newText, onComplete));
    }

    private IEnumerator SwapTextRoutine(string newText, Action onComplete)
    {
        if (button != null) button.interactable = false;

        // step 1: slide out to the right
        float timer = 0f;
        float startX = buttonRect.anchoredPosition.x;

        while (timer < slideOutDuration)
        {
            timer += Time.deltaTime;
            float progress = Mathf.Clamp01(timer / slideOutDuration);
            float curveVal = slideOutCurve.Evaluate(progress);

            float currentX = Mathf.Lerp(startX, outPosX, curveVal);
            buttonRect.anchoredPosition = new Vector2(currentX, defaultPosY);
            yield return null;
        }

        buttonRect.anchoredPosition = new Vector2(outPosX, defaultPosY);

        // step 2: update text while offscreen
        SetText(newText);
        yield return new WaitForSeconds(0.04f);

        // step 3: bounce back in
        isShown = true;
        timer = 0f;
        while (timer < slideInDuration)
        {
            timer += Time.deltaTime;
            float progress = Mathf.Clamp01(timer / slideInDuration);
            float curveVal = slideInBounceCurve.Evaluate(progress);

            float currentX = Mathf.LerpUnclamped(outPosX, inPosX, curveVal);
            buttonRect.anchoredPosition = new Vector2(currentX, defaultPosY);
            yield return null;
        }

        buttonRect.anchoredPosition = new Vector2(inPosX, defaultPosY);
        if (button != null) button.interactable = true;

        slideRoutine = null;
        onComplete?.Invoke();
    }

    public void SnapOut()
    {
        if (slideRoutine != null) StopCoroutine(slideRoutine);
        slideRoutine = null;
        isShown = false;

        if (buttonRect != null)
        {
            buttonRect.anchoredPosition = new Vector2(outPosX, defaultPosY);
        }

        if (button != null) button.interactable = false;
        gameObject.SetActive(false);
    }
}