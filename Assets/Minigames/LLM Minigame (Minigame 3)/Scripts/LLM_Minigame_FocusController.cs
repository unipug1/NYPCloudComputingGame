using System.Collections;
using UnityEngine;

public class LLM_Minigame_FocusController : MonoBehaviour
{
    [Header("Left Side (Character and Dialogue)")]
    [SerializeField] private RectTransform leftFocusGroup;
    [SerializeField] private float leftShiftDistance = 80f;

    [Header("Prop Controller Link")]
    [SerializeField] private LLM_Minigame_PropController propController;

    [Header("Animation Settings")]
    [SerializeField] private float transitionDuration = 0.3f;
    [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private Vector2 leftDefaultPos;
    private Coroutine leftRoutine;

    private void Awake()
    {
        if (leftFocusGroup != null)
        {
            leftDefaultPos = leftFocusGroup.anchoredPosition;
        }
    }

    public void SnapToNPCFocus()
    {
        if (leftRoutine != null)
        {
            StopCoroutine(leftRoutine);
        }

        if (leftFocusGroup != null)
        {
            leftFocusGroup.anchoredPosition = leftDefaultPos;
        }

        if (propController != null)
        {
            propController.SetupInitialState();
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
        // tell prop controller to move active prop to available or unavailable
        if (propController != null)
        {
            propController.SetFocusState(toPlayer);
        }

        // move left side
        if (leftRoutine != null)
        {
            StopCoroutine(leftRoutine);
        }
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
            float rawProgress = Mathf.Clamp01(timer / transitionDuration);
            float progress = transitionCurve.Evaluate(rawProgress);

            if (leftFocusGroup != null)
            {
                leftFocusGroup.anchoredPosition = Vector2.LerpUnclamped(startPos, targetPos, progress);
            }

            yield return null;
        }

        if (leftFocusGroup != null)
        {
            leftFocusGroup.anchoredPosition = targetPos;
        }
    }
}