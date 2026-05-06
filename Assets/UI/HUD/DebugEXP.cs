using UnityEngine;
using UnityEngine.InputSystem; // Using the New Input System!

public class DebugEXP : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Drag your UI ProgressBarCircle 1 object here")]
    [SerializeField] private ProgressBarCircle expBar;

    [Header("Debug Settings")]
    [Tooltip("How much EXP to add/remove per button press")]
    [SerializeField] private float expChunk = 25f;

    private void Update()
    {
        if (Keyboard.current == null || expBar == null) return;

        // ADD EXP: Press '=' (which is the '+' key) OR Numpad '+'
        if (Keyboard.current.equalsKey.wasPressedThisFrame || Keyboard.current.numpadPlusKey.wasPressedThisFrame)
        {
            expBar.AddExperience(expChunk);
        }

        // REMOVE EXP: Press '-' OR Numpad '-'
        if (Keyboard.current.minusKey.wasPressedThisFrame || Keyboard.current.numpadMinusKey.wasPressedThisFrame)
        {
            expBar.RemoveExperience(expChunk);
        }
    }
}