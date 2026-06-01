using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    #region Variables
    [Header("Movement Settings")]
    public float walkSpeed = 5f;
    public float sprintSpeed = 9f;
    
    [Header("Sprint / Roll Input)")]
    [Tooltip("How long to hold the button before it counts as a Sprint instead of a Roll")]
    public float holdToSprintTime = 0.2f; 
    private bool _isRollButtonHeld;
    private float _rollButtonHoldTimer;
    private bool _isSprinting;

    [Header("Stamina Costs")]
    public float rollStaminaCost = 15f;
    public float jumpStaminaCost = 10f;
    public float sprintStaminaDrainRate = 15f; // Drain per second
    
    [Header("Combat Settings")]
    public bool isTargetLocked = false;
    
    [Header("Roll Settings")]
    public AnimationCurve rollSpeedCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
    public float rollDistanceMultiplier = 15f; 
    public float rollDuration = 0.75f;         
    public float rollCooldown = 0.2f;          
    
    [Header("Roll I-Frames")]
    public float iFrameStartTime = 0.1f; 
    public float iFrameDuration = 0.35f; 
    
    [Header("Rotation Settings")]
    public float rotationSpeed = 15f; 
    public float strafeTurnSpeed = 20f; 

    [Header("Buffering")]
    public float bufferWindow = 0.2f; 
    private float _rollBufferTimer;
    private bool _hasBufferedRoll;

    [Header("Physics")]
    public float gravity = -9.81f;
    public float gravityMultiplier = 2.0f;
    public float jumpHeight = 2.0f;

    [Header("Jump Settings")]
    [Tooltip("How long the player can still jump after falling off a ledge.")]
    public float coyoteTime = 0.15f; 
    private float _coyoteTimeCounter;

    // Internal Variables
    private CharacterController _controller;
    private bool _isMouseInput;
    private PlayerControls _input; 
    private Vector2 _cameraInput;
    private Transform _cameraTransform;
    
    private Vector3 _velocity;
    private Vector2 _moveInput;
    private float _smoothSpeed;
    private float _targetSpeed;
    private Vector3 _horizontalVelocity;
    private PlayerStamina _playerStamina;
    
    // Roll Logic
    private bool _isRolling;
    public bool IsRolling => _isRolling;
    private float _rollTimer;
    private float _rollCooldownTimer;
    private Vector3 _rollDirection;
    [SerializeField] private float _moveRollCancel;
    [SerializeField] private float _combatRollCancel;
 
    public bool IsInvincibleViaRoll => _isRolling && _rollTimer >= iFrameStartTime && _rollTimer <= (iFrameStartTime + iFrameDuration);

    //Player Manager
    private PlayerManager _stateManager;
    #endregion
    [SerializeField] private InputActionAsset inputAsset;

    private InputActionMap _playerMap;
    private InputAction _moveAction;
    private InputAction _lookAction;

    private void Awake()
    {
        _stateManager = GetComponent<PlayerManager>();
        _controller = GetComponent<CharacterController>();
        _playerStamina = GetComponent<PlayerStamina>();
        
        if (Camera.main != null) _cameraTransform = Camera.main.transform;

        _playerMap = inputAsset.FindActionMap("Player");
        _moveAction = _playerMap.FindAction("Move");
        _lookAction = _playerMap.FindAction("Look");

        /*InputAction rollAction = _playerMap.FindAction("Roll");
        InputAction lockOnAction = _playerMap.FindAction("LockOn");
        InputAction jumpAction = _playerMap.FindAction("Jump");

        // Subscribe to the events using the mapped actions
        _moveAction.performed += ctx => _moveInput = ctx.ReadValue<Vector2>();
        _moveAction.canceled += ctx => _moveInput = Vector2.zero;

        rollAction.started += ctx => OnRollButtonDown();
        rollAction.canceled += ctx => OnRollButtonUp();

        lockOnAction.started += ctx => ToggleLockOn();

        jumpAction.started += ctx => OnJumpInput();*/

    }

    private void OnMovePerformed(InputAction.CallbackContext ctx) => _moveInput = ctx.ReadValue<Vector2>();
    private void OnMoveCanceled(InputAction.CallbackContext ctx) => _moveInput = Vector2.zero;

    private void OnEnable()
    {
        _playerMap.Enable();

        // Subscribe to events explicitly
        _moveAction.performed += OnMovePerformed;
        _moveAction.canceled += OnMoveCanceled;

        _playerMap.FindAction("Roll").started += OnRollButtonDown;
        _playerMap.FindAction("Roll").canceled += OnRollButtonUp;

        _playerMap.FindAction("LockOn").started += ToggleLockOn;
        _playerMap.FindAction("Jump").started += OnJumpInput;
    }
    private void OnDisable()
    {
        _playerMap.Disable();

        _moveInput = Vector2.zero;
        _smoothSpeed = 0f;
        _horizontalVelocity = Vector3.zero;

        if (_stateManager != null)
        {
            _stateManager.SetPlayerState(PlayerState.Idle);

        }
        // THIS CURES THE MEMORY LEAK! Unsubscribe from the global asset when disabled/dead.
        _moveAction.performed -= OnMovePerformed;
        _moveAction.canceled -= OnMoveCanceled;

        _playerMap.FindAction("Roll").started -= OnRollButtonDown;
        _playerMap.FindAction("Roll").canceled -= OnRollButtonUp;

        _playerMap.FindAction("LockOn").started -= ToggleLockOn;
        _playerMap.FindAction("Jump").started -= OnJumpInput;
    }

    private void OnRollButtonDown(InputAction.CallbackContext ctx)
    {
        _isRollButtonHeld = true;
        _rollButtonHoldTimer = 0f;
    }

    private void OnRollButtonUp(InputAction.CallbackContext ctx)
    {
        _isRollButtonHeld = false;

        if (!_isSprinting)
        {
            OnRollInput(); 
        }

        _isSprinting = false;
    }

    private void OnRollInput()
    {
        if(_stateManager.CanCancelAttack && _stateManager.GetCurrentState() == PlayerState.Attacking)
        {
            AttemptRoll();
            return;
        }
        if (_isRolling || _rollCooldownTimer > 0)
        {
            _hasBufferedRoll = true;
            _rollBufferTimer = bufferWindow;
        }
        else
        {
            AttemptRoll(); 
        }
    }

    private void Update()
    {
        if (!_controller.enabled) return;

        _stateManager.IsLockedOn = isTargetLocked;
        _stateManager.MoveDirectionIntent = GetWorldSpaceMovementDirection();

        if (_isRollButtonHeld && !_isSprinting)
        {
            _rollButtonHoldTimer += Time.deltaTime;
            if (_rollButtonHoldTimer >= holdToSprintTime)
            {
                _isSprinting = true;
            }
        }

        //stop movement if attacking
        if(_stateManager.GetCurrentState() == PlayerState.Attacking)
        {
            if (_stateManager.CarryMomentum)
            {
                //Running and jumping attacks carry momentum
                _smoothSpeed = Mathf.Lerp(_smoothSpeed, 0f, 1f * Time.deltaTime);
                _stateManager.CurrentLungeSpeed = Mathf.Lerp(_stateManager.CurrentLungeSpeed, 0f, 15f * Time.deltaTime);
            }
            else
            {
                
                _smoothSpeed = 0f; 
                //quick lerp for normal attacks so they look like they are lunging into the attacks
                _stateManager.CurrentLungeSpeed = Mathf.Lerp(_stateManager.CurrentLungeSpeed, 0f, 15f * Time.deltaTime);
            }

            // Combine whatever is left of our momentum with the active lunge
            _horizontalVelocity = (transform.forward * _smoothSpeed) + (transform.forward * _stateManager.CurrentLungeSpeed);
            
            ApplyGravity();
            Vector3 lastVelocity = _horizontalVelocity + new Vector3(0, _velocity.y, 0);
            _controller.Move(lastVelocity * Time.deltaTime);
            return;
        }

        if (_rollCooldownTimer > 0) _rollCooldownTimer -= Time.deltaTime;

        if (_hasBufferedRoll)
        {
            _rollBufferTimer -= Time.deltaTime;
            if (_rollBufferTimer <= 0) _hasBufferedRoll = false;
            else if (!_isRolling && _rollCooldownTimer <= 0)
            {
                _hasBufferedRoll = false;
                AttemptRoll();
            }
        }

        ApplyGravity();

        if(_stateManager.GetCurrentState() == PlayerState.Staggered)
        {
            _stateManager.CurrentLungeSpeed = Mathf.Lerp(_stateManager.CurrentLungeSpeed, 0f , 2f * Time.deltaTime);
            _horizontalVelocity = transform.forward * _stateManager.CurrentLungeSpeed;
            ApplyGravity();
            Vector3 lastVelocity = _horizontalVelocity + new Vector3(0, _velocity.y, 0);
            _controller.Move(_horizontalVelocity * Time.deltaTime);
            return;
        }

        if (_isRolling)
        {
            HandleRoll();
        }
        else
        {
            HandleMovement();
        }

        Vector3 finalVelocity = _horizontalVelocity + new Vector3(0, _velocity.y, 0);
        _controller.Move(finalVelocity * Time.deltaTime);
    }

    private void LateUpdate()
    {
        // Read the continuous mouse/stick delta
        _cameraInput = _lookAction.ReadValue<Vector2>();

        // Check if the current input is coming from a mouse
        if (_lookAction.activeControl != null)
        {
            _isMouseInput = _lookAction.activeControl.device.name == "Mouse";
        }

        // Feed the input and the device type to our Camera script
        if (PlayerCamera.Instance != null)
        {
            PlayerCamera.Instance.HandleAllCameraActions(_cameraInput, _isMouseInput);
        }
    }  

    private void HandleMovement()
    {
        PlayerState currentState = _stateManager.GetCurrentState();
        bool isHealing = (currentState == PlayerState.Healing);

        if(!_controller.isGrounded && !isHealing) _stateManager.SetPlayerState(PlayerState.Airborne);

        if (!_controller.isGrounded)
        {
            if (_moveInput.magnitude > 0.1f)
            {
                // Calculate camera-relative input direction
                Vector3 camFwd = _cameraTransform.forward;
                Vector3 camRt = _cameraTransform.right;
                camFwd.y = 0; camRt.y = 0;
                camFwd.Normalize(); camRt.Normalize();
                
                Vector3 airMoveDir = (camFwd * _moveInput.y + camRt * _moveInput.x).normalized;

                // Add a small amount of velocity in the air
                float airAcceleration = 12f; 
                _horizontalVelocity += airMoveDir * (airAcceleration * Time.deltaTime);

                // Cap speed so they can't infinitely accelerate
                float currentMaxSpeed = Mathf.Max(_smoothSpeed, walkSpeed); 
                if (_horizontalVelocity.magnitude > currentMaxSpeed)
                {
                    _horizontalVelocity = _horizontalVelocity.normalized * currentMaxSpeed;
                }

                // Sync the smooth speed so landing transitions seamlessly without weird hitches
                _smoothSpeed = _horizontalVelocity.magnitude;

                // Allow the character model to face the new direction
                if (!isTargetLocked)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(airMoveDir), rotationSpeed * Time.deltaTime);
                }
            }
            return; // Skip grounded logic
        }

        if (_moveInput.magnitude < 0.1f) 
        {
            _smoothSpeed = Mathf.Lerp(_smoothSpeed, 0f, 10f * Time.deltaTime);
            _horizontalVelocity = Vector3.zero; // Stop horizontal movement
            if(_controller.isGrounded && !isHealing) _stateManager.SetPlayerState(PlayerState.Idle);
            return;
        }

        bool actualSprint = _isSprinting && _moveInput.magnitude > 0.1f && !isHealing;

        if (actualSprint && _stateManager.IsInCombat)
        {
            if (_playerStamina.HasEnoughStamina(sprintStaminaDrainRate * Time.deltaTime))
            {
                _playerStamina.ConsumeStamina(sprintStaminaDrainRate * Time.deltaTime);
            }
            else
            {
                // Force the player to stop sprinting if they run out of stamina
                actualSprint = false;
                _isSprinting = false; 
            }
        }

        if (_controller.isGrounded && !isHealing)
        {
            _stateManager.SetPlayerState(actualSprint ? PlayerState.Running : PlayerState.Walking);
        }


        Vector3 camForward = _cameraTransform.forward;
        Vector3 camRight = _cameraTransform.right;
        camForward.y = 0;
        camRight.y = 0;
        camForward.Normalize();
        camRight.Normalize();

        Vector3 moveDir = (camForward * _moveInput.y + camRight * _moveInput.x).normalized;

        if (isTargetLocked && !actualSprint)
        {
            if (camForward != Vector3.zero)
            {
                Quaternion strafeRotation = Quaternion.LookRotation(camForward);
                transform.rotation = Quaternion.Slerp(transform.rotation, strafeRotation, strafeTurnSpeed * Time.deltaTime);
            }
        }
        else 
        {
            if (moveDir != Vector3.zero) 
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }

        _targetSpeed = actualSprint ? sprintSpeed : walkSpeed;

        if(isHealing) _targetSpeed = walkSpeed * 0.65f;
        _smoothSpeed = Mathf.Lerp(_smoothSpeed, _targetSpeed, 10f * Time.deltaTime);
        
        // Save the speed instead of moving directly
        _horizontalVelocity = moveDir * _smoothSpeed;
    }

