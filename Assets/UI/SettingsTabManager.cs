using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SettingsTabManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private List<GameObject> allPanels;
    [SerializeField] private List<Button> tabButtons;

    [Header("Visual Feedback")]
    [SerializeField] private Color activeColor = Color.white;
    [SerializeField] private Color inactiveColor = Color.gray;

    public void OpenTab(int tabIndex)
    {
        // 1. Hide all panels
        for (int i = 0; i < allPanels.Count; i++)
        {
            allPanels[i].SetActive(false);

            // 2. Dim the button text/color to show it's inactive
            tabButtons[i].image.color = inactiveColor;
        }

        // 3. Show only the selected panel
        allPanels[tabIndex].SetActive(true);

        // 4. Highlight the active button
        tabButtons[tabIndex].image.color = activeColor;
    }
}