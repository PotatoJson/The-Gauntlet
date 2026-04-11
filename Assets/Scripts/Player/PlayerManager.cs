using UnityEngine;

public enum PlayerState
{
    Idle,
    Moving,
    Attacking,
    Parrying,
    Dodging,
    Staggered
}

public class PlayerManager : MonoBehaviour
{
    [Header("Current State for dubugging")]
    [SerializeField] private PlayerState _currentState = PlayerState.Idle;

    public PlayerState GetCurrentState()
    {
        return _currentState;    
    }

    public void SetPlayerState(PlayerState newState)
    {
        _currentState = newState;
    }
    
}
