using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CloudNetworkMeter : MonoBehaviour
{
    public Image fillImage;      // UI Image, Image Type = Filled
    public TMP_Text percentText;
    public float fillSpeed = 1f;

    float current;
    float target;

    // QuestManager calls this (value is 0 to 1)
    public void SetValue(float value)
    {
        target = Mathf.Clamp01(value);
    }

    void Update()
    {
        // Smoothly move the bar towards the target
        current = Mathf.MoveTowards(current, target, fillSpeed * Time.deltaTime);

        if (fillImage != null) fillImage.fillAmount = current;
        if (percentText != null) percentText.text = Mathf.RoundToInt(current * 100) + "%";
    }
}
