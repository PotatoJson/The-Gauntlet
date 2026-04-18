using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems; // Required for selection logic
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
    [SerializeField] private Color inactiveColor = Color.gray;

    private int _currentTabIndex = 0;
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

    private void HandleInputDetection()
    {
        if (Gamepad.current != null && Gamepad.current.wasUpdatedThisFrame)
        {
            if (!_isUsingGamepad)
            {
                _isUsingGamepad = true;
                UpdatePromptVisuals("LT", "RT");
            }
        }
        else if (Keyboard.current != null && Keyboard.current.wasUpdatedThisFrame)
        {
            if (_isUsingGamepad)
            {
                _isUsingGamepad = false;
                UpdatePromptVisuals("Q", "E");
            }
        }
    }

    private void UpdatePromptVisuals(string left, string right)
    {
        if (leftPromptText != null) leftPromptText.text = left;
        if (rightPromptText != null) rightPromptText.text = right;
    }

    private void HandleTabSwitching()
    {
        if (Keyboard.current != null)
        {
            if (Keyboard.current.qKey.wasPressedThisFrame) CycleTab(-1);
            if (Keyboard.current.eKey.wasPressedThisFrame) CycleTab(1);
        }

        if (Gamepad.current != null)
        {
            if (Gamepad.current.leftTrigger.wasPressedThisFrame || Gamepad.current.leftShoulder.wasPressedThisFrame)
                CycleTab(-1);
            if (Gamepad.current.rightTrigger.wasPressedThisFrame || Gamepad.current.rightShoulder.wasPressedThisFrame)
                CycleTab(1);
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

        for (int i = 0; i < allPanels.Count; i++)
        {
            bool isActive = (i == tabIndex);

            if (allPanels[i] != null)
                allPanels[i].SetActive(isActive);

            if (tabButtons.Count > i && tabButtons[i] != null)
            {
                tabButtons[i].image.color = isActive ? activeColor : inactiveColor;
            }

            // --- NEW: AUTO-SELECT FIRST ITEM ---
            if (isActive && firstItemsInPanels.Count > i && firstItemsInPanels[i] != null)
            {
                // We only auto-select if the user is using a controller or keyboard to switch tabs
                if (_isUsingGamepad || (Keyboard.current != null && (Keyboard.current.qKey.wasPressedThisFrame || Keyboard.current.eKey.wasPressedThisFrame)))
                {
                    EventSystem.current.SetSelectedGameObject(null);
                    EventSystem.current.SetSelectedGameObject(firstItemsInPanels[i].gameObject);
                }
            }
        }
    }
}