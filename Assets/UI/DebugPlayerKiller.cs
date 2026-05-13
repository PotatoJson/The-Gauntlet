using UnityEngine;
using UnityEngine.InputSystem;

public class DebugPlayerKiller : MonoBehaviour
{
    [Header("References")]
    public PlayerHealth playerHealth;

    private void Update()
    {
        if (Keyboard.current == null) return;

        // Press the 'K' key to instantly kill the player
        if (Keyboard.current.kKey.wasPressedThisFrame)
        {
            if (playerHealth != null && !playerHealth.IsDead)
            {
                Debug.Log("Debug: Instant Kill Triggered!");
                playerHealth.TakeDamage(9999f);
            }
            else if (playerHealth == null)
            {
                Debug.LogWarning("PlayerHealth is not assigned to the DebugPlayerKiller script!");
            }
        }
    }
}