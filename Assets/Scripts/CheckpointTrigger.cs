using UnityEngine;

// Simple trigger component to mark checkpoints and auto-save
[RequireComponent(typeof(Collider))]
public class CheckpointTrigger : MonoBehaviour
{
    [Tooltip("Optional reference to the level's EnemySpawner to capture chamber state during save.")]
    public EnemySpawner spawner;

    private void Reset()
    {
        Collider c = GetComponent<Collider>();
        if (c != null) c.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        var ph = other.GetComponent<PlayerHealth>();
        if (ph != null && ph.LastCheckPoint != this.gameObject)
        {
            ph.LastCheckPoint = this.gameObject;
        }

        // Run auto-save using SaveSystem
        Save.SaveSystem.AutoSave(other.gameObject, spawner);
#if UNITY_EDITOR
        Debug.Log("CheckpointTrigger: Autosaved at checkpoint " + name);
#endif
    }
}
