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
            // NEW: Roll a random rarity (0 = Normal, 1 = Rare, 2 = SuperRare)
            // Note: Random.Range with integers is EXCLUSIVE of the max number, so we use 3!
            GauntletRarity randomRarity = (GauntletRarity)Random.Range(0, 3);

            Debug.Log($"Spawning {gauntletPrefab.name} with rarity: {randomRarity}");

            inventoryManager.TryEquipNewGauntlet(gauntletPrefab, randomRarity);
        }
        else
        {
            Debug.LogWarning("Inventory Manager is missing! Please drag the CharacterScreen into the Inspector.");
        }
    }
}