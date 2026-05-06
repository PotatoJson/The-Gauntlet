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
            // THE FIX: If the gem is already sitting in the inventory/reward grid, ignore the drop!
            if (incomingGem.transform.parent == incomingGem.originalInventoryGrid) return;

            // The player dragged it back to the pool! 
            incomingGem.ReturnToInventory();

            // Tell the manager the slot is free again (Passing the gem to be safe)
            if (RewardMenuManager.Instance != null && RewardMenuManager.Instance.IsRewardModeActive())
            {
                RewardMenuManager.Instance.OnGemReturned(incomingGem);
            }
        }
    }
}