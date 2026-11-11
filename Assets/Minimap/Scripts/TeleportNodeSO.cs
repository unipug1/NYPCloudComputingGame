using UnityEngine;

[CreateAssetMenu(fileName = "New Teleport Node", menuName = "Teleport System/Node")]
public class TeleportNodeSO : ScriptableObject
{
    public string nodeName;
    public string nodeID;
    public Vector2 worldPosition;
    public bool isUnlocked;

    public Sprite lockedSprite, unlockedSprite;

    // reset state when entering game start
    public void ResetNode()
    {
        isUnlocked = false;
    }
}