#region Rolling
    private void AttemptRoll()
    {
        PlayerState currentState = _stateManager.GetCurrentState();
        bool overrideRoll = _stateManager.CanRoll;
        bool isNormalRoll = (currentState == PlayerState.Idle || currentState == PlayerState.Walking || currentState == PlayerState.Running);
        bool isCombatCancel = (currentState == PlayerState.Attacking && _stateManager.CanCancelAttack);
        
        if(!isNormalRoll && !isCombatCancel && !overrideRoll) {
            return;
        }

        if(isNormalRoll)
        {
            if(_isRolling || _rollCooldownTimer > 0 || !_controller.isGrounded) return;
        }
        else if(overrideRoll)
        {
            Debug.Log("Override Roll Activated");
            //if(!_controller.isGrounded) return;
        }
        else if(isCombatCancel)
        {
          if(!_controller.isGrounded) return;
        }

        if (_stateManager.IsInCombat)
        {
            if (!_playerStamina.HasEnoughStamina(rollStaminaCost)) return; // Fail roll
            _playerStamina.ConsumeStamina(rollStaminaCost);
        }

        _hasBufferedRoll = false;
        _isRolling = true;
        _rollTimer = 0f; 
        
        _stateManager.SetPlayerState(PlayerState.Dodging);
        _rollCooldownTimer = rollDuration + rollCooldown;

        if(isCombatCancel) _stateManager.RequestBufferClear = true;

        if (_moveInput.magnitude > 0.1f)
        {
            Vector3 camForward = _cameraTransform.forward;
            Vector3 camRight = _cameraTransform.right;
            camForward.y = 0; 
            camRight.y = 0;
            _rollDirection = (camForward * _moveInput.y + camRight * _moveInput.x).normalized;
        }
        else
        {
            _rollDirection = -transform.forward; 
        }

        if (TryGetComponent<AnimationBridge>(out var animBridge))
        {
            animBridge.PlayRoll();
        }
    }

    private void HandleRoll()
    {
        _rollTimer += Time.deltaTime;
        _stateManager.CanRoll = false; //Set back to false cause should only be used for animation breaking
        
        float normalizedTime = _rollTimer / rollDuration;
        float currentCurveValue = rollSpeedCurve.Evaluate(normalizedTime);
        float currentSpeed = currentCurveValue * rollDistanceMultiplier;

        // Save the speed instead of moving directly
        _horizontalVelocity = _rollDirection * currentSpeed;
        
        if (_rollDirection != -transform.forward && _rollDirection != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(_rollDirection);
        }

        // Early Roll Cancel for smooth running / smooth attack

        if (normalizedTime >= _combatRollCancel && _stateManager.HasBufferedAttack)
        {
            ExitRollEarly();
            return;
        }
        if (normalizedTime >= _moveRollCancel && _moveInput.magnitude > 0.1f)
        {
            ExitRollEarly();
            _targetSpeed = _isSprinting ? sprintSpeed : walkSpeed;
            _smoothSpeed = _targetSpeed; 
            return;
        }

        if (_rollTimer >= rollDuration)
        {
            _isRolling = false;
            _stateManager.SetPlayerState(PlayerState.Idle);
            if (TryGetComponent<AnimationBridge>(out var animBridge))
            {
                animBridge.BackToLocomotion();
            }
        }
    }

    private void ExitRollEarly()
    {
        _isRolling = false;
        _stateManager.SetPlayerState(PlayerState.Idle); //
        
        if (TryGetComponent<AnimationBridge>(out var animBridge))
        {
            animBridge.BackToLocomotion();
        }
    }
