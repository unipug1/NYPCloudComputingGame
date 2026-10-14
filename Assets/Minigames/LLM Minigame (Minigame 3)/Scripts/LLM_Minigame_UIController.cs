using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LLM_Minigame_UIController : MonoBehaviour
{
    [Header("Controllers")]
    [SerializeField] private LLM_Minigame_FocusController focusController;
    [SerializeField] private LLM_Minigame_PropController propController;
    [SerializeField] private Button toggleModeButton;

    [Header("Player Speech Bubble")]
    [SerializeField] private LLM_Minigame_PlayerBubble playerBubble;

    [Header("Clipboard Inputs")]
    [SerializeField] private TMP_InputField playerInputField;
    [SerializeField] private Button sendButton;

    [Header("Phone Preset Buttons")]
    [SerializeField] private Button[] presetButtons;
    [SerializeField] private TextMeshProUGUI[] presetButtonTexts;

    [Header("Dialogue Box & Thinking")]
    [SerializeField] private TextMeshProUGUI dialogueText;
    [SerializeField] private Color thinkingColor = new Color(0.5f, 0.5f, 0.5f, 1f);
    [SerializeField] private Color normalTextColor = new Color(0.1f, 0.1f, 0.1f, 1f);
    [SerializeField] private float thinkingDotSpeed = 0.35f;
    [SerializeField] private float typeSpeed = 0.03f;

    [Header("Modals (Slide Panels)")]
    [SerializeField] private LLM_Minigame_SlideModal winModal;
    [SerializeField] private TextMeshProUGUI winTimeText;
    [SerializeField] private TextMeshProUGUI winAttemptsText;
    [SerializeField] private TextMeshProUGUI winTakeawayText;
    [SerializeField] private Button winRestartButton;

    [SerializeField] private LLM_Minigame_SlideModal attemptsModal;
    [SerializeField] private Button attemptsRestartButton;
    [SerializeField] private Button attemptsContinueButton;

    private Action<string> onPlayerSpeaks;
    private Action onContinueChosen;
    private Action onRestartChosen;

    private Coroutine typewriterRoutine;
    private Coroutine thinkingRoutine;
    private bool isTyping = false;
    private string fullCurrentLine = "";
    private LLM_Minigame_PresetOption[] currentPresets;

    public void Setup(Action<string> playerSpeakCallback, Action continueCallback, Action restartCallback)
    {
        onPlayerSpeaks = playerSpeakCallback;
        onContinueChosen = continueCallback;
        onRestartChosen = restartCallback;

        sendButton.onClick.AddListener(OnSendClicked);
        playerInputField.onSubmit.AddListener((text) => OnSendClicked());
        toggleModeButton.onClick.AddListener(OnToggleClicked);

        if (winRestartButton != null)
        {
            winRestartButton.onClick.AddListener(() => onRestartChosen?.Invoke());
        }

        if (attemptsRestartButton != null)
        {
            attemptsRestartButton.onClick.AddListener(() => onRestartChosen?.Invoke());
        }

        if (attemptsContinueButton != null)
        {
            attemptsContinueButton.onClick.AddListener(() => onContinueChosen?.Invoke());
        }

        for (int i = 0; i < presetButtons.Length; i++)
        {
            int index = i;
            presetButtons[i].onClick.AddListener(() => OnPresetClicked(index));
        }
    }

    private void Update()
    {
        if (propController.IsClipboardActive() && playerInputField.isFocused)
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                OnSendClicked();
            }
        }
    }

    public bool IsInputFieldFocused()
    {
        return playerInputField != null && playerInputField.isFocused;
    }

    private void OnSendClicked()
    {
        string text = playerInputField.text.Trim();
        if (string.IsNullOrEmpty(text)) return;

        playerInputField.text = "";
        onPlayerSpeaks?.Invoke(text);
    }

    private void OnPresetClicked(int index)
    {
        if (currentPresets != null && index < currentPresets.Length)
        {
            // send the full spoken dialogue, not the short button summary
            string fullSentence = currentPresets[index].fullDialogue;
            onPlayerSpeaks?.Invoke(fullSentence);
        }
    }

    private void OnToggleClicked()
    {
        propController.ToggleProps(() =>
        {
            if (propController.IsClipboardActive())
            {
                playerInputField.ActivateInputField();
            }
        });
    }

    public void SetPresets(LLM_Minigame_PresetOption[] presets)
    {
        currentPresets = presets;

        for (int i = 0; i < presetButtonTexts.Length; i++)
        {
            if (presets != null && i < presets.Length)
            {
                // display short summary on button
                presetButtonTexts[i].text = presets[i].summaryText;
            }
        }
    }

    public void PlayPlayerBubble(string dialogue, Action onComplete)
    {
        playerBubble.ShowPlayerDialogue(dialogue, onComplete);
    }

    public void SetThinking(bool thinking)
    {
        sendButton.interactable = !thinking;
        playerInputField.interactable = !thinking;

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
        dialogueText.text = "";

        for (int i = 0; i < speech.Length; i++)
        {
            dialogueText.text += speech[i];
            yield return new WaitForSeconds(typeSpeed);
        }

        isTyping = false;
        onComplete?.Invoke();
    }

    public void OnDialogueBoxClicked(Action onComplete)
    {
        if (isTyping)
        {
            StopCoroutine(typewriterRoutine);
            dialogueText.text = fullCurrentLine;
            isTyping = false;
            onComplete?.Invoke();
        }
    }

    public void ShowWinModal(string timeFormatted, int attemptsUsed, int maxAttempts)
    {
        if (winTimeText != null) winTimeText.text = timeFormatted;
        if (winAttemptsText != null) winAttemptsText.text = attemptsUsed + " / " + maxAttempts;
        if (winTakeawayText != null) winTakeawayText.text = "Social Engineering relies on urgency, authority, and casual distraction. Always verify identities before granting access.";

        winModal.SlideIn();
    }

    public void ShowAttemptsModal(Action onComplete = null)
    {
        attemptsModal.SlideIn(onComplete);
    }

    public void HideAttemptsModal(Action onComplete = null)
    {
        attemptsModal.SlideOut(onComplete);
    }

    public void ClearAll()
    {
        if (thinkingRoutine != null) StopCoroutine(thinkingRoutine);
        if (typewriterRoutine != null) StopCoroutine(typewriterRoutine);

        dialogueText.text = "";
        playerInputField.text = "";
        ForceHideAllModals();
    }

    public void ForceHideAllModals()
    {
        if (winModal != null)
        {
            winModal.SnapHidden();
        }

        if (attemptsModal != null)
        {
            attemptsModal.SnapHidden();
        }
    }
}