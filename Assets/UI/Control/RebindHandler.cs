using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.UI;

public class RebindHandler : MonoBehaviour
{
    [Header("Input Reference")]
    [SerializeField] private InputActionReference inputAction; // Drag an action here (e.g. Jump)
    [SerializeField] private int bindingIndex = 0; // 0 for Keyboard, 1 for Gamepad typically

    [Header("UI Reference")]
    [SerializeField] private TextMeshProUGUI bindingText;
    [SerializeField] private Button rebindButton;

    private InputActionRebindingExtensions.RebindingOperation _rebindOperation;

    [SerializeField] private ConflictManager conflictManager;

    private void Start()
    {
        RefreshDisplay();
        rebindButton.onClick.AddListener(StartRebind);
    }

    public void StartRebind()
    {
        bindingText.text = "..."; // Visual feedback without a separate overlay
        rebindButton.interactable = false;

        // Disable action before rebinding
        inputAction.action.Disable();

        _rebindOperation = inputAction.action.PerformInteractiveRebinding(bindingIndex)
            .WithControlsExcluding("<Pointer>/delta") // Don't bind to mouse movement
            .OnMatchWaitForAnother(0.1f)
            .OnComplete(operation => FinishRebind())
            .Start();
    }

    private void FinishRebind()
    {
        string newPath = inputAction.action.bindings[bindingIndex].overridePath;

        // Safety Check: Only check for conflicts if the manager is actually assigned
        if (conflictManager != null)
        {
            if (conflictManager.CheckForConflicts(inputAction.action, bindingIndex, newPath))
            {
                inputAction.action.RemoveBindingOverride(bindingIndex);
                RefreshDisplay();

                // Clean up and stop here so we don't save the conflict
                _rebindOperation.Dispose();
                inputAction.action.Enable();
                rebindButton.interactable = true;
                return;
            }
        }
        else
        {
            Debug.LogWarning("ConflictManager is missing on " + gameObject.name);
        }

        // No conflict (or no manager)? Save as normal
        string rebinds = inputAction.action.SaveBindingOverridesAsJson();
        PlayerPrefs.SetString("rebinds_" + inputAction.action.name, rebinds);

        _rebindOperation.Dispose();
        inputAction.action.Enable();
        rebindButton.interactable = true;
        RefreshDisplay();
    }

    private void RefreshDisplay()
    {
        // Gets the "human-readable" name (e.g., "Space" instead of "space [keyboard]")
        bindingText.text = inputAction.action.GetBindingDisplayString(bindingIndex);
    }
}