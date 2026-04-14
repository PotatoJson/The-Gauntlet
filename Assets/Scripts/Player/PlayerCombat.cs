using UnityEngine;
using UnityEngine.InputSystem;

public enum CombatInput
{
    None,
    Light,
    Heavy
}

public class PlayerCombat : MonoBehaviour
{
    #region Variables
    [Header("Universal Combo Tree")]
    public AttackNode StartingLightAttack;
    public AttackNode StartingHeavyAttack;

    [Header("References")]
    public GauntletData LeftGauntletData;
    public GauntletData RightGauntletData;
    [Space]
    private PlayerManager _stateManager;
    private Animator _animator;
    private PlayerControls _input;
    
    [Header("Physical Hitboxes")]
    [SerializeField] private HitboxController _leftHitbox;
    [SerializeField] private HitboxController _rightHitbox;
    [Space]
    private HitboxController _activeHitbox;

    [Header("Active Weapon (Wrapper)")]
    private RunTimeGauntlet _leftGauntlet;
    private RunTimeGauntlet _rightGauntlet;

    [Header("Combat Tracking")]
    private AttackNode _currentAttackNode;
    private bool _canCombo = false;
    private bool _comboQueued = false;

    [Header("Player Stats")]
    public int CurrentStamina = 100;
    public int MaxStamina = 100;
    private bool _isRotationLocked = false;
    
    [Header("Input Buffer Things")]
    public float BufferDuration;
    private CombatInput _currentBuffer = CombatInput.None;
    private float BufferTimer = 0f;    
    #endregion

    #region Setup
    void Awake()
    {
        _animator = GetComponentInChildren<Animator>();
        _stateManager = GetComponent<PlayerManager>();

        _input = new PlayerControls();

        _input.Player.LightAttack.started += ctx => OnLightAttackInput();
        _input.Player.HeavyAttack.started += ctx => OnHeavyAttackInput();
    }

    private void OnEnable() => _input.Enable();
    
    private void OnDisable() => _input.Disable();
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if(LeftGauntletData != null) _leftGauntlet = new RunTimeGauntlet(LeftGauntletData);
        if(RightGauntletData != null) _rightGauntlet = new RunTimeGauntlet(RightGauntletData);
        CurrentStamina = MaxStamina;
    }

    // Update is called once per frame
    void Update()
    {

        if(_stateManager.RequestBufferClear)
        {
            ConsumeBuffer();
            _stateManager.RequestBufferClear = false;
        }
        if(_stateManager.GetCurrentState() == PlayerState.Staggered)
        {
            ConsumeBuffer();
        }
        HandleInputBuffer();
        _stateManager.HasBufferedAttack = (BufferTimer > 0);
        ProcessAttackRotation();
        ProcessCombatLogic();
    }
    #endregion
    #region InputBuffer
    private void OnLightAttackInput()
    {
        _currentBuffer = CombatInput.Light;
        BufferTimer = BufferDuration;
    }

    private void OnHeavyAttackInput()
    {
        _currentBuffer = CombatInput.Heavy;
        BufferTimer = BufferDuration;
    }

    private void HandleInputBuffer()
    {
        if(BufferTimer > 0)
        {
            BufferTimer -= Time.deltaTime;
            if(BufferTimer <= 0) _currentBuffer = CombatInput.None;
        }
    }

    private void ConsumeBuffer()
    {
        _currentBuffer = CombatInput.None;
        BufferTimer = 0;
    }
    #endregion

    private void ProcessCombatLogic()
    {
        if(_currentBuffer == CombatInput.None) return;
        PlayerState currentState = _stateManager.GetCurrentState();

        if(currentState == PlayerState.Dodging || currentState == PlayerState.Airborne || currentState == PlayerState.Staggered) return;

        if(currentState == PlayerState.Idle || currentState == PlayerState.Walking || currentState == PlayerState.Running)
        {
            AttackNode nodeToPlay = (_currentBuffer == CombatInput.Light) 
                ? StartingLightAttack 
                : StartingHeavyAttack;
            AttemptAttack(nodeToPlay);
        }
        else if(currentState == PlayerState.Attacking && _canCombo)
        {
            AttackNode nextNode = (_currentBuffer == CombatInput.Light) 
                ? _currentAttackNode.NextLightAttack 
                : _currentAttackNode.NextHeavyAttack;

            Debug.Log($"Attempting to chain from {_currentAttackNode.name} to {(nextNode != null ? nextNode.name : "NULL")}");
            if(nextNode != null) AttemptAttack(nextNode);
        }
    }

    private void AttemptAttack(AttackNode node)
    {
        if(node == null) return;

        if(CurrentStamina < node.StaminaCost)
        {
            ConsumeBuffer();
            return;
        }

        _isRotationLocked = false;

        CurrentStamina -= node.StaminaCost;
        _currentAttackNode = node;
        _canCombo = false;
        _comboQueued = true;
        
        _stateManager.CurrentLungeSpeed = node.LungeForce;
        _stateManager.CanCancelAttack = false;
        _stateManager.SetPlayerState(PlayerState.Attacking);
        _animator.SetTrigger(node.AnimationTrigger);
        ConsumeBuffer();
    }

    private void ProcessAttackRotation()
    {
        if(_stateManager.IsLockedOn || _isRotationLocked || _stateManager.GetCurrentState() != PlayerState.Attacking) return;

        Vector3 snapDir = _stateManager.MoveDirectionIntent;
        if(snapDir != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(snapDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 30f * Time.deltaTime);
        }
    }

    public void ArmTargetHitbox()
    {
        _isRotationLocked = true;
        RunTimeGauntlet activeWeapon = _leftGauntlet;
        _activeHitbox = _leftHitbox;

        if(_currentAttackNode.StrikingHand == StrikeHand.Right)
        {
            activeWeapon = _rightGauntlet;
            _activeHitbox = _rightHitbox;
        }
        else if(_currentAttackNode.StrikingHand == StrikeHand.Both)
        {
            //future dual hand attack
        }
        int currentDamage = activeWeapon.GetCurrentDamage();
        int currentPoise = activeWeapon.GetCurrentPoise();

        if(_activeHitbox != null)
        {
            _activeHitbox.EnableCollider(currentDamage, currentPoise);
        }
    }

    public void DisarmTargetHitbox()
    {
        if(_activeHitbox != null)
        {
            _activeHitbox.DisableCollider();
            _activeHitbox = null;
        }
    }

    public void OpenComboWindow()
    {
        _canCombo = true;
        _comboQueued = false;

        _stateManager.CanCancelAttack = true;
    }

    public void EndAttack()
    {
        if(_comboQueued) return;
        _currentAttackNode = null;
        _canCombo = false;

        _stateManager.CanCancelAttack = false;
        _stateManager.SetPlayerState(PlayerState.Idle);
    }
}
