using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

// explicit aliases prevent ambiguous reference errors with system namespaces
using Debug = UnityEngine.Debug;
using Application = UnityEngine.Application;

public class DialogueTrigger : MonoBehaviour
{
    public enum TriggerMode { OnWalkIn, OnInteractKey, External }

    [Header("trigger mode")]
    [SerializeField] private TriggerMode triggerMode = TriggerMode.OnWalkIn;
    [SerializeField] private bool triggerOnlyOnce = true;

    [Header("dialogue script")]
    [SerializeField] private List<DialogueLine> dialogueLines = new List<DialogueLine>();

    [Header("events when dialogue finishes")]
    [SerializeField] private UnityEvent onDialogueCompleted;

    [Header("test in inspector (play mode)")]
    [Tooltip("click this checkbox during play mode to test the dialogue")]
    [SerializeField] private bool clickToTestDialogue = false;

    private bool hasTriggered = false;
    private bool playerIsInside = false;

    public bool CanInteract => isActiveAndEnabled
        && (!hasTriggered || !triggerOnlyOnce)
        && dialogueLines != null && dialogueLines.Count > 0
        && DialogueManager.Instance != null
        && !DialogueManager.Instance.IsDialogueRunning;

    [ContextMenu("test dialogue")]
    public void TestStartDialogueFromMenu()
    {
        if (UnityEngine.Application.isPlaying)
        {
            StartConversation();
        }
        else
        {
            UnityEngine.Debug.LogWarning("you must be in play mode to test the dialogue", this);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            playerIsInside = true;

            if (triggerMode == TriggerMode.OnWalkIn && (!hasTriggered || !triggerOnlyOnce))
            {
                StartConversation();
            }
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            playerIsInside = false;
        }
    }

    private void Update()
    {
        // test checkbox handled inside update to avoid unity onvalidate sendmessage warnings
        if (clickToTestDialogue)
        {
            clickToTestDialogue = false;

            if (UnityEngine.Application.isPlaying)
            {
                StartConversation();
            }
            return;
        }

        if (triggerMode == TriggerMode.OnInteractKey && playerIsInside && (!hasTriggered || !triggerOnlyOnce))
        {
            if (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueRunning) return;

            bool interactPressed = false;
            if (Keyboard.current != null)
            {
                interactPressed = Keyboard.current.fKey.wasPressedThisFrame || Keyboard.current.eKey.wasPressedThisFrame;
            }

            if (interactPressed)
            {
                StartConversation();
            }
        }
    }

    public void StartConversation()
    {
        if (!isActiveAndEnabled || (hasTriggered && triggerOnlyOnce))
            return;

        if (DialogueManager.Instance == null)
        {
            UnityEngine.Debug.LogError("dialoguemanager instance not found in scene", this);
            return;
        }

        if (dialogueLines == null || dialogueLines.Count == 0)
        {
            UnityEngine.Debug.LogWarning("no dialogue lines assigned on this trigger", this);
            return;
        }

        if (DialogueManager.Instance.IsDialogueRunning)
            return;

        hasTriggered = true;

        DialogueManager.Instance.StartDialogue(dialogueLines, () =>
        {
            onDialogueCompleted?.Invoke();

            if (triggerOnlyOnce)
            {
                Collider2D col = GetComponent<Collider2D>();
                if (col != null) col.enabled = false;
            }
        });
    }
}
