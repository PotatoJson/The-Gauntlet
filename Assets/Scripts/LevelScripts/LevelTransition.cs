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

            // Hell's exit leads back to the menu: that's the end of the game, if this run came through every level.
            SaveManager save = SaveManager.Instance;
            if (nextLevelName == "MainMenu" && save != null
                && save.HasVisited("Dungeon Level") && save.HasVisited("Cave Level") && save.HasVisited("Hell Level"))
            {
                SteamAchievements.Unlock(SteamAchievements.RunComplete);
            }

            // Load the scene specified in the inspector: the next level behind the loading screen, or straight to the menu.
            if (nextLevelName == "MainMenu") SceneManager.LoadScene(nextLevelName);
            else LoadingScreen.LoadLevel(nextLevelName);
        }
    }
}