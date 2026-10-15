using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.Events;
using TMPro;

// explicit aliases prevent ambiguous reference errors
using Image = UnityEngine.UI.Image;
using Debug = UnityEngine.Debug;
using Application = UnityEngine.Application;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    [Header("Cinematic Box & Root")]
    [SerializeField] private GameObject cinematicRoot;
    [SerializeField] private CanvasGroup cinematicCanvasGroup;
    [SerializeField] private RectTransform cinematicRootRect;
    [SerializeField] private RectTransform dialogueBoxRect;
    [SerializeField] private float cinematicSlideOffsetY = 80f;
    [SerializeField] private float cinematicTransitionDuration = 0.22f;
    [SerializeField] private AnimationCurve cinematicSlideCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Text References")]
    [SerializeField] private TMP_Text cinematicNameText;
    [SerializeField] private TMP_Text cinematicSentenceText;

    [Header("Portrait (Normal & Dark)")]
    [SerializeField] private Image cinematicPortraitImage;
    [SerializeField] private RectTransform cinematicPortraitRect;
    [SerializeField] private Image portraitDarkImage;
    [SerializeField] private float targetDarkAlpha = 0.65f;
    [SerializeField] private float portraitSlideDistance = 24f;
    [SerializeField] private float portraitAnimDuration = 0.18f;
    [SerializeField] private float portraitDarkFadeDuration = 0.2f;
    [SerializeField] private AnimationCurve portraitSlideCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private AnimationCurve portraitDarkCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Portrait Shadow Animation")]
    [SerializeField] private Shadow portraitShadow;
    [SerializeField] private Vector2 targetShadowDistance = new Vector2(-20f, -20f);
    [SerializeField] private float shadowAnimDuration = 0.16f;
    [SerializeField] private AnimationCurve shadowCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Continue Prompt (Looping Movement)")]
    [SerializeField] private GameObject cinematicContinuePrompt;
    [SerializeField] private RectTransform continuePromptRect;
    [SerializeField] private float promptBobDistanceX = 12f;
    [SerializeField] private float promptBobSpeed = 2.5f;
    [SerializeField] private AnimationCurve promptBobCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Choice Buttons (Staggered Entrance)")]
    [SerializeField] private GameObject choiceContainer;
    [SerializeField] private Button[] choiceButtons;
    [SerializeField] private TMP_Text[] choiceButtonTexts;
    [SerializeField] private float choiceSlideDistanceX = 60f;
    [SerializeField] private float choiceAnimDuration = 0.22f;
    [SerializeField] private float choiceStaggerDelay = 0.08f;
    [SerializeField] private AnimationCurve choiceEntranceCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("HUD Elements to Hide")]
    [SerializeField] private List<GameObject> hudElementsToHide = new List<GameObject>();

    [Header("Player Lock Event")]
    public UnityEvent<bool> onDialogueStateChanged = new UnityEvent<bool>();

    [Tooltip("Enable when the UI has Continue/Close buttons; other scenes keep click-anywhere input.")]
    [SerializeField] private bool pointerInputHandledByButtons;

    private List<DialogueLine> currentLines = new List<DialogueLine>();
    private int currentLineIndex = 0;
    private bool isDialogueRunning = false;
    private bool isDialogueClosing;
    private int dialogueStartedFrame = -1;
    private int lastAdvanceFrame = -1;
    private bool isTyping = false;
    private bool isWaitingForChoice = false;
    private System.Action onCompleteCallback;

    private Coroutine typingRoutine;
    private Coroutine entryRoutine;
    private Coroutine portraitAnimRoutine;
    private Coroutine portraitDarkRoutine;
    private Coroutine promptBobRoutine;
    private List<Coroutine> choiceAnimRoutines = new List<Coroutine>();

    private Sprite lastDisplayedPortrait = null;
    private Vector2 cinematicRestingPos;
    private Vector2 dialogueBoxRestingPos;
    private Vector2 portraitRestingPos;
    private Vector2 promptRestingPos;
    private Vector2[] choiceRestingPositions;
    private CanvasGroup[] choiceCanvasGroups;
    private bool hasSavedPositions = false;

    public bool IsDialogueRunning => isDialogueRunning || isDialogueClosing;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        // auto detect shadow component on portrait if unassigned
        if (portraitShadow == null && cinematicPortraitImage != null)
        {
            portraitShadow = cinematicPortraitImage.GetComponent<Shadow>();
        }

        SaveRestingPositions();
    }

    private void SaveRestingPositions()
    {
        if (hasSavedPositions) return;

        if (cinematicRootRect != null) cinematicRestingPos = cinematicRootRect.anchoredPosition;
        if (dialogueBoxRect != null) dialogueBoxRestingPos = dialogueBoxRect.anchoredPosition;
        if (cinematicPortraitRect != null) portraitRestingPos = cinematicPortraitRect.anchoredPosition;
        if (continuePromptRect != null) promptRestingPos = continuePromptRect.anchoredPosition;

        if (choiceButtons != null && choiceButtons.Length > 0)
        {
            choiceRestingPositions = new Vector2[choiceButtons.Length];
            choiceCanvasGroups = new CanvasGroup[choiceButtons.Length];

            for (int i = 0; i < choiceButtons.Length; i++)
            {
                if (choiceButtons[i] != null)
                {
                    RectTransform rect = choiceButtons[i].GetComponent<RectTransform>();
                    if (rect != null) choiceRestingPositions[i] = rect.anchoredPosition;

                    CanvasGroup cg = choiceButtons[i].GetComponent<CanvasGroup>();
                    if (cg == null) cg = choiceButtons[i].gameObject.AddComponent<CanvasGroup>();
                    choiceCanvasGroups[i] = cg;
                }
            }
        }

        hasSavedPositions = true;
    }

    private void Start()
    {
        HideAllDialogueUI();
    }

    private void HideAllDialogueUI()
    {
        if (cinematicRoot != null) cinematicRoot.SetActive(false);
        if (cinematicPortraitImage != null) cinematicPortraitImage.gameObject.SetActive(false);
        if (portraitDarkImage != null) portraitDarkImage.gameObject.SetActive(false);
        if (choiceContainer != null) choiceContainer.SetActive(false);
        if (portraitShadow != null) portraitShadow.effectDistance = Vector2.zero;
        SetContinuePromptActive(false);
    }

    private void Update()
    {
        if (!isDialogueRunning || isWaitingForChoice) return;

        bool advancePressed = false;
        if (Keyboard.current != null)
        {
            advancePressed = Keyboard.current.eKey.wasPressedThisFrame ||
                             Keyboard.current.fKey.wasPressedThisFrame ||
                             Keyboard.current.spaceKey.wasPressedThisFrame;
        }

        if (!advancePressed && !pointerInputHandledByButtons && Mouse.current != null)
        {
            advancePressed = Mouse.current.leftButton.wasPressedThisFrame;
        }

        if (advancePressed)
            AdvanceDialogue();
    }

    // Keyboard input and the Continue button share the same once-per-frame action.
    public void AdvanceDialogue()
    {
        if (!isDialogueRunning || isWaitingForChoice
            || Time.frameCount == dialogueStartedFrame || Time.frameCount == lastAdvanceFrame)
            return;

        lastAdvanceFrame = Time.frameCount;
        if (isTyping)
            CompleteSentenceInstantly();
        else
            AdvanceToNextLine();
    }

    public void StartDialogue(List<DialogueLine> lines, System.Action onComplete = null)
    {
        if (IsDialogueRunning || lines == null || lines.Count == 0) return;

        SaveRestingPositions();

        isDialogueRunning = true;
        dialogueStartedFrame = Time.frameCount;
        currentLines = lines;
        currentLineIndex = 0;
        onCompleteCallback = onComplete;
        lastDisplayedPortrait = null;

        onDialogueStateChanged?.Invoke(true);
        SetHUDVisibility(false);

        if (cinematicRoot != null) cinematicRoot.SetActive(true);
        if (cinematicCanvasGroup != null) cinematicCanvasGroup.alpha = 1f;

        // master root stays at resting position so sprite does not slide up
        if (cinematicRootRect != null) cinematicRootRect.anchoredPosition = cinematicRestingPos;

        // only the dialogue box slides up from below
        entryRoutine = StartCoroutine(AnimateDialogueBoxEnter());
        DisplayLine(currentLineIndex);
    }

    private void DisplayLine(int index)
    {
        if (index < 0 || index >= currentLines.Count)
        {
            EndDialogue();
            return;
        }

        SetContinuePromptActive(false);
        FadeDarkPortrait(false);

        DialogueLine line = currentLines[index];

        if (cinematicNameText != null) cinematicNameText.text = line.speakerName;
        UpdateCinematicPortrait(line.speakerPortrait);

        if (typingRoutine != null) StopCoroutine(typingRoutine);
        typingRoutine = StartCoroutine(TypeSentenceRoutine(line));
    }

    // dialogue box slides up alone on start
    private IEnumerator AnimateDialogueBoxEnter()
    {
        RectTransform targetRect = dialogueBoxRect != null ? dialogueBoxRect : cinematicRootRect;
        if (targetRect == null) yield break;

        Vector2 startPos = dialogueBoxRestingPos + new Vector2(0f, -cinematicSlideOffsetY);
        Vector2 targetPos = dialogueBoxRestingPos;

        targetRect.anchoredPosition = startPos;
        float elapsed = 0f;

        while (elapsed < cinematicTransitionDuration)
        {
            elapsed += Time.deltaTime;
            float progress = cinematicSlideCurve.Evaluate(Mathf.Clamp01(elapsed / cinematicTransitionDuration));
            targetRect.anchoredPosition = Vector2.LerpUnclamped(startPos, targetPos, progress);
            yield return null;
        }

        targetRect.anchoredPosition = targetPos;
    }

    // dialogue box and sprite both slide down together on end
    private IEnumerator AnimateCinematicExit(System.Action onFinished)
    {
        if (cinematicRootRect == null)
        {
            onFinished?.Invoke();
            yield break;
        }

        Vector2 startPos = cinematicRestingPos;
        Vector2 targetPos = cinematicRestingPos + new Vector2(0f, -cinematicSlideOffsetY);

        float elapsed = 0f;
        while (elapsed < cinematicTransitionDuration)
        {
            elapsed += Time.deltaTime;
            float progress = cinematicSlideCurve.Evaluate(Mathf.Clamp01(elapsed / cinematicTransitionDuration));

            cinematicRootRect.anchoredPosition = Vector2.LerpUnclamped(startPos, targetPos, progress);
            if (cinematicCanvasGroup != null) cinematicCanvasGroup.alpha = Mathf.Lerp(1f, 0f, progress);

            yield return null;
        }

        cinematicRootRect.anchoredPosition = cinematicRestingPos;
        if (dialogueBoxRect != null) dialogueBoxRect.anchoredPosition = dialogueBoxRestingPos;
        if (cinematicCanvasGroup != null) cinematicCanvasGroup.alpha = 0f;

        onFinished?.Invoke();
    }

    private void UpdateCinematicPortrait(Sprite newPortrait)
    {
        if (cinematicPortraitImage == null) return;

        if (newPortrait == null)
        {
            cinematicPortraitImage.gameObject.SetActive(false);
            if (portraitDarkImage != null) portraitDarkImage.gameObject.SetActive(false);
            if (portraitShadow != null) portraitShadow.effectDistance = Vector2.zero;
            lastDisplayedPortrait = null;
            return;
        }

        cinematicPortraitImage.gameObject.SetActive(true);

        if (portraitDarkImage != null)
        {
            portraitDarkImage.sprite = newPortrait;
            portraitDarkImage.gameObject.SetActive(true);
        }

        // only animate portrait and shadow when a new portrait appears
        if (newPortrait != lastDisplayedPortrait)
        {
            cinematicPortraitImage.sprite = newPortrait;
            lastDisplayedPortrait = newPortrait;

            if (portraitAnimRoutine != null) StopCoroutine(portraitAnimRoutine);
            portraitAnimRoutine = StartCoroutine(AnimatePortraitAndShadowEntry());
        }
    }

    // portrait moves left to right, THEN shadow effect distance moves to -20 -20
    private IEnumerator AnimatePortraitAndShadowEntry()
    {
        if (cinematicPortraitRect == null || cinematicPortraitImage == null) yield break;

        if (!hasSavedPositions)
        {
            portraitRestingPos = cinematicPortraitRect.anchoredPosition;
        }

        // reset shadow to zero before sliding in
        if (portraitShadow != null)
        {
            portraitShadow.effectDistance = Vector2.zero;
        }

        Vector2 startPos = portraitRestingPos + new Vector2(-portraitSlideDistance, 0f);
        Vector2 targetPos = portraitRestingPos;

        cinematicPortraitRect.anchoredPosition = startPos;
        Color c = cinematicPortraitImage.color;
        c.a = 0f;
        cinematicPortraitImage.color = c;

        // step 1: portrait slides left to right
        float elapsed = 0f;
        while (elapsed < portraitAnimDuration)
        {
            elapsed += Time.deltaTime;
            float progress = portraitSlideCurve.Evaluate(Mathf.Clamp01(elapsed / portraitAnimDuration));

            cinematicPortraitRect.anchoredPosition = Vector2.LerpUnclamped(startPos, targetPos, progress);
            c.a = Mathf.Lerp(0f, 1f, progress);
            cinematicPortraitImage.color = c;

            yield return null;
        }

        cinematicPortraitRect.anchoredPosition = targetPos;
        c.a = 1f;
        cinematicPortraitImage.color = c;

        // step 2: sprite is in place, now smoothly animate shadow effect distance to target
        if (portraitShadow != null)
        {
            float shadowElapsed = 0f;
            while (shadowElapsed < shadowAnimDuration)
            {
                shadowElapsed += Time.deltaTime;
                float progress = shadowCurve.Evaluate(Mathf.Clamp01(shadowElapsed / shadowAnimDuration));
                portraitShadow.effectDistance = Vector2.LerpUnclamped(Vector2.zero, targetShadowDistance, progress);
                yield return null;
            }

            portraitShadow.effectDistance = targetShadowDistance;
        }

        portraitAnimRoutine = null;
    }

    private void FadeDarkPortrait(bool showDark)
    {
        if (portraitDarkImage == null) return;

        if (portraitDarkRoutine != null)
        {
            StopCoroutine(portraitDarkRoutine);
        }

        portraitDarkRoutine = StartCoroutine(PortraitDarkFadeRoutine(showDark ? targetDarkAlpha : 0f));
    }

    private IEnumerator PortraitDarkFadeRoutine(float targetAlpha)
    {
        Color c = portraitDarkImage.color;
        float startAlpha = c.a;
        float elapsed = 0f;

        while (elapsed < portraitDarkFadeDuration)
        {
            elapsed += Time.deltaTime;
            float progress = portraitDarkCurve.Evaluate(Mathf.Clamp01(elapsed / portraitDarkFadeDuration));
            c.a = Mathf.Lerp(startAlpha, targetAlpha, progress);
            portraitDarkImage.color = c;
            yield return null;
        }

        c.a = targetAlpha;
        portraitDarkImage.color = c;
        portraitDarkRoutine = null;
    }

    private IEnumerator TypeSentenceRoutine(DialogueLine line)
    {
        isTyping = true;
        cinematicSentenceText.text = line.sentence;
        cinematicSentenceText.maxVisibleCharacters = 0;

        cinematicSentenceText.ForceMeshUpdate();
        int totalVisibleCharacters = cinematicSentenceText.textInfo.characterCount;

        float speed = line.typingSpeed > 0f ? line.typingSpeed : 0.03f;
        int currentVisible = 0;

        while (currentVisible < totalVisibleCharacters)
        {
            currentVisible++;
            cinematicSentenceText.maxVisibleCharacters = currentVisible;
            yield return new WaitForSeconds(speed);
        }

        isTyping = false;
        typingRoutine = null;

        line.onLineEndEvent?.Invoke();

        if (line.hasChoices && line.choices != null && line.choices.Count > 0)
        {
            ShowChoices(line);
        }
        else
        {
            SetContinuePromptActive(true);
        }
    }

    private void CompleteSentenceInstantly()
    {
        if (typingRoutine != null) StopCoroutine(typingRoutine);

        DialogueLine line = currentLines[currentLineIndex];
        cinematicSentenceText.text = line.sentence;
        cinematicSentenceText.ForceMeshUpdate();
        cinematicSentenceText.maxVisibleCharacters = cinematicSentenceText.textInfo.characterCount;

        isTyping = false;
        typingRoutine = null;

        line.onLineEndEvent?.Invoke();

        if (line.hasChoices && line.choices != null && line.choices.Count > 0)
        {
            ShowChoices(line);
        }
        else
        {
            SetContinuePromptActive(true);
        }
    }

    private void AdvanceToNextLine()
    {
        SetContinuePromptActive(false);
        currentLineIndex++;
        DisplayLine(currentLineIndex);
    }

    private void ShowChoices(DialogueLine line)
    {
        isWaitingForChoice = true;
        SetContinuePromptActive(false);
        FadeDarkPortrait(true);

        if (choiceContainer != null) choiceContainer.SetActive(true);

        for (int i = 0; i < choiceAnimRoutines.Count; i++)
        {
            if (choiceAnimRoutines[i] != null) StopCoroutine(choiceAnimRoutines[i]);
        }
        choiceAnimRoutines.Clear();

        for (int i = 0; i < choiceButtons.Length; i++)
        {
            if (i < line.choices.Count)
            {
                int targetIndex = line.choices[i].jumpToLineIndex;
                choiceButtons[i].gameObject.SetActive(true);

                if (i < choiceButtonTexts.Length && choiceButtonTexts[i] != null)
                {
                    choiceButtonTexts[i].text = line.choices[i].choiceText;
                }

                choiceButtons[i].onClick.RemoveAllListeners();
                choiceButtons[i].onClick.AddListener(() => OnChoiceSelected(targetIndex));

                RectTransform rect = choiceButtons[i].GetComponent<RectTransform>();
                CanvasGroup cg = (choiceCanvasGroups != null && i < choiceCanvasGroups.Length) ? choiceCanvasGroups[i] : null;
                Vector2 restPos = (choiceRestingPositions != null && i < choiceRestingPositions.Length) ? choiceRestingPositions[i] : rect.anchoredPosition;

                float delay = i * choiceStaggerDelay;
                choiceAnimRoutines.Add(StartCoroutine(AnimateSingleChoiceButton(rect, cg, restPos, delay)));
            }
            else
            {
                choiceButtons[i].gameObject.SetActive(false);
            }
        }
    }

    private IEnumerator AnimateSingleChoiceButton(RectTransform rect, CanvasGroup cg, Vector2 targetPos, float delay)
    {
        Vector2 startPos = targetPos - new Vector2(choiceSlideDistanceX, 0f);

        if (rect != null) rect.anchoredPosition = startPos;
        if (cg != null) cg.alpha = 0f;

        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        float elapsed = 0f;
        while (elapsed < choiceAnimDuration)
        {
            elapsed += Time.deltaTime;
            float progress = choiceEntranceCurve.Evaluate(Mathf.Clamp01(elapsed / choiceAnimDuration));

            if (rect != null) rect.anchoredPosition = Vector2.LerpUnclamped(startPos, targetPos, progress);
            if (cg != null) cg.alpha = Mathf.Lerp(0f, 1f, progress);

            yield return null;
        }

        if (rect != null) rect.anchoredPosition = targetPos;
        if (cg != null) cg.alpha = 1f;
    }

    private void OnChoiceSelected(int jumpToIndex)
    {
        if (!isDialogueRunning || !isWaitingForChoice) return;

        isWaitingForChoice = false;
        if (choiceContainer != null) choiceContainer.SetActive(false);
        FadeDarkPortrait(false);

        if (jumpToIndex >= 0 && jumpToIndex < currentLines.Count)
        {
            currentLineIndex = jumpToIndex;
        }
        else
        {
            currentLineIndex++;
        }

        DisplayLine(currentLineIndex);
    }

    private void SetContinuePromptActive(bool active)
    {
        if (cinematicContinuePrompt == null) return;

        if (promptBobRoutine != null)
        {
            StopCoroutine(promptBobRoutine);
            promptBobRoutine = null;
        }

        cinematicContinuePrompt.SetActive(active);

        if (active && continuePromptRect != null)
        {
            promptBobRoutine = StartCoroutine(ContinuePromptBobRoutine());
        }
        else if (continuePromptRect != null)
        {
            continuePromptRect.anchoredPosition = promptRestingPos;
        }
    }

    private IEnumerator ContinuePromptBobRoutine()
    {
        float timer = 0f;

        while (true)
        {
            timer += Time.deltaTime * promptBobSpeed;

            float pingPong = Mathf.PingPong(timer, 1f);
            float progress = promptBobCurve.Evaluate(pingPong);

            float offsetX = Mathf.Lerp(-promptBobDistanceX, promptBobDistanceX, progress);
            continuePromptRect.anchoredPosition = new Vector2(promptRestingPos.x + offsetX, promptRestingPos.y);

            yield return null;
        }
    }

    public void EndDialogue()
    {
        if (!isDialogueRunning) return;

        isDialogueRunning = false;
        isDialogueClosing = true;
        isTyping = false;
        isWaitingForChoice = false;

        if (typingRoutine != null)
        {
            StopCoroutine(typingRoutine);
            typingRoutine = null;
        }
        if (entryRoutine != null)
        {
            StopCoroutine(entryRoutine);
            entryRoutine = null;
        }

        SetContinuePromptActive(false);
        FadeDarkPortrait(false);

        // both sprite and dialogue box slide down together into the floor
        StartCoroutine(AnimateCinematicExit(() =>
        {
            HideAllDialogueUI();
            isDialogueClosing = false;
            onDialogueStateChanged?.Invoke(false);
            SetHUDVisibility(true);
            onCompleteCallback?.Invoke();
        }));
    }

    private void SetHUDVisibility(bool visible)
    {
        for (int i = 0; i < hudElementsToHide.Count; i++)
        {
            if (hudElementsToHide[i] != null)
            {
                hudElementsToHide[i].SetActive(visible);
            }
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            onDialogueStateChanged?.Invoke(false);
            Instance = null;
        }
    }
}
