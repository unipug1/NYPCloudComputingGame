using UnityEngine;

public class LLM_Minigame_MouseParallax : MonoBehaviour
{
    [Header("Movement Offsets")]
    [SerializeField] private Vector2 maxPositionOffset = new Vector2(8f, 6f);
    [SerializeField] private float maxRotationZ = 1.8f;

    [Header("Smoothing")]
    [SerializeField] private float smoothSpeed = 6f;

    [Header("Invert Axis")]
    [SerializeField] private bool invertX = false;
    [SerializeField] private bool invertY = false;

    private RectTransform targetRect;
    private Vector2 defaultPos;
    private float defaultRotZ;
    private bool isActive = true;

    private void Awake()
    {
        targetRect = GetComponent<RectTransform>();
        if (targetRect != null)
        {
            defaultPos = targetRect.anchoredPosition;
            defaultRotZ = targetRect.localEulerAngles.z;
        }
    }

    private void Update()
    {
        if (!isActive || targetRect == null) return;

        // calculate mouse position normalized from screen center (-1 to 1)
        float mouseNormX = (Input.mousePosition.x / Screen.width - 0.5f) * 2f;
        float mouseNormY = (Input.mousePosition.y / Screen.height - 0.5f) * 2f;

        mouseNormX = Mathf.Clamp(mouseNormX, -1f, 1f);
        mouseNormY = Mathf.Clamp(mouseNormY, -1f, 1f);

        if (invertX) mouseNormX = -mouseNormX;
        if (invertY) mouseNormY = -mouseNormY;

        // target micro offset
        Vector2 targetPos = defaultPos + new Vector2(mouseNormX * maxPositionOffset.x, mouseNormY * maxPositionOffset.y);
        float targetRotZ = defaultRotZ + (mouseNormX * maxRotationZ);

        // smooth damped lerp so it feels weighted like physical hands
        targetRect.anchoredPosition = Vector2.Lerp(targetRect.anchoredPosition, targetPos, Time.deltaTime * smoothSpeed);
        targetRect.localRotation = Quaternion.Lerp(targetRect.localRotation, Quaternion.Euler(0f, 0f, targetRotZ), Time.deltaTime * smoothSpeed);
    }

    public void SetActive(bool enable)
    {
        isActive = enable;
        if (!enable && targetRect != null)
        {
            targetRect.anchoredPosition = defaultPos;
            targetRect.localRotation = Quaternion.Euler(0f, 0f, defaultRotZ);
        }
    }
}