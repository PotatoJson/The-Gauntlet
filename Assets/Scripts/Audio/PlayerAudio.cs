using UnityEngine;

public class PlayerAudio : MonoBehaviour
{
    private PlayerManager _stateManager;

    private void Awake()
    {
        _stateManager = GetComponentInParent<PlayerManager>();

        if (_stateManager == null)
        {
            Debug.LogError("AUDIO ERROR: cannot find the PlayerManager on this object or any parent!");
        }
    }

    public void PlayJogFootstep()
    {
        if (_stateManager == null) return;

        // Only allow this if we are strictly walking/jogging
        if (_stateManager.GetCurrentState() == PlayerState.Walking)
        {
            FMODUnity.RuntimeManager.PlayOneShot("event:/Player/Player_Footstep", transform.position);
        }
    }

    public void PlaySprintFootstep()
    {
        if (_stateManager == null) return;

        // Only allow this if we are strictly sprinting
        if (_stateManager.GetCurrentState() == PlayerState.Running)
        {
            FMODUnity.RuntimeManager.PlayOneShot("event:/Player/Player_Footstep", transform.position);
        }
    }

    public void PlayRoll()
    {
        FMODUnity.RuntimeManager.PlayOneShotAttached("event:/Player/Player_Roll", gameObject);
    }

    public void PlayPotionSound()
    {
        if (_stateManager == null) return;

        // Only allow the sound if the player is actually in the Healing state
        if (_stateManager.GetCurrentState() == PlayerState.Healing)
        {
            FMODUnity.RuntimeManager.PlayOneShotAttached("event:/Player/Player_Potion", gameObject);
        }
    }

    public void PlayLightAttack()
    {
        if (_stateManager == null) return;

        
        if (_stateManager.GetCurrentState() == PlayerState.Attacking)
        {
            FMODUnity.RuntimeManager.PlayOneShotAttached("event:/Player/Player_Light_Attack", gameObject);
        }
    }

    public void PlayHeavyAttack()
    {
        if (_stateManager == null) return;

        if (_stateManager.GetCurrentState() == PlayerState.Attacking)
        {
            FMODUnity.RuntimeManager.PlayOneShotAttached("event:/Player/Player_Heavy_Attack", gameObject);
        }
    }
}