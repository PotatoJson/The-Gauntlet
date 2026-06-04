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
        if (parentTrap == null) return;

        if (other.CompareTag("Player") || other.GetComponentInParent<BaseEnemy>() != null || other.CompareTag("Enemy"))
        {
            parentTrap.ActivateTrap();
        }
    }
}