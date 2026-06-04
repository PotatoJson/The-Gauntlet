using UnityEngine;
using UnityEngine.InputSystem;

public class DebugPotionAdder : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Drag the Player GameObject here so the debugger can access its health script.")]
    public PlayerHealth playerHealth;

    private void Update()
    {
        // Safety check to ensure a keyboard is connected
        if (Keyboard.current == null) return;

        // Check if the P key was pressed
        if (Keyboard.current.pKey.wasPressedThisFrame)
        {
            if (playerHealth != null)
            {
                //playerHealth.AddPotion();
                TutorialNotificationManager.Instance.ShowTutorial(true, "Skill Gem");

            }
            else
            {
                Debug.LogWarning("PlayerHealth is not assigned in the DebugPotionAdder script!");
            }
        }
    }
}