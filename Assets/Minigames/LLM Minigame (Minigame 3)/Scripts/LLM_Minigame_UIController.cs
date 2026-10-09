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

    [Header("5 Attempts Popup")]
    [SerializeField] private GameObject attemptsPopup;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button restartButton;

    private Action<string> onMessageSubmitted;
    private Action onContinueChosen;
    private Action onRestartChosen;

    private Coroutine typewriterRoutine;
    private Coroutine thinkingRoutine;
    private bool isTyping = false;
    private string fullCurrentLine = "";

    public void Setup(Action<string> submitCallback, Action continueCallback, Action restartCallback)
    {
        onMessageSubmitted = submitCallback;
        onContinueChosen = continueCallback;
        onRestartChosen = restartCallback;

        sendButton.onClick.AddListener(OnSendClicked);

        // automatically send when the user presses Enter in the input field
        playerInputField.onSubmit.AddListener((text) => OnSendClicked());

        toggleModeButton.onClick.AddListener(OnToggleClicked);

        continueButton.onClick.AddListener(() => onContinueChosen?.Invoke());
        restartButton.onClick.AddListener(() => onRestartChosen?.Invoke());

        for (int i = 0; i < presetButtons.Length; i++)
        {
            int index = i;
            presetButtons[i].onClick.AddListener(() => OnPresetClicked(index));
        }

        focusController.SnapToNPCFocus();
        attemptsPopup.SetActive(false);
    }

    private void Update()
    {
        // check for both standard Enter (Return) and Numpad Enter
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

        // shift focus back to guard as waiting begins
        focusController.TransitionToNPCFocus();
        onMessageSubmitted?.Invoke(text);
    }

    private void OnPresetClicked(int index)
    {
        if (index < presetButtonTexts.Length)
        {
            string chosenText = presetButtonTexts[index].text;

            // shift focus back to guard as waiting begins
            focusController.TransitionToNPCFocus();
            onMessageSubmitted?.Invoke(chosenText);
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

    public void SetPresets(string[] presets)
    {
        for (int i = 0; i < presetButtonTexts.Length; i++)
        {
            if (i < presets.Length)
            {
                presetButtonTexts[i].text = presets[i];
            }
        }
    }

    // starts or stops the animated grey thinking loop
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

    // loops Thinking. to Thinking.. to Thinking... in grey
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

    public void DisplayGuardDialogue(string speech)
    {
        // stop thinking loop
        if (thinkingRoutine != null)
        {
            StopCoroutine(thinkingRoutine);
            thinkingRoutine = null;
        }

        // switch text back to solid dark color
        dialogueText.color = normalTextColor;
        fullCurrentLine = speech;

        if (typewriterRoutine != null)
        {
            StopCoroutine(typewriterRoutine);
        }
        typewriterRoutine = StartCoroutine(TypewriterRoutine(speech));
    }

    private IEnumerator TypewriterRoutine(string speech)
    {
        isTyping = true;
        dialogueText.text = "";

        for (int i = 0; i < speech.Length; i++)
        {
            dialogueText.text += speech[i];
            yield return new WaitForSeconds(typeSpeed);
        }

        isTyping = false;

        // guard finished speaking, transition focus to player
        focusController.TransitionToPlayerFocus();
        if (propController.IsClipboardActive())
        {
            playerInputField.ActivateInputField();
        }
    }

    public void OnDialogueBoxClicked()
    {
        if (isTyping)
        {
            StopCoroutine(typewriterRoutine);
            dialogueText.text = fullCurrentLine;
            isTyping = false;

            focusController.TransitionToPlayerFocus();
            if (propController.IsClipboardActive())
            {
                playerInputField.ActivateInputField();
            }
        }
    }

    public void ShowAttemptsPopup(bool show)
    {
        attemptsPopup.SetActive(show);
    }

    public void ClearAll()
    {
        if (thinkingRoutine != null) StopCoroutine(thinkingRoutine);
        if (typewriterRoutine != null) StopCoroutine(typewriterRoutine);

        dialogueText.text = "";
        playerInputField.text = "";
        attemptsPopup.SetActive(false);

        focusController.SnapToNPCFocus();
    }
}