using System.Collections.Generic;
using UnityEngine;

public class GauntletManager : MonoBehaviour
{
    [Header("Back End Data")]
    public GauntletData LinkedGauntletData;

    [Header("Gauntlet Settings")]
    [Tooltip("The base max number of slots this gauntlet naturally has (e.g., 3, 4, 5)")]
    public int baseCapacity = 5;

    [Tooltip("Drag the child 'Slot' objects here IN ORDER (Slot 1, Slot 2, etc.)")]
    public List<GameObject> fingerSlots;

    public int currentActiveSlots { get; private set; }
    public GauntletRarity currentRarity { get; private set; }

    // Called by InventoryManager the exact moment it spawns!
    public void InitializeGauntlet(bool isPrimary, GauntletRarity rarity)
    {
        currentRarity = rarity;

        // Convert the enum to a simple math bonus (Normal = 0, Rare = 1, SuperRare = 2)
        int rarityBonus = (int)rarity;

        // Apply your exact scaling rules!
        if (isPrimary)
        {
            currentActiveSlots = 3 + rarityBonus; // Scales: 3, 4, 5
        }
        else
        {
            currentActiveSlots = 1 + rarityBonus; // Scales: 1, 2, 3
        }

        // Physically turn the slots on or off (seal them)
        for (int i = 0; i < fingerSlots.Count; i++)
        {
            if (i < currentActiveSlots)
            {
                fingerSlots[i].SetActive(true);
            }
            else
            {
                fingerSlots[i].SetActive(false);
            }
        }
    }
}