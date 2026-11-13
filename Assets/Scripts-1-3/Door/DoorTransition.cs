using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerMovement))]
public class DoorTransition : MonoBehaviour
{
    [SerializeField] private GameObject blackoutPanel;
    [SerializeField, Min(0f)] private float blackoutSeconds = 0.3f;

    [SerializeField, Min(0f)] private float wipeSeconds = 0.45f;

    private static readonly int Progress = Shader.PropertyToID("_Progress");

    private static readonly int CanvasAspect = Shader.PropertyToID("_CanvasAspect");
    private static readonly int WipeTime = Shader.PropertyToID("_WipeTime");

    public static DoorTransition Active { get; private set; }

    private PlayerMovement movement;
    private PlayerInteraction interaction;
    private Rigidbody2D body;
    private bool teleporting;
    private bool restoreInteraction;
    private Image wipeImage;
    private Canvas wipeCanvas;
    private float wipeStartedAt;
    private Material originalMaterial;
    private Material wipeMaterial;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        interaction = GetComponent<PlayerInteraction>();
        body = GetComponent<Rigidbody2D>();
    }

    private void OnEnable()
    {
        if (Active != null && Active != this)
        {
            Debug.LogWarning("Only one player needs DoorTransition.", this);
            return;
        }

        Active = this;
    }

    private void OnDisable()
    {
        if (Active == this)
            Active = null;

        if (teleporting)
        {
            StopAllCoroutines();
            FinishTeleport();
        }
    }

    public void Teleport(Transform destination, UnityEvent onArrival)
    {
        if (teleporting || movement.IsMovementBlocked || destination == null)
            return;

        if (blackoutPanel == null)
        {
            Debug.LogWarning("Assign the blackout panel on DoorTransition.", this);
            return;
        }

        Transform panelParent = blackoutPanel.transform.parent;
        if (blackoutPanel.activeSelf || (panelParent != null && !panelParent.gameObject.activeInHierarchy))
        {
            Debug.LogWarning("Keep the blackout panel off and its canvas active.", this);
            return;
        }

        if (!PrepareWipe())
            return;

        StartCoroutine(TeleportRoutine(destination, onArrival));
    }

    private IEnumerator TeleportRoutine(Transform destination, UnityEvent onArrival)
    {
        teleporting = true;
        wipeMaterial.SetFloat(Progress, 0f);
        wipeStartedAt = Time.unscaledTime;
        blackoutPanel.SetActive(true);
        wipeCanvas = wipeImage.canvas;
        UpdateWipeMaterial();

        // hide the prompt and block controls during the blackout
        restoreInteraction = interaction != null && interaction.enabled;
        if (restoreInteraction)
            interaction.enabled = false;

        movement.SetMovementBlocked(true);
        try
        {
            yield return AnimateWipe(0f, 1f);

            if (blackoutSeconds > 0f)
                yield return new WaitForSecondsRealtime(blackoutSeconds);

            if (destination == null)
                yield break;

            Vector3 position = destination.position;
            position.z = transform.position.z;
            body.position = position;
            transform.position = position;
            Physics2D.SyncTransforms();

            // switch the camera before showing the new room
            onArrival?.Invoke();

            // give the camera a frame to settle before revealing the room
            yield return null;
            yield return AnimateWipe(1f, 2f);
        }
        finally
        {
            if (teleporting)
                FinishTeleport();
        }
    }

    private bool PrepareWipe()
    {
        if (wipeMaterial != null)
            return true;

        wipeImage = blackoutPanel.GetComponent<Image>();
        if (wipeImage == null || !wipeImage.material.HasProperty(Progress))
        {
            Debug.LogWarning("Assign a Chevron Wipe material to the blackout panel Image.", this);
            return false;
        }

        originalMaterial = wipeImage.material;
        wipeMaterial = new Material(originalMaterial);
        wipeImage.material = wipeMaterial;
        return true;
    }

    private void LateUpdate()
    {
        if (teleporting)
            UpdateWipeMaterial();
    }

    private void UpdateWipeMaterial()
    {
        wipeMaterial.SetFloat(WipeTime, Time.unscaledTime - wipeStartedAt);

        if (wipeCanvas == null)
            return;

        Rect viewport = wipeCanvas.rootCanvas.pixelRect;
        if (viewport.height > 0f)
            wipeMaterial.SetFloat(CanvasAspect, viewport.width / viewport.height);
    }

    private IEnumerator AnimateWipe(float start, float end)
    {
        float duration = wipeSeconds;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float time = elapsed / duration;

            // accelerate into the room change and slow down as the arrow leaves
            float easedTime = start == 0f
                ? time * time
                : 1f - (1f - time) * (1f - time);
            float travel = Mathf.Lerp(time, easedTime, 0.8f);

            wipeMaterial.SetFloat(Progress, Mathf.Lerp(start, end, travel));
            yield return null;
            elapsed += Time.unscaledDeltaTime;
        }

        wipeMaterial.SetFloat(Progress, end);
    }

    private void OnDestroy()
    {
        if (wipeMaterial == null)
            return;

        if (wipeImage != null && wipeImage.material == wipeMaterial)
            wipeImage.material = originalMaterial;

        Destroy(wipeMaterial);
    }

    private void FinishTeleport()
    {
        teleporting = false;

        if (movement != null)
            movement.SetMovementBlocked(false);

        if (restoreInteraction && interaction != null)
            interaction.enabled = true;

        if (blackoutPanel != null)
            blackoutPanel.SetActive(false);
    }
}
