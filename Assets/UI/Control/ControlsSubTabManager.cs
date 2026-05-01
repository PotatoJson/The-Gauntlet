using UnityEngine;
using TMPro;

public class ControlsSubTabManager : MonoBehaviour
{
    [Header("Pages")]
    public GameObject keyboardPage;
    public GameObject gamepadPage;

    [Header("UI Elements")]
    public TextMeshProUGUI titleText;
    public GameObject leftArrowGroup;  // The group containing the Left Arrow & "Q" icon
    public GameObject rightArrowGroup; // The group containing the Right Arrow & "E" icon

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

            titleText.text = "KeyBoard";

            leftArrowGroup.SetActive(false); // Can't go left from Keyboard
            rightArrowGroup.SetActive(true); // Can go right to Gamepad
        }
        else // Gamepad
        {
            keyboardPage.SetActive(false);
            gamepadPage.SetActive(true);

            titleText.text = "GamePad";

            leftArrowGroup.SetActive(true);  // Can go left to Keyboard
            rightArrowGroup.SetActive(false); // Can't go right from Gamepad
        }
    }

    // Returns TRUE if the input was consumed by this sub-menu.
    // Returns FALSE if we reached the edge and should switch main tabs instead.
    public bool TryHandleSubTabInput(int direction)
    {
        if (direction > 0 && _currentSubTab == 0)
        {
            // Pressing E/RT while on Keyboard -> Switch to Gamepad
            SetSubTab(1);
            return true;
        }
        else if (direction < 0 && _currentSubTab == 1)
        {
            // Pressing Q/LT while on Gamepad -> Switch to Keyboard
            SetSubTab(0);
            return true;
        }

        // Reached the edge (e.g., pressed E while already on Gamepad). Let the main menu take over.
        return false;
    }
}