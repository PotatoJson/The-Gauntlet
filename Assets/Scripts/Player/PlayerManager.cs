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
    Airborne,
    Healing
}

public class PlayerManager : MonoBehaviour
{
    [Header("Current State for dubugging")]
    [SerializeField] private PlayerState _currentState = PlayerState.Idle;
    [SerializeField] private Transform _bossRoomSpawn;
    [SerializeField] private CharacterController _characterController;

    [Header("Combat State")]
    public bool IsInCombat;
    public float CombatTimeout = 5f; // How many seconds before dropping out of combat
    private float _combatTimer;

    [Header("Shared Content")]
    public Vector3 MoveDirectionIntent;
    public bool IsLockedOn;
    public bool HasBufferedAttack;
    public bool CanCancelAttack;
    public bool RequestBufferClear;
    public bool CarryMomentum;
    public bool CanRoll;
    public float CurrentLungeSpeed;
    
    public void Awake()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
{
    // Handle the combat timeout logic
    if (IsInCombat)
    {
        _combatTimer -= Time.deltaTime;
        if (_combatTimer <= 0)
        {
            IsInCombat = false;
        }
    }
}

    public PlayerState GetCurrentState()
    {
        return _currentState;    
    }

    public void SetPlayerState(PlayerState newState)
    {
        _currentState = newState;
    }

    //boss teleport for testing
    public void DebugTeleport()
    {
        if(_bossRoomSpawn == null) return;
        if (_characterController != null) _characterController.enabled = false;
        transform.position = _bossRoomSpawn.position;
        if (_characterController != null) _characterController.enabled = true;
    }
    
    // Call this from anywhere to refresh the combat timer
    public void SetInCombat()
    {
        IsInCombat = true;
        _combatTimer = CombatTimeout;
    }

}
