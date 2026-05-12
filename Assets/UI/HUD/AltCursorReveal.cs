using UnityEngine;
using UnityEngine.InputSystem;

public class AltCursorReveal : MonoBehaviour
{
    [Header("Input References")]
    [Tooltip("Drag your Player's 'Look' input action here so we can freeze the camera!")]
    [SerializeField] private InputActionReference lookAction;

    private void Update()
    {
        // Safety check to ensure a keyboard is actually plugged in
        if (Keyboard.current == null) return;


        // hide the cursor while the player is trying to pick a reward!
        if (Time.timeScale == 0f) return;

        // SHOW CURSOR & FREEZE CAMERA
        if (Keyboard.current.altKey.wasPressedThisFrame)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            // Turn off the camera's ability to read mouse movement
            if (lookAction != null)
            {
                lookAction.action.Disable();
            }
        }

        // HIDE CURSOR & UNFREEZE CAMERA
        else if (Keyboard.current.altKey.wasReleasedThisFrame)
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;

            // Turn the camera back on
            if (lookAction != null)
            {
                lookAction.action.Enable();
            }
        }
    }
}