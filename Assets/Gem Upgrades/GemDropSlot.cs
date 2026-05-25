using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class GemDropSlot : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    private Image _slotImage;
    private Color _defaultColor; // To remember what color the slot was originally!
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

    private void Update()
    {
        if (_isDisabled) return;

        // THE FIX: If the player picks the gem back up and the slot is empty, reset the color!
        if (transform.childCount == 0 && _slotImage != null && _slotImage.color != _defaultColor)
        {
            _slotImage.color = _defaultColor;
        }
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

            Image gemImage = incomingGem.GetComponent<Image>();
            if (gemImage != null && _slotImage != null)
            {
                _slotImage.color = gemImage.color;
            }

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

    private void ShowBracket()
    {
        if (_isDisabled || _manager == null) return;
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