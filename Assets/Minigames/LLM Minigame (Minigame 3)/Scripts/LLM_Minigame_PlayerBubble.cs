using System;
using System.Collections;
using TMPro;
using UnityEngine;

public class LLM_Minigame_PlayerBubble : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private RectTransform bubbleRect;
    [SerializeField] private TextMeshProUGUI bubbleText;

    [Header("Timing Settings")]
    [SerializeField] private float minReadDuration = 1.8f;
    [SerializeField] private float readSecondsPerChar = 0.035f;

    [Header("Animation Settings")]
    [SerializeField] private float popInDuration = 0.22f;
    [SerializeField] private float popOutDuration = 0.16f;
    [SerializeField] private AnimationCurve popInCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private AnimationCurve popOutCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private Coroutine playRoutine;
    private Action onCompleteCallback;
    private bool isWaitingForAdvance = false;

    private void Awake()
    {
        if (bubbleRect == null)
        {
            bubbleRect = GetComponent<RectTransform>();
        }

        bubbleRect.localScale = Vector3.zero;
        gameObject.SetActive(false);
    }

    private void Update()
    {
        // click or enter skips the reading pause
        if (isWaitingForAdvance && (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Return)))
        {
            isWaitingForAdvance = false;
        }
    }

    public void ShowPlayerDialogue(string dialogue, Action onComplete)
    {
        onCompleteCallback = onComplete;

        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
        }

        bubbleText.text = dialogue;
        gameObject.SetActive(true);
        playRoutine = StartCoroutine(BubbleRoutine(dialogue));
    }

    private IEnumerator BubbleRoutine(string dialogue)
    {
        // pop in animation with curve
        float timer = 0f;
        while (timer < popInDuration)
        {
            timer += Time.deltaTime;
            float progress = popInCurve.Evaluate(Mathf.Clamp01(timer / popInDuration));
            bubbleRect.localScale = Vector3.one * progress;
            yield return null;
        }
        bubbleRect.localScale = Vector3.one;

        // calculate read time based on sentence length
        float readTime = Mathf.Max(minReadDuration, dialogue.Length * readSecondsPerChar);
        isWaitingForAdvance = true;

        timer = 0f;
        while (timer < readTime && isWaitingForAdvance)
        {
            timer += Time.deltaTime;
            yield return null;
        }
        isWaitingForAdvance = false;

        // pop out animation
        timer = 0f;
        while (timer < popOutDuration)
        {
            timer += Time.deltaTime;
            float progress = popOutCurve.Evaluate(Mathf.Clamp01(timer / popOutDuration));
            bubbleRect.localScale = Vector3.one * (1f - progress);
            yield return null;
        }

        bubbleRect.localScale = Vector3.zero;
        gameObject.SetActive(false);

        onCompleteCallback?.Invoke();
    }
}