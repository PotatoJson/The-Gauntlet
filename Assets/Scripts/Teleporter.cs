using UnityEngine;

public class Teleporter : MonoBehaviour
{
    [Header("Teleport Settings")]
    public Transform destinationPoint;

    private void OnTriggerEnter(Collider other)
    {
        // This will print BOTH the name and the tag so you can see exactly what Unity sees
        print("Entered by Name: " + other.name + " | Tag: " + other.tag);

        if (other.CompareTag("Player"))
        {
            print("Tag check passed! Attempting teleport...");

            // Look for a Character Controller on the object that entered
            CharacterController cc = other.GetComponent<CharacterController>();

            if (cc != null)
            {
                // Bypass the Character Controller override
                cc.enabled = false;
                other.transform.position = destinationPoint.position;
                cc.enabled = true;
                print("Teleported Character Controller successfully.");
            }
            else
            {
                // Fallback for standard Rigidbody objects
                other.transform.position = destinationPoint.position;
                print("Teleported standard Rigidbody successfully.");
            }
        }
    }
}