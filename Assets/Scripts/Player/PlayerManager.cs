using UnityEngine;

public enum PlayerState
{
    Idle,
    Walking,
    Running,
    Attacking,
    Parrying,
    Dodging,
    Staggered,
    Airborne
}

public class PlayerManager : MonoBehaviour
{
    [Header("Current State for dubugging")]
    [SerializeField] private PlayerState _currentState = PlayerState.Idle;

    [Header("Shared Content")]
    public Vector3 MoveDirectionIntent;
    public bool IsLockedOn;
    public bool HasBufferedAttack;
    public bool CanCancelAttack;
    public float CurrentLungeSpeed;

    public PlayerState GetCurrentState()
    {
        return _currentState;    
    }

    public void SetPlayerState(PlayerState newState)
    {
        _currentState = newState;
    }
    
}
