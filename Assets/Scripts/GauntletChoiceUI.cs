using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Localization;

public class GauntletChoiceUI : MonoBehaviour
{
    public static GauntletChoiceUI Instance;

    [Header("Localization")]
    public LocalizedString PrimarySlotsString; 
    public LocalizedString SecondarySlotsString;

    [Header("Option A UI")]
    public Button ButtonA;
    public TMP_Text NameTextA;
    public TMP_Text SlotsTextA;
    public Transform GauntletAnchorA;

    [Header("Option B UI")]
    public Button ButtonB;
    public TMP_Text NameTextB;
    public TMP_Text SlotsTextB;
    public Transform GauntletAnchorB;

    [Header("Warning Dialog UI")]
    public GameObject WarningPanel;
    public Button WarningConfirmBtn;
    public Button WarningCancelBtn;
    private bool _isWarningActive = false;

    [Header("Visual Tuning")]
    [Range(0.1f, 1f)] public float GauntletVisualScale = 0.4f;

    private GauntletInteractable _activeInteractable;
    private GameObject _spawnedPrefabA;
    private GameObject _spawnedPrefabB;

    private void Awake() 
    { 
        Instance = this; 
        gameObject.SetActive(false); 
        if (WarningPanel != null) WarningPanel.SetActive(false);
    }

    // --- ESCAPE / CANCEL INPUT LOOP ---
    private void Update()
    {
        if (!gameObject.activeInHierarchy) return;

        bool cancelPressed = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
                             (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);

        if (cancelPressed)
        {
            if (_isWarningActive) CancelClose();
            else ShowWarning();
        }
    }

    public void OpenChoiceMenu(GauntletInteractable interactable)
    {
        _activeInteractable = interactable;
        _isWarningActive = false; // Reset warning state
        if (WarningPanel != null) WarningPanel.SetActive(false);

        gameObject.SetActive(true);
        Time.timeScale = 0f; 
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (_spawnedPrefabA != null) Destroy(_spawnedPrefabA);
        if (_spawnedPrefabB != null) Destroy(_spawnedPrefabB);

        _spawnedPrefabA = SetupChoice(ButtonA, NameTextA, SlotsTextA, GauntletAnchorA, interactable.GauntletPrefabA, interactable.RarityA);
        _spawnedPrefabB = SetupChoice(ButtonB, NameTextB, SlotsTextB, GauntletAnchorB, interactable.GauntletPrefabB, interactable.RarityB);
        
        /*Navigation navA = new Navigation { mode = Navigation.Mode.Explicit };
        navA.selectOnLeft = ButtonB;
        navA.selectOnRight = ButtonB;
        ButtonA.navigation = navA;

        Navigation navB = new Navigation { mode = Navigation.Mode.Explicit };
        navB.selectOnLeft = ButtonA;
        navB.selectOnRight = ButtonA;
        ButtonB.navigation = navB;*/

        if (Gamepad.current != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(ButtonA.gameObject);
        }
    }

    // --- WARNING MENU LOGIC ---
    private void ShowWarning()
    {
        Debug.Log("Warning Test");
        _isWarningActive = true;
        if (WarningPanel != null) WarningPanel.SetActive(true);

        if (WarningConfirmBtn != null)
        {
            WarningConfirmBtn.onClick.RemoveAllListeners();
            WarningConfirmBtn.onClick.AddListener(ConfirmClose);
        }

        if (WarningCancelBtn != null)
        {
            WarningCancelBtn.onClick.RemoveAllListeners();
            WarningCancelBtn.onClick.AddListener(CancelClose);
        }

        // Snap controller focus to cancel button for safety
        if (Gamepad.current != null && EventSystem.current != null && WarningCancelBtn != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(WarningCancelBtn.gameObject);
        }
    }

    private void CancelClose()
    {
        _isWarningActive = false;
        if (WarningPanel != null) WarningPanel.SetActive(false);

        // Snap focus back to Option A
        if (Gamepad.current != null && EventSystem.current != null && ButtonA != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(ButtonA.gameObject);
        }
    }

    private void ConfirmClose()
    {
        _isWarningActive = false;
        if (WarningPanel != null) WarningPanel.SetActive(false);

        gameObject.SetActive(false);
        Time.timeScale = 1f;
        
        // Hide cursor and unlock game since we backed out
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    private GameObject SetupChoice(Button btn, TMP_Text nameTxt, TMP_Text slotsTxt, Transform anchor, GameObject prefab, GauntletRarity rarity)
    {
        if (prefab == null) return null;

        GauntletManager gmInfo = prefab.GetComponent<GauntletManager>();
        string localizedName = (gmInfo != null && gmInfo.LinkedGauntletData != null) ? gmInfo.LinkedGauntletData.Name : prefab.name;

        nameTxt.text = $"{localizedName}\n<size=70%>{rarity}</size>";

        int rarityBonus = (int)rarity;
        int pSlots = 3 + rarityBonus;
        int sSlots = 1 + rarityBonus;
        
        PrimarySlotsString.Arguments = new object[] { pSlots };
        SecondarySlotsString.Arguments = new object[] { sSlots };
        slotsTxt.text = $"{PrimarySlotsString.GetLocalizedString()}\n{SecondarySlotsString.GetLocalizedString()}";

        GameObject visualGauntlet = Instantiate(prefab, anchor);
        RectTransform rt = visualGauntlet.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero; 
            visualGauntlet.transform.localScale = new Vector3(GauntletVisualScale, GauntletVisualScale, 1f);
        }

        if (gmInfo != null) gmInfo.InitializeGauntlet(true, rarity); 
        CanvasGroup cg = visualGauntlet.GetComponent<CanvasGroup>();
        if (cg == null) cg = visualGauntlet.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false; 

        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => OnGauntletChosen(prefab, rarity));
        return visualGauntlet;
    }

    private void OnGauntletChosen(GameObject chosenPrefab, GauntletRarity chosenRarity)
    {
        gameObject.SetActive(false);
        Time.timeScale = 1f;

        // Note: Cursor remains visible because InventoryManager Swap menu opens next!
        if (InventoryManager.Instance != null && chosenPrefab != null)
        {
            InventoryManager.Instance.TryEquipNewGauntlet(chosenPrefab, chosenRarity);
            if (_activeInteractable != null) _activeInteractable.CompleteInteraction();
        }
    }
}