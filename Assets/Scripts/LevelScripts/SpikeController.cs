using UnityEngine;

public class SpikeController : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    public void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            Debug.Log("Player hit spikes!");
            other.GetComponent<PlayerHealth>().PlayerRespawnSpikes();
        }
    }
}
