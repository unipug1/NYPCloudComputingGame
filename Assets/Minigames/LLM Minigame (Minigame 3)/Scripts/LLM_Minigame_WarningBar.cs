using System;
using System.Collections;
using UnityEngine;

public class LLM_Minigame_WarningBar : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private RectTransform bannerRect;

    [Header("Line Settings")]
    [SerializeField] private float initialLineHeightY = 0.08f;
    [SerializeField] private float holdDuration = 2.0f;

    [Header("Animation Durations")]
    [SerializeField] private float expandXDuration = 0.25f;
    [SerializeField] private float expandYDuration = 0.22f;
    [SerializeField] private float contractYDuration = 0.18f;
    [SerializeField] private float contractXDuration = 0.22f;

    [Header("Curves")]
    [SerializeField] private AnimationCurve animCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private Coroutine playRoutine;
    private Action onFinishCallback;
    private bool isHolding = false;
    private bool skipRequested = false;

    private void Awake()
    {
        if (bannerRect == null)
        {
            bannerRect = GetComponent<RectTransform>();
        }

        bannerRect.localScale = Vector3.zero;
        gameObject.SetActive(false);
    }

    private void Update()
    {
        // skip is only registered if the banner is in the middle hold phase
        if (isHolding && (Input.anyKeyDown || Input.GetMouseButtonDown(0)))
        {
            skipRequested = true;
            isHolding = false;
            LLM_Minigame_AudioManager.Instance?.PlayButtonClick();
        }
    }

    public void PlayWarning(Action onComplete)
    {
        onFinishCallback = onComplete;
        isHolding = false;
        skipRequested = false;

        LLM_Minigame_AudioManager.Instance?.PlayWarningBar();

        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
        }

        gameObject.SetActive(true);
        playRoutine = StartCoroutine(WarningSequenceRoutine());
    }

    private IEnumerator WarningSequenceRoutine()
    {
        // start animation part 1: horizontal expand (unskippable)
        bannerRect.localScale = new Vector3(0f, initialLineHeightY, 1f);
        float timer = 0f;
        while (timer < expandXDuration)
        {
            timer += Time.deltaTime;
            float progress = animCurve.Evaluate(Mathf.Clamp01(timer / expandXDuration));
            bannerRect.localScale = new Vector3(progress, initialLineHeightY, 1f);
            yield return null;
        }

        // start animation part 2: vertical expand (unskippable)
        timer = 0f;
        while (timer < expandYDuration)
        {
            timer += Time.deltaTime;
            float progress = animCurve.Evaluate(Mathf.Clamp01(timer / expandYDuration));
            float currentY = Mathf.Lerp(initialLineHeightY, 1f, progress);
            bannerRect.localScale = new Vector3(1f, currentY, 1f);
            yield return null;
        }
        bannerRect.localScale = Vector3.one;

        // middle phase: hold (skippable here)
        isHolding = true;
        skipRequested = false;
        timer = 0f;
        while (timer < holdDuration && !skipRequested)
        {
            timer += Time.deltaTime;
            yield return null;
        }
        isHolding = false;

        // end animation part 1: vertical contract (unskippable)
        timer = 0f;
        while (timer < contractYDuration)
        {
            timer += Time.deltaTime;
            float progress = animCurve.Evaluate(Mathf.Clamp01(timer / contractYDuration));
            float currentY = Mathf.Lerp(1f, initialLineHeightY, progress);
            bannerRect.localScale = new Vector3(1f, currentY, 1f);
            yield return null;
        }

        // end animation part 2: horizontal contract to zero (unskippable)
        timer = 0f;
        while (timer < contractXDuration)
        {
            timer += Time.deltaTime;
            float progress = animCurve.Evaluate(Mathf.Clamp01(timer / contractXDuration));
            float currentX = Mathf.Lerp(1f, 0f, progress);
            bannerRect.localScale = new Vector3(currentX, initialLineHeightY, 1f);
            yield return null;
        }

        bannerRect.localScale = Vector3.zero;
        gameObject.SetActive(false);

        // officially trigger game start only after end animation completes
        onFinishCallback?.Invoke();
    }
}