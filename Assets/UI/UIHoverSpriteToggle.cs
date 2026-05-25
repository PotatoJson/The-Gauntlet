using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIHoverSpriteToggle : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler, IBeginDragHandler, IEndDragHandler
{
    [Header("Visuals")]
    [Tooltip("Drag the child object containing your corner brackets here")]
    public Image selectionBrackets;

    [Header("Optional Link")]
    [Tooltip("If this is on a parent, drag the Slider/Button here. If empty, it will try to find one in children.")]
    [SerializeField] private Selectable targetSelectable;

    private bool isSelected = false;
    private bool isHovered = false;
    private bool isDragging = false;

    private void Awake()
    {
        if (selectionBrackets != null)
        {
            selectionBrackets.gameObject.SetActive(false);
        }

        if (targetSelectable == null)
        {
            targetSelectable = GetComponentInChildren<Selectable>();
        }

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

    private void UpdateVisuals()
    {
        if (selectionBrackets != null)
        {
            selectionBrackets.gameObject.SetActive(isSelected || isHovered || isDragging);
        }
    }

    public void OnSelect(BaseEventData eventData) { isSelected = true; UpdateVisuals(); }
    public void OnDeselect(BaseEventData eventData) { isSelected = false; UpdateVisuals(); }
    public void OnPointerEnter(PointerEventData eventData) { isHovered = true; UpdateVisuals(); }
    public void OnPointerExit(PointerEventData eventData) { isHovered = false; UpdateVisuals(); }
    public void OnBeginDrag(PointerEventData eventData) { isDragging = true; UpdateVisuals(); }
    public void OnEndDrag(PointerEventData eventData) { isDragging = false; UpdateVisuals(); }

    private void OnDisable()
    {
        isSelected = false;
        isHovered = false;
        isDragging = false;
        UpdateVisuals();
    }
}