using UnityEngine;
using UnityEngine.InputSystem;

public class AltCursorReveal : MonoBehaviour
{
    private void Update()
    {
        // Safety check to ensure a keyboard is actually plugged in
        if (Keyboard.current == null) return;

        // CRITICAL SAFETY: If the game is paused (Time.timeScale == 0), 
        // we assume a menu is open. We don't want the Alt key to accidentally 
        // hide the cursor while the player is trying to pick a reward!
        if (Time.timeScale == 0f) return;

        // SHOW CURSOR: When the player presses and holds the Alt key
        if (Keyboard.current.altKey.wasPressedThisFrame)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        // HIDE CURSOR: When the player lets go of the Alt key
        else if (Keyboard.current.altKey.wasReleasedThisFrame)
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
    }
}