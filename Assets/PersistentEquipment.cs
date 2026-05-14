using UnityEngine;
using System.Collections.Generic;

public class PersistentEquipment : MonoBehaviour
{
    public static PersistentEquipment Instance;

    public bool hasSavedData = false;

    [Header("Saved Primary")]
    public GameObject primaryGauntletPrefab;
    public GauntletRarity primaryRarity;
    public List<GameObject> primaryGems = new List<GameObject>();

    [Header("Saved Secondary")]
    public GameObject secondaryGauntletPrefab;
    public GauntletRarity secondaryRarity;
    public List<GameObject> secondaryGems = new List<GameObject>();

    private void Awake()
    {
        // The Highlander Rule: Only one backpack allowed!
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // Call this right before you load the next scene!
    public void SaveEquipment(InventoryManager inv)
    {
        hasSavedData = true;

        // 1. Save Gauntlet Data
        primaryGauntletPrefab = inv.activePrimaryPrefab;
        primaryRarity = inv.activePrimaryRarity;

        secondaryGauntletPrefab = inv.activeSecondaryPrefab;
        secondaryRarity = inv.activeSecondaryRarity;

        // 2. Clear old gems and save the new ones
        primaryGems.Clear();
        secondaryGems.Clear();

        ExtractGems(inv.primaryGauntlet, primaryGems);
        ExtractGems(inv.secondaryGauntlet, secondaryGems);

        Debug.Log("Equipment successfully saved to the Backpack!");
    }

    private void ExtractGems(Transform gauntletParent, List<GameObject> gemList)
    {
        GauntletManager gm = gauntletParent.GetComponentInChildren<GauntletManager>();
        if (gm != null)
        {
            foreach (GameObject slot in gm.fingerSlots)
            {
                DraggableGem gem = slot.GetComponentInChildren<DraggableGem>();
                if (gem != null && gem.originalPrefab != null)
                {
                    gemList.Add(gem.originalPrefab); // Save the original prefab so we can spawn it later!
                }
            }
        }
    }
}