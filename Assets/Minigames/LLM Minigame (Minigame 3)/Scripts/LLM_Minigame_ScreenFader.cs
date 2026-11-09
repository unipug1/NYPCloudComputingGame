using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class LLM_Minigame_ScreenFader : MonoBehaviour
{
    [SerializeField] private UnityEngine.UI.Image faderImage;
    [SerializeField] private float fadeDuration = 0.35f;
    [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private Coroutine fadeRoutine;

    private void Awake()
    {
        if (faderImage == null)
        {
            faderImage = GetComponent<UnityEngine.UI.Image>();
        }

        // start transparent and non blocking
        if (faderImage != null)
        {
            Color c = faderImage.color;
            c.a = 0f;
            faderImage.color = c;
            faderImage.raycastTarget = false;
        }
    }

    public void DoFullTransition(Action onBlack, Action onFinished = null)
    {
        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
        }

        fadeRoutine = StartCoroutine(TransitionRoutine(onBlack, onFinished));
    }

    private IEnumerator TransitionRoutine(Action onBlack, Action onFinished)
    {
        // block all button clicks and inputs
        faderImage.raycastTarget = true;
        Color c = faderImage.color;

        // fade to solid black
        float timer = 0f;
        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float progress = fadeCurve.Evaluate(Mathf.Clamp01(timer / fadeDuration));
            c.a = progress;
            faderImage.color = c;
            yield return null;
        }

        c.a = 1f;
        faderImage.color = c;

        // execute reset logic while screen is fully black
        onBlack?.Invoke();
        yield return new WaitForSeconds(0.1f);

        // fade back to transparent
        timer = 0f;
        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float progress = fadeCurve.Evaluate(Mathf.Clamp01(timer / fadeDuration));
            c.a = 1f - progress;
            faderImage.color = c;
            yield return null;
        }

        c.a = 0f;
        faderImage.color = c;
        faderImage.raycastTarget = false;

        onFinished?.Invoke();
    }
}