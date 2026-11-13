using System.Collections.Generic;
using UnityEngine;

// One per map section (room). Set up by Tools > Gameplay > Build Map Gameplay.
public class MapZone : MonoBehaviour
{
    public static readonly List<MapZone> All = new List<MapZone>();

    public string zoneName;
    public Bounds bounds;              // world-space area of the room's floor
    public RoomData minimapRoom;       // matching entry in the MinimapManager (optional)

    void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    void OnDisable() { All.Remove(this); }

    public bool Contains(Vector3 p) =>
        p.x >= bounds.min.x && p.x <= bounds.max.x && p.y >= bounds.min.y && p.y <= bounds.max.y;

    public static MapZone At(Vector3 p)
    {
        foreach (var z in All) if (z != null && z.Contains(p)) return z;
        return null;
    }

    public static MapZone Named(string name)
    {
        foreach (var z in All) if (z != null && z.zoneName == name) return z;
        return null;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.6f);
        Gizmos.DrawWireCube(bounds.center, bounds.size);
    }
}
