using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class LLM_Minigame_ShadowEffect : MonoBehaviour
{
    [Header("Shadow Color and Opacity")]
    [SerializeField] private Color shadowColor = new Color(0f, 0f, 0f, 0.5f);

    [Header("Position and Angle")]
    [SerializeField] private bool useAngleAndDistance = true;
    [Range(0f, 360f)]
    [SerializeField] private float angle = 315f;
    [SerializeField] private float distance = 15f;
    [SerializeField] private Vector2 manualOffset = new Vector2(10f, -10f);

    private Image uiImage;
    private SpriteRenderer spriteRenderer;

    private GameObject shadowObject;
    private Image shadowUiImage;
    private SpriteRenderer shadowSpriteRenderer;
    private RectTransform myRect;
    private RectTransform shadowRect;

    private void OnEnable()
    {
        InitializeComponents();
        CreateOrUpdateShadow();
    }

    private void OnDisable()
    {
        if (shadowObject != null)
        {
            shadowObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (shadowObject != null)
        {
            if (Application.isPlaying)
            {
                Destroy(shadowObject);
            }
            else
            {
                DestroyImmediate(shadowObject);
            }
        }
    }

    private void LateUpdate()
    {
        UpdateShadowTransformAndVisuals();
    }

    private void InitializeComponents()
    {
        uiImage = GetComponent<Image>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        myRect = GetComponent<RectTransform>();
    }

    private Vector2 GetCurrentOffset()
    {
        if (useAngleAndDistance)
        {
            float radians = angle * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * distance;
        }
        return manualOffset;
    }

    private void CreateOrUpdateShadow()
    {
        if (uiImage == null && spriteRenderer == null)
        {
            return;
        }

        // if shadow object does not exist yet, create it
        if (shadowObject == null)
        {
            string shadowName = "[Shadow] " + gameObject.name;

            // check if one already exists in hierarchy from previous session
            if (uiImage != null && transform.parent != null)
            {
                Transform existing = transform.parent.Find(shadowName);
                if (existing != null) shadowObject = existing.gameObject;
            }
            else
            {
                Transform existing = transform.Find(shadowName);
                if (existing != null) shadowObject = existing.gameObject;
            }

            if (shadowObject == null)
            {
                shadowObject = new GameObject(shadowName);
            }
        }

        shadowObject.SetActive(gameObject.activeInHierarchy);

        // handle ui image setup
        if (uiImage != null)
        {
            // ui shadow must be a sibling right behind this image
            if (transform.parent != null)
            {
                shadowObject.transform.SetParent(transform.parent, false);
            }

            shadowRect = shadowObject.GetComponent<RectTransform>();
            if (shadowRect == null)
            {
                shadowRect = shadowObject.AddComponent<RectTransform>();
            }

            shadowUiImage = shadowObject.GetComponent<Image>();
            if (shadowUiImage == null)
            {
                shadowUiImage = shadowObject.AddComponent<Image>();
            }

            // shadow should never block mouse clicks
            shadowUiImage.raycastTarget = false;
        }
        // handle 2d sprite renderer setup
        else if (spriteRenderer != null)
        {
            // 2d game shadow can be a direct child
            shadowObject.transform.SetParent(transform, false);

            shadowSpriteRenderer = shadowObject.GetComponent<SpriteRenderer>();
            if (shadowSpriteRenderer == null)
            {
                shadowSpriteRenderer = shadowObject.AddComponent<SpriteRenderer>();
            }
        }

        UpdateShadowTransformAndVisuals();
    }

    private void UpdateShadowTransformAndVisuals()
    {
        if (shadowObject == null)
        {
            CreateOrUpdateShadow();
            return;
        }

        Vector2 offset = GetCurrentOffset();

        // update ui image shadow
        if (uiImage != null && shadowUiImage != null && shadowRect != null && myRect != null)
        {
            // make sure shadow sits right behind this object in the canvas draw order
            int myIndex = transform.GetSiblingIndex();
            int targetIndex = Mathf.Max(0, myIndex - 1);
            if (shadowObject.transform.GetSiblingIndex() != targetIndex)
            {
                shadowObject.transform.SetSiblingIndex(targetIndex);
            }

            // copy rect transform properties
            shadowRect.anchorMin = myRect.anchorMin;
            shadowRect.anchorMax = myRect.anchorMax;
            shadowRect.pivot = myRect.pivot;
            shadowRect.sizeDelta = myRect.sizeDelta;
            shadowRect.localRotation = myRect.localRotation;
            shadowRect.localScale = myRect.localScale;
            shadowRect.anchoredPosition = myRect.anchoredPosition + offset;

            // copy visual image properties
            shadowUiImage.sprite = uiImage.sprite;
            shadowUiImage.type = uiImage.type;
            shadowUiImage.preserveAspect = uiImage.preserveAspect;
            shadowUiImage.color = shadowColor;
        }
        // update 2d world sprite shadow
        else if (spriteRenderer != null && shadowSpriteRenderer != null)
        {
            shadowObject.transform.localPosition = new Vector3(offset.x, offset.y, 0f);
            shadowObject.transform.localRotation = Quaternion.identity;
            shadowObject.transform.localScale = Vector3.one;

            shadowSpriteRenderer.sprite = spriteRenderer.sprite;
            shadowSpriteRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
            // render one layer behind the original sprite
            shadowSpriteRenderer.sortingOrder = spriteRenderer.sortingOrder - 1;
            shadowSpriteRenderer.color = shadowColor;
        }
    }
}