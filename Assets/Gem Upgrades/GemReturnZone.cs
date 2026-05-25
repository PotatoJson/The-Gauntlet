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
            // 1. Tell the manager the finger slot is now officially empty
            if (RewardMenuManager.Instance != null && RewardMenuManager.Instance.IsRewardModeActive())
            {
                RewardMenuManager.Instance.OnGemReturned(incomingGem);
            }

            incomingGem.ReturnToInventory();
        }
    }
}