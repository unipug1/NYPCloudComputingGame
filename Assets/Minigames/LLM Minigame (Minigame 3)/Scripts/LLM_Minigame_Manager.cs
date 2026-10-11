using UnityEngine;

public enum LLM_Minigame_State
{
    IntroWarning,
    NPCSpeaking,
    PlayerInput,
    PlayerSpeaking,
    NPCThinking,
    AttemptsUpModal,
    GameWonModal,
    TransitionLock
}

public class LLM_Minigame_Manager : MonoBehaviour
{
    [Header("Controllers")]
    [SerializeField] private LLM_Minigame_ServiceBridge bridge;
    [SerializeField] private LLM_Minigame_UIController uiController;
    [SerializeField] private LLM_Minigame_TrustMeter trustMeter;
    [SerializeField] private LLM_Minigame_CharacterView characterView;
    [SerializeField] private LLM_Minigame_FocusController focusController;
    [SerializeField] private LLM_Minigame_PropController propController;

    [Header("Intro & Screen Transitions")]
    [SerializeField] private LLM_Minigame_WarningBar warningBar;
    [SerializeField] private LLM_Minigame_ScreenFader screenFader;

    [Header("Settings")]
    [SerializeField] private int maxAttempts = 5;

    private LLM_Minigame_State currentState;
    private int attemptsUsed = 0;
    private bool unlimitedMode = false;
    private bool hasShownAttemptsModal = false;

    private float elapsedTime = 0f;
    private bool isTimerRunning = false;
    private string lastSubmittedPlayerText = "";

    private void Start()
    {
        uiController.Setup(OnPlayerSubmittedAnswer, OnContinueChosen, OnRestartChosen);
        StartNewGame();
    }

    private void Update()
    {
        if (isTimerRunning)
        {
            elapsedTime += Time.deltaTime;
        }

        // pressing r triggers the clean restart transition
        if (Input.GetKeyDown(KeyCode.R) && !uiController.IsInputFieldFocused() && currentState != LLM_Minigame_State.TransitionLock)
        {
            OnRestartChosen();
        }
    }

    public void StartNewGame()
    {
        ResetStateAndBoards();

        // play warning banner
        SetState(LLM_Minigame_State.IntroWarning);

        if (warningBar != null)
        {
            warningBar.PlayWarning(OnWarningComplete);
        }
        else
        {
            OnWarningComplete();
        }
    }

    private void ResetStateAndBoards()
    {
        isTimerRunning = false;
        elapsedTime = 0f;
        attemptsUsed = 0;
        unlimitedMode = false;
        hasShownAttemptsModal = false;
        lastSubmittedPlayerText = "";

        bridge.ResetSteps();
        trustMeter.ResetTrust();
        uiController.ClearAll();
        characterView.ResetCharacter("Neutral");
        propController.SetupInitialState();
        focusController.SnapToNPCFocus();
    }

    private void OnWarningComplete()
    {
        isTimerRunning = true;
        SetState(LLM_Minigame_State.NPCSpeaking);
        uiController.SetThinking(true);
        bridge.SendPlayerMessage("start", OnGuardResponseReceived);
    }

    private void OnPlayerSubmittedAnswer(string spokenDialogue)
    {
        if (currentState != LLM_Minigame_State.PlayerInput) return;

        // save text so it carries over to the bridge
        lastSubmittedPlayerText = spokenDialogue;
        attemptsUsed++;
        SetState(LLM_Minigame_State.PlayerSpeaking);

        // tool slides out to offscreen pose
        propController.SwitchOutActiveProp(() =>
        {
            // player bubble pops in only after tool is out
            uiController.PlayPlayerBubble(spokenDialogue, OnPlayerBubbleFinished);
        });
    }

    private void OnPlayerBubbleFinished()
    {
        SetState(LLM_Minigame_State.NPCThinking);

        // left shifts back into focus and prop returns to unavailable pose
        focusController.TransitionToNPCFocus();
        uiController.SetThinking(true);

        // send the actual player dialogue to the bridge
        bridge.SendPlayerMessage(lastSubmittedPlayerText, OnGuardResponseReceived);
    }

    private void OnGuardResponseReceived(LLM_Minigame_ResponseData response)
    {
        uiController.SetThinking(false);

        characterView.ApplyEmotion(response.emotionKey);

        if (response.presetOptions != null && response.presetOptions.Length > 0)
        {
            uiController.SetPresets(response.presetOptions);
        }

        trustMeter.ChangeTrust(response.trustChange);

        SetState(LLM_Minigame_State.NPCSpeaking);
        uiController.DisplayGuardDialogue(response.guardDialogue, OnGuardDialogueFinished);
    }

    private void OnGuardDialogueFinished()
    {
        // check win condition
        if (trustMeter.GetCurrentTrust() >= 100f)
        {
            TriggerWin();
            return;
        }

        // check attempts condition
        if (!unlimitedMode && attemptsUsed >= maxAttempts && !hasShownAttemptsModal)
        {
            TriggerAttemptsExhausted();
            return;
        }

        SetState(LLM_Minigame_State.PlayerInput);
        focusController.TransitionToPlayerFocus();
    }

    private void TriggerWin()
    {
        isTimerRunning = false;
        SetState(LLM_Minigame_State.GameWonModal);

        int minutes = Mathf.FloorToInt(elapsedTime / 60f);
        int seconds = Mathf.FloorToInt(elapsedTime % 60f);
        string formattedTime = string.Format("{0:00}:{1:00}", minutes, seconds);

        uiController.ShowWinModal(formattedTime, attemptsUsed, maxAttempts);
    }

    private void TriggerAttemptsExhausted()
    {
        hasShownAttemptsModal = true;
        SetState(LLM_Minigame_State.TransitionLock);

        uiController.ShowAttemptsModal(() =>
        {
            SetState(LLM_Minigame_State.AttemptsUpModal);
        });
    }

    private void OnContinueChosen()
    {
        if (currentState != LLM_Minigame_State.AttemptsUpModal) return;

        SetState(LLM_Minigame_State.TransitionLock);

        uiController.HideAttemptsModal(() =>
        {
            unlimitedMode = true;
            SetState(LLM_Minigame_State.PlayerInput);
            focusController.TransitionToPlayerFocus();
        });
    }

    private void OnRestartChosen()
    {
        SetState(LLM_Minigame_State.TransitionLock);

        if (screenFader != null)
        {
            screenFader.DoFullTransition(
                onBlack: () =>
                {
                    // clear boards and hide all modals while screen is black
                    ResetStateAndBoards();
                },
                onFinished: () =>
                {
                    // only play warning after screen has faded back to clear
                    SetState(LLM_Minigame_State.IntroWarning);
                    if (warningBar != null)
                    {
                        warningBar.PlayWarning(OnWarningComplete);
                    }
                    else
                    {
                        OnWarningComplete();
                    }
                }
            );
        }
        else
        {
            StartNewGame();
        }
    }

    public void OnDialogueBoxClicked()
    {
        if (currentState == LLM_Minigame_State.NPCSpeaking)
        {
            uiController.OnDialogueBoxClicked(OnGuardDialogueFinished);
        }
    }

    private void SetState(LLM_Minigame_State newState)
    {
        currentState = newState;
    }
}