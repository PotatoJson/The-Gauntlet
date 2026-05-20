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

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    // ========================================================
    // PUBLIC METHODS (Select these in your Animation Events!)
    // ========================================================

    public void PlayFootstep() => PlayClip(footstepSound);
    public void PlayRoll() => PlayClip(rollSound);
    public void PlayHeavyWhoosh() => PlayClip(heavyWhooshSound);
    public void PlayWallImpact() => PlayClip(wallImpactSound);
    public void PlayPotion() => PlayClip(potionSound);

    // These two pick a random sound from your arrays to keep combat dynamic
    public void PlayLightWhoosh() => PlayRandomClip(lightWhooshSounds);
    public void PlayEnemyImpact() => PlayRandomClip(enemyImpactSounds);


    // ========================================================
    // INTERNAL AUDIO LOGIC
    // ========================================================

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