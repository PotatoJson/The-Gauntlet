using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class GemDropSlot : MonoBehaviour, IDropHandler
{
    private Image _slotImage;
    private Color _defaultColor; // To remember what color the slot was originally!

    private void Awake()
    {
        _slotImage = GetComponent<Image>();
        if (_slotImage != null)
        {
            _defaultColor = _slotImage.color;
        }
    }

    private void Update()
    {
        // THE FIX: If the player picks the gem back up and the slot is empty, reset the color!
        if (transform.childCount == 0 && _slotImage != null && _slotImage.color != _defaultColor)
        {
            _slotImage.color = _defaultColor;
        }
    }

    public void OnDrop(PointerEventData eventData)
    {
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
}