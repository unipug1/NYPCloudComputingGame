using UnityEngine;

public enum LLM_Minigame_State
{
    IntroWarning,
    NPCSpeaking,
    PlayerInput,
    PlayerSpeaking,
    NPCThinking,
    DeadEndModal,
    ResetConfirmModal,
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

    [Header("rainbow reason text")]
    [SerializeField] private LLM_Minigame_RainbowText rainbowReasonText;

    [Header("modals")]
    [SerializeField] private LLM_Minigame_DeadEndModal deadEndModal;
    [SerializeField] private LLM_Minigame_ResetConfirmModal resetConfirmModal;

    [Header("intro and screen transitions")]
    [SerializeField] private LLM_Minigame_WarningBar warningBar;
    [SerializeField] private LLM_Minigame_ScreenFader screenFader;

    [Header("telemetry")]
    [SerializeField] private LLM_Minigame_Telemetry telemetry;

    [Header("settings")]
    [SerializeField] private int maxAttempts = 5;

    private LLM_Minigame_State currentState;
    private LLM_Minigame_State stateBeforeConfirm;

    private string currentLockedReason = "";
    private float elapsedTime = 0f;
    private bool isTimerRunning = false;
    private string lastSubmittedPlayerText = "";
    private bool isReviewOpen = false;

    private bool isFirstBoot = true;
    private bool deadEndIgnoredThisSession = false;

