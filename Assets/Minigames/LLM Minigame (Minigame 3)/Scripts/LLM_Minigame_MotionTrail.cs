using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Image = UnityEngine.UI.Image;

public class LLM_Minigame_MotionTrail : MonoBehaviour
{
    [Header("Trail Settings")]
    [SerializeField] private Image sourceImage;
    [SerializeField] private int ghostCount = 3;
    [SerializeField] private float ghostFadeTime = 0.15f;
    [SerializeField] private float initialGhostAlpha = 0.35f;

    private RectTransform sourceRect;
    private List<Image> ghostPool = new List<Image>();
    private Coroutine trailRoutine;

    private void Awake()
    {
        if (sourceImage == null)
        {
            sourceImage = GetComponent<Image>();
        }

        if (sourceImage != null)
        {
            sourceRect = sourceImage.GetComponent<RectTransform>();
            CreateGhostPool();
        }
    }

    private void CreateGhostPool()
    {
        for (int i = 0; i < ghostCount; i++)
        {
            GameObject ghostObj = new GameObject("Ghost_" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            ghostObj.transform.SetParent(transform.parent, false);
            ghostObj.transform.SetSiblingIndex(transform.GetSiblingIndex());

            Image img = ghostObj.GetComponent<Image>();
            img.sprite = sourceImage.sprite;
            img.color = new Color(sourceImage.color.r, sourceImage.color.g, sourceImage.color.b, 0f);
            img.raycastTarget = false;

            ghostObj.SetActive(false);
            ghostPool.Add(img);
        }
    }

    public void PlaySmear(float duration)
    {
        if (trailRoutine != null)
        {
            StopCoroutine(trailRoutine);
        }
        trailRoutine = StartCoroutine(SmearRoutine(duration));
    }

    private IEnumerator SmearRoutine(float duration)
    {
        float timer = 0f;
        int ghostIndex = 0;

        while (timer < duration)
        {
            timer += Time.deltaTime;

            if (ghostPool.Count > 0)
            {
                Image ghost = ghostPool[ghostIndex];
                ghost.sprite = sourceImage.sprite;
                ghost.gameObject.SetActive(true);

                RectTransform ghostRect = ghost.GetComponent<RectTransform>();
                ghostRect.anchoredPosition = sourceRect.anchoredPosition;
                ghostRect.localRotation = sourceRect.localRotation;
                ghostRect.localScale = sourceRect.localScale;
                ghostRect.sizeDelta = sourceRect.sizeDelta;

                StartCoroutine(FadeGhost(ghost));
                ghostIndex = (ghostIndex + 1) % ghostPool.Count;
            }

            yield return new WaitForSeconds(0.04f);
        }
    }

    private IEnumerator FadeGhost(Image ghost)
    {
        float fadeTimer = 0f;
        Color c = ghost.color;

        while (fadeTimer < ghostFadeTime)
        {
            fadeTimer += Time.deltaTime;
            c.a = Mathf.Lerp(initialGhostAlpha, 0f, fadeTimer / ghostFadeTime);
            ghost.color = c;
            yield return null;
        }

        c.a = 0f;
        ghost.color = c;
        ghost.gameObject.SetActive(false);
    }
}