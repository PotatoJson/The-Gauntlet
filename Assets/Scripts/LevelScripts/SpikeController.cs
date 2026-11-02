using UnityEngine;

public class SpikeController : MonoBehaviour
{
    // ponytail: found by name so the 300 spike instances need no scene edit; renaming the root makes them respawn instead, use a serialized flag if that ever bites
    private const string LethalRoot = "Boss Chamber";

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    public void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            Debug.Log("Player hit spikes!");
            FMODUnity.RuntimeManager.PlayOneShot("event:/Environment/Spike_Trap", transform.position);

            PlayerHealth health = other.GetComponent<PlayerHealth>();
            if (transform.root.name == LethalRoot) health.Kill(gameObject);
            else health.PlayerRespawnSpikes();
        }
    }
}