#endregion

//combat Helper funciton
    public Vector3 GetWorldSpaceMovementDirection()
    {
        if(_moveInput.magnitude < 0.1f) return Vector3.zero;

        Vector3 camForward = _cameraTransform.forward;
        Vector3 camRight = _cameraTransform.right;
        camForward.y = 0;
        camRight.y = 0;

        return (camForward * _moveInput.y + camRight * _moveInput.x).normalized;
    }

    private void ApplyGravity()
    {
        if (_controller.isGrounded) 
        {
            _coyoteTimeCounter = coyoteTime; // Reset coyote time when on the ground

            if (_velocity.y <= 0) 
            {
                _velocity.y = -2f; 
            }
        }
        else
        {
            _coyoteTimeCounter -= Time.deltaTime; // Tick down when in the air
        }

        _velocity.y += gravity * gravityMultiplier * Time.deltaTime;
    }
    
    private void ToggleLockOn(InputAction.CallbackContext ctx)
    {
        if (PlayerCamera.Instance == null) return;

        if (isTargetLocked)
        {
            // We are already locked on, so unlock
            isTargetLocked = false;
            PlayerCamera.Instance.ClearLockOnTarget();
        }
        else
        {
            // Try to find a target. If the camera finds one, set our state to locked on!
            if (PlayerCamera.Instance.FindLockOnTarget())
            {
                isTargetLocked = true;
            }
        }
    }

    private void OnJumpInput(InputAction.CallbackContext ctx)
    {
        PlayerState currentState = _stateManager.GetCurrentState();

        // Allow jumping from standard ground states, OR if airborne but still within the coyote window
        bool canJump = (currentState == PlayerState.Idle || 
                        currentState == PlayerState.Walking ||
                        currentState == PlayerState.Running ||
                        (currentState == PlayerState.Airborne && _coyoteTimeCounter > 0f));

        if (_coyoteTimeCounter > 0f && canJump)
        {
            if (_stateManager.IsInCombat)
            {
                if (!_playerStamina.HasEnoughStamina(jumpStaminaCost)) return; // Fail jump
                _playerStamina.ConsumeStamina(jumpStaminaCost);
            }

            // Physics formula for jump height
            _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity * gravityMultiplier);

            // Set counter to 0 so the player can't double jump in the air
            _coyoteTimeCounter = 0f;

            _stateManager.SetPlayerState(PlayerState.Airborne);

            if (TryGetComponent<AnimationBridge>(out var animBridge))
            {
                animBridge.TriggerJump();
            }
        }
    }
}