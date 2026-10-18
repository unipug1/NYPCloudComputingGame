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
    [Header("controllers")]
    [SerializeField] private LLM_Minigame_ServiceBridge bridge;
    [SerializeField] private LLM_Minigame_UIController uiController;
    [SerializeField] private LLM_Minigame_TrustMeter trustMeter;
    [SerializeField] private LLM_Minigame_CharacterView characterView;
    [SerializeField] private LLM_Minigame_FocusController focusController;
    [SerializeField] private LLM_Minigame_PropController propController;
    [SerializeField] private LLM_Minigame_PlayerBubble playerBubble;

    [Header("intro and screen transitions")]
    [SerializeField] private LLM_Minigame_WarningBar warningBar;
    [SerializeField] private LLM_Minigame_ScreenFader screenFader;

    [Header("settings")]
    [SerializeField] private int maxAttempts = 5;

    private LLM_Minigame_State currentState;
    private int attemptsUsed = 0;
    private bool unlimitedMode = false;
    private bool hasShownAttemptsModal = false;

    private float elapsedTime = 0f;
    private bool isTimerRunning = false;
    private string lastSubmittedPlayerText = "";
    private bool isReviewOpen = false;

    private void Start()
    {
        uiController.Setup(
            OnPlayerSubmittedAnswer,
            OnContinueChosen,
            OnRestartChosen,
            OnReviewToggleRequested,
            OnTabRequested,
            CanPlayerInteract
        );

        StartNewGame();
    }

    private void Update()
    {
        if (isTimerRunning)
        {
            elapsedTime += Time.deltaTime;
        }

        if (Input.GetKeyDown(KeyCode.R) && !uiController.IsInputFieldFocused() && currentState != LLM_Minigame_State.TransitionLock)
        {
            OnRestartChosen();
        }
    }

    public bool CanPlayerInteract()
    {
        if (currentState != LLM_Minigame_State.PlayerInput) return false;
        if (propController != null && propController.IsAnimating()) return false;
        if (playerBubble != null && playerBubble.IsAnimating()) return false;
        if (focusController != null && focusController.IsAnimating()) return false;
        return true;
    }

    public void StartNewGame()
    {
        ResetStateAndBoards();

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
        isReviewOpen = false;

        bridge.ResetSteps();
        trustMeter.ResetTrust();
        uiController.ClearAll();
        if (playerBubble != null) playerBubble.SnapClosed();
        characterView.ResetCharacter("Neutral");
        propController.SetupInitialState();
        focusController.SnapToNPCFocus();
    }

    private void OnWarningComplete()
    {
        isTimerRunning = true;

        LLM_Minigame_ResponseData openingData = bridge.GetInstantOpeningLine();

        characterView.ApplyEmotion(openingData.emotionKey);
        uiController.SetPresets(openingData.presetOptions);

        SetState(LLM_Minigame_State.NPCSpeaking);
        uiController.UpdateHotkeyPrompt("");
        uiController.DisplayGuardDialogue(openingData.guardDialogue, OnOpeningDialogueFinished);
    }

    private void OnOpeningDialogueFinished()
    {
        SetState(LLM_Minigame_State.PlayerInput);
        focusController.TransitionToPlayerFocus();

        propController.TransitionUnavailableToActive(() =>
        {
            if (propController.IsClipboardActive())
            {
                uiController.FocusInputFieldSafe();
            }

            uiController.RefreshPromptState();
        });
    }

    private void OnPlayerSubmittedAnswer(string spokenDialogue)
    {
        if (!CanPlayerInteract()) return;

        lastSubmittedPlayerText = spokenDialogue;
        attemptsUsed++;
        SetState(LLM_Minigame_State.PlayerSpeaking);
        uiController.UpdateHotkeyPrompt("");

        propController.SwitchOutActiveProp(() =>
        {
            playerBubble.ShowPlayerDialogue(spokenDialogue, true);

            SetState(LLM_Minigame_State.NPCThinking);
            focusController.TransitionToNPCFocus();
            uiController.SetThinking(true);

            float currentTrust = trustMeter != null ? trustMeter.GetCurrentTrust() : 10f;
            bridge.SendPlayerMessage(lastSubmittedPlayerText, currentTrust, OnGuardResponseReceived);
        });
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

        uiController.DisplayGuardDialogue(response.guardDialogue, () =>
        {
            uiController.ShowContinuePrompt();
            playerBubble.EnableContinuePrompt(OnPlayerBubbleDismissed);
        });
    }

    private void OnPlayerBubbleDismissed()
    {
        uiController.UpdateHotkeyPrompt("");

        if (trustMeter.GetCurrentTrust() >= 100f)
        {
            TriggerWin();
            return;
        }

        if (!unlimitedMode && attemptsUsed >= maxAttempts && !hasShownAttemptsModal)
        {
            TriggerAttemptsExhausted();
            return;
        }

        SetState(LLM_Minigame_State.PlayerInput);
        focusController.TransitionToPlayerFocus();
        propController.SwitchInActiveProp(() =>
        {
            if (propController.IsClipboardActive())
            {
                uiController.FocusInputFieldSafe();
            }

            uiController.RefreshPromptState();
        });
    }

    public void OnTabRequested()
    {
        // block tab if review dialogue is active or if any animation is running
        if (isReviewOpen || !CanPlayerInteract()) return;

        uiController.ExecutePropToggle();
    }

    public void OnReviewToggleRequested()
    {
        // if review is open and stable, close it
        if (isReviewOpen)
        {
            if (playerBubble != null && playerBubble.IsAnimating()) return;
            CloseReview();
            return;
        }

        // if opening review, ensure nothing else is animating
        if (!CanPlayerInteract()) return;
        if (string.IsNullOrEmpty(lastSubmittedPlayerText)) return;

        isReviewOpen = true;
        uiController.DeactivateInputField();
        uiController.UpdateHotkeyPrompt("");

        propController.TransitionActiveToUnavailable(() =>
        {
            playerBubble.ReopenForReview(
                onClosed: () =>
                {
                    // callback fires when bubble dismisses naturally via click
                    FinishClosingReview();
                },
                onSettled: () =>
                {
                    uiController.ShowReviewPrompt();
                }
            );
        });
    }

    private void CloseReview()
    {
        if (!isReviewOpen) return;
        isReviewOpen = false;

        uiController.UpdateHotkeyPrompt("");
        playerBubble.DismissBubble();
    }

    // restores active prop once review bubble has fully closed
    private void FinishClosingReview()
    {
        isReviewOpen = false;
        uiController.UpdateHotkeyPrompt("");

        propController.TransitionUnavailableToActive(() =>
        {
            if (propController.IsClipboardActive())
            {
                uiController.FocusInputFieldSafe();
            }

            uiController.RefreshPromptState();
        });
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
            propController.SwitchInActiveProp(() =>
            {
                if (propController.IsClipboardActive())
                {
                    uiController.FocusInputFieldSafe();
                }

                uiController.RefreshPromptState();
            });
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
                    ResetStateAndBoards();
                },
                onFinished: () =>
                {
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
            if (attemptsUsed == 0 && !isTimerRunning)
            {
                uiController.OnDialogueBoxClicked(OnOpeningDialogueFinished);
            }
            else
            {
                uiController.OnDialogueBoxClicked(() =>
                {
                    uiController.ShowContinuePrompt();
                    playerBubble.EnableContinuePrompt(OnPlayerBubbleDismissed);
                });
            }
        }
    }

    private void SetState(LLM_Minigame_State newState)
    {
        currentState = newState;
    }
}