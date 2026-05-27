using UnityEngine;
using TMPro;
using UnityEngine.Localization;

public class ControlsSubTabManager : MonoBehaviour
{
    [Header("Pages")]
    public GameObject keyboardPage;
    public GameObject gamepadPage;

    [Header("UI Elements")]
    public TextMeshProUGUI titleText;
    public GameObject leftArrowGroup;  // The group containing the Left Arrow & "Q" icon
    public GameObject rightArrowGroup; // The group containing the Right Arrow & "E" icon

[Header("Localized Strings")]
    // 2. Add these variables to link your translation keys in the Inspector
    public LocalizedString keyboardTitleString; 
    public LocalizedString gamepadTitleString;
    private int _currentSubTab = 0; // 0 = Keyboard, 1 = Gamepad

    private void OnEnable()
    {
        // Always reset to the Keyboard tab when first opening the Controls menu
        SetSubTab(0);
    }

    public void SetSubTab(int index)
    {
        _currentSubTab = index;

        if (_currentSubTab == 0) // Keyboard
        {
            keyboardPage.SetActive(true);
            gamepadPage.SetActive(false);

            titleText.text = keyboardTitleString.GetLocalizedString();

            leftArrowGroup.SetActive(false); // Can't go left from Keyboard
            rightArrowGroup.SetActive(true); // Can go right to Gamepad
        }
        else // Gamepad
        {
            keyboardPage.SetActive(false);
            gamepadPage.SetActive(true);

            titleText.text = gamepadTitleString.GetLocalizedString();

            leftArrowGroup.SetActive(true);  // Can go left to Keyboard
            rightArrowGroup.SetActive(false); // Can't go right from Gamepad
        }
    }

    // Returns TRUE if the input was consumed by this sub-menu.
    // Returns FALSE if we reached the edge and should switch main tabs instead.
    public void TryHandleSubTabInput(int direction)
    {
        if (direction > 0)
        {
            // Go right. If on Keyboard (0), go to Gamepad (1). Otherwise, wrap to Keyboard.
            SetSubTab(_currentSubTab == 0 ? 1 : 0);
        }
        else if (direction < 0)
        {
            // Go left. If on Gamepad (1), go to Keyboard (0). Otherwise, wrap to Gamepad.
            SetSubTab(_currentSubTab == 1 ? 0 : 1);
        }
    }
}