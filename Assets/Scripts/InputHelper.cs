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
}