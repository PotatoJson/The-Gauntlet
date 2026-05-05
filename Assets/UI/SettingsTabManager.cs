using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using TMPro;

public class SettingsTabManager : MonoBehaviour
{
    [Header("Events")]
    public UnityEngine.Events.UnityEvent onExitSettingsRequested;

    [Header("Input Actions")]
    [SerializeField] private InputActionReference previousTabAction;
    [SerializeField] private InputActionReference nextTabAction;
    [SerializeField] private InputActionReference enterTabAction; // South / Enter
    [SerializeField] private InputActionReference backTabAction;  // East / Escape

    [Header("UI References")]
    [SerializeField] private List<GameObject> allPanels;
    [SerializeField] private List<Button> tabButtons;

    [Header("Panel Navigation")]
    [Tooltip("Assign the first item (Dropdown/Toggle) of each panel here in order.")]
    [SerializeField] private List<Selectable> firstItemsInPanels;

    [Header("Controller Prompts")]
    [SerializeField] private TextMeshProUGUI leftPromptText;
    [SerializeField] private TextMeshProUGUI rightPromptText;

    [Header("Visual Feedback")]
    [SerializeField] private Color activeColor = Color.white;
    [SerializeField] private Color previewColor = Color.red;
    [SerializeField] private Color inactiveColor = Color.gray;

    [Header("Sub-Tab Managers")]
    [SerializeField] private ControlsSubTabManager controlsSubTabManager;

    private int _currentTabIndex = 0;
    private int _previewTabIndex = 0;
    private bool _isUsingGamepad = false;
    private bool _isFocusOnHeader = true;
    private float _inputCooldown = 0f;

    private void Start()
    {
        OpenTab(0);
    }

    public void InitializeSettingsMenu()
    {
        _inputCooldown = 0.2f; // Reset the cooldown to prevent input bleed
        _isFocusOnHeader = true; // Force the logical state back to the header

        _previewTabIndex = _currentTabIndex;

        OpenTab(_currentTabIndex);
    }

    private void Update()
    {
        if (allPanels.Count == 0 || !allPanels[0].transform.parent.gameObject.activeInHierarchy) return;

        if (_inputCooldown > 0)
        {
            _inputCooldown -= Time.unscaledDeltaTime;
            return;
        }

        HandleInputDetection();
        HandleTabSwitching();
    }

    private void OnEnable()
    {
        // You must enable the actions for them to read input
        if (previousTabAction != null) previousTabAction.action.Enable();
        if (nextTabAction != null) nextTabAction.action.Enable();
        if (enterTabAction != null) enterTabAction.action.Enable();
        if (backTabAction != null) backTabAction.action.Enable();
    }

    private void OnDisable()
    {
        if (previousTabAction != null) previousTabAction.action.Disable();
        if (nextTabAction != null) nextTabAction.action.Disable();
        if (enterTabAction != null) enterTabAction.action.Disable();
        if (backTabAction != null) backTabAction.action.Disable();
    }

