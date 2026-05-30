using UnityEngine;

public class ReverseVFX : MonoBehaviour
{
    public float duration = 2f;
    private ParticleSystem[] systems;
    private float timer;
    private bool initialized = false;

    void Start()
    {
        systems = GetComponentsInChildren<ParticleSystem>();
        timer = duration;
        
        // Disable regular playback
        foreach (var ps in systems)
        {
            var main = ps.main;
            main.playOnAwake = false;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        initialized = true;
    }

    void Update()
    {
        if (!initialized) return;

        timer -= Time.deltaTime;
        
        if (timer <= 0)
        {
            // Optional: You might want to stay at 0 for a frame or just destroy
            Destroy(gameObject);
            return;
        }

        foreach (var ps in systems)
        {
            // Simulate(time, withChildren, restart, fixedTimeStep)
            // We want to show the state of the particle system at 'timer'
            ps.Simulate(timer, true, true, true);
        }
    }
}
