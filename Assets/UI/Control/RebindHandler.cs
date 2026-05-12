using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.UI;
using UnityEngine.EventSystems; // Required for focus restoration

public class RebindHandler : MonoBehaviour
{
    [Header("Input Reference")]
    [SerializeField] private InputActionReference inputAction;
    [SerializeField] private int bindingIndex = 0;
    [SerializeField] private bool useGamepad = false; // Check this for Gamepad rows!

    [Header("UI Reference")]
    [SerializeField] private TextMeshProUGUI bindingText;
    [SerializeField] private Button rebindButton;

    [Header("Managers")]
    [SerializeField] private ConflictManager conflictManager; // Single reference

    private InputActionRebindingExtensions.RebindingOperation _rebindOperation;
    private string _oldOverridePath;

    private void Start()
    {
        RefreshDisplay();
        rebindButton.onClick.AddListener(StartRebind);
    }

    public void StartRebind()
    {
        bindingText.text = "...";
        rebindButton.interactable = false;
        inputAction.action.Disable();

        // Save old path to revert if they mess up or cancel
        _oldOverridePath = inputAction.action.bindings[bindingIndex].overridePath;

        _rebindOperation = inputAction.action.PerformInteractiveRebinding(bindingIndex)
            .WithControlsExcluding("<Pointer>/delta")
            .WithControlsExcluding("<Pointer>/position")
            .WithExpectedControlType("Button")
            .WithControlsHavingToMatchPath("*")
            .OnMatchWaitForAnother(0.15f) 
            .OnComplete(operation => FinishRebind(true))
            .OnCancel(operation => FinishRebind(false))
            .Start();
    }

    private void FinishRebind(bool success)
    {
        _rebindOperation.Dispose();

        if (success)
        {
            string newPath = inputAction.action.bindings[bindingIndex].effectivePath;
            bool isGamepadInput = newPath.Contains("<Gamepad>");
            bool isKeyboardInput = newPath.Contains("<Keyboard>") || newPath.Contains("<Mouse>");

            // 1. Device Validation
            if (useGamepad && !isGamepadInput)
            {
                RejectRebind("Please use a <b>Controller</b> to bind this action.");
                return;
            }
            else if (!useGamepad && !isKeyboardInput)
            {
                RejectRebind("Please use a <b>Keyboard</b> or <b>Mouse</b> to bind this action.");
                return;
            }

            // 2. Conflict Validation (Uses the new string check!)
            if (conflictManager != null)
            {
                string conflictMsg = conflictManager.GetConflictMessage(inputAction.action, bindingIndex, newPath);
                if (conflictMsg != null)
                {
                    RejectRebind(conflictMsg);
                    return;
                }
            }

            // 3. Success! Save and restore focus cleanly.
            string rebinds = inputAction.action.SaveBindingOverridesAsJson();
            PlayerPrefs.SetString("rebinds_" + inputAction.action.name, rebinds);

            inputAction.action.Enable();
            rebindButton.interactable = true;
            RefreshDisplay();

            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(rebindButton.gameObject);
            }
        }
        else
        {
            RejectRebind(null); // Silent reject for manual cancels
        }
    }

    private void RejectRebind(string warningMessage)
    {
        // Revert binding
        if (string.IsNullOrEmpty(_oldOverridePath))
            inputAction.action.RemoveBindingOverride(bindingIndex);
        else
            inputAction.action.ApplyBindingOverride(bindingIndex, _oldOverridePath);

        // Turn the UI back on
        inputAction.action.Enable();
        rebindButton.interactable = true;
        RefreshDisplay();

        // THE FIX: Route the focus properly!
        if (!string.IsNullOrEmpty(warningMessage) && conflictManager != null)
        {
            // If there is an error, pass the Warning Message AND the Rebind Button to the popup!
            conflictManager.ShowWarning(warningMessage, rebindButton.gameObject);
        }
        else
        {
            // If there is no error (the player just hit ESC to cancel), return focus normally.
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(rebindButton.gameObject);
            }
        }
    }

    private void RefreshDisplay()
    {
        bindingText.text = inputAction.action.GetBindingDisplayString(bindingIndex);
    }
}