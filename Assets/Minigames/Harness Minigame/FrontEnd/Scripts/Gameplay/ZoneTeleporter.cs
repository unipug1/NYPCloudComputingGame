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
        var player = GameObject.FindWithTag("Player");
        if (player == null) return;

        System.Action move = () =>
        {
            Vector3 pos = arrivalPoint.position; pos.z = player.transform.position.z;
            var rb = player.GetComponent<Rigidbody2D>();
            if (rb != null) { rb.position = pos; rb.linearVelocity = Vector2.zero; }
            player.transform.position = pos;
            var cam = Camera.main;
            if (cam != null) cam.transform.position = new Vector3(pos.x, pos.y, cam.transform.position.z);
            var tracker = player.GetComponent<ZoneTracker>();
            if (tracker != null) tracker.Refresh(false);
            if (QuestNavigator.Instance != null) QuestNavigator.Instance.ShowBanner(targetZoneName);
        };

        if (ScreenFader.Instance != null) ScreenFader.Instance.Teleport(move);
        else move();
    }

    void OnDrawGizmosSelected()
    {
        if (arrivalPoint == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, arrivalPoint.position);
        Gizmos.DrawWireSphere(arrivalPoint.position, 0.4f);
    }
}
