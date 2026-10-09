using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class LLM_Minigame_CharacterView : MonoBehaviour
{
    [System.Serializable]
    public struct EmotionSprite
    {
        public string emotionKey;
        public Sprite sprite;
    }

    [SerializeField] private Image characterImage;
    [SerializeField] private EmotionSprite[] emotionList;

    [Header("Bounce Settings")]
    [SerializeField] private float bounceHeight = 35f;
    [SerializeField] private float bounceDuration = 0.35f;

    [Header("Comic Mark")]
    [SerializeField] private LLM_Minigame_ManpuEffect manpuEffect;

    private RectTransform rectTransform;
    private Vector2 defaultAnchoredPos;
    private string currentEmotionKey = "";
    private Coroutine bounceRoutine;

    private void Awake()
    {
        if (characterImage != null)
        {
            rectTransform = characterImage.GetComponent<RectTransform>();
            defaultAnchoredPos = rectTransform.anchoredPosition;
        }
    }

    public void ApplyEmotion(string newEmotionKey)
    {
        // if emotion is the same we do not change anything and do not bounce
        if (newEmotionKey == currentEmotionKey)
        {
            return;
        }

        currentEmotionKey = newEmotionKey;

        // swap the sprite
        for (int i = 0; i < emotionList.Length; i++)
        {
            if (emotionList[i].emotionKey.Equals(newEmotionKey, StringComparison.OrdinalIgnoreCase))
            {
                if (characterImage != null)
                {
                    characterImage.sprite = emotionList[i].sprite;
                }
                break;
            }
        }

        // trigger the comic realization mark pop
        if (manpuEffect != null)
        {
            manpuEffect.PlayPop();
        }

        // play bounce because the emotion actually changed
        if (bounceRoutine != null)
        {
            StopCoroutine(bounceRoutine);
        }
        bounceRoutine = StartCoroutine(CustomBounceRoutine());
    }

    // custom bounce using a sine wave so we do not need external packages
    private IEnumerator CustomBounceRoutine()
    {
        if (rectTransform == null) yield break;

        float timer = 0f;
        while (timer < bounceDuration)
        {
            timer += Time.deltaTime;
            float progress = timer / bounceDuration;

            // sine gives a nice smooth up and down curve
            float yOffset = Mathf.Sin(progress * Mathf.PI) * bounceHeight;
            rectTransform.anchoredPosition = new Vector2(defaultAnchoredPos.x, defaultAnchoredPos.y + yOffset);

            yield return null;
        }

        rectTransform.anchoredPosition = defaultAnchoredPos;
    }

    public void ResetCharacter(string defaultKey)
    {
        currentEmotionKey = "";
        ApplyEmotion(defaultKey);
    }
}