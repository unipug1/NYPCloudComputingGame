using System.Collections.Generic;
using UnityEngine;

// Put one on each quest area of the map (in the map scene).
// The quest panel teleports the player to its Spawn Point and shows the area name.
public class MapArea : MonoBehaviour
{
    public static readonly List<MapArea> All = new List<MapArea>();

    [Tooltip("Shown on screen when the player arrives, e.g. \"Server Room\"")]
    public string areaName = "Area name";

    [Tooltip("Must match the quest name in QuestManager, e.g. \"LLM Game\"")]
    public string questName;

    [Tooltip("Where the player appears. Leave empty to use this object's position.")]
    public Transform spawnPoint;

    public Vector3 SpawnPosition => spawnPoint ? spawnPoint.position : transform.position;

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    public static MapArea ForQuest(string quest) => All.Find(a => a.questName == quest);

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.1f, 0.6f, 1f, 0.8f);
        Gizmos.DrawWireSphere(SpawnPosition, 0.5f);
    }
}
