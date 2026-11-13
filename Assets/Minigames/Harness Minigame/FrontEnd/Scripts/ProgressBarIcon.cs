using UnityEngine;
using UnityEngine.UI;

// Swaps the "?" icon for the medal when the Cloud Network bar reaches 100%.
public class ProgressBarIcon : MonoBehaviour
{
    public Image fill;            // the bar's fill Image (Filled)
    public Image icon;
    public Sprite questionSprite;
    public Sprite medalSprite;

    bool done;

    void Update()
    {
        if (fill == null || icon == null) return;
        bool full = fill.fillAmount >= 0.999f;
        if (full == done) return;
        done = full;
        icon.sprite = full ? medalSprite : questionSprite;
        icon.SetNativeSize();
        icon.rectTransform.sizeDelta *= 4f;
    }
}
