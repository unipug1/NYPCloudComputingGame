using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class LLM_Minigame_TrustMeter : MonoBehaviour
{
    [Header("Meter Visuals")]
    [SerializeField] private RectTransform meterRootRect;
    [SerializeField] private UnityEngine.UI.Image fillImage;
    [SerializeField] private float fillSpeed = 2f;

    [Header("Floating Change Number")]
    [SerializeField] private TextMeshProUGUI changeNumberText;
    [SerializeField] private Color gainColor = new Color(0.2f, 1f, 0.3f);
    [SerializeField] private Color loseColor = new Color(1f, 0.2f, 0.2f);
    [SerializeField] private float floatDistance = 30f;

    [Header("Shake Settings")]
    [SerializeField] private float shakeDuration = 0.35f;
    [SerializeField] private float shakeMagnitude = 8f;

    private float currentTrust = 10f;
    private Vector2 meterDefaultPos;
    private Vector2 textDefaultPos;

    private Coroutine fillRoutine;
    private Coroutine shakeRoutine;
    private Coroutine numberRoutine;

    private void Awake()
    {
        if (meterRootRect == null)
        {
            meterRootRect = GetComponent<RectTransform>();
        }

        if (meterRootRect != null)
        {
            meterDefaultPos = meterRootRect.anchoredPosition;
        }

        if (changeNumberText != null)
        {
            textDefaultPos = changeNumberText.rectTransform.anchoredPosition;
            Color c = changeNumberText.color;
            c.a = 0f;
            changeNumberText.color = c;
        }
    }

    private void Start()
    {
        currentTrust = 10f;
        if (fillImage != null)
        {
            fillImage.fillAmount = 0.1f;
        }
    }

    public float GetCurrentTrust()
    {
        return currentTrust;
    }

    public void ChangeTrust(float delta)
    {
        if (Mathf.Approximately(delta, 0f))
        {
            return;
        }

        float target = currentTrust + delta;
        target = Mathf.Clamp(target, 10f, 100f);

        if (delta < 0f)
        {
            if (shakeRoutine != null)
            {
                StopCoroutine(shakeRoutine);
            }
            shakeRoutine = StartCoroutine(ShakeRoutine());
        }

        if (changeNumberText != null)
        {
            if (numberRoutine != null)
            {
                StopCoroutine(numberRoutine);
            }
            numberRoutine = StartCoroutine(FloatingNumberRoutine(delta));
        }

        currentTrust = target;

        if (fillRoutine != null)
        {
            StopCoroutine(fillRoutine);
        }
        fillRoutine = StartCoroutine(SmoothFillRoutine(target / 100f));
    }

    public void ResetTrust()
    {
        currentTrust = 10f;

        if (fillRoutine != null) StopCoroutine(fillRoutine);
        if (shakeRoutine != null) StopCoroutine(shakeRoutine);
        if (numberRoutine != null) StopCoroutine(numberRoutine);

        if (fillImage != null)
        {
            fillImage.fillAmount = 0.1f;
        }

        if (meterRootRect != null)
        {
            meterRootRect.anchoredPosition = meterDefaultPos;
        }

        if (changeNumberText != null)
        {
            Color c = changeNumberText.color;
            c.a = 0f;
            changeNumberText.color = c;
            changeNumberText.rectTransform.anchoredPosition = textDefaultPos;
        }
    }

    private IEnumerator SmoothFillRoutine(float targetFill)
    {
        while (fillImage != null && Mathf.Abs(fillImage.fillAmount - targetFill) > 0.005f)
        {
            fillImage.fillAmount = Mathf.MoveTowards(fillImage.fillAmount, targetFill, fillSpeed * Time.deltaTime);
            yield return null;
        }

        if (fillImage != null)
        {
            fillImage.fillAmount = targetFill;
        }
    }

    private IEnumerator ShakeRoutine()
    {
        float timer = 0f;

        while (timer < shakeDuration)
        {
            timer += Time.deltaTime;
            float currentMag = Mathf.Lerp(shakeMagnitude, 0f, timer / shakeDuration);
            float offsetX = Random.Range(-currentMag, currentMag);
            float offsetY = Random.Range(-currentMag, currentMag);

            meterRootRect.anchoredPosition = new Vector2(meterDefaultPos.x + offsetX, meterDefaultPos.y + offsetY);
            yield return null;
        }

        meterRootRect.anchoredPosition = meterDefaultPos;
    }

    private IEnumerator FloatingNumberRoutine(float delta)
    {
        bool isGain = delta > 0f;
        changeNumberText.text = isGain ? "+" + Mathf.RoundToInt(delta) : Mathf.RoundToInt(delta).ToString();
        changeNumberText.color = isGain ? gainColor : loseColor;

        RectTransform textRect = changeNumberText.rectTransform;
        textRect.anchoredPosition = textDefaultPos;

        Color textColor = changeNumberText.color;

        float fadeInTime = 0.1f;
        float timer = 0f;
        while (timer < fadeInTime)
        {
            timer += Time.deltaTime;
            textColor.a = Mathf.Lerp(0f, 1f, timer / fadeInTime);
            changeNumberText.color = textColor;
            yield return null;
        }

        textColor.a = 1f;
        changeNumberText.color = textColor;

        float fadeOutTime = 0.65f;
        timer = 0f;
        Vector2 startPos = textDefaultPos;
        Vector2 endPos = new Vector2(textDefaultPos.x, textDefaultPos.y + floatDistance);

        while (timer < fadeOutTime)
        {
            timer += Time.deltaTime;
            float progress = timer / fadeOutTime;

            textColor.a = Mathf.Lerp(1f, 0f, progress);
            changeNumberText.color = textColor;

            textRect.anchoredPosition = Vector2.Lerp(startPos, endPos, progress);
            yield return null;
        }

        textColor.a = 0f;
        changeNumberText.color = textColor;
        textRect.anchoredPosition = textDefaultPos;
    }
}