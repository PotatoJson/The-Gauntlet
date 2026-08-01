using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.Localization;
public enum GemType { Stat, Skill }

[RequireComponent(typeof(CanvasGroup))]
public class DraggableGem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler, ISubmitHandler
//IPointerClickHandler
{
    [Header("Back End Stuff")]
    public GemData LinkedGemData;

    [HideInInspector] public Transform parentAfterDrag;

    [Tooltip("Can be null in the new Reward System!")]
    [HideInInspector] public Transform originalInventoryGrid;

    [Header("Visuals")]
    [SerializeField] private GameObject selectionBracket;

    [Header("Gem Details")]
    public LocalizedString gemName;
    public LocalizedString gemDescription;
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
        if (selectionBracket != null) selectionBracket.SetActive(false);
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
                transform.DOShakePosition(0.4f, new Vector3(15, 0, 0), 25, 90, false, true)
                    .SetUpdate(true)
                    .SetLink(gameObject);
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

        if (RewardMenuManager.Instance != null && RewardMenuManager.Instance.mainPaperPlate != null)
        {
            Vector3[] plateCorners = new Vector3[4];
            RewardMenuManager.Instance.mainPaperPlate.GetWorldCorners(plateCorners);

            Vector3 clampedPos = _rectTransform.position;

            float halfWidth = (_rectTransform.rect.width * _rectTransform.lossyScale.x) / 2f;
            float halfHeight = (_rectTransform.rect.height * _rectTransform.lossyScale.y) / 2f;

            clampedPos.x = Mathf.Clamp(clampedPos.x, plateCorners[0].x + halfWidth, plateCorners[2].x - halfWidth);
            clampedPos.y = Mathf.Clamp(clampedPos.y, plateCorners[0].y + halfHeight, plateCorners[2].y - halfHeight);

            _rectTransform.position = clampedPos;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_canDrag) return;
        transform.SetParent(parentAfterDrag);
        _canvasGroup.blocksRaycasts = true;
        _canvasGroup.alpha = 1f;

        AnimateToNewHome();

        if (InventoryManager.Instance != null && gameObject.activeInHierarchy)
        {
            InventoryManager.Instance.SetupNavigation();
        }
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


    public void ForceOpenPopUI()
    {
        if (!IsEquipped())
        {
            if (RewardMenuManager.Instance != null && RewardMenuManager.Instance.IsRewardModeActive())
            {
                if (!RewardMenuManager.Instance.CanDragGem(this))
                {
                    transform.DOKill();
                    transform.DOShakePosition(0.4f, new Vector3(15, 0, 0), 25, 90, false, true)
                        .SetUpdate(true)
                        .SetLink(gameObject);
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
        if (IsEquipped())
        {
            _rectTransform.DOLocalMove(Vector3.zero, 0.25f)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        transform.localScale = Vector3.one * 0.6f;
        transform.DOScale(Vector3.one, 0.35f)
            .SetEase(Ease.OutBack)
            .SetUpdate(true)
            .SetLink(gameObject);
    }

    /// <summary>
    /// Forces this gem's bracket off. Called when input switches to the controller, where the
    /// pointer stops sending exit events and the hover highlight would otherwise stay lit.
    /// </summary>
    public void HideSelectionBracket()
    {
        if (selectionBracket != null) selectionBracket.SetActive(false);
    }

    // Only one input source is allowed to draw the bracket at a time. On a controller there is no
    // hover, so EventSystem selection owns it; with a mouse the pointer owns it and selection is
    // ignored, otherwise a left-over selection and the current hover light up two gems at once.

    public void OnSelect(BaseEventData eventData)
    {
        if (selectionBracket != null && InputHelper.IsGamepadLastUsed()) selectionBracket.SetActive(true);
        if (InventoryManager.Instance != null) InventoryManager.Instance.SetFocusedGem(this, true);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        if (selectionBracket != null) selectionBracket.SetActive(false);
        if (InventoryManager.Instance != null) InventoryManager.Instance.SetFocusedGem(this, false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (selectionBracket != null && !InputHelper.IsGamepadLastUsed()) selectionBracket.SetActive(true);
        if (InventoryManager.Instance != null) InventoryManager.Instance.SetHoveredGem(this, true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // Used to keep the bracket alive when this gem was also the selected object, which is
        // precisely how a hover highlight and a selection highlight ended up on screen together.
        if (selectionBracket != null && !InputHelper.IsGamepadLastUsed()) selectionBracket.SetActive(false);
        if (InventoryManager.Instance != null) InventoryManager.Instance.SetHoveredGem(this, false);
    }

    public void OnSubmit(BaseEventData eventData)
    {
        ForceOpenPopUI();
    }

    private void OnDestroy()
    {
        transform.DOKill();
        if (_rectTransform != null) _rectTransform.DOKill();
    }
}