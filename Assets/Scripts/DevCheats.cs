using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;


public class DevCheats : MonoBehaviour
{
    [Header("Level Skipping")]
    [Tooltip("The exact name of the next scene to load (e.g., Level_2)")]
    public string nextLevelName;

    [Header("Teleportation")]
    [Tooltip("Drag your Player's main object here.")]
    public Transform playerTransform;
    [Tooltip("Drag empty GameObjects here to act as teleport destinations.")]
    public Transform[] teleportLocations;
    
    private int _teleportIndex = 0;

    // Update is called once per frame
    void Update()
    {
        if (Keyboard.current == null) return;

        // --- JUMP TO NEXT LEVEL (Press N) ---
        if (Keyboard.current.nKey.wasPressedThisFrame)
        {
            JumpToNextLevel();
        }

        // --- TELEPORT CHEAT (Press T) ---
        if (Keyboard.current.tKey.wasPressedThisFrame)
        {
            TeleportPlayer();
        }
    }

    private void JumpToNextLevel()
    {
        if (string.IsNullOrEmpty(nextLevelName))
        {
            Debug.LogWarning("<color=red>[DevCheats]</color> No next level name provided in the Inspector!");
            return;
        }

        Debug.Log($"<color=cyan>[DevCheats]</color> Forcing transition to {nextLevelName}...");

        // Save the player's current gauntlets and gems before swapping scenes!
        if (PersistentEquipment.Instance != null && InventoryManager.Instance != null)
        {
            PersistentEquipment.Instance.SaveEquipment(InventoryManager.Instance);
        }
        else
        {
            Debug.LogWarning("<color=orange>[DevCheats]</color> Could not find PersistentEquipment or InventoryManager to save gear!");
        }

        SceneManager.LoadScene(nextLevelName);
    }

    private void TeleportPlayer()
    {
        if (playerTransform != null && teleportLocations != null && teleportLocations.Length > 0)
        {
            // Note: If your player uses a CharacterController, Unity hates it when you 
            // change their position directly via code. We turn it off for a split second to move them safely!
            CharacterController cc = playerTransform.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            playerTransform.position = teleportLocations[_teleportIndex].position;
            Debug.Log($"<color=cyan>[DevCheats]</color> Teleported to: {teleportLocations[_teleportIndex].name}");

            if (cc != null) cc.enabled = true;

            // Cycle to the next location in the list for the next time you press T
            _teleportIndex++;
            if (_teleportIndex >= teleportLocations.Length) _teleportIndex = 0;
        }
        else
        {
            Debug.LogWarning("<color=red>[DevCheats]</color> Teleport locations or player transform missing!");
        }
    }
}
