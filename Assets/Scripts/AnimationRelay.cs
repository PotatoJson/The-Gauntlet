using UnityEngine;

public class AnimationEventRelay : MonoBehaviour
{
    private PlayerCombat _playerCombat;
    private PlayerManager _stateManager;

    private void Awake()
    {
        // When the game starts, look UP the hierarchy to find the General
        _playerCombat = GetComponentInParent<PlayerCombat>();
        _stateManager = GetComponentInParent<PlayerManager>();

        if (_playerCombat == null)
        {
            Debug.LogError("AnimationEventRelay could not find PlayerCombat on the parent!");
        }
    }

    public void ActivateHitbox()
    {
        if (_playerCombat != null) _playerCombat.ArmTargetHitbox();
    }

    public void DeactivateHitbox()
    {
        if (_playerCombat != null) _playerCombat.DisarmTargetHitbox();
    }

    public void OpenComboWindow()
    {
        if (_playerCombat != null) _playerCombat.OpenComboWindow();
    }

    public void EndAttack()
    {
        if (_playerCombat != null) _playerCombat.EndAttack();
    }
    
    public void ResetFromStagger()
    {
        Debug.Log("Resetting from stagger");
        _stateManager.SetPlayerState(PlayerState.Idle);
    }
}