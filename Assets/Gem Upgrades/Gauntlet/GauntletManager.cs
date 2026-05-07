using System.Collections.Generic;
using UnityEngine;

public class GauntletManager : MonoBehaviour
{
    [Header("Gauntlet Settings")]
    [Tooltip("The max number of slots this specific gauntlet type can have (e.g., 5 for Primary, 3 for Secondary)")]
    [SerializeField] public int absoluteMaxSlots = 5;

    [Tooltip("Drag the child 'Slot' objects here IN ORDER (Slot 1, Slot 2, etc.)")]
    [SerializeField] public List<GameObject> fingerSlots;

    public int currentActiveSlots { get; private set; }

    private void Start()
    {
        // Default to max slots for testing, you can change this later!
        SetGauntletCapacity(absoluteMaxSlots);
    }

    // Call this when the player picks up or equips a new gauntlet!
    public void SetGauntletCapacity(int newCapacity)
    {
        // Clamp the capacity to ensure it doesn't break your UI
        currentActiveSlots = Mathf.Clamp(newCapacity, 1, absoluteMaxSlots);

        List<GameObject> overflowGems = new List<GameObject>();

        // Iterate through all physical slots on the UI image
        for (int i = 0; i < fingerSlots.Count; i++)
        {
            GameObject slot = fingerSlots[i];

            if (i < currentActiveSlots)
            {
                // This slot is allowed to exist on the new gauntlet! Turn it on.
                slot.SetActive(true);
            }
            else
            {
                // This slot was cut off by the downgrade! Turn it off.

                // 1. Check if a gem was currently sitting in this amputated slot
                if (slot.transform.childCount > 0)
                {
                    Transform trappedGem = slot.transform.GetChild(0);

                    // We need to save the PREFAB or Data of this gem, not the UI instance itself.
                    // For now, we will grab the prefab reference if you store it, or just pass the object.
                    // *Assuming DraggableGem has a way to identify its prefab, or we just steal the object:*

                    overflowGems.Add(GetPrefabFromInstance(trappedGem.gameObject));

                    // Destroy the UI instance since the slot is going away
                    Destroy(trappedGem.gameObject);
                }

                // 2. Hide the slot completely
                slot.SetActive(false);
            }
        }

        // Trigger the Overflow Draft Menu if any gems were squeezed out!
        if (overflowGems.Count > 0 && RewardMenuManager.Instance != null)
        {
            RewardMenuManager.Instance.OpenOverflowMenu(overflowGems);
        }
    }

    // Helper method: You need a way to map an equipped UI Gem back to a Prefab 
    // so RewardMenuManager can spawn a fresh draggable version of it.
    private GameObject GetPrefabFromInstance(GameObject instance)
    {
        DraggableGem gemData = instance.GetComponent<DraggableGem>();

        // **IMPORTANT:** You will need to add a 'public GameObject originalPrefab' 
        // variable to your DraggableGem.cs script and assign it in the Inspector 
        // for all your gem prefabs so the system knows what to spawn!
        if (gemData != null && gemData.originalPrefab != null)
        {
            return gemData.originalPrefab;
        }

        Debug.LogWarning("Could not find the original prefab for the overflow gem!");
        return null;
    }
}