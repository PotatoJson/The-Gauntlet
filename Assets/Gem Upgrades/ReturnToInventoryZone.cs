using UnityEngine;
using UnityEngine.EventSystems;

public class ReturnToInventoryZone : MonoBehaviour, IDropHandler
{
    public void OnDrop(PointerEventData eventData)
    {
        GameObject droppedObject = eventData.pointerDrag;
        if (droppedObject != null)
        {
            DraggableGem gem = droppedObject.GetComponent<DraggableGem>();
            if (gem != null)
            {
                // Send the gem back to the grid it originated from
                gem.parentAfterDrag = gem.originalInventoryGrid;
            }
        }
    }
}