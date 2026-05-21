using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem; // Required for the New Input System

public class SceneResetter : MonoBehaviour
{
    void Update()
    {
        // Check if a keyboard is connected, then check if Tilde was pressed
        if (Keyboard.current != null && Keyboard.current.backquoteKey.wasPressedThisFrame)
        {
            ResetCurrentScene();
        }
    }

    private void ResetCurrentScene()
    {
        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.buildIndex);
        Debug.Log($"Scene '{currentScene.name}' has been reset.");
    }
}