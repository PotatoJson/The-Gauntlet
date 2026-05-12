using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class GemPopupMenu : MonoBehaviour
{
    public static GemPopupMenu Instance;

    [Header("UI Panels")]
    [SerializeField] private GameObject mainPanel; // The object holding Equip/Return
    [SerializeField] private GameObject subPanel;  // The object holding Primary/Secondary/Arrow
    [SerializeField] private GameObject equipButton;

    [Header("Gauntlet Slot Containers")]
    [Tooltip("Drag the parent object that holds Slot1-Slot5 for Primary here")]
    [SerializeField] private Transform primaryGauntlet;
    [Tooltip("Drag the parent object that holds Slot1-Slot5 for Secondary here")]
    [SerializeField] private Transform secondaryGauntlet;

    private DraggableGem _targetGem;
    private RectTransform _rectTransform;

    private void Awake()
    {
        Instance = this;
        _rectTransform = GetComponent<RectTransform>();
        gameObject.SetActive(false); // Hide the menu when the game starts
    }

    private void Update()
    {
        if (gameObject.activeInHierarchy)
        {
            bool cancelPressed = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
                                 (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);

            if (cancelPressed)
            {
                // LAYER 1: If the Sub Panel (Primary/Secondary) is open, close that and return to Equip!
                if (subPanel != null && subPanel.activeSelf)
                {
                    subPanel.SetActive(false);
                    if (EventSystem.current != null && equipButton != null)
                    {
                        EventSystem.current.SetSelectedGameObject(equipButton);
                    }
                }
                // LAYER 2: Otherwise, close the whole PopUI and go back to the Gem!
                else
                {
                    CloseMenu();

                    if (EventSystem.current != null && _targetGem != null)
                    {
                        EventSystem.current.SetSelectedGameObject(null);
                        EventSystem.current.SetSelectedGameObject(_targetGem.gameObject);
                    }
                }
            }
        }
    }

    public void OpenMenu(DraggableGem gem, RectTransform gemRect)
    {
        _targetGem = gem;
        transform.position = gemRect.position;
        _rectTransform.anchoredPosition += new Vector2(gemRect.rect.width / 2f, -gemRect.rect.height / 2f);

        mainPanel.SetActive(true);
        subPanel.SetActive(false);
        gameObject.SetActive(true);

        // Instantly give the controller cursor to the Equip button
        if (Gamepad.current != null && equipButton != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(equipButton);
        }
    }

    public void CloseMenu()
    {
        gameObject.SetActive(false);
    }

    // --- BUTTON METHODS ---

    public void OnEquipClicked()
    {
        subPanel.SetActive(true);
    }

    public void OnReturnClicked()
    {
        if (_targetGem != null)
        {
            // Send it back to its specific category grid
            _targetGem.ReturnToInventory(); // Uses the safe return method we added earlier!
        }
        CloseMenu();
    }

    public void OnPrimaryClicked()
    {
        EquipToFirstAvailableSlot(primaryGauntlet);
    }

    public void OnSecondaryClicked()
    {
        EquipToFirstAvailableSlot(secondaryGauntlet);
    }

    // --- CORE LOGIC ---

    private void EquipToFirstAvailableSlot(Transform gauntletParent)
    {
        // Loop through all children (image, Text, Slot1, Slot2...)
        foreach (Transform child in gauntletParent)
        {
            // Skip this child completely if it is not a Gauntlet Slot
            if (!child.name.Contains("Slot"))
            {
                continue;
            }

            // If we made it past the check, it is a valid slot! Is it empty?
            if (child.childCount == 0)
            {
                // Move the gem
                _targetGem.parentAfterDrag = child;
                _targetGem.transform.SetParent(child);

                // Trigger the juicy DOTween slide and bounce
                _targetGem.AnimateToNewHome();

                CloseMenu();
                return; // Stop looking, we found a slot
            }
        }

        // If the loop finishes without returning, the gauntlet is full.
        Debug.Log("This Gauntlet is full!");
        CloseMenu();
    }
}