using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Image = UnityEngine.UI.Image;

public class LLM_Minigame_PropController : MonoBehaviour
{
    [System.Serializable]
    public class PropData
    {
        public string name;
        public RectTransform rect;
        public UnityEngine.UI.Image shadowOverlay;

        [Header("Available Pose (Player Turn - In Hand)")]
        public Vector3 availablePos;
        public float availableRotZ;
        public Vector3 availableScale = Vector3.one;

        [Header("Unavailable Pose (Guard Speaking - Off Focus)")]
        public Vector3 unavailablePos;
        public float unavailableRotZ;
        public Vector3 unavailableScale = Vector3.one;

        [Header("Switch Offscreen Pose (Swap Animation Exit/Entry)")]
        public Vector3 switchOffscreenPos;
        public float switchOffscreenRotZ;
        public Vector3 switchOffscreenScale = Vector3.one;
    }

    [Header("Props Configuration")]
    [SerializeField]
    private PropData clipboard = new PropData
    {
        name = "Clipboard",
        availablePos = new Vector3(13f, -65f, 0f),
        availableRotZ = 8f,
        availableScale = Vector3.one,

        unavailablePos = new Vector3(216f, -686f, 0f),
        unavailableRotZ = -8f,
        unavailableScale = new Vector3(0.1f, 0.1f, 1f),

        switchOffscreenPos = new Vector3(450f, -900f, 0f),
        switchOffscreenRotZ = -25f,
        switchOffscreenScale = Vector3.one
    };

    [SerializeField]
    private PropData phone = new PropData
    {
        name = "Phone",
        availablePos = new Vector3(-44f, -35f, 0f),
        availableRotZ = -7f,
        availableScale = Vector3.one,

        unavailablePos = new Vector3(242f, -383f, 0f),
        unavailableRotZ = -21f,
        unavailableScale = new Vector3(0.9f, 0.9f, 1f),

        switchOffscreenPos = new Vector3(450f, -900f, 0f),
        switchOffscreenRotZ = 20f,
        switchOffscreenScale = Vector3.one
    };

    [Header("Shadow Settings")]
    [SerializeField] private float dimAlpha = 0.5f;

    [Header("Focus Transition Settings (Dialogue / Turns)")]
    [SerializeField] private float focusDuration = 0.3f;
    [SerializeField] private AnimationCurve focusCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Switch Transition Settings (Prop Swapping)")]
    [SerializeField] private float switchOutDuration = 0.2f;
    [SerializeField] private float switchInDuration = 0.22f;
    [SerializeField] private AnimationCurve switchCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private bool isClipboardActive = true;
    private bool isPlayerTurn = false;
    private bool isSwitching = false;
    private Coroutine propRoutine;

    public bool IsClipboardActive()
    {
        return isClipboardActive;
    }

    public PropData GetCurrentProp()
    {
        return isClipboardActive ? clipboard : phone;
    }

    public void SetupInitialState()
    {
        isClipboardActive = true;
        isPlayerTurn = false;
        isSwitching = false;

        // clipboard starts in unavailable pose because guard speaks first
        clipboard.rect.gameObject.SetActive(true);
        ApplyPoseInstant(clipboard, clipboard.unavailablePos, clipboard.unavailableRotZ, clipboard.unavailableScale);
        SetShadowInstant(clipboard, dimAlpha, true);

        // phone is hidden completely
        phone.rect.gameObject.SetActive(false);
        ApplyPoseInstant(phone, phone.switchOffscreenPos, phone.switchOffscreenRotZ, phone.switchOffscreenScale);
        SetShadowInstant(phone, 0f, false);
    }

    // called when dialogue starts or finishes
    public void SetFocusState(bool toPlayerTurn)
    {
        isPlayerTurn = toPlayerTurn;

        if (isSwitching) return;

        PropData activeProp = GetCurrentProp();

        if (propRoutine != null)
        {
            StopCoroutine(propRoutine);
        }

        propRoutine = StartCoroutine(AnimatePropFocus(activeProp, toPlayerTurn));
    }

