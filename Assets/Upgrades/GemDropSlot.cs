using UnityEngine;
using UnityEngine.EventSystems;

public class GemDropSlot : MonoBehaviour, IDropHandler
{
    public void OnDrop(PointerEventData eventData)
    {
        GameObject droppedObject = eventData.pointerDrag;
        if (droppedObject == null) return;

        DraggableGem incomingGem = droppedObject.GetComponent<DraggableGem>();
        if (incomingGem != null)
        {
            // 1. SWAP LOGIC: Is there already a gem in this slot?
            if (transform.childCount > 0)
            {
                // Grab the gem currently sitting in this slot
                DraggableGem existingGem = transform.GetChild(0).GetComponent<DraggableGem>();

                if (existingGem != null)
                {
                    // Tell the existing gem to go back to where the new gem came from!
                    Transform previousHome = incomingGem.parentAfterDrag;
                    existingGem.transform.SetParent(previousHome);

                    // Trigger the animation for the gem that got kicked out
                    existingGem.AnimateToNewHome();
                }
            }

            // 2. Accept the incoming gem
            incomingGem.parentAfterDrag = transform;
        }
    }
}