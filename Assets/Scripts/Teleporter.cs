using UnityEngine;

public class Teleporter : MonoBehaviour
{
    [Header("Teleport Settings")]
    [Tooltip("Drag your Destination Empty GameObject here in the Inspector")]
    public Transform destinationPoint;

    // This function runs automatically when another collider enters this object's trigger
    private void OnTriggerEnter(Collider other)
    {
        print ("Trigger entered by: " + other.name); // Debug line to check which object is entering the trigger
        // 1. Check if the object entering the trigger is the Player
        if (other.CompareTag("Player"))
        {
            // 2. Instantly change the player's position to the destination's position
            other.transform.position = destinationPoint.position;

            // Optional: Match the player's rotation to the destination's rotation
            // other.transform.rotation = destinationPoint.rotation;
        }
    }
}