    private IEnumerator AnimatePropFocus(PropData prop, bool toAvailable)
    {
        Vector3 targetPos = toAvailable ? prop.availablePos : prop.unavailablePos;
        float targetRot = toAvailable ? prop.availableRotZ : prop.unavailableRotZ;
        Vector3 targetScale = toAvailable ? prop.availableScale : prop.unavailableScale;
        float targetAlpha = toAvailable ? 0f : dimAlpha;

        Vector3 startPos = prop.rect.anchoredPosition;
        float startRot = prop.rect.localEulerAngles.z;
        Vector3 startScale = prop.rect.localScale;
        float startAlpha = prop.shadowOverlay != null ? prop.shadowOverlay.color.a : 0f;

        if (prop.shadowOverlay != null)
        {
            prop.shadowOverlay.raycastTarget = !toAvailable;
        }

        float timer = 0f;
        while (timer < focusDuration)
        {
            timer += Time.deltaTime;
            float rawProgress = Mathf.Clamp01(timer / focusDuration);
            float progress = focusCurve.Evaluate(rawProgress);

            prop.rect.anchoredPosition = Vector3.LerpUnclamped(startPos, targetPos, progress);
            prop.rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpAngle(startRot, targetRot, progress));
            prop.rect.localScale = Vector3.LerpUnclamped(startScale, targetScale, progress);

            if (prop.shadowOverlay != null)
            {
                Color c = prop.shadowOverlay.color;
                c.a = Mathf.Lerp(startAlpha, targetAlpha, progress);
                prop.shadowOverlay.color = c;
            }

            yield return null;
        }

        ApplyPoseInstant(prop, targetPos, targetRot, targetScale);
        SetShadowInstant(prop, targetAlpha, !toAvailable);
    }

    // called when player clicks the toggle button
    public void ToggleProps(Action onComplete = null)
    {
        if (!isPlayerTurn || isSwitching) return;

        isSwitching = true;
        bool switchingToPhone = isClipboardActive;
        isClipboardActive = !isClipboardActive;

        PropData outgoing = switchingToPhone ? clipboard : phone;
        PropData incoming = switchingToPhone ? phone : clipboard;

        if (propRoutine != null)
        {
            StopCoroutine(propRoutine);
        }

        propRoutine = StartCoroutine(TwoPhaseSwapRoutine(outgoing, incoming, onComplete));
    }

    private IEnumerator TwoPhaseSwapRoutine(PropData outgoing, PropData incoming, Action onComplete)
    {
        // phase 1: outgoing prop switches out from available to its offscreen pose
        Vector3 outStartPos = outgoing.rect.anchoredPosition;
        float outStartRot = outgoing.rect.localEulerAngles.z;
        Vector3 outStartScale = outgoing.rect.localScale;

        float timer = 0f;
        while (timer < switchOutDuration)
        {
            timer += Time.deltaTime;
            float rawProgress = Mathf.Clamp01(timer / switchOutDuration);
            float progress = switchCurve.Evaluate(rawProgress);

            outgoing.rect.anchoredPosition = Vector3.LerpUnclamped(outStartPos, outgoing.switchOffscreenPos, progress);
            outgoing.rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpAngle(outStartRot, outgoing.switchOffscreenRotZ, progress));
            outgoing.rect.localScale = Vector3.LerpUnclamped(outStartScale, outgoing.switchOffscreenScale, progress);

            yield return null;
        }

        ApplyPoseInstant(outgoing, outgoing.switchOffscreenPos, outgoing.switchOffscreenRotZ, outgoing.switchOffscreenScale);
        outgoing.rect.gameObject.SetActive(false);

        // phase 2: incoming prop enters from its offscreen pose directly into available pose
        incoming.rect.gameObject.SetActive(true);
        ApplyPoseInstant(incoming, incoming.switchOffscreenPos, incoming.switchOffscreenRotZ, incoming.switchOffscreenScale);
        SetShadowInstant(incoming, 0f, false);

        timer = 0f;
        while (timer < switchInDuration)
        {
            timer += Time.deltaTime;
            float rawProgress = Mathf.Clamp01(timer / switchInDuration);
            float progress = switchCurve.Evaluate(rawProgress);

            incoming.rect.anchoredPosition = Vector3.LerpUnclamped(incoming.switchOffscreenPos, incoming.availablePos, progress);
            incoming.rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpAngle(incoming.switchOffscreenRotZ, incoming.availableRotZ, progress));
            incoming.rect.localScale = Vector3.LerpUnclamped(incoming.switchOffscreenScale, incoming.availableScale, progress);

            yield return null;
        }

        ApplyPoseInstant(incoming, incoming.availablePos, incoming.availableRotZ, incoming.availableScale);
        SetShadowInstant(incoming, 0f, false);

        isSwitching = false;
        onComplete?.Invoke();
    }

    private void ApplyPoseInstant(PropData prop, Vector3 pos, float rotZ, Vector3 scale)
    {
        if (prop.rect == null) return;
        prop.rect.anchoredPosition = pos;
        prop.rect.localRotation = Quaternion.Euler(0f, 0f, rotZ);
        prop.rect.localScale = scale;
    }

    private void SetShadowInstant(PropData prop, float alpha, bool blockClicks)
    {
        if (prop.shadowOverlay == null) return;
        Color c = prop.shadowOverlay.color;
        c.a = alpha;
        prop.shadowOverlay.color = c;
        prop.shadowOverlay.raycastTarget = blockClicks;
    }
}