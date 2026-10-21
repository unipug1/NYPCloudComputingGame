using UnityEngine;

public class Waypoint : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private TeleportNodeSO nodeData;

    [Header("Event Channels")]
    [SerializeField] private VoidEventChannelSO m_NodeUnlockedEventChannel;

    [Header("Visuals")]
    [SerializeField] private SpriteRenderer worldSpriteRenderer;
    [SerializeField] private SpriteRenderer minimapSpriteRenderer;

    private void Start()
    {
        nodeData.worldPosition = transform.position;
        UpdateVisuals();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player") && !nodeData.isUnlocked)
        {
            nodeData.isUnlocked = true;
            UpdateVisuals();

            // raising event
            if (m_NodeUnlockedEventChannel != null)
            {
                m_NodeUnlockedEventChannel.RaiseEvent();
            }
            Debug.Log("${nodeData.nodeName} unlocked!");
        }
    }

    private void UpdateVisuals()
    {
        Sprite currentSprite = nodeData.isUnlocked ? nodeData.unlockedSprite : nodeData.lockedSprite;
        if (worldSpriteRenderer != null) worldSpriteRenderer.sprite = currentSprite;
        if (minimapSpriteRenderer != null) minimapSpriteRenderer.sprite = currentSprite;
    }
}