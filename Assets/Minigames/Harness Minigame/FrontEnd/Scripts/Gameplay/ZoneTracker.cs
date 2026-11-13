using UnityEngine;

// On the player: notices when you walk into another map section,
// shows its name and tells the minimap which room you're in.
public class ZoneTracker : MonoBehaviour
{
    public float checkEvery = 0.25f;
    public MapZone Current { get; private set; }

    float timer;
    bool first = true;
    MinimapManager minimap;

    void Start() { minimap = FindFirstObjectByType<MinimapManager>(); }

    void Update()
    {
        timer -= Time.deltaTime;
        if (timer > 0) return;
        timer = checkEvery;
        Refresh(!first);
        first = false;
    }

    // showBanner = false right after a teleport (the teleporter shows its own)
    public void Refresh(bool showBanner)
    {
        var z = MapZone.At(transform.position);
        if (z == null || z == Current) return;
        Current = z;
        if (minimap != null && z.minimapRoom != null) minimap.SetCurrentPhysicalRoom(z.minimapRoom);
        if (showBanner && QuestNavigator.Instance != null) QuestNavigator.Instance.ShowBanner(z.zoneName);
    }
}
