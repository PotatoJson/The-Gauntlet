using UnityEngine;

public class DestroyAfterTime : MonoBehaviour
{
    [Tooltip("Minimum time in seconds before the object is destroyed")]
    public float minLifetime = 5f;

    [Tooltip("Maximum time in seconds before the object is destroyed")]
    public float maxLifetime = 10f;

    void Start()
    {
        // Pick a random duration between the min and max values
        float randomLifetime = Random.Range(minLifetime, maxLifetime);
        
        // Destroy the game object after the random duration has passed
        Destroy(gameObject, randomLifetime);
    }
}