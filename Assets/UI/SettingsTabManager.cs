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
    [SerializeField] private InputActionReference previousSubTabAction; 
    [SerializeField] private InputActionReference nextSubTabAction;

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
    [SerializeField] private ConflictManager conflictManager;

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
        if (previousTabAction != null) previousTabAction.action.Enable();
        if (nextTabAction != null) nextTabAction.action.Enable();
        if (previousSubTabAction != null) previousSubTabAction.action.Enable();
        if (nextSubTabAction != null) nextSubTabAction.action.Enable();
        if (enterTabAction != null) enterTabAction.action.Enable();
        if (backTabAction != null) backTabAction.action.Enable();
    }

    private void OnDisable()
    {
        if (previousTabAction != null) previousTabAction.action.Disable();
        if (nextTabAction != null) nextTabAction.action.Disable();
        if (previousSubTabAction != null) previousSubTabAction.action.Disable();
        if (nextSubTabAction != null) nextSubTabAction.action.Disable();
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
        if (conflictManager != null && conflictManager.IsPopupActive()) return;

        // 1. Check Main Tab Navigation (Up/Down)
        int mainDirection = 0;
        if (previousTabAction.action.WasPressedThisFrame()) mainDirection = -1;
        else if (nextTabAction.action.WasPressedThisFrame()) mainDirection = 1;

        // Main Tab movement ONLY happens if we are on the header
        if (mainDirection != 0 && _isFocusOnHeader)
        {
            CyclePreview(mainDirection);
        }

        // 2. Check Sub-Tab Navigation (Left/Right)
        int subDirection = 0;
        if (previousSubTabAction != null && previousSubTabAction.action.WasPressedThisFrame()) subDirection = -1;
        else if (nextSubTabAction != null && nextSubTabAction.action.WasPressedThisFrame()) subDirection = 1;

        // Sub Tab movement ONLY happens if we are inside the Controls panel
        if (subDirection != 0 && !_isFocusOnHeader && allPanels[_currentTabIndex].name == "Controls" && controlsSubTabManager != null)
        {
            controlsSubTabManager.TryHandleSubTabInput(subDirection);
        }

        // Dive In / Confirm Tab (South Button)
        if (enterTabAction.action.WasPressedThisFrame() && _isFocusOnHeader)
        {
            _isFocusOnHeader = false;

            if (_previewTabIndex != _currentTabIndex)
            {
                OpenTab(_previewTabIndex);
            }
            else
            {
                UpdateTabVisuals();
                FocusCurrentTab();
            }
        }

        // Back Out (East Button)
        if (backTabAction.action.WasPressedThisFrame())
        {
            if (!_isFocusOnHeader)
            {
                _isFocusOnHeader = true;
                _previewTabIndex = _currentTabIndex;
                ReturnFocusToTabHeader();
            }
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

        // Sync the EventSystem directly to the newly previewed tab button.
        if (EventSystem.current != null && tabButtons.Count > _previewTabIndex)
        {
            EventSystem.current.SetSelectedGameObject(tabButtons[_previewTabIndex].gameObject);
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
        // THE FIX: Define a perfectly transparent white color to match your new UI borders
        Color transparentState = new Color(1f, 1f, 1f, 0f);

        for (int i = 0; i < tabButtons.Count; i++)
        {
            if (tabButtons[i] == null) continue;

            // 1. KEYBOARD & MOUSE: Keep it simple. Open tab is Active, others are Transparent.
            if (!_isUsingGamepad)
            {
                tabButtons[i].image.color = (i == _currentTabIndex) ? activeColor : transparentState;
                continue;
            }

            // 2. GAMEPAD: Use the 3-state logic (Active, Preview, Transparent)
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
                // Unselected and un-previewed tabs vanish entirely!
                tabButtons[i].image.color = transparentState;
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