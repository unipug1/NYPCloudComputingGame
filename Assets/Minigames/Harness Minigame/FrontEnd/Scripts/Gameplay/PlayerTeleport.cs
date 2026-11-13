using System.Reflection;
using UnityEngine;
using UnityEngine.Events;

// Moves the player somewhere on the map with a transition:
//  - the team's DoorTransition chevron wipe (on the Player) when it's set up
//  - otherwise the ScreenFader navy fade
// Used by the quest panel (QuestNavigator) and TP pads (ZoneTeleporter).
public static class PlayerTeleport
{
    static Transform target;
    public static event System.Action OnTeleported;
    public static event System.Action OnTeleportStarted;

    public static void To(Vector3 position, string bannerText, bool animate = true)
    {
        var player = GameObject.FindWithTag("Player");
        if (player == null) { Debug.LogWarning("[Teleport] No object tagged 'Player'."); return; }
        position.z = player.transform.position.z;
        if (animate) OnTeleportStarted?.Invoke();

        var door = animate ? DoorTransition.Active : null;
        var move = player.GetComponent<PlayerMovement>();
        bool canWipe = door != null && door.isActiveAndEnabled && (move == null || !move.IsMovementBlocked);

        if (canWipe)
        {
            if (target == null)
            {
                var go = new GameObject("[Teleport Target]") { hideFlags = HideFlags.HideInHierarchy };
                target = go.transform;
            }
            target.position = position;
            var arrived = new UnityEvent();
            arrived.AddListener(() => Arrived(player, bannerText));
            door.Teleport(target, arrived);
            return;
        }

        System.Action jump = () => { Snap(player, position); Arrived(player, bannerText); };
        if (animate && ScreenFader.Instance != null) ScreenFader.Instance.Teleport(jump);
        else jump();
    }

    static void Snap(GameObject player, Vector3 pos)
    {
        var rb = player.GetComponent<Rigidbody2D>();
        if (rb != null) { rb.position = pos; rb.linearVelocity = Vector2.zero; }
        player.transform.position = pos;
        Physics2D.SyncTransforms();
    }

    static void Arrived(GameObject player, string bannerText)
    {
        SnapCamera(player.transform.position);
        var tracker = player.GetComponent<ZoneTracker>();
        if (tracker != null) tracker.Refresh(false);
        if (!string.IsNullOrEmpty(bannerText) && QuestNavigator.Instance != null) QuestNavigator.Instance.ShowBanner(bannerText);
        OnTeleported?.Invoke();
    }

    // jump the camera to the player so it doesn't glide across the whole map
    public static void SnapCamera(Vector3 pos)
    {
        var cam = Camera.main;
        if (cam == null) return;
        var follow = cam.GetComponent<CameraFollow>();
        if (follow != null)
        {
            // SwitchArea snaps to the target; keep whatever room bounds it already uses
            var f = typeof(CameraFollow).GetField("areaBounds", BindingFlags.NonPublic | BindingFlags.Instance);
            var bounds = f != null ? f.GetValue(follow) as BoxCollider2D : null;
            follow.SwitchArea(bounds);
        }
        else cam.transform.position = new Vector3(pos.x, pos.y, cam.transform.position.z);
    }
}
