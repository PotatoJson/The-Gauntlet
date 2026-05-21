using UnityEngine;

public class AudioStateBehaviour : StateMachineBehaviour
{
    [Header("Audio Settings")]
    [SerializeField] private AudioClip stateEnterSound;
    [SerializeField] private float volume = 1f;
    [SerializeField, Range(0.5f, 1.5f)] private float pitchRandomness = 0.1f;

    // This runs the exact frame the Animator enters this specific state
    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (stateEnterSound == null) return;

        // Grab the AudioSource attached to the character
        AudioSource source = animator.GetComponent<AudioSource>();
        
        if (source != null)
        {
            // Add a little pitch variation
            source.pitch = 1f + Random.Range(-pitchRandomness, pitchRandomness);
            source.PlayOneShot(stateEnterSound, volume);
        }
        else
        {
            Debug.LogWarning($"Missing AudioSource on {animator.gameObject.name}");
        }
    }
}