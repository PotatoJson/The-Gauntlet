using UnityEngine;

public class DebugHUD : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Drag your CharacterScreen object here so the script can find it while it's hidden!")]
    public InventoryManager inventoryManager;

    // Call this from the OnClick() of your 5 elemental buttons!
    public void SpawnGauntlet(GameObject gauntletPrefab)
    {
        if (inventoryManager != null)
        {
            inventoryManager.TryEquipNewGauntlet(gauntletPrefab);
        }
        else
        {
            Debug.LogWarning("Inventory Manager is missing! Please drag the CharacterScreen into the Inspector.");
        }
    }
}