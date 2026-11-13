using UnityEngine;

// On a "TP to ..." object: press E on it to travel to another map section.
// Hooked up by Tools > Gameplay > Build Map Gameplay (Interactable -> On Interact -> Go).
public class ZoneTeleporter : MonoBehaviour
{
    [Tooltip("Name of the map section this pad goes to (shown in the banner)")]
    public string targetZoneName;
    [Tooltip("Where the player lands. Move this object to change the landing spot.")]
    public Transform arrivalPoint;

    public void Go()
    {
        if (arrivalPoint == null) { Debug.LogWarning("[TP] No arrival point on " + name, this); return; }
        PlayerTeleport.To(arrivalPoint.position, targetZoneName);
    }

    void OnDrawGizmosSelected()
    {
        if (arrivalPoint == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, arrivalPoint.position);
        Gizmos.DrawWireSphere(arrivalPoint.position, 0.4f);
    }
}
