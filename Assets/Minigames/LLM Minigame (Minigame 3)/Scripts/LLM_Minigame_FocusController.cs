using System.Collections;
using UnityEngine;

public class LLM_Minigame_FocusController : MonoBehaviour
{
    [Header("left side group")]
    [SerializeField] private RectTransform leftFocusGroup;
    [SerializeField] private float leftShiftDistance = 80f;

    [Header("animation settings")]
    [SerializeField] private float transitionDuration = 0.3f;
    [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private Vector2 leftDefaultPos;
    private Coroutine leftRoutine;

    public bool IsAnimating() => leftRoutine != null;

    private void Awake()
    {
        if (leftFocusGroup != null)
        {
            leftDefaultPos = leftFocusGroup.anchoredPosition;
        }
    }

    public void SnapToNPCFocus()
    {
        if (leftRoutine != null) StopCoroutine(leftRoutine);

        if (leftFocusGroup != null)
        {
            leftFocusGroup.anchoredPosition = leftDefaultPos;
        }
    }

    public void TransitionToPlayerFocus()
    {
        StartFocusTransition(true);
    }

    public void TransitionToNPCFocus()
    {
        StartFocusTransition(false);
    }

    private void StartFocusTransition(bool toPlayer)
    {
        if (leftRoutine != null) StopCoroutine(leftRoutine);
        leftRoutine = StartCoroutine(LeftSlideRoutine(toPlayer));
    }

    private IEnumerator LeftSlideRoutine(bool toPlayer)
    {
        Vector2 targetPos = toPlayer ? new Vector2(leftDefaultPos.x - leftShiftDistance, leftDefaultPos.y) : leftDefaultPos;
        Vector2 startPos = leftFocusGroup.anchoredPosition;

        float timer = 0f;
        while (timer < transitionDuration)
        {
            timer += Time.deltaTime;
            float progress = transitionCurve.Evaluate(Mathf.Clamp01(timer / transitionDuration));
            leftFocusGroup.anchoredPosition = Vector2.LerpUnclamped(startPos, targetPos, progress);
            yield return null;
        }

        leftFocusGroup.anchoredPosition = targetPos;
        leftRoutine = null;
    }
}