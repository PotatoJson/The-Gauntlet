using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerPersistence : MonoBehaviour
{//this is for carrying over the player data to the next scene
    public static PlayerPersistence Instance;

    private void Awake()
    {
        // 1. The Highlander Rule: There can be only one!
        if (Instance == null)
        {
            Instance = this;
            
            // Tell Unity to completely ignore this object when unloading the scene
            DontDestroyOnLoad(gameObject);
            
            // Listen for when a new level finishes loading
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            // If the new level has a "dummy" player prefab sitting in it, destroy the dummy 
            // so we keep the original player with all their gems!
            Destroy(gameObject);
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 2. Find the spawn point in the new level
        GameObject spawnPoint = GameObject.Find("PlayerSpawn"); 
        
        if (spawnPoint != null)
        {
            // 3. Disable the CharacterController so it releases its physics grip
            CharacterController cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            // 4. Teleport the player
            transform.position = spawnPoint.transform.position;
            transform.rotation = spawnPoint.transform.rotation;

            // 5. Re-enable the physics
            if (cc != null) cc.enabled = true;
            
            // 6. Update your respawn system so spikes don't send you back to Level 1!
            PlayerHealth health = GetComponent<PlayerHealth>();
            if (health != null) health.LastCheckPoint = spawnPoint;
        }
        else
        {
            Debug.LogWarning("No GameObject named 'PlayerSpawn' found in the new level!");
        }
    }

    private void OnDestroy()
    {
        // Clean up the event listener to prevent memory leaks if the game is actually closed
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }
}