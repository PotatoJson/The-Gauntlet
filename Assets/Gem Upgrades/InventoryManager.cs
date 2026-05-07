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
    [SerializeField] private GameObject rightSideDetails;
    [SerializeField] private GameObject rightSideRewards;

    [Header("Swap Mode UI")]
    [SerializeField] private GameObject swapOverlayPanel; // A dark transparent panel covering the screen
    [SerializeField] private GameObject replacePrimaryBtn; // Invisible button covering the left gauntlet
    [SerializeField] private GameObject replaceSecondaryBtn; // Invisible button covering the right gauntlet

    [Header("Details Panel UI")]
    [SerializeField] private TMP_Text detailNameText;
    [SerializeField] private TMP_Text detailDescriptionText;
    [SerializeField] private Image detailIcon;

    [Header("Navigation & Setup")]
    [SerializeField] private GameObject firstGauntletSlot;
    public Transform primaryGauntlet;
    public Transform secondaryGauntlet;

    private GameObject _lastSelectedSlot;
    private GameObject _pendingGauntletPrefab;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        SetupHoverEvents(primaryGauntlet);
        SetupHoverEvents(secondaryGauntlet);
        if (swapOverlayPanel != null) swapOverlayPanel.SetActive(false);
    }

    // --- NEW: THE EQUIP PIPELINE ---

    public void TryEquipNewGauntlet(GameObject gauntletPrefab)
    {
        // 1. Is the Primary Slot empty? (No Gauntlet child)
        if (primaryGauntlet.GetComponentInChildren<GauntletManager>() == null)
        {
            Instantiate(gauntletPrefab, primaryGauntlet);
            SetupHoverEvents(primaryGauntlet);
            return;
        }

        // 2. Is the Secondary Slot empty?
        if (secondaryGauntlet.GetComponentInChildren<GauntletManager>() == null)
        {
            Instantiate(gauntletPrefab, secondaryGauntlet);
            SetupHoverEvents(secondaryGauntlet);
            return;
        }

        // 3. Both are full! Ask the player which one to destroy.
        BeginGauntletSwap(gauntletPrefab);
    }

    private void BeginGauntletSwap(GameObject prefab)
    {
        _pendingGauntletPrefab = prefab;

        Time.timeScale = 0f;

        // Open the character screen and turn on the Swap Overlay!
        gameObject.SetActive(true);
        rightSideDetails.SetActive(true);
        rightSideRewards.SetActive(false);

        swapOverlayPanel.SetActive(true);

        swapOverlayPanel.transform.SetAsLastSibling();

        // Give controller focus to the "Replace Primary" button so they can choose
        if (Gamepad.current != null && replacePrimaryBtn != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(replacePrimaryBtn);
        }
    }

    // Call this from the OnClick of your 'ReplacePrimaryBtn'
    public void OnReplacePrimaryClicked() { ExecuteGauntletSwap(true); }

    // Call this from the OnClick of your 'ReplaceSecondaryBtn'
    public void OnReplaceSecondaryClicked() { ExecuteGauntletSwap(false); }

    private void ExecuteGauntletSwap(bool isPrimary)
    {
        Transform targetContainer = isPrimary ? primaryGauntlet : secondaryGauntlet;
        GauntletManager oldManager = targetContainer.GetComponentInChildren<GauntletManager>();
        List<GameObject> gemsToTransfer = new List<GameObject>();

        if (oldManager != null)
        {
            // 1. Extract all gems from the old gauntlet
            for (int i = 0; i < oldManager.fingerSlots.Count; i++)
            {
                Transform slot = oldManager.fingerSlots[i].transform;
                if (slot.childCount > 0)
                {
                    GameObject gem = slot.GetChild(0).gameObject;
                    gem.transform.SetParent(this.transform); // Hold temporarily
                    gemsToTransfer.Add(gem);
                }
                else
                {
                    gemsToTransfer.Add(null); // Keep the exact index alignment!
                }
            }

            // Disable and destroy the old gauntlet
            oldManager.gameObject.SetActive(false);
            oldManager.name = "DEAD_GAUNTLET";
            Destroy(oldManager.gameObject);
        }

        // 2. Spawn the new gauntlet
        GameObject newGauntlet = Instantiate(_pendingGauntletPrefab, targetContainer);
        GauntletManager newManager = newGauntlet.GetComponent<GauntletManager>();

        // 3. Jam the gems into the new physical slots! 
        // (Even if the new gauntlet has fewer 'active' slots, we put them in the physical UI slots. 
        // When the newManager's Start() method runs in a split second, it will automatically 
        // detect the overflow and open the Reward Screen for us!)
        for (int i = 0; i < gemsToTransfer.Count; i++)
        {
            GameObject gem = gemsToTransfer[i];
            if (gem != null && i < newManager.fingerSlots.Count)
            {
                gem.transform.SetParent(newManager.fingerSlots[i].transform);
                gem.transform.localPosition = Vector3.zero;
                gem.GetComponent<RectTransform>().sizeDelta = newManager.fingerSlots[i].GetComponent<RectTransform>().rect.size;
            }
        }

        // 4. Clean up and restore UI
        SetupHoverEvents(targetContainer);
        swapOverlayPanel.SetActive(false);
        _pendingGauntletPrefab = null;

        // Give focus back to normal inventory navigation
        if (Gamepad.current != null && firstGauntletSlot != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(firstGauntletSlot);
        }
    }

    // --- STANDARD INVENTORY LOGIC ---

    private void SetupHoverEvents(Transform gauntletParent)
    {
        if (gauntletParent == null) return;
        foreach (Transform child in gauntletParent.GetComponentsInChildren<Transform>(true))
        {
            if (child.name.Contains("Slot") && child.GetComponent<EventTrigger>() == null)
            {
                EventTrigger trigger = child.gameObject.AddComponent<EventTrigger>();
                EventTrigger.Entry entry = new EventTrigger.Entry();
                entry.eventID = EventTriggerType.PointerEnter;
                entry.callback.AddListener((data) => {
                    _lastSelectedSlot = child.gameObject;
                    UpdateDetailsPanel(child.gameObject);
                });
                trigger.triggers.Add(entry);
            }
        }
    }

    private void OnEnable()
    {
        if (swapOverlayPanel != null && !swapOverlayPanel.activeSelf)
        {
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
        // Cancel logic (Esc / East Button)
        bool cancelPressed = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
                             (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);

        if (cancelPressed)
        {
            if (swapOverlayPanel.activeSelf)
            {
                // Cancel the swap entirely!
                swapOverlayPanel.SetActive(false);
                _pendingGauntletPrefab = null;
                gameObject.SetActive(false);
            }
            else if (rightSideDetails.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        // Standard controller hover logic (Disable if swapping!)
        if (!swapOverlayPanel.activeSelf && EventSystem.current != null && EventSystem.current.currentSelectedGameObject != _lastSelectedSlot)
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
}