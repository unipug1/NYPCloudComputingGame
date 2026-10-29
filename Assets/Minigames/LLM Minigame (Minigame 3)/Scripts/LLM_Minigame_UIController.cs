using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LLM_Minigame_UIController : MonoBehaviour
{
    [Header("controllers")]
    [SerializeField] private LLM_Minigame_FocusController focusController;
    [SerializeField] private LLM_Minigame_PropController propController;

    [Header("top right slide buttons")]
    [SerializeField] private LLM_Minigame_SlideButton phoneSlideButton;
    [SerializeField] private LLM_Minigame_SlideButton chatSlideButton;
    [SerializeField] private string chatButtonLetter = "R";

    [Header("custom keybinds")]
    [SerializeField] private KeyCode togglePropKey = KeyCode.Tab;
    [SerializeField] private KeyCode reviewDialogueKey = KeyCode.CapsLock;
    [SerializeField] private KeyCode phoneChoice1Key = KeyCode.Alpha1;
    [SerializeField] private KeyCode phoneChoice2Key = KeyCode.Alpha2;
    [SerializeField] private KeyCode phoneChoice3Key = KeyCode.Alpha3;

    [Header("hotkey prompt text")]
    [SerializeField] private TextMeshProUGUI hotkeyPromptText;
    [SerializeField] private CanvasGroup hotkeyPromptCanvasGroup;
    [SerializeField] private float hotkeyFadeDuration = 0.12f;

    [Header("player speech bubble")]
    [SerializeField] private LLM_Minigame_PlayerBubble playerBubble;

    [Header("clipboard inputs")]
    [SerializeField] private TMP_InputField playerInputField;
    [SerializeField] private Button sendButton;

    [Header("phone preset buttons")]
    [SerializeField] private Button[] presetButtons;
    [SerializeField] private TextMeshProUGUI[] presetButtonTexts;

    [Header("dialogue box & thinking")]
    [SerializeField] private TextMeshProUGUI dialogueText;
    [SerializeField] private Color thinkingColor = new Color(0.5f, 0.5f, 0.5f, 1f);
    [SerializeField] private Color normalTextColor = new Color(0.1f, 0.1f, 0.1f, 1f);
    [SerializeField] private float thinkingDotSpeed = 0.35f;
    [SerializeField] private float typeSpeed = 0.03f;

    [Header("modals")]
    [SerializeField] private LLM_Minigame_SlideModal winModal;
    [SerializeField] private TextMeshProUGUI winTimeText;
    [SerializeField] private TextMeshProUGUI winTakeawayText;
    [SerializeField] private Button winRestartButton;

    private Action<string> onPlayerSpeaks;
    private Action onRestartChosen;
    private Action onReviewRequested;
    private Action onTabPressedRequested;
    private Func<bool> canPlayerInteractCheck;

    private Coroutine typewriterRoutine;
    private Coroutine thinkingRoutine;
    private Coroutine hotkeyFadeRoutine;
    private bool isTyping = false;
    private bool hasPastAnswer = false;
    private string fullCurrentLine = "";
    private LLM_Minigame_PresetOption[] currentPresets;

    private void Awake()
    {
        if (hotkeyPromptText != null && hotkeyPromptCanvasGroup == null)
        {
            hotkeyPromptCanvasGroup = hotkeyPromptText.GetComponent<CanvasGroup>();
            if (hotkeyPromptCanvasGroup == null)
            {
                hotkeyPromptCanvasGroup = hotkeyPromptText.gameObject.AddComponent<CanvasGroup>();
            }
        }

        if (hotkeyPromptCanvasGroup != null)
        {
            hotkeyPromptCanvasGroup.alpha = 0f;
        }

        if (phoneSlideButton != null)
        {
            phoneSlideButton.SetText("P");
            phoneSlideButton.SnapOut();
        }

        if (chatSlideButton != null)
        {
            chatSlideButton.SetText(chatButtonLetter);
            chatSlideButton.SnapOut();
        }
    }

    public void Setup(
        Action<string> playerSpeakCallback, 
        Action restartCallback, 
        Action reviewCallback, 
        Action tabCallback, 
        Func<bool> canInteractCheck)
    {
        onPlayerSpeaks = playerSpeakCallback;
        onRestartChosen = restartCallback;
        onReviewRequested = reviewCallback;
        onTabPressedRequested = tabCallback;
        canPlayerInteractCheck = canInteractCheck;

        hasPastAnswer = false;

        if (sendButton != null) sendButton.onClick.AddListener(OnSendClicked);
        if (playerInputField != null) playerInputField.onSubmit.AddListener((text) => OnSendClicked());

        // hook top-right slide buttons
        if (phoneSlideButton != null)
        {
            phoneSlideButton.AddClickListener(() =>
            {
                if (canPlayerInteractCheck != null && canPlayerInteractCheck.Invoke())
                {
                    if (playerInputField != null) playerInputField.DeactivateInputField();
                    onTabPressedRequested?.Invoke();
                }
            });
        }

        if (chatSlideButton != null)
        {
            chatSlideButton.AddClickListener(() =>
            {
                if (canPlayerInteractCheck != null && canPlayerInteractCheck.Invoke() && hasPastAnswer)
                {
                    DeactivateInputField();
                    onReviewRequested?.Invoke();
                }
            });
        }

        if (winRestartButton != null) winRestartButton.onClick.AddListener(() => onRestartChosen?.Invoke());

        for (int i = 0; i < presetButtons.Length; i++)
        {
            int index = i;
            presetButtons[i].onClick.AddListener(() => OnPresetClicked(index));
        }

        UpdateHotkeyPrompt("");
    }

    private void Update()
    {
        if (canPlayerInteractCheck == null || !canPlayerInteractCheck.Invoke())
        {
            if (playerInputField != null && playerInputField.isFocused)
            {
                playerInputField.DeactivateInputField();
            }
            return;
        }

        // tab switches between clipboard and phone
        if (Input.GetKeyDown(togglePropKey))
        {
            if (playerInputField != null)
            {
                playerInputField.DeactivateInputField();
            }

            onTabPressedRequested?.Invoke();
            return;
        }

        // capslock toggles review
        if (hasPastAnswer && Input.GetKeyDown(reviewDialogueKey))
        {
            DeactivateInputField();
            onReviewRequested?.Invoke();
            return;
        }

        // 1 2 3 keys on phone
        if (!propController.IsClipboardActive())
        {
            if (playerInputField != null && playerInputField.isFocused)
            {
                playerInputField.DeactivateInputField();
            }

            if (Input.GetKeyDown(phoneChoice1Key) || Input.GetKeyDown(KeyCode.Keypad1))
            {
                OnPresetClicked(0);
            }
            else if (Input.GetKeyDown(phoneChoice2Key) || Input.GetKeyDown(KeyCode.Keypad2))
            {
                OnPresetClicked(1);
            }
            else if (Input.GetKeyDown(phoneChoice3Key) || Input.GetKeyDown(KeyCode.Keypad3))
            {
                OnPresetClicked(2);
            }
        }
        else
        {
            if (playerInputField != null && playerInputField.isFocused)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    OnSendClicked();
                    return;
                }
            }

            // clipboard active: maintain focus
            if (playerInputField != null && !playerInputField.isFocused)
            {
                playerInputField.ActivateInputField();
            }
        }
    }

    public bool IsInputFieldFocused()
    {
        return playerInputField != null && playerInputField.isFocused;
    }

    public void FocusInputFieldSafe()
    {
        StartCoroutine(DeferredFocusRoutine());
    }

    private IEnumerator DeferredFocusRoutine()
    {
        yield return null;

        if (playerInputField != null)
        {
            playerInputField.text = playerInputField.text.Replace("\t", "");
            playerInputField.interactable = true;
            playerInputField.ActivateInputField();
            playerInputField.Select();
            playerInputField.caretPosition = playerInputField.text.Length;
        }
    }

    public void DeactivateInputField()
    {
        if (playerInputField != null)
        {
            playerInputField.DeactivateInputField();
            playerInputField.interactable = false;
        }
    }

    // manages sliding in the top-right buttons when player turn starts
    public void ShowPlayerTurnButtons()
    {
        if (phoneSlideButton != null)
        {
            phoneSlideButton.SetText(propController.IsClipboardActive() ? "P" : "C");
            phoneSlideButton.SlideIn();
        }

        // chat button only appears if player has already submitted an answer
        if (chatSlideButton != null)
        {
            if (hasPastAnswer)
            {
                chatSlideButton.SetText(chatButtonLetter);
                chatSlideButton.SlideIn();
            }
            else
            {
                chatSlideButton.SnapOut();
            }
        }
    }

    // slides out top-right buttons when ai talks or during review
    public void HidePlayerTurnButtons()
    {
        if (phoneSlideButton != null) phoneSlideButton.SlideOut();
        if (chatSlideButton != null) chatSlideButton.SlideOut();
    }

    public void UpdateHotkeyPrompt(string prompt)
    {
        if (hotkeyPromptText == null) return;

        if (hotkeyFadeRoutine != null)
        {
            StopCoroutine(hotkeyFadeRoutine);
        }

        hotkeyFadeRoutine = StartCoroutine(FadeHotkeyPromptRoutine(prompt));
    }

    private IEnumerator FadeHotkeyPromptRoutine(string newPrompt)
    {
        if (string.IsNullOrEmpty(newPrompt))
        {
            if (hotkeyPromptCanvasGroup != null && hotkeyPromptCanvasGroup.alpha > 0.01f)
            {
                float timer = 0f;
                float startAlpha = hotkeyPromptCanvasGroup.alpha;
                while (timer < hotkeyFadeDuration)
                {
                    timer += Time.deltaTime;
                    hotkeyPromptCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, timer / hotkeyFadeDuration);
                    yield return null;
                }
                hotkeyPromptCanvasGroup.alpha = 0f;
            }

            hotkeyPromptText.text = "";
            yield break;
        }

        if (hotkeyPromptCanvasGroup != null && hotkeyPromptCanvasGroup.alpha > 0.05f)
        {
            float timer = 0f;
            float startAlpha = hotkeyPromptCanvasGroup.alpha;
            while (timer < hotkeyFadeDuration * 0.5f)
            {
                timer += Time.deltaTime;
                hotkeyPromptCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, timer / (hotkeyFadeDuration * 0.5f));
                yield return null;
            }
        }

        hotkeyPromptText.text = newPrompt;

        if (hotkeyPromptCanvasGroup != null)
        {
            float timer = 0f;
            while (timer < hotkeyFadeDuration)
            {
                timer += Time.deltaTime;
                hotkeyPromptCanvasGroup.alpha = Mathf.Lerp(0f, 1f, timer / hotkeyFadeDuration);
                yield return null;
            }
            hotkeyPromptCanvasGroup.alpha = 1f;
        }
    }

    private void OnSendClicked()
    {
        if (canPlayerInteractCheck != null && !canPlayerInteractCheck.Invoke()) return;

        string text = playerInputField.text.Trim();
        if (string.IsNullOrEmpty(text)) return;

        hasPastAnswer = true;
        playerInputField.text = "";
        DeactivateInputField();
        onPlayerSpeaks?.Invoke(text);
    }

    private void OnPresetClicked(int index)
    {
        if (canPlayerInteractCheck != null && !canPlayerInteractCheck.Invoke()) return;

        if (currentPresets != null && index < currentPresets.Length)
        {
            UpdateHotkeyPrompt("");
            string fullSentence = currentPresets[index].fullDialogue;
            propController.SelectPhonePresetToClipboard(fullSentence, playerInputField, () =>
            {
                RefreshPromptState();
            });
        }
    }

    public void ExecutePropToggle()
    {
        UpdateHotkeyPrompt("");

        // animate the phone button text swap: slides out, changes to P or C, bounces back in
        string nextLetter = propController.IsClipboardActive() ? "C" : "P";
        if (phoneSlideButton != null)
        {
            phoneSlideButton.SwapTextWithAnimation(nextLetter);
        }

        propController.ToggleProps(() =>
        {
            if (propController.IsClipboardActive())
            {
                FocusInputFieldSafe();
            }
            else
            {
                DeactivateInputField();
            }

            RefreshPromptState();
        });
    }

    public void RefreshPromptState()
    {
        string toggleLabel = GetCleanKeyName(togglePropKey);
        string reviewLabel = GetCleanKeyName(reviewDialogueKey);
        string c1 = GetCleanKeyName(phoneChoice1Key);
        string c3 = GetCleanKeyName(phoneChoice3Key);

        string pastLine = hasPastAnswer ? "\n<color=#FF6666>[" + reviewLabel + "]</color> Past" : "";

        if (propController.IsClipboardActive())
        {
            UpdateHotkeyPrompt("<color=#FFE600>[ENTER]</color> Send\n<color=#00F0FF>[" + toggleLabel + "]</color> Phone" + pastLine);
        }
        else
        {
            UpdateHotkeyPrompt("<color=#FFE600>[" + c1 + "-" + c3 + "]</color> Pick\n<color=#00F0FF>[" + toggleLabel + "]</color> Board" + pastLine);
        }
    }

    public void ShowReviewPrompt()
    {
        UpdateHotkeyPrompt("<color=#FFE600>[CLICK]</color> Close");
    }

    public void ShowContinuePrompt()
    {
        UpdateHotkeyPrompt("<color=#FFE600>[CLICK]</color> Next");
    }

    private string GetCleanKeyName(KeyCode key)
    {
        switch (key)
        {
            case KeyCode.Alpha1: return "1";
            case KeyCode.Alpha2: return "2";
            case KeyCode.Alpha3: return "3";
            case KeyCode.Return: case KeyCode.KeypadEnter: return "ENTER";
            case KeyCode.CapsLock: return "CAPS";
            case KeyCode.Tab: return "TAB";
            case KeyCode.Space: return "SPACE";
            default: return key.ToString().ToUpper();
        }
    }

    public void SetPresets(LLM_Minigame_PresetOption[] presets)
    {
        currentPresets = presets;

        for (int i = 0; i < presetButtonTexts.Length; i++)
        {
            if (presets != null && i < presets.Length)
            {
                presetButtonTexts[i].text = presets[i].summaryText;
            }
        }
    }

    public void SetThinking(bool thinking)
    {
        if (sendButton != null) sendButton.interactable = !thinking;
        if (playerInputField != null) playerInputField.interactable = !thinking;

        for (int i = 0; i < presetButtons.Length; i++)
        {
            presetButtons[i].interactable = !thinking;
        }

        if (thinking)
        {
            if (typewriterRoutine != null) StopCoroutine(typewriterRoutine);
            if (thinkingRoutine != null) StopCoroutine(thinkingRoutine);

            thinkingRoutine = StartCoroutine(ThinkingDotsRoutine());
        }
        else
        {
            if (thinkingRoutine != null)
            {
                StopCoroutine(thinkingRoutine);
                thinkingRoutine = null;
            }
        }
    }

    private IEnumerator ThinkingDotsRoutine()
    {
        dialogueText.color = thinkingColor;
        int dotCount = 1;

        while (true)
        {
            if (dotCount == 1) dialogueText.text = "Thinking.";
            else if (dotCount == 2) dialogueText.text = "Thinking..";
            else dialogueText.text = "Thinking...";

            dotCount++;
            if (dotCount > 3) dotCount = 1;

            yield return new WaitForSeconds(thinkingDotSpeed);
        }
    }

    public void DisplayGuardDialogue(string speech, Action onComplete)
    {
        if (thinkingRoutine != null)
        {
            StopCoroutine(thinkingRoutine);
            thinkingRoutine = null;
        }

        dialogueText.color = normalTextColor;
        fullCurrentLine = speech;

        if (typewriterRoutine != null)
        {
            StopCoroutine(typewriterRoutine);
        }
        typewriterRoutine = StartCoroutine(TypewriterRoutine(speech, onComplete));
    }

    private IEnumerator TypewriterRoutine(string speech, Action onComplete)
    {
        isTyping = true;
        dialogueText.text = speech;
        dialogueText.maxVisibleCharacters = 0;
        dialogueText.ForceMeshUpdate();

        int totalVisibleCharacters = dialogueText.textInfo.characterCount;
        float speed = typeSpeed > 0f ? typeSpeed : 0.03f;
        int currentVisible = 0;

        while (currentVisible < totalVisibleCharacters)
        {
            currentVisible++;
            dialogueText.maxVisibleCharacters = currentVisible;
            yield return new WaitForSeconds(speed);
        }

        isTyping = false;
        onComplete?.Invoke();
    }

    public void DisplayGuardDialogueInstant(string speech)
    {
        if (thinkingRoutine != null)
        {
            StopCoroutine(thinkingRoutine);
            thinkingRoutine = null;
        }

        dialogueText.color = normalTextColor;
        fullCurrentLine = speech;
        dialogueText.text = speech;
        dialogueText.ForceMeshUpdate();
        dialogueText.maxVisibleCharacters = dialogueText.textInfo.characterCount;
    }

    public void OnDialogueBoxClicked(Action onComplete)
    {
        if (isTyping)
        {
            StopCoroutine(typewriterRoutine);
            dialogueText.text = fullCurrentLine;
            dialogueText.ForceMeshUpdate();
            dialogueText.maxVisibleCharacters = dialogueText.textInfo.characterCount;
            isTyping = false;
            onComplete?.Invoke();
        }
    }

    public void ShowWinModal(string timeFormatted)
    {
        HidePlayerTurnButtons();

        if (winTimeText != null) winTimeText.text = timeFormatted;
        if (winTakeawayText != null) winTakeawayText.text = "Social Engineering relies on urgency, authority, and distraction. Always verify identities before granting access.";

        winModal.SlideIn();
    }

    public void ForceHideAllModals()
    {
        if (winModal != null) winModal.SnapHidden();
    }

    public void ClearAll()
    {
        hasPastAnswer = false;

        if (thinkingRoutine != null) StopCoroutine(thinkingRoutine);
        if (typewriterRoutine != null) StopCoroutine(typewriterRoutine);

        dialogueText.text = "";
        if (playerInputField != null) playerInputField.text = "";
        DeactivateInputField();
        UpdateHotkeyPrompt("");
        HidePlayerTurnButtons();

        // forcefully closes win and attempt modals on reset
        ForceHideAllModals();
    }
}