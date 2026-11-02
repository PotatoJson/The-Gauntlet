using UnityEngine;
using UnityEngine.SceneManagement;

public class DeathZone : MonoBehaviour
{
    [SerializeField, Tooltip("Kill the player and play the death screen instead of quietly reloading the level")]
    private bool killsPlayer;

    private void OnTriggerEnter(Collider other)
    {
        // Checks if the object entering the zone has the "Player" tag
        if (other.CompareTag("Player"))
        {
            if (killsPlayer)
            {
                other.GetComponentInParent<PlayerHealth>().Kill(gameObject);
                return;
            }

            // Reloads the current active scene
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
