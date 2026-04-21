using UnityEngine;

public class TrapDetection : MonoBehaviour
{
    private TrapEnemy parentTrap;

    private void Awake()
    {
        // Automatically find the TrapEnemy script on the parent object
        parentTrap = GetComponentInParent<TrapEnemy>();

        if (parentTrap == null)
        {
            Debug.LogError("TrapDetection must be a child of an object with the TrapEnemy script!");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // If the player enters the detection area, tell the parent to animate
        if (other.CompareTag("Player"))
        {
            if (parentTrap != null)
            {
                parentTrap.ActivateTrap();
            }
        }
    }
}