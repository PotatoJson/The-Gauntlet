using UnityEngine;
using UnityEngine.SceneManagement;

public class DeathZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // Checks if the object entering the zone has the "Player" tag
        if (other.CompareTag("Player"))
        {
            // Reloads the current active scene
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}    