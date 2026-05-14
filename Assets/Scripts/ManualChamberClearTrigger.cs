using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ManualChamberClearTrigger : MonoBehaviour
{
    [Header("Configuration")]
    [Tooltip("Drag your EnemySpawner GameObject in here.")]
    public EnemySpawner enemySpawner;
    
    [Tooltip("The index of the chamber this trigger should clear (e.g., 0 for Chamber 1, 1 for Chamber 2).")]
    public int chamberIndexToClear;

    [Tooltip("Should this trigger deactivate itself after being used once?")]
    public bool disableAfterUse = true;

    private void Awake()
    {
        // Force the collider to act as a trigger so it doesn't block the player physically
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        // Only trigger if the Player walks into it
        if (other.CompareTag("Player"))
        {
            if (enemySpawner != null)
            {
                // Tell the spawner to clear the room, open the doors, and fire the metrics!
                enemySpawner.ForceClearChamber(chamberIndexToClear);
                
                Debug.Log($"[Manual Trigger] Player manually cleared Chamber Index: {chamberIndexToClear}");
                
                if (disableAfterUse)
                {
                    gameObject.SetActive(false); // Turn off the trigger so it doesn't fire twice
                }
            }
            else
            {
                Debug.LogError("ManualChamberClearTrigger is missing a reference to the EnemySpawner!");
            }
        }
    }
}