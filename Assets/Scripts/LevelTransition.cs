using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelTransition : MonoBehaviour
{
    [Tooltip("The exact name of the scene you want to load.")]
    [SerializeField] private string nextLevelName;

    private void OnTriggerEnter(Collider other)
    {
        // Checks if the object entering the trigger has the "Player" tag
        if (other.CompareTag("Player"))
        {
            Debug.Log("Player reached the exit! Loading next level...");

            if (PersistentEquipment.Instance != null && InventoryManager.Instance != null)
            {
                PersistentEquipment.Instance.SaveEquipment(InventoryManager.Instance);
            }

            // Load the scene specified in the inspector
            SceneManager.LoadScene(nextLevelName);
        }
    }
}