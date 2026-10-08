using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.Localization;
public enum GemType { Stat, Skill }

[RequireComponent(typeof(CanvasGroup))]
public class DraggableGem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler, ISubmitHandler, IDropHandler
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

    public const int MaxTier = 5;

    /// <summary>
    /// This gem's tier. Starts at the linked data's GemTier (1 for every gem) and a stat gem's
    /// modifiers are multiplied by it. Raised by <see cref="GemCrafting"/>, carried between levels
    /// by PersistentEquipment.
    /// </summary>
    // 0 = never set, so the data's tier is used. Not initialised in Awake: gems restored under the
    // hidden character screen get their tier assigned before Awake runs, and Awake would overwrite it.
    private int _tier;
    public int Tier
    {
        get => _tier > 0 ? _tier : Mathf.Clamp(LinkedGemData != null ? LinkedGemData.GemTier : 1, 1, MaxTier);
        set
        {
            _tier = Mathf.Clamp(value, 1, MaxTier);
            RefreshTierVisuals();
        }
    }

    [Header("Tier Look")]
    [Tooltip("Small roman numeral in the corner of the gem; hidden at tier 1.")]
    [SerializeField] private TMPro.TMP_Text tierBadge;

    /// <summary>Corner numeral and outline colour for this gem's tier. Safe to call before Awake.</summary>
    public void RefreshTierVisuals()
    {
        int tier = Tier;
        Color color = GemTierInfo.TierColor(tier);

        if (tierBadge != null)
        {
            tierBadge.gameObject.SetActive(tier > 1);
            tierBadge.text = GemTierInfo.RomanNumeral(tier);
            tierBadge.color = Color.Lerp(color, Color.white, 0.35f);
        }

        // Tier 1 keeps the original plain black outline; higher tiers glow in their quality colour.
        Outline outline = GetComponent<Outline>();
        if (outline != null) outline.effectColor = tier > 1 ? color : Color.black;

        // ...and get an animated aura that gets richer with the quality.
        if (tier > 1) GemAura.Ensure(this).Apply(tier, color);
        else GemAura.Remove(this);
    }

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        _canvasGroup = GetComponent<CanvasGroup>();
        _originalSizeDelta = _rectTransform.sizeDelta;
        RefreshTierVisuals();
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

    /// <summary>Set by a drop that has already moved this gem (a swap), so OnEndDrag doesn't place it a second time.</summary>
    [System.NonSerialized] public bool placedByDrop;

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_canDrag) return;

        if (placedByDrop)
        {
            placedByDrop = false;
            _canvasGroup.alpha = 1f; // the swap flight handles raycasts and position itself
        }
        else
        {
            transform.SetParent(parentAfterDrag);
            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.alpha = 1f;

            AnimateToNewHome();
        }

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

    /// <summary>
    /// Another gem was dropped on this one. Two identical gems of the same tier merge into one a tier
    /// higher. An equipped gem refuses anything else; a gem that isn't equipped hands the drop on to
    /// whatever holds it (the return zone, the board).
    /// </summary>
    public void OnDrop(PointerEventData eventData)
    {
        DraggableGem dropped = eventData.pointerDrag != null ? eventData.pointerDrag.GetComponent<DraggableGem>() : null;

        if (dropped != null && dropped != this)
        {
            if (GemCrafting.TryMergeDropped(this, dropped)) return;

            // The only thing that may happen to an equipped gem is a merge. Anything else (a different gem, the
            // same gem at another tier, a full-tier gem) would fall through to the slot, which replaces the
            // occupant and destroys it. Refuse instead and leave both gems as they are.
            if (IsEquipped())
            {
                // Two equipped gems of the same kind of slot can trade places. Nothing is lost, so that is allowed.
                Transform sourceSlot = dropped.parentAfterDrag;
                bool fromGauntlet = GemSwapFx.IsGauntletSlot(sourceSlot);
                bool compatible = dropped.LinkedGemData != null && LinkedGemData != null &&
                                  dropped.LinkedGemData.gemType == LinkedGemData.gemType;

                if (fromGauntlet && compatible)
                {
                    dropped.placedByDrop = true;
                    GemSwapFx.Swap(dropped, transform.parent, this, sourceSlot);
                    return;
                }

                PlayRejectFeedback();
                return;
            }
        }

        // Not equipped (e.g. on the reward board): let whatever holds this gem handle the drop as before.
        if (transform.parent != null)
        {
            ExecuteEvents.ExecuteHierarchy(transform.parent.gameObject, eventData, ExecuteEvents.dropHandler);
        }
    }

    /// <summary>A small pop when a gem lands in its slot after a swap.</summary>
    public void PlayLandFeedback()
    {
        transform.DOKill(true);
        transform.localScale = Vector3.one;
        transform.DOPunchScale(Vector3.one * 0.3f, 0.3f, 8, 0.8f).SetUpdate(true).SetLink(gameObject);
    }

    /// <summary>A short shake: "that drop did nothing".</summary>
    public void PlayRejectFeedback()
    {
        transform.DOKill(true);
        transform.DOShakePosition(0.3f, new Vector3(8f, 0f, 0f), 22, 90f, false, true).SetUpdate(true).SetLink(gameObject);
    }

    /// <summary>A quick pop so the player sees which gem just levelled up.</summary>
    public void PlayMergeFeedback()
    {
        transform.DOKill(true);
        transform.localScale = Vector3.one;
        transform.DOPunchScale(Vector3.one * 0.45f, 0.4f, 8, 0.8f).SetUpdate(true).SetLink(gameObject);
    }

    private void OnDestroy()
    {
        transform.DOKill();
        if (_rectTransform != null) _rectTransform.DOKill();
    }
}