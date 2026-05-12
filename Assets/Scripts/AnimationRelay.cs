using UnityEngine;

public class AnimationEventRelay : MonoBehaviour
{
    /* --- used for animations events --- */
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

    //used for when you shouldn't be able to move during anim but can still roll
    public void CanRoll()
    {
        _stateManager.CanRoll = true;
    }
    
    public void CantRoll()
    {
        _stateManager.CanRoll = false;
    }

    public void CheckHeavyCharge()
    {
        _playerCombat.AttemptHeavyChargePause();
    }

    public void HeavyWindUp()
    {
        _playerCombat.HeavyAttackWindUp();
    }

    public void HeavySwing()
    {
        _playerCombat.HeavyAttackSwing();
    }

    public void TriggerAnimationLunge()
    {
        _playerCombat.ApplyLungeForce();
    }
}