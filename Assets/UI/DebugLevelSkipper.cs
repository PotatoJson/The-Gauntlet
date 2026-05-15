using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem; // Needed for the new Input System!

public class DebugLevelSkipper : MonoBehaviour
{
    [Header("Debug Settings")]
    [Tooltip("The exact name of the scene you want to skip to")]
    public string targetSceneName = "Cave Level";

    private void Update()
    {
        // Listen for the 'M' key using the new Input System
        if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame)
        {
            Debug.Log("Debug Skip Activated! Packing bags and moving to " + targetSceneName);

            // 1. Pack the backpack so your gems actually carry over!
            if (PersistentEquipment.Instance != null && InventoryManager.Instance != null)
            {
                PersistentEquipment.Instance.SaveEquipment(InventoryManager.Instance);
            }

            // 2. Unpause time just in case you hit 'M' while a menu was open
            Time.timeScale = 1f;

            // 3. Load the scene
            SceneManager.LoadScene(targetSceneName);
        }
    }
}