using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.UI;

public class InventoryManager : MonoBehaviour
{
    [Header("Mode Panels")]
    [SerializeField] private GameObject rightSideDetails;
    [SerializeField] private GameObject rightSideRewards;

    [Header("Details Panel UI")]
    [SerializeField] private TMP_Text detailNameText;
    [SerializeField] private TMP_Text detailDescriptionText;
    [SerializeField] private Image detailIcon;

    [Header("Navigation & Setup")]
    [SerializeField] private GameObject firstGauntletSlot; // E.g., Primary Slot 1

    [Tooltip("Drag your Gauntlet Primary parent object here")]
    [SerializeField] private Transform primaryGauntlet;
    [Tooltip("Drag your Gauntlet Secondary parent object here")]
    [SerializeField] private Transform secondaryGauntlet;

    private GameObject _lastSelectedSlot;

    private void Start()
    {
        // Dynamically wire up the Mouse Hover events for all slots!
        SetupHoverEvents(primaryGauntlet);
        SetupHoverEvents(secondaryGauntlet);
    }

    private void SetupHoverEvents(Transform gauntletParent)
    {
        if (gauntletParent == null) return;

        foreach (Transform child in gauntletParent)
        {
            // Only attach hover events to the actual finger slots!
            if (child.name.Contains("Slot"))
            {
                EventTrigger trigger = child.GetComponent<EventTrigger>();
                if (trigger == null) trigger = child.gameObject.AddComponent<EventTrigger>();

                EventTrigger.Entry entry = new EventTrigger.Entry();
                entry.eventID = EventTriggerType.PointerEnter;
                entry.callback.AddListener((data) => {
                    _lastSelectedSlot = child.gameObject; // Sync it up so the controller doesn't fight it
                    UpdateDetailsPanel(child.gameObject);
                });
                trigger.triggers.Add(entry);
            }
        }
    }

    private void OnEnable()
    {
        // When opened normally, show Details, hide Rewards
        rightSideDetails.SetActive(true);
        rightSideRewards.SetActive(false);

        // Give controller focus to the first gauntlet slot
        if (Gamepad.current != null && firstGauntletSlot != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(firstGauntletSlot);
        }
    }

    private void Update()
    {
        // Controller Navigation Update
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != _lastSelectedSlot)
        {
            GameObject currentSel = EventSystem.current.currentSelectedGameObject;

            // Only update the panel if the controller is actually highlighting a Slot!
            if (currentSel != null && currentSel.name.Contains("Slot"))
            {
                _lastSelectedSlot = currentSel;
                UpdateDetailsPanel(_lastSelectedSlot);
            }
        }

        // Handle backing out (East/Escape)
        bool cancelPressed = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
                             (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);

        // Only allow backing out to the pause menu if we are in Details mode
        if (cancelPressed && rightSideDetails.activeSelf)
        {
            gameObject.SetActive(false);
        }
    }

    private void UpdateDetailsPanel(GameObject slot)
    {
        if (slot == null) return;

        // Check if the highlighted slot actually has a gem inside it
        DraggableGem equippedGem = slot.GetComponentInChildren<DraggableGem>();

        if (equippedGem != null)
        {
            detailNameText.text = equippedGem.gemName;
            detailDescriptionText.text = equippedGem.gemDescription;

            if (detailIcon != null)
            {
                detailIcon.sprite = equippedGem.gemIcon;
                detailIcon.color = Color.white;
            }
        }
        else
        {
            // The slot is empty!
            detailNameText.text = "Empty Slot";
            detailDescriptionText.text = "No gem equipped here.";

            if (detailIcon != null)
            {
                detailIcon.color = Color.clear; // Hide the icon image completely
            }
        }
    }
}