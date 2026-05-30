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

    [Header("References")]
    private PlayerManager _stateManager;
    private Animator _animator;
    private PlayerControls _input;
    private PlayerStatsManager _statsManager;
    private PlayerStamina _staminaScript;
    private PlayerHealth _healthScript;

    [Header("Physical Hitboxes")]
    [SerializeField] private HitboxController _leftHitbox;
    [SerializeField] private HitboxController _rightHitbox;
    [Space]
    private HitboxController _activeHitbox;

    [Header("Combat Tracking")]
    private AttackNode _currentAttackNode;
    private CombatInput _currentAttackType = CombatInput.None;
    private bool _canCombo = false;
    private bool _comboQueued = false;
    private bool _isHoldingHeavy = false;
    private bool _isCharging = false;
    private bool _isRotationLocked = true;

    private bool _hasPlayedHeavyAudio = false;

    [Header("ChargeSettings")]
    [SerializeField] private float _pullBackSpeed;
    [SerializeField] private float _normHeavyWindUp;
    public float MaxChargeDuration;
    private float _chargeTimer = 0f;
    
    [Header("Input Buffer Things")]
    public float BufferDuration;
    private CombatInput _currentBuffer = CombatInput.None;
    private float BufferTimer = 0f;

    [Header("Skill Setings")]
    [SerializeField] private Transform _leftSkillSpawnPoint;
    [SerializeField] private Transform _rightSkillSpawnPoint;
    private AnimatorOverrideController _overrideController;

    private float _leftSkillCooldownTimer = 0f;
    private float _rightSkillCooldownTimer = 0f;

    public float LeftSkillTimer => _leftSkillCooldownTimer;
    public float RightSkillTimer => _rightSkillCooldownTimer;

    public float LeftMaxCooldown => 5.0f; 
    public float RightMaxCooldown => 5.0f;

    private InputAction _leftSkillAction;
    private InputAction _rightSkillAction;

    private RunTimeGauntlet _currentlyCastingGauntlet;
    private Transform _currentSpawnPoint;

    [Header("Input Setup")]
    [SerializeField] private InputActionAsset inputAsset;

    private InputActionMap _playerMap;
    private InputAction _lightAttackAction;
    private InputAction _heavyAttackAction;
    private InputAction _healAction;

    private InputAction _debugTeleportAction;
    #endregion

    #region Setup
    void Awake()
    {
        _animator = GetComponentInChildren<Animator>();
        _overrideController = new AnimatorOverrideController(_animator.runtimeAnimatorController);
        _animator.runtimeAnimatorController = _overrideController;
        _stateManager = GetComponent<PlayerManager>();
        _statsManager = GetComponent<PlayerStatsManager>();
        _staminaScript = GetComponent<PlayerStamina>();
        _healthScript = GetComponent<PlayerHealth>();

        _playerMap = inputAsset.FindActionMap("Player");
        _lightAttackAction = _playerMap.FindAction("LightAttack");
        _heavyAttackAction = _playerMap.FindAction("HeavyAttack");
        _healAction = _playerMap.FindAction("Heal");
        _debugTeleportAction = _playerMap.FindAction("DebugTeleport");
        
        _leftSkillAction = _playerMap.FindAction("LeftSkill"); 
        _leftSkillAction.started += ctx => AttemptSkillCast(isLeftGauntlet: true);

        _rightSkillAction = _playerMap.FindAction("RightSkill");
        _rightSkillAction.started += ctx => AttemptSkillCast(isLeftGauntlet: false);

        _lightAttackAction.started += ctx => OnLightAttackInput();
        _heavyAttackAction.started += ctx => 
        {
            _isHoldingHeavy = true;
            OnHeavyAttackInput();
        };
        _heavyAttackAction.canceled += ctx => OnHeavyAttackReleased();
        _healAction.started += ctx => UsePotion();
        _debugTeleportAction.started += ctx => _stateManager.DebugTeleport();
    }

    private void OnEnable() => _playerMap.Enable();
    
    private void OnDisable() => _playerMap.Disable();

    // Update is called once per frame
    void Update()
    {
        if (_leftSkillCooldownTimer > 0) _leftSkillCooldownTimer -= Time.deltaTime;
        if (_rightSkillCooldownTimer > 0) _rightSkillCooldownTimer -= Time.deltaTime;

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

    #region skill Handling
    private void AttemptSkillCast(bool isLeftGauntlet)
    {
        PlayerState currentState = _stateManager.GetCurrentState();
        if (currentState != PlayerState.Idle && currentState != PlayerState.Walking) return;

        RunTimeGauntlet targetGauntlet = isLeftGauntlet ? _statsManager.SecondaryGauntlet : _statsManager.PrimaryGauntlet;
        float currentCooldown = isLeftGauntlet ? _leftSkillCooldownTimer : _rightSkillCooldownTimer;
        
        if (targetGauntlet == null || targetGauntlet.ActiveSkillGem == null) return;
        if (currentCooldown > 0) return;

        SkillGemData slottedSkill = targetGauntlet.ActiveSkillGem;

        if (!_staminaScript.HasEnoughStamina(slottedSkill.StaminaCost)) return;
        _staminaScript.ConsumeStamina(slottedSkill.StaminaCost);

        ForceSnapToLockOn();
        _currentlyCastingGauntlet = targetGauntlet;
        _currentSpawnPoint = isLeftGauntlet ? _leftSkillSpawnPoint : _rightSkillSpawnPoint;

        ConsumeBuffer();
        _stateManager.SetPlayerState(PlayerState.Attacking); 

        SkillVariation? variant = slottedSkill.GetVariationForElement(targetGauntlet.BaseGauntlet.Element);
        
        if (variant.HasValue)
        {
            if (isLeftGauntlet && variant.Value.LeftGauntletAnim != null)
            {
                // Swap the empty dummy state with the actual left-handed animation
                _overrideController["CastLeft_Dummy"] = variant.Value.LeftGauntletAnim;
                _animator.SetTrigger("CastLeftSkill");
                _leftSkillCooldownTimer = slottedSkill.Cooldown;
            }
            else if (!isLeftGauntlet && variant.Value.RightGauntletAnim != null)
            {
                // Swap the empty dummy state with the actual right-handed animation
                _overrideController["CastRight_Dummy"] = variant.Value.RightGauntletAnim;
                _animator.SetTrigger("CastRightSkill");
                _rightSkillCooldownTimer = slottedSkill.Cooldown;
            }
            else
            {
                Debug.LogWarning($"Missing animation clip on {slottedSkill.Name} for the active gauntlet!");
            }
        }
    }

    public void ExecuteSkillWindUpVFX()
    {
        if (_currentlyCastingGauntlet == null || _currentlyCastingGauntlet.ActiveSkillGem == null) return;

        SkillVariation? variant = _currentlyCastingGauntlet.ActiveSkillGem.GetVariationForElement(_currentlyCastingGauntlet.BaseGauntlet.Element);
        
        if (variant.HasValue && variant.Value.WindUpVFXPrefab != null && _currentSpawnPoint != null)
        {
            // Instantiate the VFX and set the hand (_currentSpawnPoint) as its parent
            GameObject vfx = Instantiate(variant.Value.WindUpVFXPrefab, _currentSpawnPoint.position, _currentSpawnPoint.rotation, _currentSpawnPoint);
            //Destroy if Particle System doesnt stop on its own
            Destroy(vfx, 2f); 
        }
    }

    public void ExecuteSkillSpawn()
    {
        if (_currentlyCastingGauntlet == null || _currentlyCastingGauntlet.ActiveSkillGem == null)
        {
            Debug.LogWarning("ExecuteSkillSpawn fired, but no gauntlet was cached!");
            return; 
        }

        SkillGemData slottedSkill = _currentlyCastingGauntlet.ActiveSkillGem;
        ElementType currentElement = _currentlyCastingGauntlet.BaseGauntlet.Element;

        GameObject prefabToSpawn = slottedSkill.GetPrefabForElement(currentElement);

        if (prefabToSpawn != null && _currentSpawnPoint != null)
        {
            GameObject activeSkill = Instantiate(prefabToSpawn, _currentSpawnPoint.position, transform.rotation);
            
            BaseSkillProjectile projectileScript = activeSkill.GetComponent<BaseSkillProjectile>();
            if (projectileScript != null)
            {
                Transform currentTarget = null;
                //handle the chance the player is locked on to an enemy
                if (_stateManager.IsLockedOn && PlayerCamera.Instance != null)
                {
                    currentTarget = PlayerCamera.Instance.currentLockOnTarget;
                }

                projectileScript.Initialize(_statsManager.CurrentDamage, _statsManager.CurrentMaxPoise, currentTarget);

                if (projectileScript is FireballProjectile fireball)
                {
                    fireball.AttachFeedingVFX(_currentSpawnPoint);
                }
            }
        }

        _currentlyCastingGauntlet = null;
        _currentSpawnPoint = null;
    }

    #endregion

    private void UsePotion()
    {
        PlayerState currentState = _stateManager.GetCurrentState();
        if(currentState != PlayerState.Idle && currentState != PlayerState.Walking) return;

        if(_healthScript != null && _healthScript.TryConsumePotion())
        {
            ConsumeBuffer();
            _stateManager.SetPlayerState(PlayerState.Healing);
            _animator.SetTrigger("DrinkPotion");
            if (MetricsTracker.Instance != null)
            {
                MetricsTracker.Instance.RecordPotionUsed();
            }
        }
    }

    private void ProcessCombatLogic()
    {
        if(_currentBuffer == CombatInput.None) return;
        PlayerState currentState = _stateManager.GetCurrentState();

        if(currentState == PlayerState.Dodging || currentState == PlayerState.Staggered) return;

        if(currentState == PlayerState.Airborne)
        {
            AttemptAttack(JumpAttack, true, _currentBuffer);
            return;
        }

        if(currentState == PlayerState.Running)
        {
            AttackNode nodeToPlay = (_currentBuffer == CombatInput.Light) ? RunningLightAttack : RunningHeavyAttack;
            AttemptAttack(nodeToPlay, true, _currentBuffer);
        }
        else if(currentState == PlayerState.Idle || currentState == PlayerState.Walking)
        {
            AttackNode nodeToPlay = (_currentBuffer == CombatInput.Light) ? StartingLightAttack : StartingHeavyAttack;
            AttemptAttack(nodeToPlay, false, _currentBuffer);
        }
        else if(currentState == PlayerState.Attacking && _canCombo)
        {
            AttackNode nextNode = (_currentBuffer == CombatInput.Light) ? _currentAttackNode.NextLightAttack : _currentAttackNode.NextHeavyAttack;
            if(nextNode != null) AttemptAttack(nextNode, false, _currentBuffer);
        }
    }

    private void AttemptAttack(AttackNode node, bool keepMomentum, CombatInput attackType)
    {
        {
            if(node == null) return;

            if (_stateManager.IsInCombat)
            {
                // We ARE in combat: enforce stamina rules strictly
                if(!_staminaScript.HasEnoughStamina(node.StaminaCost))
                {
                    ConsumeBuffer();
                    return;
                }
                _staminaScript.ConsumeStamina(node.StaminaCost);
            }

            _stateManager.CarryMomentum = keepMomentum;
            _isRotationLocked = true;

            _currentAttackNode = node;
            _currentAttackType = attackType;
            _canCombo = false;
            _comboQueued = true;

            _hasPlayedHeavyAudio = false;
            
            //_stateManager.CurrentLungeSpeed = node.LungeForce; Removed for Testing a better way
            _stateManager.CanCancelAttack = false;
            _stateManager.SetPlayerState(PlayerState.Attacking);
            _animator.SetTrigger(node.AnimationTrigger);
            ConsumeBuffer();
        }
    }

    //instant snap for spells
    private void ForceSnapToLockOn()
    {
        // Only snap if we are actively locked on to a valid target
        if (_stateManager.IsLockedOn && PlayerCamera.Instance != null && PlayerCamera.Instance.currentLockOnTarget != null)
        {
            Vector3 directionToTarget = PlayerCamera.Instance.currentLockOnTarget.position - transform.position;
            directionToTarget.y = 0; // Keep the rotation strictly horizontal so we don't tilt into the floor

            if (directionToTarget != Vector3.zero)
            {
                // Bypass Slerp and instantly snap the rotation
                transform.rotation = Quaternion.LookRotation(directionToTarget.normalized);
            }
        }
    }

    private void ProcessAttackRotation()
    {
        if (_stateManager.GetCurrentState() != PlayerState.Attacking) return;

        // LOCK-ON
        if (_stateManager.IsLockedOn && PlayerCamera.Instance != null && PlayerCamera.Instance.currentLockOnTarget != null)
        {
            // Track the enemy during the wind-up phase (while _isRotationLocked is true).
            // Once the hitbox is armed (_isRotationLocked = false), we stop tracking so the swing follows through naturally.
            if (_isRotationLocked) 
            {
                Vector3 directionToTarget = PlayerCamera.Instance.currentLockOnTarget.position - transform.position;
                directionToTarget.y = 0; // Keep the rotation strictly horizontal

                if (directionToTarget != Vector3.zero)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(directionToTarget.normalized);
                    
                    // Check if we are performing a running attack
                    bool isRunningAttack = (_currentAttackNode == RunningLightAttack || _currentAttackNode == RunningHeavyAttack);
                    
                    // Use a slower turn speed (e.g., 5f) for running attacks to create U-turn arc, 
                    // and a fast snap (30f) for standing/walking attacks
                    float currentTurnSpeed = isRunningAttack ? 5f : 30f; 

                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, currentTurnSpeed * Time.deltaTime);
                }
            }
        }
        // FREE-AIM BEHAVIOR
        else
        {
            // Only allow free-aim snapping if the rotation is unlocked (hitbox is armed / active frames)
            if (_isRotationLocked) return;

            Vector3 snapDir = _stateManager.MoveDirectionIntent;
            if (snapDir != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(snapDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 30f * Time.deltaTime);
            }
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
        _activeHitbox = _leftHitbox;

        if(_currentAttackNode.StrikingHand == StrikeHand.Right)
        {
            _activeHitbox = _rightHitbox;
        }
        else if(_currentAttackNode.StrikingHand == StrikeHand.Both)
        {
            //future dual hand attack
        }
        int currentDamage = Mathf.RoundToInt(_statsManager.CurrentDamage);
        int currentPoise = Mathf.RoundToInt(_statsManager.CurrentPoiseDamage); // need to add poiseDamage to _statsManager

        float chargeBonus = 1.0f;

        if(_isCharging && _chargeTimer > 0)
        {
            chargeBonus += (_chargeTimer / MaxChargeDuration) * 0.5f;
        }

        int finalDamage = Mathf.RoundToInt(currentDamage * _currentAttackNode.DamageMult * chargeBonus);
        int finalPoise = Mathf.RoundToInt(currentPoise * _currentAttackNode.DamageMult * chargeBonus);

        if(_activeHitbox != null)
        {
            _activeHitbox.EnableCollider(finalDamage, finalPoise, _currentAttackType);
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

        if (!_hasPlayedHeavyAudio)
        {
            PlayerAudio audioScript = GetComponentInChildren<PlayerAudio>();
            if (audioScript != null)
            {
                audioScript.PlayHeavyAttack();
            }
            
            // Close the latch so the timeline bookmark can't trigger it again
            _hasPlayedHeavyAudio = true; 
        }
    }

    public void ApplyLungeForce()
    {
        _stateManager.CurrentLungeSpeed = _currentAttackNode.LungeForce;
    }
#endregion
}