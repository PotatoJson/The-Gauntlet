using UnityEngine;

public class SpawnBasicGauntlet : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject characterScreenRoot;

    [Header("Starting Equipment")]
    [SerializeField] private GameObject defaultGauntletPrefab;

    private void Start()
    {
        InitializeStartingGear();
    }

    private void InitializeStartingGear()
    {
        if (characterScreenRoot == null || defaultGauntletPrefab == null) return;

        // 1. WAKE IT UP: Force the UI on so Awake() and Layout Groups run properly
        characterScreenRoot.SetActive(true);

        // 2. SPAWN GEAR: Now that it is active, safely spawn the gauntlets!
        if (InventoryManager.Instance != null)
        {
            // Equip Primary
            InventoryManager.Instance.TryEquipNewGauntlet(defaultGauntletPrefab, GauntletRarity.Common);
            // Equip Secondary (TryEquipNewGauntlet will auto-route this to the empty left hand)
            InventoryManager.Instance.TryEquipNewGauntlet(defaultGauntletPrefab, GauntletRarity.Common);
        }

        // 3. PUT IT TO SLEEP: Turn the UI back off before the first frame renders to the player's screen
        characterScreenRoot.SetActive(false);
    }
}

