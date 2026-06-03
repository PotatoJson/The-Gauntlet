using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class GauntletChoiceUI : MonoBehaviour
{
    public static GauntletChoiceUI Instance;

    [Header("Option A UI")]
    public Button ButtonA;
    public TMP_Text NameTextA;
    public TMP_Text SlotsTextA;
    public Transform GauntletAnchorA; // The empty GameObject inside the button

    [Header("Option B UI")]
    public Button ButtonB;
    public TMP_Text NameTextB;
    public TMP_Text SlotsTextB;
    public Transform GauntletAnchorB; // The empty GameObject inside the button

    private GauntletInteractable _activeInteractable;
    private GameObject _spawnedPrefabA;
    private GameObject _spawnedPrefabB;

    private void Awake()
    {
        Instance = this;
        gameObject.SetActive(false); 
    }

    public void OpenChoiceMenu(GauntletInteractable interactable)
    {
        _activeInteractable = interactable;
        gameObject.SetActive(true);
        Time.timeScale = 0f; 

        // Clear previous prefabs if they exist
        if (_spawnedPrefabA != null) Destroy(_spawnedPrefabA);
        if (_spawnedPrefabB != null) Destroy(_spawnedPrefabB);

        // Setup both options
        _spawnedPrefabA = SetupChoice(ButtonA, NameTextA, SlotsTextA, GauntletAnchorA, interactable.GauntletPrefabA, interactable.RarityA);
        _spawnedPrefabB = SetupChoice(ButtonB, NameTextB, SlotsTextB, GauntletAnchorB, interactable.GauntletPrefabB, interactable.RarityB);

        // Snap controller focus to the first option
        if (Gamepad.current != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(ButtonA.gameObject);
        }
    }

    private GameObject SetupChoice(Button btn, TMP_Text nameTxt, TMP_Text slotsTxt, Transform anchor, GameObject prefab, GauntletRarity rarity)
    {
        if (prefab == null) return null;

        // 1. Text Setup
        nameTxt.text = $"{prefab.name}\n<size=70%>{rarity}</size>";

        int rarityBonus = (int)rarity;
        int primarySlots = 3 + rarityBonus;
        int secondarySlots = 1 + rarityBonus;
        slotsTxt.text = $"Primary Slots: {primarySlots}\nSecondary Slots: {secondarySlots}";

        // 2. Spawn the visual Gauntlet UI
        GameObject visualGauntlet = Instantiate(prefab, anchor);
        visualGauntlet.transform.localPosition = Vector3.zero;
        visualGauntlet.transform.localScale = Vector3.one;

        // 3. Initialize it as Primary (true) so it visually displays maximum slots!
        GauntletManager gm = visualGauntlet.GetComponent<GauntletManager>();
        if (gm != null)
        {
            gm.InitializeGauntlet(true, rarity); 
        }

        // 4. Force the prefab to be unclickable so the parent button catches the click
        CanvasGroup cg = visualGauntlet.GetComponent<CanvasGroup>();
        if (cg == null) cg = visualGauntlet.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false; 

        // 5. Button Click Logic
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => OnGauntletChosen(prefab, rarity));

        return visualGauntlet;
    }

    private void OnGauntletChosen(GameObject chosenPrefab, GauntletRarity chosenRarity)
    {
        gameObject.SetActive(false);
        Time.timeScale = 1f;

        if (InventoryManager.Instance != null && chosenPrefab != null)
        {
            // Send it to the inventory manager to handle equipping
            InventoryManager.Instance.TryEquipNewGauntlet(chosenPrefab, chosenRarity);
            
            if (_activeInteractable != null)
            {
                _activeInteractable.CompleteInteraction();
            }
        }
    }
}