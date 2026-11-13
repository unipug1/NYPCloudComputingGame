using UnityEngine;
using UnityEngine.EventSystems;

// Shows the "< ENTER" hint next to a button while it's hovered or selected.
public class ButtonHoverHint : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    public GameObject hint;

    void OnEnable() { if (hint) hint.SetActive(false); }
    void OnDisable() { if (hint) hint.SetActive(false); }

    public void OnPointerEnter(PointerEventData e) { if (hint) hint.SetActive(true); }
    public void OnPointerExit(PointerEventData e)  { if (hint) hint.SetActive(false); }
    public void OnSelect(BaseEventData e)          { if (hint) hint.SetActive(true); }
    public void OnDeselect(BaseEventData e)        { if (hint) hint.SetActive(false); }
}
