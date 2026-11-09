using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Connects screen buttons to interaction and the shared dialogue manager.</summary>
[DisallowMultipleComponent]
public class InteractionUI : MonoBehaviour
{
    [SerializeField] private Button interactButton;
    [SerializeField] private TMP_Text promptLabel;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private DialogueManager dialogueManager;

    private Interactable displayedTarget;

    public event Action InteractionRequested;

    private void Awake()
    {
        ShowPrompt(null);
    }

    private void OnEnable()
    {
        if (interactButton != null)
            interactButton.onClick.AddListener(RequestInteraction);
        if (continueButton != null)
            continueButton.onClick.AddListener(ContinueDialogue);
        if (closeButton != null)
            closeButton.onClick.AddListener(CloseDialogue);
    }

    private void OnDisable()
    {
        if (interactButton != null)
            interactButton.onClick.RemoveListener(RequestInteraction);
        if (continueButton != null)
            continueButton.onClick.RemoveListener(ContinueDialogue);
        if (closeButton != null)
            closeButton.onClick.RemoveListener(CloseDialogue);
        ShowPrompt(null);
    }

    public void ShowPrompt(Interactable target)
    {
        bool changed = displayedTarget != target;
        displayedTarget = target;
        bool visible = target != null;

        if (interactButton != null && interactButton.gameObject.activeSelf != visible)
            interactButton.gameObject.SetActive(visible);

        if (promptLabel != null && (changed || !visible))
            promptLabel.text = visible ? $"{target.Prompt} (E / tap)" : string.Empty;
    }

    private void RequestInteraction()
    {
        InteractionRequested?.Invoke();
    }

    private void ContinueDialogue()
    {
        if (dialogueManager != null)
            dialogueManager.AdvanceDialogue();
    }

    private void CloseDialogue()
    {
        if (dialogueManager != null)
            dialogueManager.EndDialogue();
    }
}
