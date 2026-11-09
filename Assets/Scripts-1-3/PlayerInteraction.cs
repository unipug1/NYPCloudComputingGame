using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Selects one nearby target and routes keyboard and button interaction to it.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerMovement))]
public class PlayerInteraction : MonoBehaviour
{
    [SerializeField] private InteractionUI interactionUI;
    [SerializeField] private DialogueManager dialogueManager;
    [SerializeField] private PlayerMovement movement;
    [SerializeField] private LayerMask interactionLayers = ~0;

    private readonly List<Collider2D> nearbyAreas = new List<Collider2D>(16);
    private int lastInteractionFrame = -1;

    public Interactable CurrentTarget { get; private set; }

    private void Awake()
    {
        if (movement == null)
            movement = GetComponent<PlayerMovement>();
    }

    private void OnEnable()
    {
        if (interactionUI != null)
            interactionUI.InteractionRequested += TryInteract;

        if (dialogueManager != null)
            dialogueManager.onDialogueStateChanged.AddListener(SetDialogueActive);
    }

    private void Start()
    {
        SetDialogueActive(dialogueManager != null && dialogueManager.IsDialogueRunning);
    }

    private void OnDisable()
    {
        if (interactionUI != null)
        {
            interactionUI.InteractionRequested -= TryInteract;
            interactionUI.ShowPrompt(null);
        }

        if (dialogueManager != null)
            dialogueManager.onDialogueStateChanged.RemoveListener(SetDialogueActive);

        CurrentTarget = null;
        if (movement != null)
            movement.SetMovementBlocked(false);
    }

    private void Update()
    {
        RefreshTarget();

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
            TryInteract();
    }

    // The prompt button calls this same method as the E key.
    public void TryInteract()
    {
        if (lastInteractionFrame == Time.frameCount)
            return;

        RefreshTarget();
        if (CurrentTarget == null)
            return;

        lastInteractionFrame = Time.frameCount;
        CurrentTarget.Interact();
        RefreshTarget();
    }

    private void RefreshTarget()
    {
        CurrentTarget = FindNearestTarget();
        if (interactionUI != null)
            interactionUI.ShowPrompt(CurrentTarget);
    }

    private Interactable FindNearestTarget()
    {
        if (!isActiveAndEnabled || (dialogueManager != null && dialogueManager.IsDialogueRunning))
            return null;

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(interactionLayers);
        filter.useTriggers = true;
        nearbyAreas.Clear();
        Physics2D.OverlapPoint(transform.position, filter, nearbyAreas);

        Interactable nearest = null;
        float nearestDistance = float.PositiveInfinity;

        foreach (Collider2D area in nearbyAreas)
        {
            if (!area.isTrigger)
                continue;

            Interactable candidate = area.GetComponentInParent<Interactable>();
            if (candidate == null || candidate.InteractionArea != area || !candidate.CanInteract)
                continue;

            float distance = ((Vector2)(candidate.transform.position - transform.position)).sqrMagnitude;
            if (distance < nearestDistance
                || (distance == nearestDistance && nearest != null
                    && candidate.GetInstanceID() < nearest.GetInstanceID()))
            {
                nearestDistance = distance;
                nearest = candidate;
            }
        }

        return nearest;
    }

    private void SetDialogueActive(bool active)
    {
        if (movement != null)
            movement.SetMovementBlocked(active);

        if (active)
        {
            CurrentTarget = null;
            if (interactionUI != null)
                interactionUI.ShowPrompt(null);
        }
    }
}
