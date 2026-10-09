using UnityEngine;

public class LLM_Minigame_Manager : MonoBehaviour
{
    [Header("Controllers")]
    [SerializeField] private LLM_Minigame_ServiceBridge bridge;
    [SerializeField] private LLM_Minigame_UIController uiController;
    [SerializeField] private LLM_Minigame_TrustMeter trustMeter;
    [SerializeField] private LLM_Minigame_CharacterView characterView;

    [Header("Settings")]
    [SerializeField] private int maxAttempts = 5;

    private int remainingAttempts;
    private bool unlimitedMode = false;
    private bool gameWon = false;

    private void Start()
    {
        uiController.Setup(OnPlayerSubmitted, OnContinueChosen, OnRestartChosen);
        StartNewGame();
    }

    private void Update()
    {
        // only reset if admin pressed r AND the player is NOT currently typing in the input box
        if (Input.GetKeyDown(KeyCode.R) && !uiController.IsInputFieldFocused())
        {
            StartNewGame();
        }
    }

    public void StartNewGame()
    {
        remainingAttempts = maxAttempts;
        unlimitedMode = false;
        gameWon = false;

        bridge.ResetSteps();
        trustMeter.ResetTrust();
        uiController.ClearAll();
        characterView.ResetCharacter("Neutral");

        // trigger initial guard line
        uiController.SetThinking(true);
        bridge.SendPlayerMessage("start", OnGuardReplied);
    }

    private void OnPlayerSubmitted(string message)
    {
        if (gameWon) return;

        // check if out of attempts
        if (!unlimitedMode)
        {
            remainingAttempts--;
            if (remainingAttempts < 0)
            {
                uiController.ShowAttemptsPopup(true);
                return;
            }
        }

        uiController.SetThinking(true);
        bridge.SendPlayerMessage(message, OnGuardReplied);
    }

    private void OnGuardReplied(LLM_Minigame_ResponseData response)
    {
        uiController.SetThinking(false);

        // update character emotion and bounce if it changed
        characterView.ApplyEmotion(response.emotionKey);

        // update dialogue and guard history
        uiController.DisplayGuardDialogue(response.guardDialogue);

        // update the preset button texts
        if (response.presetOptions != null && response.presetOptions.Length > 0)
        {
            uiController.SetPresets(response.presetOptions);
        }

        // move trust slider smoothly
        trustMeter.ChangeTrust(response.trustChange);

        // check if player reached 100 percent
        if (trustMeter.GetCurrentTrust() >= 100f)
        {
            gameWon = true;
            Debug.Log("Server room access granted");
        }
        // if player used up their last attempt on this turn
        else if (!unlimitedMode && remainingAttempts <= 0)
        {
            uiController.ShowAttemptsPopup(true);
        }
    }

    private void OnContinueChosen()
    {
        unlimitedMode = true;
        uiController.ShowAttemptsPopup(false);
    }

    private void OnRestartChosen()
    {
        StartNewGame();
    }
}