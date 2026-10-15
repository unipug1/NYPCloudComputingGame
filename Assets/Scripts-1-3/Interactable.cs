using UnityEngine;
using UnityEngine.Events;

/// <summary>A sign, NPC or door that the player can activate from its trigger area.</summary>
[DisallowMultipleComponent]
public class Interactable : MonoBehaviour
{
    [SerializeField] private string prompt = "Interact";
    [Tooltip("The trigger collider that defines how close the player must be.")]
    [SerializeField] private Collider2D interactionArea;
    [SerializeField] private bool interactOnce;
    [Tooltip("Optional conversation, configured with Trigger Mode = External.")]
    [SerializeField] private DialogueTrigger dialogueTrigger;
    [SerializeField] private UnityEvent onInteract = new UnityEvent();

    private bool hasInteracted;

    public string Prompt => prompt;
    public Collider2D InteractionArea => interactionArea;
    public UnityEvent OnInteract => onInteract;
    public bool CanInteract => isActiveAndEnabled
        && (!interactOnce || !hasInteracted)
        && interactionArea != null && interactionArea.enabled
        && interactionArea.isTrigger && interactionArea.gameObject.activeInHierarchy
        && (dialogueTrigger == null || dialogueTrigger.CanInteract);

    public void Interact()
    {
        if (!CanInteract)
            return;

        hasInteracted = true;
        if (dialogueTrigger != null)
            dialogueTrigger.StartConversation();

        onInteract.Invoke();
    }

    private void Reset()
    {
        interactionArea = GetComponent<Collider2D>();
    }
}
