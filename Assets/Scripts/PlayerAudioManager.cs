using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class PlayerAudioManager : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private AudioSource audioSource;

    [Header("Movement SFX")]
    [SerializeField] private AudioClip footstepSound;
    [SerializeField] private AudioClip rollSound;

    [Header("Weapon SFX")]
    [Tooltip("Add Light Whoosh (1) and (2) here")]
    [SerializeField] private AudioClip[] lightWhooshSounds; 
    [SerializeField] private AudioClip heavyWhooshSound;
    
    [Header("Impact SFX")]
    [Tooltip("Add Enemy Impact (1) and (2) here")]
    [SerializeField] private AudioClip[] enemyImpactSounds; 
    [SerializeField] private AudioClip wallImpactSound;

    [Header("Item SFX")]
    [SerializeField] private AudioClip potionSound;

    [Header("Global Audio Settings")]
    [SerializeField, Range(0.8f, 1.2f)] private float pitchMin = 0.9f;
    [SerializeField, Range(0.8f, 1.2f)] private float pitchMax = 1.1f;
    [SerializeField, Range(0f, 1f)] private float volume = 0.8f;

    private PlayerManager _stateManager;
    private Animator _animator;

    [Header("Footstep Fixes")]
    [Tooltip("Prevents footsteps from rapid-firing during animation glitches")]
    [SerializeField] private float footstepCooldown = 0.2f;
    private float _lastFootstepTime;

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        _stateManager = GetComponentInParent<PlayerManager>(); 
        _animator = GetComponentInChildren<Animator>();
    }

    // public methods

    public void PlayFootstep() 
    {
        // check if the animator is currently blending 
        // between a punch and walking, block the footstep!
        if (_animator != null && _animator.IsInTransition(0)) return;

        // Only allow footsteps if actively moving
        if (_stateManager != null)
        {
            PlayerState currentState = _stateManager.GetCurrentState();
            if (currentState != PlayerState.Walking && currentState != PlayerState.Running)
            {
                return; // Block the sound if Idle, Attacking, Airborne, etc.
            }
        }

        //Prevents the Frame 0 from instantly firing
        if (Time.time - _lastFootstepTime < footstepCooldown) return;

        _lastFootstepTime = Time.time;
        PlayClip(footstepSound);
    }
    public void PlayRoll() => PlayClip(rollSound);
    public void PlayHeavyWhoosh() => PlayClip(heavyWhooshSound);
    public void PlayWallImpact() => PlayClip(wallImpactSound);
    public void PlayPotion() => PlayClip(potionSound);

    // These two pick a random sound from your arrays to keep combat dynamic
    public void PlayLightWhoosh() => PlayRandomClip(lightWhooshSounds);
    public void PlayEnemyImpact(Vector3 impactPosition) 
    {
        // Array configuration test
        if (enemyImpactSounds == null || enemyImpactSounds.Length == 0) 
        {
            Debug.LogError("<color=red><b>[MANAGER ERROR]</b> The EnemyImpactSounds array is EMPTY in the Inspector window!</color>");
            return;
        }

        AudioClip randomClip = enemyImpactSounds[Random.Range(0, enemyImpactSounds.Length)];
        
        if (randomClip == null)
        {
            Debug.LogError("<color=red><b>[MANAGER ERROR]</b> A slot inside your EnemyImpactSounds array is empty/null!</color>");
            return;
        }

        // Clear visual confirmation log
        Debug.Log($"<color=cyan><b>[AUDIO PLAYING]</b> Successfully executing impact sound: {randomClip.name} at coordinates: {impactPosition}</color>");
        
        // FALLBACK TEST: Play directly on the player audio source (2D) to bypass any camera distance attenuation issues
        PlayClip(randomClip);
    }


    // INTERNAL AUDIO LOGIC

    private void PlayClip(AudioClip clip)
    {
        if (clip == null) return;
        
        // Slightly randomize pitch for a more natural feel
        audioSource.pitch = Random.Range(pitchMin, pitchMax);
        audioSource.PlayOneShot(clip, volume);
    }

    private void PlayRandomClip(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0) return;

        // Pick a random clip from the array, then play it
        AudioClip randomClip = clips[Random.Range(0, clips.Length)];
        PlayClip(randomClip);
    }
}