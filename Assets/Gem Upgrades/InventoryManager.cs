using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class InventoryManager : MonoBehaviour
{
    // Simplified State Machine: Just the Headers and the Grid
    public enum UpgradeMenuState { Category, Grid }
    private Transform _activeGridContainer;
    private UpgradeMenuState _currentState = UpgradeMenuState.Category;

    [Header("Events")]
    public UnityEvent onExitUpgradeRequested; // Tells GauntletMenu to close us

    [Header("UI References")]
    [Tooltip("Drag your 'Core' Text Button here")]
    [SerializeField] private GameObject firstCategoryButton;

    private GameObject _lastSelectedGridItem; // Remembers our spot in the grid
    private bool _isUsingGamepad; // Tracks which device the player is currently touching
    private GameObject _lastSelectedCategoryTab;

    private void OnEnable()
    {
        // 1. Always reset to the top layer when the menu opens
        _currentState = UpgradeMenuState.Category;

        // 2. Only force the controller highlight if they are actively using a Gamepad
        if (Gamepad.current != null)
        {
            _isUsingGamepad = true;
            SetFocus(firstCategoryButton);
        }
        else
        {
            _isUsingGamepad = false;
            SetFocus(null);
        }
    }

    private void Update()
    {
        HandleInputDetection();

        // --- NEW: Constantly track which Tab we are hovering over! ---
        if (_currentState == UpgradeMenuState.Category && EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
        {
            _lastSelectedCategoryTab = EventSystem.current.currentSelectedGameObject;
        }

        // Listen for the "Back / Cancel" button
        bool cancelPressed = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
                             (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);

        // NEW: Only cancel the grid if the Popup Menu is closed!
        if (cancelPressed && (GemPopupMenu.Instance == null || !GemPopupMenu.Instance.gameObject.activeInHierarchy))
        {
            HandleCancelInput();
        }

        // --- THE ELECTRIC FENCE ---
        if (_currentState == UpgradeMenuState.Grid && EventSystem.current != null &&
           (GemPopupMenu.Instance == null || !GemPopupMenu.Instance.gameObject.activeInHierarchy))
        {
            GameObject currentSel = EventSystem.current.currentSelectedGameObject;

            if (currentSel != null)
            {
                if (!currentSel.transform.IsChildOf(_activeGridContainer))
                {
                    SetFocus(_lastSelectedGridItem);
                }
                else
                {
                    _lastSelectedGridItem = currentSel;
                }
            }
        }
    }

    private void HandleInputDetection()
    {
        // Switch TO Gamepad
        if (Gamepad.current != null && Gamepad.current.wasUpdatedThisFrame)
        {
            if (!_isUsingGamepad)
            {
                _isUsingGamepad = true;

                // Immediately highlight the correct layer based on where the player was!
                if (_currentState == UpgradeMenuState.Category)
                    SetFocus(firstCategoryButton);
                else if (_currentState == UpgradeMenuState.Grid && _lastSelectedGridItem != null)
                    SetFocus(_lastSelectedGridItem);
            }
        }
        // Switch TO Keyboard/Mouse
        else if ((Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) ||
                 (Mouse.current != null && Mouse.current.delta.ReadValue().sqrMagnitude > 0.1f))
        {
            if (_isUsingGamepad)
            {
                _isUsingGamepad = false;

                // Clear the controller highlight so the mouse can freely hover and click
                SetFocus(null);
            }
        }
    }

    private void HandleCancelInput()
    {
        switch (_currentState)
        {
            case UpgradeMenuState.Grid:
                // Leave the gem grid, return focus to the specific tab we just came from!
                _currentState = UpgradeMenuState.Category;

                if (_isUsingGamepad)
                {
                    SetFocus(_lastSelectedCategoryTab != null ? _lastSelectedCategoryTab : firstCategoryButton);
                }
                break;

            case UpgradeMenuState.Category:
                // We are at the top level. Tell the main pause menu to close us!
                onExitUpgradeRequested?.Invoke();
                break;
        }
    }

    // --- BUTTON EVENT: Call this when pressing South/Enter on a Category Text ---
    public void EnterGrid(Transform gridContainer)
    {
        // Safety Check: Are there actually gems in this category?
        if (gridContainer.childCount > 0)
        {
            _currentState = UpgradeMenuState.Grid;

            _activeGridContainer = gridContainer;

            // Grab the very first gem in the list at runtime
            GameObject firstGem = gridContainer.GetChild(0).gameObject;
            _lastSelectedGridItem = firstGem; // Store it in memory immediately

            // Give the controller the cursor
            if (_isUsingGamepad) SetFocus(firstGem);
        }
        else
        {
            Debug.Log("This inventory is empty! Cannot enter grid.");
        }
    }

    // --- HELPER METHOD ---
    private void SetFocus(GameObject target)
    {
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            if (target != null)
            {
                EventSystem.current.SetSelectedGameObject(target);
            }
        }
    }
}