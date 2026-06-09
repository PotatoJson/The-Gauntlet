using UnityEngine;
using UnityEngine.InputSystem;

public static class InputHelper
{
    public static bool IsGamepadLastUsed()
    {
        if (Gamepad.current == null) return false;

        double lastGamepadTime = Gamepad.current.lastUpdateTime;
        double lastMouseTime = Mouse.current != null ? Mouse.current.lastUpdateTime : 0.0;
        double lastKeyboardTime = Keyboard.current != null ? Keyboard.current.lastUpdateTime : 0.0;

        return lastGamepadTime >= lastMouseTime && lastGamepadTime >= lastKeyboardTime;
    }

    public static string GetBindingString(string actionName, InputBinding.DisplayStringOptions options = InputBinding.DisplayStringOptions.DontIncludeInteractions)
    {
        InputAction action = InputSystem.actions.FindAction(actionName);
        if (action == null) return "";

        bool isGamepad = IsGamepadLastUsed();
        
        for (int i = 0; i < action.bindings.Count; i++)
        {
            var binding = action.bindings[i];
            if (binding.isComposite) continue;

            bool isGamepadBinding = binding.effectivePath.Contains("<Gamepad>");
            bool isKeyboardBinding = binding.effectivePath.Contains("<Keyboard>") || binding.effectivePath.Contains("<Mouse>");

            if ((isGamepad && isGamepadBinding) || (!isGamepad && isKeyboardBinding))
            {
                return action.GetBindingDisplayString(i, options);
            }
        }

        // Fallback to default binding display string if no specific match found
        return action.GetBindingDisplayString(options);
    }
}