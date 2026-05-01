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

            // 2. Conflict Validation
            if (conflictManager != null)
            {
                if (conflictManager.CheckForConflicts(inputAction.action, bindingIndex, newPath))
                {
                    RejectRebind(null); // ConflictManager already handles the text
                    return;
                }
            }

            // 3. Success!
            string rebinds = inputAction.action.SaveBindingOverridesAsJson();
            PlayerPrefs.SetString("rebinds_" + inputAction.action.name, rebinds);
        }
        else
        {
            RejectRebind(null); // Silent reject for manual cancels
            return;
        }

        CleanupAndRestoreFocus();
    }

    private void RejectRebind(string warningMessage)
    {
        // Revert binding
        if (string.IsNullOrEmpty(_oldOverridePath))
            inputAction.action.RemoveBindingOverride(bindingIndex);
        else
            inputAction.action.ApplyBindingOverride(bindingIndex, _oldOverridePath);

        // Tell the central manager to show the error
        if (!string.IsNullOrEmpty(warningMessage) && conflictManager != null)
        {
            conflictManager.ShowWarning(warningMessage);
        }

        CleanupAndRestoreFocus();
    }

    private void CleanupAndRestoreFocus()
    {
        inputAction.action.Enable();
        rebindButton.interactable = true;
        RefreshDisplay();

        // Fix the vanishing cursor bug!
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(rebindButton.gameObject);
        }
    }

    private void RefreshDisplay()
    {
        bindingText.text = inputAction.action.GetBindingDisplayString(bindingIndex);
    }
}