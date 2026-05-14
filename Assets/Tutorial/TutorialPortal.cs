using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement; // Needed to load scenes!

public class TutorialPortal : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Type the exact name of your Main Menu scene here")]
    public string mainMenuSceneName = "MainMenu";

    private void OnTriggerEnter(Collider other)
    {
        // Check if the object walking into the portal is the Player
        if (other.GetComponent<CharacterController>() != null)
        {
            Debug.Log("Player entered the portal! Cleaning up and returning to Main Menu...");

            // 1. Unpause the game
            Time.timeScale = 1f;

            // 2. Show the cursor so the Main Menu buttons work!
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            // 3. Kill ALL active DOTween animations so they don't glitch the next scene
            DOTween.KillAll();

            // 4. Load the Main Menu scene
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }
}