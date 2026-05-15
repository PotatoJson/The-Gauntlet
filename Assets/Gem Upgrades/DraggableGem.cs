using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;
using UnityEngine.UI;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CanvasGroup))]
public class DraggableGem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler, ISubmitHandler
{
    [Header("Back End Stuff")]
    public GemData LinkedGemData;

    [HideInInspector] public Transform parentAfterDrag;

    [Tooltip("Can be null in the new Reward System!")]
    [HideInInspector] public Transform originalInventoryGrid;

    [Header("Visuals")]
    [SerializeField] private Outline selectionOutline;

    [Header("Gem Details")]
    public string gemName = "Unknown Gem";
    [TextArea(3, 5)]
    public string gemDescription = "A mysterious gem with untold power.";
    public Sprite gemIcon;

    private CanvasGroup _canvasGroup;
    private RectTransform _rectTransform;

    private bool _canDrag = true;
    private Vector2 _originalSizeDelta;

    public GameObject originalPrefab;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        _canvasGroup = GetComponent<CanvasGroup>();
        _originalSizeDelta = _rectTransform.sizeDelta;
    }

    private void Start()
    {
        originalInventoryGrid = transform.parent;
        if (selectionOutline != null) selectionOutline.enabled = false;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (GemPopupMenu.Instance != null &&
           (GemPopupMenu.Instance.gameObject.activeInHierarchy || GemPopupMenu.Instance.IsPlacingMode))
        {
            eventData.pointerDrag = null;
            return;
        }

        if (RewardMenuManager.Instance != null && RewardMenuManager.Instance.IsRewardModeActive())
        {
            if (!RewardMenuManager.Instance.CanDragGem(this))
            {
                _canDrag = false;

                transform.DOKill();
                transform.DOShakePosition(0.4f, new Vector3(15, 0, 0), 25, 90, false, true);
                return;
            }
        }

        _canDrag = true;
        parentAfterDrag = transform.parent;
        transform.SetParent(transform.root);
        transform.SetAsLastSibling();

        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.alpha = 0.8f;

        _rectTransform.DOKill();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_canDrag) return;
        _rectTransform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_canDrag) return;
        transform.SetParent(parentAfterDrag);
        _canvasGroup.blocksRaycasts = true;
        _canvasGroup.alpha = 1f;

        AnimateToNewHome();
    }

    public void ReturnToInventory()
    {
        if (originalInventoryGrid != null)
        {
            parentAfterDrag = originalInventoryGrid;

            transform.SetParent(originalInventoryGrid);
            _rectTransform.sizeDelta = _originalSizeDelta;
            AnimateToNewHome();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public bool IsEquipped()
    {
        return transform.parent != null && transform.parent.name.Contains("Slot");
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {   //These BREAK GEMS
            if (Mouse.current != null && (Mouse.current.leftButton.wasReleasedThisFrame || Mouse.current.leftButton.wasPressedThisFrame)) return;
            if (Keyboard.current != null && (Keyboard.current.enterKey.wasReleasedThisFrame || Keyboard.current.spaceKey.wasReleasedThisFrame)) return;

            if (!IsEquipped())
            {
                if (RewardMenuManager.Instance != null && RewardMenuManager.Instance.IsRewardModeActive())
                {
                    if (!RewardMenuManager.Instance.CanDragGem(this))
                    {
                        return;
                    }
                }
            }

            if (GemPopupMenu.Instance != null)
            {
                RectTransform myRect = GetComponent<RectTransform>();
                GemPopupMenu.Instance.OpenMenu(this, myRect);
            }
        }
    }

    // --- UPDATED: Block the gamepad's submit button if the gem is locked! ---
    public void ForceOpenPopUI()
    {
        if (!IsEquipped())
        {
            if (RewardMenuManager.Instance != null && RewardMenuManager.Instance.IsRewardModeActive())
            {
                if (!RewardMenuManager.Instance.CanDragGem(this))
                {
                    transform.DOKill();
                    transform.DOShakePosition(0.4f, new Vector3(15, 0, 0), 25, 90, false, true);
                    return;
                }
            }
        }

        if (GemPopupMenu.Instance != null)
        {
            GemPopupMenu.Instance.OpenMenu(this, _rectTransform);
        }
    }

    public void AnimateToNewHome()
    {
        if (transform.parent != null && transform.parent.GetComponent<UnityEngine.UI.LayoutGroup>() == null)
        {
            _rectTransform.DOLocalMove(Vector3.zero, 0.25f).SetEase(Ease.OutQuad).SetUpdate(true);
        }

        transform.localScale = Vector3.one * 0.6f;
        transform.DOScale(Vector3.one, 0.35f).SetEase(Ease.OutBack).SetUpdate(true);
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (selectionOutline != null) selectionOutline.enabled = true;
    }

    public void OnDeselect(BaseEventData eventData)
    {
        if (selectionOutline != null) selectionOutline.enabled = false;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (selectionOutline != null) selectionOutline.enabled = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != gameObject)
        {
            if (selectionOutline != null) selectionOutline.enabled = false;
        }
    }

    public void OnSubmit(BaseEventData eventData)
    {
        ForceOpenPopUI();
    }
}