    public void FocusCurrentTab()
    {
        _previewTabIndex = _currentTabIndex;
        UpdateTabVisuals();

        // Only focus the UI element if the player is using a gamepad
        if (firstItemsInPanels.Count > _currentTabIndex && firstItemsInPanels[_currentTabIndex] != null && _isUsingGamepad)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(firstItemsInPanels[_currentTabIndex].gameObject);
        }
    }

    private void HandleInputDetection()
    {
        // 1. Switch TO Gamepad
        if (Gamepad.current != null && Gamepad.current.wasUpdatedThisFrame)
        {
            if (!_isUsingGamepad) // Only trigger this the exact moment we switch
            {
                _isUsingGamepad = true;
                UpdatePromptVisuals("LT", "RT");
                UpdateTabVisuals(); // Instantly apply the preview color!
            }
        }
        // 2. Switch TO Keyboard/Mouse
        else if ((Keyboard.current != null && Keyboard.current.wasUpdatedThisFrame) ||
                 (Mouse.current != null && Mouse.current.delta.ReadValue().sqrMagnitude > 0.1f))
        {
            if (_isUsingGamepad) // Only trigger this the exact moment we switch
            {
                _isUsingGamepad = false;
                UpdatePromptVisuals("Q", "E");
                UpdateTabVisuals(); // Instantly remove the preview color!
            }
        }
    }

    // NEW: Decides where the controller cursor should spawn based on where you are in the menu
    private void RestoreGamepadFocus()
    {
        if (_isFocusOnHeader)
        {
            if (tabButtons != null && tabButtons.Count > _currentTabIndex && tabButtons[_currentTabIndex] != null)
            {
                EventSystem.current.SetSelectedGameObject(tabButtons[_currentTabIndex].gameObject);
            }
        }
        else
        {
            FocusCurrentTab(); // Uses your existing method to select the first item inside the panel
        }
    }

    private void UpdatePromptVisuals(string left, string right)
    {
        if (leftPromptText != null) leftPromptText.text = left;
        if (rightPromptText != null) rightPromptText.text = right;
    }

    private void HandleTabSwitching()
    {
        int direction = 0;

        if (previousTabAction.action.WasPressedThisFrame()) direction = -1;
        else if (nextTabAction.action.WasPressedThisFrame()) direction = 1;

        if (direction != 0)
        {
            // 1. Dived inside Controls? Move the sub-tabs.
            if (!_isFocusOnHeader && allPanels[_currentTabIndex].name == "Controls" && controlsSubTabManager != null)
            {
                controlsSubTabManager.TryHandleSubTabInput(direction);
            }
            // 2. On the Header? Move the PREVIEW highlight, do not switch the panel yet!
            else if (_isFocusOnHeader)
            {
                CyclePreview(direction);
            }
        }

        // Dive In / Confirm Tab (South Button)
        if (enterTabAction.action.WasPressedThisFrame() && _isFocusOnHeader)
        {
            _isFocusOnHeader = false;

            // If they pressed confirm on a NEW tab, actually open it!
            if (_previewTabIndex != _currentTabIndex)
            {
                OpenTab(_previewTabIndex);
            }
            // If they pressed confirm on the ALREADY OPEN tab, just dive in!
            else
            {
                UpdateTabVisuals();
                FocusCurrentTab();
            }
        }

        // Back Out (East Button)
        if (backTabAction.action.WasPressedThisFrame())
        {
            // 1. Are we inside a sub-menu? Just back out to the header.
            if (!_isFocusOnHeader)
            {
                _isFocusOnHeader = true;
                _previewTabIndex = _currentTabIndex;
                ReturnFocusToTabHeader();
            }
            // 2. Are we ALREADY on the header? Exit the settings menu entirely!
            else
            {
                onExitSettingsRequested?.Invoke();
            }
        }
    }

    private void CyclePreview(int direction)
    {
        _previewTabIndex += direction;

        // Wrap around logic
        if (_previewTabIndex >= allPanels.Count) _previewTabIndex = 0;
        if (_previewTabIndex < 0) _previewTabIndex = allPanels.Count - 1;

        // Clear Unity's default selection so it doesn't fight your custom colors
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        UpdateTabVisuals();
    }

    private void ReturnFocusToTabHeader()
    {
        if (tabButtons != null && tabButtons.Count > _currentTabIndex)
        {
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(tabButtons[_currentTabIndex].gameObject);
            }
            UpdateTabVisuals();
        }
    }

    private void UpdateTabVisuals()
    {
        for (int i = 0; i < tabButtons.Count; i++)
        {
            if (tabButtons[i] == null) continue;

            // 1. KEYBOARD & MOUSE: Keep it simple. Open tab is Active (Red), others are Inactive (Gray).
            if (!_isUsingGamepad)
            {
                tabButtons[i].image.color = (i == _currentTabIndex) ? activeColor : inactiveColor;
                continue; // Skip the rest of the loop for this button!
            }

            // 2. GAMEPAD: Use the 3-state logic (Active, Preview, Inactive)
            if (i == _previewTabIndex)
            {
                if (i == _currentTabIndex && !_isFocusOnHeader)
                {
                    tabButtons[i].image.color = activeColor;
                }
                else
                {
                    tabButtons[i].image.color = previewColor;
                }
            }
            else if (i == _currentTabIndex)
            {
                tabButtons[i].image.color = activeColor;
            }
            else
            {
                tabButtons[i].image.color = inactiveColor;
            }
        }
    }

    public void CycleTab(int direction)
    {
        int newIndex = _currentTabIndex + direction;
        if (newIndex >= allPanels.Count) newIndex = 0;
        if (newIndex < 0) newIndex = allPanels.Count - 1;

        OpenTab(newIndex);
    }

    public void OpenTab(int tabIndex)
    {
        _currentTabIndex = tabIndex;

        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);

        for (int i = 0; i < allPanels.Count; i++)
        {
            if (allPanels[i] != null) allPanels[i].SetActive(i == tabIndex);
        }

        UpdateTabVisuals();

        // Re-assign the correct UI focus depending on where the player is
        if (_isFocusOnHeader)
        {
            if (EventSystem.current != null && tabButtons.Count > tabIndex && tabButtons[tabIndex] != null)
            {
                EventSystem.current.SetSelectedGameObject(tabButtons[tabIndex].gameObject);
            }
        }
        else if (_isUsingGamepad)
        {
            FocusCurrentTab();
        }
    }
}