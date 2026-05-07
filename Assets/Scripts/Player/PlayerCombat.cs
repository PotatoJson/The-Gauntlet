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
    public AttackNode JumpAttack;
    public AttackNode RunningLightAttack;
    public AttackNode RunningHeavyAttack;

    [Header("External Links")]
    [SerializeField] private PlayerHealth _healthScript;

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
    private bool _isHoldingHeavy = false;
    private bool _isCharging = false;
    private bool _isRotationLocked = true;

    [Header("ChargeSettings")]
    [SerializeField] private float _pullBackSpeed;
    [SerializeField] private float _normHeavyWindUp;
    public float MaxChargeDuration;
    private float _chargeTimer = 0f;

    [Header("Stamina Settings")]
    [SerializeField] private StaminaBar _staminaBar;
    public float RegenRate = 60f;
    public float RegenDelay = 3f;
    private float _staminaRegenTimer = 0f;
    private float _internalStamina;
    private int CurrentStamina;
    public int MaxStamina = 100;
    
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
        _input.Player.HeavyAttack.started += ctx => 
        {
            _isHoldingHeavy = true;
            OnHeavyAttackInput();
        };
        _input.Player.HeavyAttack.canceled += ctx => OnHeavyAttackReleased();
        _input.Player.Heal.started += ctx => UsePotion();
        _input.Player.DebugTeleport.started += ctx => _stateManager.DebugTeleport();
    }

    private void OnEnable() => _input.Enable();
    
    private void OnDisable() => _input.Disable();
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if(LeftGauntletData != null) _leftGauntlet = new RunTimeGauntlet(LeftGauntletData, EquipSlot.Secondary);
        if(RightGauntletData != null) _rightGauntlet = new RunTimeGauntlet(RightGauntletData, EquipSlot.Primary);
        CurrentStamina = MaxStamina;
        _internalStamina = MaxStamina;
        UpdateStaminaUI();
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

        HandleHeavyChargeTimer();
        HandleStaminaRegen();
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

    private void OnHeavyAttackReleased()
    {
        _isHoldingHeavy = false;
        if (_isCharging)
        {
            HeavyAttackSwing();
        }
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

    //stamina stuff
    private void HandleStaminaRegen()
    {
        if(_internalStamina < MaxStamina)
        {
            if (_staminaRegenTimer > 0)
            {
                _staminaRegenTimer -= Time.deltaTime;
            }
            else
            {
                // Rapidly refill once timer hits zero
                _internalStamina += RegenRate * Time.deltaTime;
                _internalStamina = Mathf.Min(_internalStamina, MaxStamina);
                
                CurrentStamina = Mathf.RoundToInt(_internalStamina);
                UpdateStaminaUI();
            }
        }
    }

    private void UpdateStaminaUI()
    {
        if(_staminaBar != null)
        {
            _staminaBar.SetStamina(Mathf.RoundToInt(_internalStamina), MaxStamina);
        }
    }

    //Temp Potion logic
    private void UsePotion()
    {
        if(_healthScript != null)
        {
            _healthScript.Heal(25f);
            Debug.Log("Used Potion");
        }
    }

    private void ProcessCombatLogic()
    {
        if(_currentBuffer == CombatInput.None) return;
        PlayerState currentState = _stateManager.GetCurrentState();

        if(currentState == PlayerState.Dodging || currentState == PlayerState.Staggered) return;

        //checking for a jump attack
        if(currentState == PlayerState.Airborne)
        {
            AttemptAttack(JumpAttack, true);
            ConsumeBuffer();
            return;
        }

        //checking for a running attack
        if(currentState == PlayerState.Running)
        {
            Debug.Log("Test Running attack");
            AttackNode nodeToPlay = (_currentBuffer == CombatInput.Light)
                ? RunningLightAttack
                : RunningHeavyAttack;
            AttemptAttack(nodeToPlay, true);
        }
        else if(currentState == PlayerState.Idle || currentState == PlayerState.Walking)
        {
            AttackNode nodeToPlay = (_currentBuffer == CombatInput.Light) 
                ? StartingLightAttack 
                : StartingHeavyAttack;
            AttemptAttack(nodeToPlay, false);
        }
        else if(currentState == PlayerState.Attacking && _canCombo)
        {
            AttackNode nextNode = (_currentBuffer == CombatInput.Light) 
                ? _currentAttackNode.NextLightAttack 
                : _currentAttackNode.NextHeavyAttack;

            Debug.Log($"Attempting to chain from {_currentAttackNode.name} to {(nextNode != null ? nextNode.name : "NULL")}");
            if(nextNode != null) AttemptAttack(nextNode, false);
        }
    }

    private void AttemptAttack(AttackNode node, bool keepMomentum)
    {
        if(node == null) return;

        if(_internalStamina < node.StaminaCost)
        {
            ConsumeBuffer();
            return;
        }

        _stateManager.CarryMomentum = keepMomentum;
        _isRotationLocked = true;

        _internalStamina -= node.StaminaCost;
        _staminaRegenTimer = RegenDelay;
        UpdateStaminaUI();
        _currentAttackNode = node;
        _canCombo = false;
        _comboQueued = true;
        
        //_stateManager.CurrentLungeSpeed = node.LungeForce; Removed for Testing a better way
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

    private void HandleHeavyChargeTimer()
    {
        if(!_isCharging) return;
        _chargeTimer += Time.deltaTime;
        if(_chargeTimer >= MaxChargeDuration)
        {
            HeavyAttackSwing();
        }
    }

#region AnimationEvents
    public void ArmTargetHitbox()
    {
        _isRotationLocked = false;
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

        float chargeBonus = 1.0f;

        if(_isCharging && _chargeTimer > 0)
        {
            chargeBonus += (_chargeTimer / MaxChargeDuration) * 0.5f;
        }

        int finalDamage = Mathf.RoundToInt(currentDamage * _currentAttackNode.DamageMult * chargeBonus);
        int finalPoise = Mathf.RoundToInt(currentPoise * _currentAttackNode.DamageMult * chargeBonus);

        if(_activeHitbox != null)
        {
            _activeHitbox.EnableCollider(finalDamage, finalPoise);
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
        Debug.Log(_comboQueued);
        if(_comboQueued) return;
        Debug.Log("Test 2");
        _currentAttackNode = null;
        _canCombo = false;
        _comboQueued = false;
        _stateManager.CanCancelAttack = false;
        _stateManager.SetPlayerState(PlayerState.Idle);
    }

    public void AttemptHeavyChargePause()
    {
        if (_isHoldingHeavy)
        {
            _isCharging = true;
            _animator.speed = _pullBackSpeed;
        }
    }

    public void HeavyAttackWindUp()
    {
        if (!_isCharging)
        {
            _animator.speed = _normHeavyWindUp;
        }
    }

    public void HeavyAttackSwing()
    {
        _animator.speed = 1f;
        _isCharging = false;
        _chargeTimer = 0f;
    }

    public void ApplyLungeForce()
    {
        _stateManager.CurrentLungeSpeed = _currentAttackNode.LungeForce;
    }
#endregion
}