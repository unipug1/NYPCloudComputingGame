using TMPro;
using UnityEngine;

public class LLM_Minigame_GhostAutocomplete : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private TextMeshProUGUI ghostTextDisplay;

    private string currentPresetSuggestion = "";

    private void Awake()
    {
        if (ghostTextDisplay != null)
        {
            ghostTextDisplay.text = "";
        }

        if (inputField != null)
        {
            inputField.onValueChanged.AddListener(OnInputChanged);
        }
    }

    private void Update()
    {
        // press tab to accept ghost autocomplete
        if (inputField.isFocused && Input.GetKeyDown(KeyCode.Tab))
        {
            AcceptAutocomplete();
        }
    }

    public void SetPresetSuggestion(string suggestion)
    {
        currentPresetSuggestion = string.IsNullOrEmpty(suggestion) ? "" : suggestion.Trim();
        UpdateGhostText(inputField != null ? inputField.text : "");
    }

    private void OnInputChanged(string currentTyped)
    {
        UpdateGhostText(currentTyped);
    }

    private void UpdateGhostText(string currentTyped)
    {
        if (ghostTextDisplay == null || string.IsNullOrEmpty(currentPresetSuggestion))
        {
            if (ghostTextDisplay != null) ghostTextDisplay.text = "";
            return;
        }

        // if box is empty, show full suggestion in light grey
        if (string.IsNullOrEmpty(currentTyped))
        {
            ghostTextDisplay.text = currentPresetSuggestion;
            return;
        }

        // if player is typing something that matches the suggestion, show remainder
        if (currentPresetSuggestion.StartsWith(currentTyped, System.StringComparison.OrdinalIgnoreCase))
        {
            string invisibleTypedPrefix = "<color=#00000000>" + currentTyped + "</color>";
            string remainingGhost = currentPresetSuggestion.Substring(currentTyped.Length);
            ghostTextDisplay.text = invisibleTypedPrefix + remainingGhost;
        }
        else
        {
            // player is typing their own custom idea so hide ghost
            ghostTextDisplay.text = "";
        }
    }

    public void AcceptAutocomplete()
    {
        if (string.IsNullOrEmpty(currentPresetSuggestion)) return;

        inputField.text = currentPresetSuggestion;
        inputField.caretPosition = inputField.text.Length;
        ghostTextDisplay.text = "";
    }
}