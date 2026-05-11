using UnityEngine;
using UnityEngine.EventSystems;

public class GemReturnZone : MonoBehaviour, IDropHandler
{
    public void OnDrop(PointerEventData eventData)
    {
        GameObject droppedObject = eventData.pointerDrag;
        if (droppedObject == null) return;

        DraggableGem incomingGem = droppedObject.GetComponent<DraggableGem>();
        if (incomingGem != null)
        {
            // THE FIX: Instead of returning to "original grid", set the Gem Holder as its new permanent home!
            incomingGem.parentAfterDrag = transform;

            // Fix its scale so it doesn't look squished like a finger slot
            RectTransform gemRect = incomingGem.GetComponent<RectTransform>();
            if (gemRect != null) gemRect.sizeDelta = new Vector2(100, 100); // Adjust size to whatever fits your grid

            // Tell the manager the finger slot is now officially empty
            if (RewardMenuManager.Instance != null && RewardMenuManager.Instance.IsRewardModeActive())
            {
                RewardMenuManager.Instance.OnGemReturned(incomingGem);
            }
        }
    }
}