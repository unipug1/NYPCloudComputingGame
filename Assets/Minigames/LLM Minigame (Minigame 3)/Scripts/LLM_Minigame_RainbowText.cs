using TMPro;
using UnityEngine;

public class LLM_Minigame_RainbowText : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI textMesh;

    [Header("smooth slow wave")]
    [SerializeField] private float rainbowSpeed = 0.45f;
    [SerializeField] private float colorSpread = 0.05f;

    [Header("contrast tuning for white background")]
    [Range(0.8f, 1f)][SerializeField] private float saturation = 0.95f;
    [Range(0.5f, 0.75f)][SerializeField] private float valueBrightness = 0.65f;

    private void Awake()
    {
        if (textMesh == null)
        {
            textMesh = GetComponent<TextMeshProUGUI>();
        }
    }

    private void Update()
    {
        if (textMesh == null || string.IsNullOrEmpty(textMesh.text) || !textMesh.gameObject.activeInHierarchy)
        {
            return;
        }

        textMesh.ForceMeshUpdate();
        TMP_TextInfo textInfo = textMesh.textInfo;
        int characterCount = textInfo.characterCount;

        if (characterCount == 0) return;

        for (int i = 0; i < characterCount; i++)
        {
            if (!textInfo.characterInfo[i].isVisible) continue;

            int materialIndex = textInfo.characterInfo[i].materialReferenceIndex;
            int vertexIndex = textInfo.characterInfo[i].vertexIndex;
            Color32[] newVertexColors = textInfo.meshInfo[materialIndex].colors32;

            // smooth, slow wave through high-contrast jewel tones
            float hue = (Time.time * rainbowSpeed + i * colorSpread) % 1f;
            Color32 readableRainbowColor = Color.HSVToRGB(hue, saturation, valueBrightness);

            newVertexColors[vertexIndex + 0] = readableRainbowColor;
            newVertexColors[vertexIndex + 1] = readableRainbowColor;
            newVertexColors[vertexIndex + 2] = readableRainbowColor;
            newVertexColors[vertexIndex + 3] = readableRainbowColor;
        }

        textMesh.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
    }

    public void SetReasonTag(string reason)
    {
        if (textMesh == null) return;

        if (string.IsNullOrEmpty(reason))
        {
            textMesh.text = "";
            gameObject.SetActive(false);
        }
        else
        {
            // formats explicitly as REASON: {REASON}
            textMesh.text = "REASON: " + reason.Trim().ToUpper();
            gameObject.SetActive(true);
        }
    }
}