using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class UIFocusLabel : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler, IBeginDragHandler, IEndDragHandler
{
    [SerializeField] private TextMeshProUGUI labelText;
    [SerializeField] private Color selectedColor = Color.red;
    [SerializeField] private Color normalColor = Color.black;

    [Header("Optional Link")]
    [Tooltip("If this is on a parent, drag the Slider/Button here. If empty, it will try to find one in children.")]
    [SerializeField] private Selectable targetSelectable;

    private bool isSelected = false;
    private bool isHovered = false;
    private bool isDragging = false;

    private void Awake()
    {
        // If no target is set, look for a selectable in children
        if (targetSelectable == null)
        {
            targetSelectable = GetComponentInChildren<Selectable>();
        }

        // If we found a target that is NOT this object, hook its events
        if (targetSelectable != null && targetSelectable.gameObject != gameObject)
        {
            HookSelectable(targetSelectable.gameObject);
        }
    }

    private void HookSelectable(GameObject go)
    {
        EventTrigger trigger = go.GetComponent<EventTrigger>();
        if (trigger == null) trigger = go.AddComponent<EventTrigger>();

        AddTrigger(trigger, EventTriggerType.Select, (data) => OnSelect(data));
        AddTrigger(trigger, EventTriggerType.Deselect, (data) => OnDeselect(data));
        AddTrigger(trigger, EventTriggerType.PointerEnter, (data) => OnPointerEnter((PointerEventData)data));
        AddTrigger(trigger, EventTriggerType.PointerExit, (data) => OnPointerExit((PointerEventData)data));
        AddTrigger(trigger, EventTriggerType.BeginDrag, (data) => OnBeginDrag((PointerEventData)data));
        AddTrigger(trigger, EventTriggerType.EndDrag, (data) => OnEndDrag((PointerEventData)data));
    }

    private void AddTrigger(EventTrigger trigger, EventTriggerType type, System.Action<BaseEventData> action)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener((data) => action(data));
        trigger.triggers.Add(entry);
    }

    private void UpdateColor()
    {
        if (labelText != null)
        {
            labelText.color = (isSelected || isHovered || isDragging) ? selectedColor : normalColor;
        }
    }

    public void OnSelect(BaseEventData eventData) { isSelected = true; UpdateColor(); }
    public void OnDeselect(BaseEventData eventData) { isSelected = false; UpdateColor(); }
    public void OnPointerEnter(PointerEventData eventData) { isHovered = true; UpdateColor(); }
    public void OnPointerExit(PointerEventData eventData) { isHovered = false; UpdateColor(); }
    public void OnBeginDrag(PointerEventData eventData) { isDragging = true; UpdateColor(); }
    public void OnEndDrag(PointerEventData eventData) { isDragging = false; UpdateColor(); }

    private void OnDisable()
    {
        isSelected = false;
        isHovered = false;
        isDragging = false;
        ResetColor();
    }

    private void ResetColor()
    {
        if (labelText != null) labelText.color = normalColor;
    }
}