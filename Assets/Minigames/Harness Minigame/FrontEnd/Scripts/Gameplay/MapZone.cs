using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// One per map section (room). Set up by Tools > Gameplay > Build Map Gameplay.
// A point is "in" the room when one of the room's floor tilemaps has a tile there
// (the rooms of the new map overlap as boxes, so tiles decide). Smaller rooms win.
public class MapZone : MonoBehaviour
{
    public static readonly List<MapZone> All = new List<MapZone>();

    public string zoneName;
    public Bounds bounds;                       // world-space box around the room's floor
    public List<Tilemap> floors = new List<Tilemap>();
    public RoomData minimapRoom;                // matching entry in the MinimapManager (optional)

    void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    void OnDisable() { All.Remove(this); }

    float Area => bounds.size.x * bounds.size.y;

    public bool Contains(Vector3 p)
    {
        if (p.x < bounds.min.x || p.x > bounds.max.x || p.y < bounds.min.y || p.y > bounds.max.y) return false;
        if (floors == null || floors.Count == 0) return true;
        foreach (var tm in floors)
            if (tm != null && tm.HasTile(tm.WorldToCell(p))) return true;
        return false;
    }

    public static MapZone At(Vector3 p)
    {
        MapZone best = null;
        foreach (var z in All)
            if (z != null && z.Contains(p) && (best == null || z.Area < best.Area)) best = z;
        return best;
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
