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

    public void UpdateButtonInteractability()
    {
        if (_isDisabled) return;

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

            if (RewardMenuManager.Instance != null && RewardMenuManager.Instance.IsRewardModeActive())
            {
                RewardMenuManager.Instance.OnGemSlotted(incomingGem);
            }
        }
    }

    public void OnPointerEnter(PointerEventData eventData) => ShowBracket();
    public void OnPointerExit(PointerEventData eventData) => HideBracket();
    public void OnSelect(BaseEventData eventData) => ShowBracket();
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
        if (HasGem()) return;

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