using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem; // Use New Input System as requested

public class SettingsTabManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private List<GameObject> allPanels;
    [SerializeField] private List<Button> tabButtons;

    [Header("Visual Feedback")]
    [SerializeField] private Color activeColor = Color.white;
    [SerializeField] private Color inactiveColor = Color.gray;

    private int _currentTabIndex = 0;

    private void Start()
    {
        // Default to the first tab (Video) on start
        OpenTab(0);
    }

    private void Update()
    {
        // Only listen for keys if the settings menu is currently active
        if (Keyboard.current == null) return;

        // E key moves to the next tab (Right)
        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            CycleTab(1);
        }
        // Q key moves to the previous tab (Left)
        else if (Keyboard.current.qKey.wasPressedThisFrame)
        {
            CycleTab(-1);
        }
    }

    public void QPress()
    {
        CycleTab(-1);
    }

    public void EPress()
    {
        CycleTab(1);
    }
    private void CycleTab(int direction)
    {
        _currentTabIndex += direction;

        // Loop back to start if we go past the end, and vice versa
        if (_currentTabIndex >= allPanels.Count) _currentTabIndex = 0;
        if (_currentTabIndex < 0) _currentTabIndex = allPanels.Count - 1;

        OpenTab(_currentTabIndex);
    }

    public void OpenTab(int tabIndex)
    {
        _currentTabIndex = tabIndex; // Keep index in sync if user clicks a button manually

        for (int i = 0; i < allPanels.Count; i++)
        {
            bool isActive = (i == tabIndex);

            // Show/Hide the panel
            if (allPanels[i] != null)
                allPanels[i].SetActive(isActive);

            // Update button visual state
            if (tabButtons.Count > i && tabButtons[i] != null)
            {
                tabButtons[i].image.color = isActive ? activeColor : inactiveColor;
            }
        }
    }
}