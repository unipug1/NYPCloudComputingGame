using System;
using System.Collections;
using TMPro;
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
        public LLM_Minigame_PropMotionBlur motionBlur;

        [Header("available pose")]
        public Vector3 availablePos;
        public float availableRotZ;
        public Vector3 availableScale = Vector3.one;

        [Header("unavailable pose")]
        public Vector3 unavailablePos;
        public float unavailableRotZ;
        public Vector3 unavailableScale = Vector3.one;

        [Header("switch offscreen pose")]
        public Vector3 switchOffscreenPos;
        public float switchOffscreenRotZ;
        public Vector3 switchOffscreenScale = Vector3.one;
    }

    [Header("props configuration")]
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

    [Header("shadow settings")]
    [SerializeField] private float dimAlpha = 0.5f;

    [Header("animation settings")]
    [SerializeField] private float focusDuration = 0.35f;
    [SerializeField] private float switchOutDuration = 0.2f;
    [SerializeField] private float switchInDuration = 0.24f;
    [SerializeField] private AnimationCurve switchCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private AnimationCurve focusCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private bool isClipboardActive = true;
    private bool isSwitching = false;
    private Coroutine propRoutine;
    private Coroutine typeRoutine;

    public bool IsClipboardActive() => isClipboardActive;
    public PropData GetCurrentProp() => isClipboardActive ? clipboard : phone;
    public bool IsAnimating() => propRoutine != null || isSwitching;

    public void SetupInitialState()
    {
        StopCurrentRoutine();

        isClipboardActive = true;

        clipboard.rect.gameObject.SetActive(true);
        ApplyPoseInstant(clipboard, clipboard.unavailablePos, clipboard.unavailableRotZ, clipboard.unavailableScale);
        SetShadowInstant(clipboard, dimAlpha, true);

        phone.rect.gameObject.SetActive(false);
        ApplyPoseInstant(phone, phone.switchOffscreenPos, phone.switchOffscreenRotZ, phone.switchOffscreenScale);
        SetShadowInstant(phone, 0f, false);
    }

    private void StopCurrentRoutine()
    {
        if (propRoutine != null)
        {
            StopCoroutine(propRoutine);
            propRoutine = null;
        }

        isSwitching = false;

        if (clipboard.motionBlur != null) clipboard.motionBlur.ResetBlur();
        if (phone.motionBlur != null) phone.motionBlur.ResetBlur();
    }

    // moves prop from unavailable (dim on side) to active (in hand)
    public void TransitionUnavailableToActive(Action onComplete = null)
    {
        StopCurrentRoutine();

        PropData prop = GetCurrentProp();
        prop.rect.gameObject.SetActive(true);

        // play swap in sound
        PlayPropAudio(prop, isEntering: true);

        if (prop.motionBlur != null) prop.motionBlur.TriggerBlur(focusDuration);
        propRoutine = StartCoroutine(UnavailableToActiveRoutine(prop, onComplete));
    }

    private IEnumerator UnavailableToActiveRoutine(PropData prop, Action onComplete)
    {
        Vector3 startPos = prop.rect.anchoredPosition;
        float startRot = prop.rect.localEulerAngles.z;
        Vector3 startScale = prop.rect.localScale;

        float timer = 0f;
        while (timer < focusDuration)
        {
            timer += Time.deltaTime;
            float progress = focusCurve.Evaluate(Mathf.Clamp01(timer / focusDuration));

            prop.rect.anchoredPosition = Vector3.LerpUnclamped(startPos, prop.availablePos, progress);
            prop.rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpAngle(startRot, prop.availableRotZ, progress));
            prop.rect.localScale = Vector3.LerpUnclamped(startScale, prop.availableScale, progress);

            if (prop.shadowOverlay != null)
            {
                Color c = prop.shadowOverlay.color;
                c.a = Mathf.Lerp(dimAlpha, 0f, progress);
                prop.shadowOverlay.color = c;
            }

            yield return null;
        }

        ApplyPoseInstant(prop, prop.availablePos, prop.availableRotZ, prop.availableScale);
        SetShadowInstant(prop, 0f, false);

        propRoutine = null;
        onComplete?.Invoke();
    }

    // dims active prop and moves it aside (review mode)
    public void TransitionActiveToUnavailable(Action onComplete = null)
    {
        StopCurrentRoutine();

        PropData prop = GetCurrentProp();
        prop.rect.gameObject.SetActive(true);

        // play swap out sound
        PlayPropAudio(prop, isEntering: false);

        if (prop.motionBlur != null) prop.motionBlur.TriggerBlur(focusDuration);
        propRoutine = StartCoroutine(ActiveToUnavailableRoutine(prop, onComplete));
    }

    private IEnumerator ActiveToUnavailableRoutine(PropData prop, Action onComplete)
    {
        Vector3 startPos = prop.rect.anchoredPosition;
        float startRot = prop.rect.localEulerAngles.z;
        Vector3 startScale = prop.rect.localScale;

        if (prop.shadowOverlay != null) prop.shadowOverlay.raycastTarget = true;

        float timer = 0f;
        while (timer < focusDuration)
        {
            timer += Time.deltaTime;
            float progress = focusCurve.Evaluate(Mathf.Clamp01(timer / focusDuration));

            prop.rect.anchoredPosition = Vector3.LerpUnclamped(startPos, prop.unavailablePos, progress);
            prop.rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpAngle(startRot, prop.unavailableRotZ, progress));
            prop.rect.localScale = Vector3.LerpUnclamped(startScale, prop.unavailableScale, progress);

            if (prop.shadowOverlay != null)
            {
                Color c = prop.shadowOverlay.color;
                c.a = Mathf.Lerp(0f, dimAlpha, progress);
                prop.shadowOverlay.color = c;
            }

            yield return null;
        }

        ApplyPoseInstant(prop, prop.unavailablePos, prop.unavailableRotZ, prop.unavailableScale);
        SetShadowInstant(prop, dimAlpha, true);

        propRoutine = null;
        onComplete?.Invoke();
    }

    // exits prop completely offscreen (player submits answer)
    public void SwitchOutActiveProp(Action onComplete)
    {
        StopCurrentRoutine();

        PropData activeProp = GetCurrentProp();

        // play swap out sound
        PlayPropAudio(activeProp, isEntering: false);

        if (activeProp.rect.gameObject.activeInHierarchy && activeProp.motionBlur != null)
        {
            activeProp.motionBlur.TriggerBlur(switchOutDuration);
        }

        propRoutine = StartCoroutine(SwitchOutRoutine(activeProp, onComplete));
    }

    private IEnumerator SwitchOutRoutine(PropData prop, Action onComplete)
    {
        Vector3 startPos = prop.rect.anchoredPosition;
        float startRot = prop.rect.localEulerAngles.z;
        Vector3 startScale = prop.rect.localScale;

        float timer = 0f;
        while (timer < switchOutDuration)
        {
            timer += Time.deltaTime;
            float progress = switchCurve.Evaluate(Mathf.Clamp01(timer / switchOutDuration));

            prop.rect.anchoredPosition = Vector3.LerpUnclamped(startPos, prop.switchOffscreenPos, progress);
            prop.rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpAngle(startRot, prop.switchOffscreenRotZ, progress));
            prop.rect.localScale = Vector3.LerpUnclamped(startScale, prop.switchOffscreenScale, progress);

            yield return null;
        }

        ApplyPoseInstant(prop, prop.switchOffscreenPos, prop.switchOffscreenRotZ, prop.switchOffscreenScale);
        prop.rect.gameObject.SetActive(false);

        propRoutine = null;
        onComplete?.Invoke();
    }

    // re-enters prop from offscreen into active pose (after bubble dismisses)
    public void SwitchInActiveProp(Action onComplete = null)
    {
        StopCurrentRoutine();

        PropData activeProp = GetCurrentProp();
        activeProp.rect.gameObject.SetActive(true);

        // play swap in sound
        PlayPropAudio(activeProp, isEntering: true);

        if (activeProp.motionBlur != null) activeProp.motionBlur.TriggerBlur(switchInDuration);
        propRoutine = StartCoroutine(SwitchInRoutine(activeProp, onComplete));
    }

    private IEnumerator SwitchInRoutine(PropData prop, Action onComplete)
    {
        ApplyPoseInstant(prop, prop.switchOffscreenPos, prop.switchOffscreenRotZ, prop.switchOffscreenScale);
        SetShadowInstant(prop, 0f, false);

        float timer = 0f;
        while (timer < switchInDuration)
        {
            timer += Time.deltaTime;
            float progress = switchCurve.Evaluate(Mathf.Clamp01(timer / switchInDuration));

            prop.rect.anchoredPosition = Vector3.LerpUnclamped(prop.switchOffscreenPos, prop.availablePos, progress);
            prop.rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpAngle(prop.switchOffscreenRotZ, prop.availableRotZ, progress));
            prop.rect.localScale = Vector3.LerpUnclamped(prop.switchOffscreenScale, prop.availableScale, progress);
            yield return null;
        }

        ApplyPoseInstant(prop, prop.availablePos, prop.availableRotZ, prop.availableScale);
        SetShadowInstant(prop, 0f, false);

        propRoutine = null;
        onComplete?.Invoke();
    }

    // swaps between clipboard and phone (Tab or top-right button)
    public void ToggleProps(Action onComplete = null)
    {
        if (IsAnimating()) return;

        isSwitching = true;
        bool switchingToPhone = isClipboardActive;
        isClipboardActive = !isClipboardActive;

        PropData outgoing = switchingToPhone ? clipboard : phone;
        PropData incoming = switchingToPhone ? phone : clipboard;

        incoming.rect.gameObject.SetActive(true);

        // play outgoing audio immediately
        PlayPropAudio(outgoing, isEntering: false);

        if (outgoing.rect.gameObject.activeInHierarchy && outgoing.motionBlur != null)
        {
            outgoing.motionBlur.TriggerBlur(switchOutDuration);
        }

        if (incoming.motionBlur != null)
        {
            incoming.motionBlur.TriggerBlur(switchInDuration);
        }

        if (propRoutine != null) StopCoroutine(propRoutine);
        propRoutine = StartCoroutine(TwoPhaseSwapRoutine(outgoing, incoming, onComplete));
    }

    private IEnumerator TwoPhaseSwapRoutine(PropData outgoing, PropData incoming, Action onComplete)
    {
        Vector3 outStartPos = outgoing.rect.anchoredPosition;
        float outStartRot = outgoing.rect.localEulerAngles.z;
        Vector3 outStartScale = outgoing.rect.localScale;

        float timer = 0f;
        while (timer < switchOutDuration)
        {
            timer += Time.deltaTime;
            float progress = switchCurve.Evaluate(Mathf.Clamp01(timer / switchOutDuration));

            outgoing.rect.anchoredPosition = Vector3.LerpUnclamped(outStartPos, outgoing.switchOffscreenPos, progress);
            outgoing.rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpAngle(outStartRot, outgoing.switchOffscreenRotZ, progress));
            outgoing.rect.localScale = Vector3.LerpUnclamped(outStartScale, outgoing.switchOffscreenScale, progress);
            yield return null;
        }

        ApplyPoseInstant(outgoing, outgoing.switchOffscreenPos, outgoing.switchOffscreenRotZ, outgoing.switchOffscreenScale);
        outgoing.rect.gameObject.SetActive(false);

        // play incoming audio as phase 2 starts
        PlayPropAudio(incoming, isEntering: true);

        ApplyPoseInstant(incoming, incoming.switchOffscreenPos, incoming.switchOffscreenRotZ, incoming.switchOffscreenScale);
        SetShadowInstant(incoming, 0f, false);

        timer = 0f;
        while (timer < switchInDuration)
        {
            timer += Time.deltaTime;
            float progress = switchCurve.Evaluate(Mathf.Clamp01(timer / switchInDuration));

            incoming.rect.anchoredPosition = Vector3.LerpUnclamped(incoming.switchOffscreenPos, incoming.availablePos, progress);
            incoming.rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpAngle(incoming.switchOffscreenRotZ, incoming.availableRotZ, progress));
            incoming.rect.localScale = Vector3.LerpUnclamped(incoming.switchOffscreenScale, incoming.availableScale, progress);
            yield return null;
        }

        ApplyPoseInstant(incoming, incoming.availablePos, incoming.availableRotZ, incoming.availableScale);
        SetShadowInstant(incoming, 0f, false);

        isSwitching = false;
        propRoutine = null;
        onComplete?.Invoke();
    }

    public void SelectPhonePresetToClipboard(string fullText, TMP_InputField inputField, Action onFinished = null)
    {
        if (IsAnimating()) return;

        ToggleProps(() =>
        {
            if (typeRoutine != null) StopCoroutine(typeRoutine);
            typeRoutine = StartCoroutine(RapidTypeRoutine(fullText, inputField, onFinished));
        });
    }

    private IEnumerator RapidTypeRoutine(string text, TMP_InputField inputField, Action onFinished)
    {
        inputField.text = "";
        inputField.interactable = false;

        for (int i = 0; i < text.Length; i++)
        {
            inputField.text += text[i];
            inputField.caretPosition = inputField.text.Length;

            // plays throttled keystroke at a comfortable background volume
            LLM_Minigame_AudioManager.Instance?.PlayKeystroke(0.22f);

            yield return new WaitForSeconds(0.015f);
        }

        inputField.interactable = true;
        inputField.ActivateInputField();
        onFinished?.Invoke();
    }

    private void PlayPropAudio(PropData prop, bool isEntering)
    {
        if (prop == clipboard)
        {
            if (isEntering) LLM_Minigame_AudioManager.Instance?.PlayClipboardSwapIn();
            else LLM_Minigame_AudioManager.Instance?.PlayClipboardSwapOut();
        }
        else if (prop == phone)
        {
            if (isEntering) LLM_Minigame_AudioManager.Instance?.PlayPhoneSwapIn();
            else LLM_Minigame_AudioManager.Instance?.PlayPhoneSwapOut();
        }
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