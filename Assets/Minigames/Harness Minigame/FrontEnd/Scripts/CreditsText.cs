using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

// Fills the credits text from a .txt file (edit Credits/credits.txt - no rebuild needed)
// and makes <link="https://..."> parts clickable (opens in a new tab in WebGL).
[RequireComponent(typeof(TMP_Text))]
public class CreditsText : MonoBehaviour, IPointerClickHandler
{
    public TextAsset source;
    TMP_Text text;

    void Awake()
    {
        text = GetComponent<TMP_Text>();
        if (source != null) text.text = source.text;
    }

    public void OnPointerClick(PointerEventData e)
    {
        var canvas = text.canvas;
        Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        int i = TMP_TextUtilities.FindIntersectingLink(text, e.position, cam);
        if (i < 0) return;
        var url = text.textInfo.linkInfo[i].GetLinkID();
        if (!string.IsNullOrEmpty(url)) Application.OpenURL(url);
    }
}
