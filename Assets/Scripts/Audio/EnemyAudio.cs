using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAudio : MonoBehaviour
{
    [Header("FMOD Events")]
    // Paste your FMOD enemy footstep path here in the inspector
    public string footstepEventPath = "event:/Enemies/Grunt_Footstep"; 

    [Header("Distance Tracking")]
    [Tooltip("How far the enemy must move in world space to trigger a step.")]
    public float strideLength = 1.8f; 
    
    private Vector3 _lastStepPosition;
    private NavMeshAgent _navAgent;
    private BaseEnemy _enemyScript;

    private void Awake()
    {
        _navAgent = GetComponent<NavMeshAgent>();
        _enemyScript = GetComponent<BaseEnemy>();
    }

    private void Start()
    {
        // Anchor the measuring tape at the enemy's starting position
        _lastStepPosition = transform.position;
    }

    private void Update()
    {
        // Don't play footsteps if the enemy is dead, stunned, or the agent is disabled
        if (_navAgent == null || !_navAgent.enabled || _enemyScript.IsDead()) return;

        // How far have we moved since the last footstep?
        float distanceMoved = Vector3.Distance(transform.position, _lastStepPosition);

        // If we've moved further than our stride length, play a step!
        if (distanceMoved >= strideLength)
        {
            PlayFootstep();
            
            // Reset the measuring tape to our current position
            _lastStepPosition = transform.position; 
        }
    }

    private void PlayFootstep()
    {
        // Play the 3D sound exactly at the enemy's feet
        FMODUnity.RuntimeManager.PlayOneShot(footstepEventPath, transform.position);
    }
}