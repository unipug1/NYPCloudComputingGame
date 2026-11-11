using UnityEngine;
using UnityEngine.UI;

public class MinimapNodeButton : MonoBehaviour
{
    [SerializeField] private TeleportNodeSO nodeData;
    [SerializeField] private Image uiImage;

    [Header("Event Channels")]
    [SerializeField] private VoidEventChannelSO m_NodeUnlockedEventChannel;
    [SerializeField] private TeleportNodeEventChannelSO m_TeleportRequestChannel;

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(OnIconClicked);
    }

    private void OnEnable()
    {
        // listening to channel on enable
        if (m_NodeUnlockedEventChannel != null)
        {
            m_NodeUnlockedEventChannel.OnEventRaised += RefreshUI;
        }

        RefreshUI();
    }

    private void OnDisable()
    {
        // unsubscribe
        if (m_NodeUnlockedEventChannel != null)
        {
            m_NodeUnlockedEventChannel.OnEventRaised -= RefreshUI;
        }
    }

    public void RefreshUI()
    {
        uiImage.sprite = nodeData.isUnlocked ? nodeData.unlockedSprite : nodeData.lockedSprite;
        button.interactable = nodeData.isUnlocked;
    }

    private void OnIconClicked()
    {
        if (m_TeleportRequestChannel != null)
        {
            m_TeleportRequestChannel.RaiseEvent(nodeData);
        }
    }
}