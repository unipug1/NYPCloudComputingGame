using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Image = UnityEngine.UI.Image;

public class LLM_Minigame_PropMotionBlur : MonoBehaviour
{
    [SerializeField] private Image targetImage;
    [SerializeField] private float blurAngle = -45f;
    [SerializeField] private float maxBlurAmount = 0.025f;

    private Material blurMatInstance;
    private Coroutine blurRoutine;

    private void Awake()
    {
        if (targetImage == null) targetImage = GetComponent<Image>();

        if (targetImage != null && targetImage.material != null)
        {
            blurMatInstance = new Material(targetImage.material);
            targetImage.material = blurMatInstance;
            blurMatInstance.SetFloat("_BlurAmount", 0f);
            blurMatInstance.SetFloat("_BlurAngle", blurAngle);
        }
    }

    public void TriggerBlur(float duration)
    {
        // safety check: never start coroutine if object is inactive or material is missing
        if (blurMatInstance == null || !gameObject.activeInHierarchy || !enabled)
        {
            return;
        }

        if (blurRoutine != null) StopCoroutine(blurRoutine);
        blurRoutine = StartCoroutine(BlurRoutine(duration));
    }

    private IEnumerator BlurRoutine(float duration)
    {
        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            float progress = timer / duration;

            float currentBlur = Mathf.Sin(progress * Mathf.PI) * maxBlurAmount;
            blurMatInstance.SetFloat("_BlurAmount", currentBlur);
            yield return null;
        }

        blurMatInstance.SetFloat("_BlurAmount", 0f);
    }

    public void ResetBlur()
    {
        if (blurRoutine != null) StopCoroutine(blurRoutine);
        if (blurMatInstance != null) blurMatInstance.SetFloat("_BlurAmount", 0f);
    }

    private void OnDestroy()
    {
        if (blurMatInstance != null)
        {
            Destroy(blurMatInstance);
        }
    }
}