using TMPro;
using UnityEngine.UI;
using UnityEngine;

public class TeleportConfirmUI : MonoBehaviour
{
    [Header("Event Channels")]
    [SerializeField] private TeleportNodeEventChannelSO m_TeleportRequestChannel;

    [Header("UI Elements")]
    [SerializeField] private GameObject windowPanel;
    [SerializeField] private TextMeshProUGUI promptText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    private TeleportNodeSO pendingNode;
    private Transform playerTransform;

    private void Awake()
    {
        confirmButton.onClick.AddListener(ExecuteTeleport);
        cancelButton.onClick.AddListener(CloseWindow);
        windowPanel.SetActive(false);
    }

    private void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) playerTransform = player.transform;
    }

    private void OnEnable()
    {
        if (m_TeleportRequestChannel != null)
        {
            m_TeleportRequestChannel.OnEventRaised += ShowPrompt;
        }
    }

    private void OnDisable()
    {
        if (m_TeleportRequestChannel != null)
        {
            m_TeleportRequestChannel.OnEventRaised -= ShowPrompt;
        }
    }

    private void ShowPrompt(TeleportNodeSO node)
    {
        pendingNode = node;
        promptText.text = $"Teleport to {node.nodeName}?";
        windowPanel.SetActive(true);
    }

    private void ExecuteTeleport()
    {
        if (pendingNode != null && playerTransform != null)
        {
            playerTransform.position = pendingNode.worldPosition;
            CloseWindow();
        }
    }

    private void CloseWindow()
    {
        pendingNode = null;
        windowPanel.SetActive(false);
    }
}