using UnityEngine;
using UnityEngine.SceneManagement; // Required for loading scenes

public class RandomPortal : MonoBehaviour
{
    [Header("Portal Settings")]
    [Tooltip("Add the exact names of the scenes you want to randomly load.")]
    public string[] possibleLevels;

    [Tooltip("The tag of the object allowed to use the portal.")]
    public string playerTag = "Player";

    private void OnTriggerEnter(Collider other)
    {
        // 1. Check if the object entering the trigger has the correct tag
        if (other.CompareTag(playerTag))
        {
            // 2. Safety check: Ensure the list actually has levels in it
            if (possibleLevels.Length > 0)
            {
                // 3. Pick a random number between 0 and the number of items in the array
                // Note: Random.Range for integers is exclusive at the top end, which perfectly aligns with array indexes.
                int randomIndex = Random.Range(0, possibleLevels.Length);

                // 4. Get the scene name and load it
                string sceneToLoad = possibleLevels[randomIndex];
                Debug.Log("Player entered portal. Loading: " + sceneToLoad);
                SceneManager.LoadScene(sceneToLoad);
            }
            else
            {
                Debug.LogWarning("RandomPortal script is missing target scenes! Please add them in the Inspector.");
            }
        }
    }
}