    private void Start()
    {
        isFirstBoot = true;
        uiController.Setup(
            OnPlayerSubmittedAnswer,
            OnRestartConfirmed,
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
            RequestResetConfirmation();
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
        lastSubmittedPlayerText = "";
        isReviewOpen = false;
        deadEndIgnoredThisSession = false;

        if (telemetry != null) telemetry.StartNewSession();

        bridge.ResetSteps();
        trustMeter.ResetTrust();
        uiController.ClearAll();

        currentLockedReason = "";
        if (rainbowReasonText != null) rainbowReasonText.SetReasonTag("");
        if (deadEndModal != null) deadEndModal.SnapClosed();
        if (resetConfirmModal != null) resetConfirmModal.SnapClosed();
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
        uiController.HidePlayerTurnButtons();

        if (isFirstBoot)
        {
            isFirstBoot = false;
            uiController.DisplayGuardDialogue(openingData.guardDialogue, OnOpeningDialogueFinished);
        }
        else
        {
            uiController.DisplayGuardDialogueInstant(openingData.guardDialogue);
            OnOpeningDialogueFinished();
        }
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
            uiController.ShowPlayerTurnButtons();
        });
    }

    private void OnPlayerSubmittedAnswer(string spokenDialogue)
    {
        if (!CanPlayerInteract()) return;

        lastSubmittedPlayerText = spokenDialogue;
        SetState(LLM_Minigame_State.PlayerSpeaking);
        uiController.UpdateHotkeyPrompt("");
        uiController.HidePlayerTurnButtons();

        propController.SwitchOutActiveProp(() =>
        {
            playerBubble.ShowPlayerDialogue(spokenDialogue, true);

            SetState(LLM_Minigame_State.NPCThinking);
            focusController.TransitionToNPCFocus();
            uiController.SetThinking(true);

            float currentTrust = trustMeter != null ? trustMeter.GetCurrentTrust() : 10f;
            bridge.SendPlayerMessage(lastSubmittedPlayerText, currentTrust, currentLockedReason, OnGuardResponseReceived);
        });
    }

    private void OnGuardResponseReceived(LLM_Minigame_ResponseData response)
    {
        uiController.SetThinking(false);
        characterView.ApplyEmotion(response.emotionKey);

        if (!string.IsNullOrEmpty(response.reasonTag))
        {
            currentLockedReason = response.reasonTag;
            if (rainbowReasonText != null) rainbowReasonText.SetReasonTag(currentLockedReason);
        }

        if (response.presetOptions != null && response.presetOptions.Length > 0)
        {
            uiController.SetPresets(response.presetOptions);
        }

        trustMeter.ChangeTrust(response.trustChange);

        if (telemetry != null)
        {
            string modeUsed = propController.IsClipboardActive() ? "Clipboard" : "Phone";
            bool won = (trustMeter.GetCurrentTrust() >= 100f);

            telemetry.RecordTurn(
                inputMode: modeUsed,
                playerText: lastSubmittedPlayerText,
                reasonTag: currentLockedReason,
                guardReply: response.guardDialogue,
                emotionKey: response.emotionKey,
                trustDelta: response.trustChange,
                totalTrust: trustMeter.GetCurrentTrust(),
                isDeadEnd: response.isDeadEnd,
                deadEndReason: response.deadEndReason,
                hasWon: won
            );
        }

        SetState(LLM_Minigame_State.NPCSpeaking);

        uiController.DisplayGuardDialogue(response.guardDialogue, () =>
        {
            if (response.isDeadEnd && !deadEndIgnoredThisSession)
            {
                TriggerDeadEndModal(response.deadEndReason);
            }
            else
            {
                uiController.ShowContinuePrompt();
                playerBubble.EnableContinuePrompt(OnPlayerBubbleDismissed);
            }
        });
    }

    private void TriggerDeadEndModal(string deadEndExplanation)
    {
        SetState(LLM_Minigame_State.DeadEndModal);
        uiController.UpdateHotkeyPrompt("");
        uiController.HidePlayerTurnButtons();

        string explanation = string.IsNullOrEmpty(deadEndExplanation) 
            ? "Your story has fallen apart. Mr. Tan caught you in a contradiction." 
            : deadEndExplanation;

        deadEndModal.ShowModal(
            explanation,
            onContinue: () =>
            {
                deadEndIgnoredThisSession = true;
                deadEndModal.HideModal(() =>
                {
                    uiController.ShowContinuePrompt();
                    playerBubble.EnableContinuePrompt(OnPlayerBubbleDismissed);
                });
            },
            onRestart: () =>
            {
                deadEndModal.HideModal(() =>
                {
                    OnRestartConfirmed();
                });
            }
        );
    }

    private void OnPlayerBubbleDismissed()
    {
        uiController.UpdateHotkeyPrompt("");

        if (trustMeter.GetCurrentTrust() >= 100f)
        {
            TriggerWin();
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
            uiController.ShowPlayerTurnButtons();
        });
    }

    public void OnTabRequested()
    {
        if (isReviewOpen || !CanPlayerInteract()) return;
        uiController.ExecutePropToggle();
    }

    public void OnReviewToggleRequested()
    {
        if (!CanPlayerInteract()) return;

        if (isReviewOpen)
        {
            if (playerBubble != null && playerBubble.IsAnimating()) return;
            CloseReview();
            return;
        }

        if (string.IsNullOrEmpty(lastSubmittedPlayerText)) return;

        isReviewOpen = true;
        uiController.DeactivateInputField();
        uiController.UpdateHotkeyPrompt("");

        // slide out top right buttons when reviewing dialogue
        uiController.HidePlayerTurnButtons();

        propController.TransitionActiveToUnavailable(() =>
        {
            playerBubble.ReopenForReview(
                onClosed: () =>
                {
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
            uiController.ShowPlayerTurnButtons();
        });
    }

    public void RequestResetConfirmation()
    {
        if (currentState == LLM_Minigame_State.ResetConfirmModal || currentState == LLM_Minigame_State.TransitionLock)
        {
            return;
        }

        stateBeforeConfirm = currentState;
        SetState(LLM_Minigame_State.ResetConfirmModal);
        uiController.UpdateHotkeyPrompt("");
        uiController.HidePlayerTurnButtons();

        if (resetConfirmModal != null)
        {
            resetConfirmModal.Show(
                confirmCallback: OnRestartConfirmed,
                cancelCallback: OnRestartCancelled
            );
        }
        else
        {
            OnRestartConfirmed();
        }
    }

    private void OnRestartConfirmed()
    {
        if (resetConfirmModal != null) resetConfirmModal.SnapClosed();

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

    private void OnRestartCancelled()
    {
        if (resetConfirmModal != null) resetConfirmModal.Hide();
        SetState(stateBeforeConfirm);

        if (currentState == LLM_Minigame_State.PlayerInput)
        {
            uiController.RefreshPromptState();
            uiController.ShowPlayerTurnButtons();

            if (propController.IsClipboardActive())
            {
                uiController.FocusInputFieldSafe();
            }
        }
    }

    private void TriggerWin()
    {
        isTimerRunning = false;
        SetState(LLM_Minigame_State.GameWonModal);

        int minutes = Mathf.FloorToInt(elapsedTime / 60f);
        int seconds = Mathf.FloorToInt(elapsedTime % 60f);
        string formattedTime = string.Format("{0:00}:{1:00}", minutes, seconds);

        uiController.ShowWinModal(formattedTime);
    }

    public void OnDialogueBoxClicked()
    {
        if (currentState == LLM_Minigame_State.NPCSpeaking)
        {
            if (isFirstBoot)
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