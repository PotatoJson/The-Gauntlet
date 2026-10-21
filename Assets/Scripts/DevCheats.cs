using UnityEngine;

/// <summary>
/// Per-scene debug data (next level name, teleport spots) for the debug menu (press 0). It no longer reads
/// any keys itself.
/// </summary>
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

    public void TeleportPlayer()
    {
        if (playerTransform == null || teleportLocations == null || teleportLocations.Length == 0)
        {
            Debug.LogWarning("[DevCheats] Teleport locations or player transform missing!");
            return;
        }

        // A CharacterController overrides direct position changes, so switch it off for the move.
        CharacterController cc = playerTransform.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        playerTransform.position = teleportLocations[_teleportIndex].position;

        if (cc != null) cc.enabled = true;

        _teleportIndex = (_teleportIndex + 1) % teleportLocations.Length;
    }
}
