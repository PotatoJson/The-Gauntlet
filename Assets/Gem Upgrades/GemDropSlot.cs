using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class GemDropSlot : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    private Image _slotImage;
    private Color _defaultColor; 
    [SerializeField] private Sprite _defaultSprite; // Set this in the inspector or let Awake capture it
    private GauntletManager _manager;
    private bool _isDisabled = false;

    private void Awake()
    {
        EnsureSlotImage();

        Button btn = GetComponent<Button>();
        if (btn != null)
        {
            btn.transition = Selectable.Transition.None;
        }
    }

    private void EnsureSlotImage()
    {
        if (_slotImage == null)
        {
            _slotImage = GetComponent<Image>();
            if (_slotImage != null)
            {
                _defaultColor = _slotImage.color;
                // ONLY capture if we haven't manually assigned one in the inspector
                if (_defaultSprite == null) _defaultSprite = _slotImage.sprite;
            }
        }
    }

    public void Setup(GauntletManager manager)
    {
        _manager = manager;
    }

    public void SetSlotDisabled(Sprite crossSprite)
    {
        _isDisabled = true;
        EnsureSlotImage();
        if (_slotImage != null)
        {
            _slotImage.sprite = crossSprite;
            // Set color to white (fully opaque) so the cross sprite is visible.
            // Since the cross is a sprite with transparency, it won't show a white background.
            _slotImage.color = Color.white;
        }

        Button btn = GetComponent<Button>();
        if (btn != null)
        {
            btn.interactable = false;
        }
    }

    public void SetSlotEnabled(Sprite defaultSlotSprite)
    {
        _isDisabled = false;
        EnsureSlotImage();
        if (_slotImage != null)
        {
            // Fallback to internal _defaultSprite if the manager provides null
            _slotImage.sprite = (defaultSlotSprite != null) ? defaultSlotSprite : _defaultSprite;
            _slotImage.color = _defaultColor;
        }

        UpdateButtonInteractability();
    }

    /// <summary>
    /// True while the player is carrying a gem around with the controller, looking for a slot.
    /// </summary>
    private static bool IsPlacingMode =>
        GemPopupMenu.Instance != null && GemPopupMenu.Instance.IsPlacingMode;

    public void UpdateButtonInteractability()
    {
        if (_isDisabled) return;

        // During controller placement EVERY lit slot has to stay reachable, occupied ones included,
        // because that is how a gem gets replaced. GemPopupMenu.StartPlacementMode switches them on
        // and EndPlacementMode puts them back to sleep, so backing off here is what lets that hold:
        // otherwise this ran a frame later and switched the occupied slots straight back off, which
        // left controller navigation with nowhere to go and the gem stranded mid-screen.
        if (IsPlacingMode) return;

        Button btn = GetComponent<Button>();
        if (btn != null)
        {
            // If a gem is present, the slot's own button should NOT be interactable
            // so that it doesn't intercept controller focus or navigation.
            btn.interactable = transform.childCount == 0;
        }
    }

    private void Update()
    {
        if (_isDisabled) return;

        // THE FIX: If the player picks the gem back up and the slot is empty, reset the color!
        if (transform.childCount == 0 && _slotImage != null && _slotImage.color != _defaultColor)
        {
            _slotImage.color = _defaultColor;
        }
        
        // Safety net for every other way a gem can arrive in this slot: a gauntlet swap, or a save
        // file being restored. ClearBracket only acts when this slot is the one currently holding
        // the bracket, so it is a no-op the rest of the time. Skipped during placement, where the
        // bracket is the player's cursor and has to stay put over an occupied slot.
        if (_manager != null && HasGem() && !IsPlacingMode) _manager.ClearBracket(transform);

        // Dynamic check for child gems to update interactability
        UpdateButtonInteractability();
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (_isDisabled) return;

        GameObject droppedObject = eventData.pointerDrag;
        if (droppedObject == null) return;

        DraggableGem incomingGem = droppedObject.GetComponent<DraggableGem>();
        if (incomingGem != null)
        {
            if (incomingGem.LinkedGemData.gemType != GemType.Stat)
            {
                Debug.Log("Invalid Gem: Only Stat Gems can be equipped in finger slots.");
                return; // Stops the drop completely!
            }

            // If it is locked, ignore the drop completely!
            if (RewardMenuManager.Instance != null && RewardMenuManager.Instance.IsRewardModeActive())
            {
                if (!RewardMenuManager.Instance.CanDragGem(incomingGem)) return;
            }

            foreach (Transform child in transform) { Destroy(child.gameObject); }

            incomingGem.parentAfterDrag = transform;

            RectTransform slotRect = GetComponent<RectTransform>();
            RectTransform gemRect = incomingGem.GetComponent<RectTransform>();

            if (slotRect != null && gemRect != null)
            {
                gemRect.sizeDelta = slotRect.rect.size;
            }

            // Deliberately NOT tinting the slot with the gem's colour here. Most gems carry their
            // colour in the sprite and leave Image.color white, so copying it turned the slot's
            // backing sprite into a solid white square behind the gem. Leaving the slot at its
            // default also matches how gems restored from a save file look, since those are
            // parented directly without going through OnDrop.

            // The gem owns the highlight from here. The slot lit its own bracket while the gem was
            // mid-drag (unparented, so the slot looked empty) and will never receive a pointer-exit
            // to clear it, because the pointer is still inside the slot - just over the gem now.
            HideBracket();

            if (RewardMenuManager.Instance != null && RewardMenuManager.Instance.IsRewardModeActive())
            {
                RewardMenuManager.Instance.OnGemSlotted(incomingGem);
            }
        }
    }

    // Pointer events only count on mouse/keyboard, selection events only on a controller, so a
    // stale selection can never sit on screen next to the thing the mouse is hovering.
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!InputHelper.IsGamepadLastUsed()) ShowBracket();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!InputHelper.IsGamepadLastUsed()) HideBracket();
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (InputHelper.IsGamepadLastUsed()) ShowBracket();
    }

    public void OnDeselect(BaseEventData eventData) => HideBracket();

    /// <summary>True when a gem is currently sitting in this slot (ignores one being dragged out).</summary>
    private bool HasGem()
    {
        DraggableGem gem = GetComponentInChildren<DraggableGem>(true);
        return gem != null && gem.transform.parent == transform;
    }

    private void ShowBracket()
    {
        if (_isDisabled || _manager == null) return;

        // A gem draws its own selection bracket, and Unity delivers pointer-enter to this parent
        // slot as well as to the gem. Without this the two highlights stack on the same square.
        //
        // Placement mode is the exception: the gem being placed is detached and follows the popup,
        // so the slot's bracket is the only cursor the player has and must show even when occupied.
        if (HasGem() && !IsPlacingMode) return;

        _manager.UpdateBracket(transform);
    }

    private void HideBracket()
    {
        if (_manager != null)
        {
            _manager.ClearBracket(transform);
        }
    }
}