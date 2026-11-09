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
    Healing,
    Trapped
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

    // Boss teleport for testing: to the level's own spot if it has one, otherwise a few metres above the floor of the
    // boss chamber, so the player drops in the way they would arrive (and a boss intro sees them coming).
    public void DebugTeleport()
    {
        Vector3? target = _bossRoomSpawn != null ? _bossRoomSpawn.position : BossChamberDrop();
        if (target == null)
        {
            Debug.LogWarning("[DebugMenu] This level has no boss room.");
            return;
        }
        if (_characterController != null) _characterController.enabled = false;
        transform.position = target.Value;
        if (_characterController != null) _characterController.enabled = true;
    }

    private static Vector3? BossChamberDrop()
    {
        EnemySpawner spawner = FindFirstObjectByType<EnemySpawner>();
        if (spawner == null) return null;

        foreach (ChamberData chamber in spawner.chambers)
        {
            if (chamber.bossSpawns.Count == 0 || chamber.chamberTriggers.Count == 0 || chamber.chamberTriggers[0] == null) continue;

            Bounds floor = chamber.chamberTriggers[0].bounds; // a boss chamber's first trigger is the plate over its floor
            return new Vector3(floor.center.x, floor.max.y + 3f, floor.center.z);
        }
        return null;
    }
    
    // Call this from anywhere to refresh the combat timer
    public void SetInCombat()
    {
        IsInCombat = true;
        _combatTimer = CombatTimeout;
    }

}
