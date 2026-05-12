using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    [Header("Mode Panels")]
    [SerializeField] private GameObject leftSideGauntlets; //they wernt showing up normally so I added this to reference
    [SerializeField] private GameObject rightSideDetails;
    [SerializeField] private GameObject rightSideRewards;

    [Header("Swap Mode UI")]
    [SerializeField] private GameObject swapOverlayPanel;
    [SerializeField] private GameObject replacePrimaryBtn;
    [SerializeField] private GameObject replaceSecondaryBtn;

    [Header("Warning Dialog UI")]
    [SerializeField] private GameObject warningPanel;
    [SerializeField] private Button warningConfirmBtn;
    [SerializeField] private Button warningCancelBtn;
    [SerializeField] private TMP_Text warningBodyText;

    [Header("Details Panel UI")]
    [SerializeField] private TMP_Text detailNameText;
    [SerializeField] private TMP_Text detailDescriptionText;
    [SerializeField] private Image detailIcon;

    [Header("Navigation & Setup")]
    [SerializeField] private GameObject firstGauntletSlot;
    public Transform primaryGauntlet;
    public Transform secondaryGauntlet;

    [SerializeField] private GameObject primaryTitleButton;
    [SerializeField] private GameObject secondaryTitleButton;

    private GameObject _lastSelectedSlot;
    private GameObject _pendingGauntletPrefab;
    private bool _isWarningActive = false;

    private GauntletRarity _pendingGauntletRarity;
    private void Awake()
    {
        Instance = this;
        SetupHoverEvents(primaryGauntlet);
        SetupHoverEvents(secondaryGauntlet);
        if (swapOverlayPanel != null) swapOverlayPanel.SetActive(false);
        if (warningPanel != null) warningPanel.SetActive(false);
    }

    private void Start()
    {

    }

    public void TryEquipNewGauntlet(GameObject gauntletPrefab, GauntletRarity rarity)
    {
        if (primaryGauntlet.GetComponentInChildren<GauntletManager>() == null)
        {
            GameObject newGauntlet = Instantiate(gauntletPrefab, primaryGauntlet);
            // UPDATE: Pass the rarity!
            newGauntlet.GetComponent<GauntletManager>().InitializeGauntlet(true, rarity);
            SetupHoverEvents(primaryGauntlet);
            return;
        }

        if (secondaryGauntlet.GetComponentInChildren<GauntletManager>() == null)
        {
            GameObject newGauntlet = Instantiate(gauntletPrefab, secondaryGauntlet);
            // UPDATE: Pass the rarity!
            newGauntlet.GetComponent<GauntletManager>().InitializeGauntlet(false, rarity);
            SetupHoverEvents(secondaryGauntlet);
            return;
        }

        // UPDATE: Pass the rarity into the swap phase
        BeginGauntletSwap(gauntletPrefab, rarity);
    }

    private void BeginGauntletSwap(GameObject prefab, GauntletRarity rarity)
    {
        _pendingGauntletPrefab = prefab;
        _pendingGauntletRarity = rarity; // REMEMBER IT!
        Time.timeScale = 0f;

        gameObject.SetActive(true);
        leftSideGauntlets.SetActive(true);
        rightSideDetails.SetActive(true);
        rightSideRewards.SetActive(false);
        swapOverlayPanel.SetActive(true);
        swapOverlayPanel.transform.SetAsLastSibling();

        if (Gamepad.current != null && replacePrimaryBtn != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(replacePrimaryBtn);
        }
    }

    public void OnReplacePrimaryClicked() { ExecuteGauntletSwap(true); }
    public void OnReplaceSecondaryClicked() { ExecuteGauntletSwap(false); }

    private void ExecuteGauntletSwap(bool isPrimary)
    {
        Transform targetContainer = isPrimary ? primaryGauntlet : secondaryGauntlet;
        GauntletManager oldManager = targetContainer.GetComponentInChildren<GauntletManager>();

        List<GameObject> gemsToTransfer = new List<GameObject>();

        if (oldManager != null)
        {
            for (int i = 0; i < oldManager.currentActiveSlots; i++)
            {
                Transform slot = oldManager.fingerSlots[i].transform;
                if (slot.childCount > 0)
                {
                    DraggableGem gemScript = slot.GetChild(0).GetComponent<DraggableGem>();
                    if (gemScript != null && gemScript.originalPrefab != null)
                    {
                        gemsToTransfer.Add(gemScript.originalPrefab);
                    }
                }
            }
            oldManager.gameObject.SetActive(false);
            Destroy(oldManager.gameObject);
        }

        GameObject newGauntlet = Instantiate(_pendingGauntletPrefab, targetContainer);
        GauntletManager newManager = newGauntlet.GetComponent<GauntletManager>();

        // UPDATE: Pass the remembered rarity into the new gauntlet!
        newManager.InitializeGauntlet(isPrimary, _pendingGauntletRarity);

        List<GameObject> overflowGems = new List<GameObject>();
        int availableSlots = newManager.currentActiveSlots;

        for (int i = 0; i < gemsToTransfer.Count; i++)
        {
            if (i < availableSlots)
            {
                GameObject newlySpawnedGem = Instantiate(gemsToTransfer[i], newManager.fingerSlots[i].transform);
                newlySpawnedGem.GetComponent<RectTransform>().sizeDelta = newManager.fingerSlots[i].GetComponent<RectTransform>().rect.size;

                Image slotImage = newManager.fingerSlots[i].GetComponent<Image>();
                Image gemImage = newlySpawnedGem.GetComponent<Image>();
                if (slotImage != null && gemImage != null) slotImage.color = gemImage.color;
            }
            else
            {
                overflowGems.Add(gemsToTransfer[i]);
            }
        }

        SetupHoverEvents(targetContainer);
        swapOverlayPanel.SetActive(false);
        _pendingGauntletPrefab = null;

        if (RewardMenuManager.Instance != null)
        {
            RewardMenuManager.Instance.OpenOverflowMenu(overflowGems);
        }
        else
        {
            Debug.LogWarning("RewardMenuManager Instance is missing! Closing menu instead.");
            TryCloseCharacterScreen();
        }
    }

    private void SetupHoverEvents(Transform gauntletParent)
    {
        if (gauntletParent == null) return;

        foreach (Transform child in gauntletParent.GetComponentsInChildren<Transform>(true))
        {
            // Find all the Gauntlet Slots
            if (child.name.Contains("Slot") && child.GetComponent<EventTrigger>() == null)
            {
                // 1. Keep your existing Hover Logic for the Details Panel
                EventTrigger trigger = child.gameObject.AddComponent<EventTrigger>();
                EventTrigger.Entry entry = new EventTrigger.Entry();
                entry.eventID = EventTriggerType.PointerEnter;
                entry.callback.AddListener((data) => {
                    if (!_isWarningActive)
                    {
                        _lastSelectedSlot = child.gameObject;
                        UpdateDetailsPanel(child.gameObject);
                    }
                });
                trigger.triggers.Add(entry);

                // 2. THE FIX: Wire the Slot's physical Button to the South Button!
                Button slotBtn = child.GetComponent<Button>();
                if (slotBtn != null)
                {
                    slotBtn.onClick.RemoveAllListeners();
                    slotBtn.onClick.AddListener(() =>
                    {
                        // Check if there is a gem currently sitting inside this slot
                        DraggableGem equippedGem = child.GetComponentInChildren<DraggableGem>();

                        // If there is a gem, AND we aren't currently trying to place a new one, open the PopUI!
                        if (equippedGem != null && GemPopupMenu.Instance != null && !GemPopupMenu.Instance.IsPlacingMode)
                        {
                            GemPopupMenu.Instance.OpenMenu(equippedGem, equippedGem.GetComponent<RectTransform>());
                        }
                    });
                }
            }
        }
    }

    private void OnEnable()
    {
        if (swapOverlayPanel != null && !swapOverlayPanel.activeSelf && !_isWarningActive)
        {
            leftSideGauntlets.SetActive(true);
            rightSideDetails.SetActive(true);
            rightSideRewards.SetActive(false);

            if (Gamepad.current != null && firstGauntletSlot != null && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
                EventSystem.current.SetSelectedGameObject(firstGauntletSlot);
            }
        }
    }

    private void Update()
    {
        if (GemPopupMenu.Instance != null && GemPopupMenu.Instance.gameObject.activeInHierarchy) return;

        bool cancelPressed = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
                             (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);

        if (cancelPressed)
        {
            if (_isWarningActive)
            {
                CancelClose();
            }
            else if (swapOverlayPanel.activeSelf)
            {
                swapOverlayPanel.SetActive(false);
                _pendingGauntletPrefab = null;
                TryCloseCharacterScreen();
            }
            else if (rightSideDetails.activeSelf)
            {
                // THE FIX: Step Back Logic!
                GameObject currentSel = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;

                // If we are actively focused on a finger slot...
                if (currentSel != null && currentSel.name.Contains("Slot"))
                {
                    // Step back to the Primary Title
                    if (currentSel.transform.IsChildOf(primaryGauntlet) && primaryTitleButton != null)
                    {
                        EventSystem.current.SetSelectedGameObject(null);
                        EventSystem.current.SetSelectedGameObject(primaryTitleButton);
                    }
                    // Step back to the Secondary Title
                    else if (currentSel.transform.IsChildOf(secondaryGauntlet) && secondaryTitleButton != null)
                    {
                        EventSystem.current.SetSelectedGameObject(null);
                        EventSystem.current.SetSelectedGameObject(secondaryTitleButton);
                    }
                    else
                    {
                        TryCloseCharacterScreen();
                    }
                }
                else
                {
                    // If we are NOT focused on a slot (meaning we are on the headers), show the Warning Panel!
                    TryCloseCharacterScreen();
                }
            }
        }

        // Keep the detail panel hover logic exactly the same!
        if (!swapOverlayPanel.activeSelf && !_isWarningActive && EventSystem.current != null && EventSystem.current.currentSelectedGameObject != _lastSelectedSlot)
        {
            GameObject currentSel = EventSystem.current.currentSelectedGameObject;
            if (currentSel != null && currentSel.name.Contains("Slot"))
            {
                _lastSelectedSlot = currentSel;
                UpdateDetailsPanel(_lastSelectedSlot);
            }
        }
    }

    private void UpdateDetailsPanel(GameObject slot)
    {
        if (slot == null) return;
        DraggableGem equippedGem = slot.GetComponentInChildren<DraggableGem>();

        if (equippedGem != null)
        {
            detailNameText.text = equippedGem.gemName;
            detailDescriptionText.text = equippedGem.gemDescription;
            if (detailIcon != null) { detailIcon.sprite = equippedGem.gemIcon; detailIcon.color = Color.white; }
        }
        else
        {
            detailNameText.text = "Empty Slot";
            detailDescriptionText.text = "No gem equipped here.";
            if (detailIcon != null) detailIcon.color = Color.clear;
        }
    }

    // --- NEW: CENTRALIZED CLOSE AND WARNING LOGIC ---

    public void TryCloseCharacterScreen()
    {
        _isWarningActive = true;
        warningPanel.SetActive(true);

        if (warningBodyText != null)
            warningBodyText.text = "Are you sure you want to leave the inventory?";

        // Dynamically wire the buttons so they don't conflict with RewardMenuManager
        warningConfirmBtn.onClick.RemoveAllListeners();
        warningConfirmBtn.onClick.AddListener(ConfirmClose);

        warningCancelBtn.onClick.RemoveAllListeners();
        warningCancelBtn.onClick.AddListener(CancelClose);

        if (Gamepad.current != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(warningCancelBtn.gameObject);
        }
    }

    public void FocusPrimaryGauntlet()
    {
        FocusFirstActiveSlot(primaryGauntlet);
    }

    // Call this from the OnClick() of your "Gauntlet Secondary" Title Button!
    public void FocusSecondaryGauntlet()
    {
        FocusFirstActiveSlot(secondaryGauntlet);
    }

    private void FocusFirstActiveSlot(Transform gauntletContainer)
    {
        if (gauntletContainer == null) return;

        // Find the Gauntlet Prefab that is currently spawned inside this container
        GauntletManager gauntlet = gauntletContainer.GetComponentInChildren<GauntletManager>();

        if (gauntlet != null && gauntlet.fingerSlots != null)
        {
            // Loop through the slots and find the first one that is active (not sealed!)
            foreach (GameObject slot in gauntlet.fingerSlots)
            {
                if (slot.activeInHierarchy)
                {
                    // Pass controller focus to this slot!
                    if (EventSystem.current != null)
                    {
                        EventSystem.current.SetSelectedGameObject(null);
                        EventSystem.current.SetSelectedGameObject(slot);
                    }
                    return; // Stop looking once we found the first valid one
                }
            }
        }
    }

    public void CancelClose()
    {
        _isWarningActive = false;
        warningPanel.SetActive(false);

        if (Gamepad.current != null && _lastSelectedSlot != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(_lastSelectedSlot);
        }
    }

    public void ConfirmClose()
    {
        _isWarningActive = false;
        warningPanel.SetActive(false);

        PlayerStatsManager statsManager = FindFirstObjectByType<PlayerStatsManager>();
        if(statsManager != null)
        {
            statsManager.SyncWithUI(primaryGauntlet, secondaryGauntlet);
        }

        // Find the Gauntlet Menu to see if we came from the Pause Screen
        GauntletMenu gauntletMenu = FindFirstObjectByType<GauntletMenu>();

        if (gauntletMenu != null && gauntletMenu.gameObject.activeInHierarchy)
        {
            // We opened it from the pause menu, return control to it safely!
            gauntletMenu.CloseUpgradeMenu();
        }
        else
        {
            // We opened it from gameplay via the Debug Swap, just close and unpause!
            gameObject.SetActive(false);
            Time.timeScale = 1f;
        }
    }
}