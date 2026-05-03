using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class DraggableGem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler, ISubmitHandler
{
    [HideInInspector] public Transform parentAfterDrag;
    [HideInInspector] public Transform originalInventoryGrid; // NEW: Remembers its specific category

    [Header("Visuals")]
    [SerializeField] private Outline selectionOutline;

    private CanvasGroup _canvasGroup;
    private RectTransform _rectTransform;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        _canvasGroup = GetComponent<CanvasGroup>();
    }

    private void Start()
    {
        // NEW: The moment the gem spawns, remember its starting inventory grid
        originalInventoryGrid = transform.parent;

        if (selectionOutline != null) selectionOutline.enabled = false;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        parentAfterDrag = transform.parent;
        transform.SetParent(transform.root);
        transform.SetAsLastSibling();

        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.alpha = 0.8f;

        _rectTransform.DOKill();
    }

    public void OnDrag(PointerEventData eventData)
    {
        _rectTransform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        transform.SetParent(parentAfterDrag);
        _canvasGroup.blocksRaycasts = true;
        _canvasGroup.alpha = 1f;

        AnimateToNewHome();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Detect if it was a Right-Click
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            // Tell the popup menu to open and pass this specific gem's data to it
            if (GemPopupMenu.Instance != null)
            {
                GemPopupMenu.Instance.OpenMenu(this, _rectTransform);
            }
        }
    }

    public void AnimateToNewHome()
    {
        // 1. Check if the new parent is a Grid/Layout Group
        if (transform.parent.GetComponent<UnityEngine.UI.LayoutGroup>() == null)
        {
            // NOT a grid (it's a Gauntlet slot). Safe to do the slide animation!
            _rectTransform.DOLocalMove(Vector3.zero, 0.25f).SetEase(Ease.OutQuad).SetUpdate(true);
        }

        // 2. The "Pop" Animation (This always runs, even in the grid!)
        transform.localScale = Vector3.one * 0.6f;
        transform.DOScale(Vector3.one, 0.35f).SetEase(Ease.OutBack).SetUpdate(true);
    }
    // --- CONTROLLER / KEYBOARD HIGHLIGHT LOGIC ---

    public void OnSelect(BaseEventData eventData)
    {
        // Turned on when the EventSystem selects it (D-pad/Arrow keys)
        if (selectionOutline != null) selectionOutline.enabled = true;
    }

    public void OnDeselect(BaseEventData eventData)
    {
        // Turned off when the cursor moves away
        if (selectionOutline != null) selectionOutline.enabled = false;
    }

    // --- MOUSE HOVER LOGIC ---

    public void OnPointerEnter(PointerEventData eventData)
    {
        // Turned on when the mouse hovers over it
        if (selectionOutline != null) selectionOutline.enabled = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // Turned off when the mouse leaves... 
        // BUT only if the controller isn't currently selecting it!
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != gameObject)
        {
            if (selectionOutline != null) selectionOutline.enabled = false;
        }
    }

    public void OnSubmit(BaseEventData eventData)
    {
        if (GemPopupMenu.Instance != null)
        {
            GemPopupMenu.Instance.OpenMenu(this, _rectTransform);
        }
    }

}