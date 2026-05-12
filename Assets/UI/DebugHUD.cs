using UnityEngine;
using UnityEngine.InputSystem; // NEW: Required for checking key presses!

public class DebugHUD : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Drag the UI Panel containing your 5 buttons here so we can hide/show it.")]
    public GameObject debugMenuPanel;

    [Header("References")]
    [Tooltip("Drag your CharacterScreen object here so the script can find it while it's hidden!")]
    public InventoryManager inventoryManager;

    private void Start()
    {
        // Hide the debug menu automatically when the game starts
        if (debugMenuPanel != null)
        {
            debugMenuPanel.SetActive(false);
        }
    }

    private void Update()
    {
        // Safety check to ensure a keyboard is connected
        if (Keyboard.current == null) return;

        // Toggle the menu if the player presses '0' on the top row OR the numpad
        if (Keyboard.current.digit0Key.wasPressedThisFrame || Keyboard.current.numpad0Key.wasPressedThisFrame)
        {
            if (debugMenuPanel != null)
            {
                // Flips the active state: if it's on, turn it off. If it's off, turn it on!
                debugMenuPanel.SetActive(!debugMenuPanel.activeSelf);
            }
        }
    }

    // Call this from the OnClick() of your 5 elemental buttons!
    public void SpawnGauntlet(GameObject gauntletPrefab)
    {
        if (inventoryManager != null)
        {
            // Roll a random rarity (0 = Normal, 1 = Rare, 2 = SuperRare)
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