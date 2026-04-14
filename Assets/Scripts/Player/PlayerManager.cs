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
    public bool RequestBufferClear;
    public float CurrentLungeSpeed;
    
    public void Awake()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public PlayerState GetCurrentState()
    {
        return _currentState;    
    }

    public void SetPlayerState(PlayerState newState)
    {
        _currentState = newState;
    }
    
}
