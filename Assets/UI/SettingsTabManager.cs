using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using TMPro;

public class SettingsTabManager : MonoBehaviour
{
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

    private int _currentTabIndex = 0;
    private int _previewTabIndex = 0;
    private bool _isUsingGamepad = false;

    private void Start()
    {
        OpenTab(0);
    }

    private void Update()
    {
        HandleInputDetection();
        HandleTabSwitching();
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
        // Detect current input device to manage cursor and highlights
        if (Gamepad.current != null && Gamepad.current.wasUpdatedThisFrame)
        {
            _isUsingGamepad = true;
            UpdatePromptVisuals("LT", "RT");
        }
        else if (Keyboard.current != null && Keyboard.current.wasUpdatedThisFrame)
        {
            _isUsingGamepad = false;
            UpdatePromptVisuals("Q", "E");
        }
    }

    private void UpdatePromptVisuals(string left, string right)
    {
        if (leftPromptText != null) leftPromptText.text = left;
        if (rightPromptText != null) rightPromptText.text = right;
    }

    private void HandleTabSwitching()
    {
        // 1. KEYBOARD: Immediate switching as before
        if (Keyboard.current != null)
        {
            if (Keyboard.current.qKey.wasPressedThisFrame) CycleTab(-1);
            else if (Keyboard.current.eKey.wasPressedThisFrame) CycleTab(1);
        }

        // 2. GAMEPAD: Select then Confirm flow
        if (Gamepad.current != null)
        {
            int direction = 0;
            if (Gamepad.current.leftTrigger.wasPressedThisFrame || Gamepad.current.leftShoulder.wasPressedThisFrame)
                direction = -1;
            else if (Gamepad.current.rightTrigger.wasPressedThisFrame || Gamepad.current.rightShoulder.wasPressedThisFrame)
                direction = 1;

            if (direction != 0)
            {
                UpdatePreviewSelection(direction);
            }

            // Confirm selection with Button South
            if (Gamepad.current.buttonSouth.wasPressedThisFrame && _previewTabIndex != _currentTabIndex)
            {
                OpenTab(_previewTabIndex);
            }
        }
    }

    private void UpdatePreviewSelection(int direction)
    {
        _previewTabIndex += direction;

        // Cycle through tabs and skip the currently active one
        if (_previewTabIndex >= allPanels.Count) _previewTabIndex = 0;
        if (_previewTabIndex < 0) _previewTabIndex = allPanels.Count - 1;

        if (_previewTabIndex == _currentTabIndex)
        {
            _previewTabIndex += direction;
            if (_previewTabIndex >= allPanels.Count) _previewTabIndex = 0;
            if (_previewTabIndex < 0) _previewTabIndex = allPanels.Count - 1;
        }

        // Clear panel selection while choosing a new tab to avoid double highlights
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        UpdateTabVisuals();
    }

    private void UpdateTabVisuals()
    {
        for (int i = 0; i < tabButtons.Count; i++)
        {
            if (tabButtons[i] == null) continue;

            if (i == _currentTabIndex)
                tabButtons[i].image.color = activeColor;
            else if (i == _previewTabIndex)
                tabButtons[i].image.color = previewColor;
            else
                tabButtons[i].image.color = inactiveColor;
        }
    }

    public void CycleTab(int direction)
    {
        _currentTabIndex += direction;
        if (_currentTabIndex >= allPanels.Count) _currentTabIndex = 0;
        if (_currentTabIndex < 0) _currentTabIndex = allPanels.Count - 1;

        OpenTab(_currentTabIndex);
    }

    public void OpenTab(int tabIndex)
    {
        _currentTabIndex = tabIndex;
        _previewTabIndex = tabIndex;

        // Clear focus before switching to ensure a clean visual state
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        for (int i = 0; i < allPanels.Count; i++)
        {
            bool isActive = (i == tabIndex);
            if (allPanels[i] != null) allPanels[i].SetActive(isActive);
        }

        UpdateTabVisuals();

        // Only force selection if the user is using a controller
        if (firstItemsInPanels.Count > tabIndex && firstItemsInPanels[tabIndex] != null && _isUsingGamepad)
        {
            EventSystem.current.SetSelectedGameObject(firstItemsInPanels[tabIndex].gameObject);
        }
    }
}