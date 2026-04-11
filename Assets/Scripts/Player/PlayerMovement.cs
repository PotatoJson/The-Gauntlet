using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 5f;
    public float sprintSpeed = 9f;
    
    [Header("Sprint / Roll Input)")]
    [Tooltip("How long to hold the button before it counts as a Sprint instead of a Roll")]
    public float holdToSprintTime = 0.2f; 
    private bool _isRollButtonHeld;
    private float _rollButtonHoldTimer;
    private bool _isSprinting;
    
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

    // Internal Variables
    private CharacterController _controller;
    private PlayerControls _input; 
    private Transform _cameraTransform;
    
    private Vector3 _velocity;
    private Vector2 _moveInput;
    private float _smoothSpeed;
    private float _targetSpeed;
    private Vector3 _horizontalVelocity;
    
    // Roll Logic
    private bool _isRolling;
    public bool IsRolling => _isRolling;
    private float _rollTimer;
    private float _rollCooldownTimer;
    private Vector3 _rollDirection;

    public bool IsInvincibleViaRoll => _isRolling && _rollTimer >= iFrameStartTime && _rollTimer <= (iFrameStartTime + iFrameDuration);

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        
        if (Camera.main != null) _cameraTransform = Camera.main.transform;
        
        _input = new PlayerControls();
        
        _input.Player.Move.performed += ctx => _moveInput = ctx.ReadValue<Vector2>();
        _input.Player.Move.canceled += ctx => _moveInput = Vector2.zero;
<<<<<<< Updated upstream

        _input.Player.Roll.started += ctx => OnRollButtonDown();
        _input.Player.Roll.canceled += ctx => OnRollButtonUp();

        _input.Player.LockOn.started += ctx => ToggleLockOn();

        _input.Player.Jump.started += ctx => OnJumpInput();
=======
        
        _input.Player.Roll.performed += ctx => OnDashInput();
>>>>>>> Stashed changes
    }

    private void OnEnable() => _input.Enable();
    private void OnDisable() => _input.Disable();

    private void OnRollButtonDown()
    {
        _isRollButtonHeld = true;
        _rollButtonHoldTimer = 0f;
    }

    private void OnRollButtonUp()
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
        if (_isRollButtonHeld && !_isSprinting)
        {
            _rollButtonHoldTimer += Time.deltaTime;
            if (_rollButtonHoldTimer >= holdToSprintTime)
            {
                _isSprinting = true;
            }
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

    private void HandleMovement()
    {
        if (_moveInput.magnitude < 0.1f) 
        {
            _smoothSpeed = Mathf.Lerp(_smoothSpeed, 0f, 10f * Time.deltaTime);
            _horizontalVelocity = Vector3.zero; // Stop horizontal movement
            return;
        }

<<<<<<< Updated upstream
        bool actualSprint = _isSprinting && _moveInput.magnitude > 0.1f;
=======
        // Note: Currently using the Dash button for sprinting based on the original code
        bool isSprinting = false;
>>>>>>> Stashed changes

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
        _smoothSpeed = Mathf.Lerp(_smoothSpeed, _targetSpeed, 10f * Time.deltaTime);
        
        // Save the speed instead of moving directly
        _horizontalVelocity = moveDir * _smoothSpeed;
    }

    private void AttemptRoll()
    {
        if (_isRolling || _rollCooldownTimer > 0 || !_controller.isGrounded) return;
        
        _hasBufferedRoll = false;
        _isRolling = true;
        _rollTimer = 0f; 
        
        _rollCooldownTimer = rollDuration + rollCooldown; 

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
        
        float normalizedTime = _rollTimer / rollDuration;
        float currentCurveValue = rollSpeedCurve.Evaluate(normalizedTime);
        float currentSpeed = currentCurveValue * rollDistanceMultiplier;

        // Save the speed instead of moving directly
        _horizontalVelocity = _rollDirection * currentSpeed;
        
        if (_rollDirection != -transform.forward && _rollDirection != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(_rollDirection);
        }

        // Early Roll Cancel for smooth running
        float cancelThreshold = rollDuration * 0.8f; 
        if (_rollTimer >= cancelThreshold && _moveInput.magnitude > 0.1f)
        {
            _isRolling = false;
            _targetSpeed = _isSprinting ? sprintSpeed : walkSpeed;
            _smoothSpeed = _targetSpeed; 
            
            if (TryGetComponent<AnimationBridge>(out var animBridge))
            {
                animBridge.BackToLocomotion();
            }
            return;
        }

        if (_rollTimer >= rollDuration)
        {
            _isRolling = false;
            if (TryGetComponent<AnimationBridge>(out var animBridge))
            {
                animBridge.BackToLocomotion();
            }
        }
    }

    private void ApplyGravity()
    {
        if (_controller.isGrounded && _velocity.y <= 0) 
        {
            _velocity.y = -2f; 
        }

        _velocity.y += gravity * gravityMultiplier * Time.deltaTime;
    }
    
    private void ToggleLockOn()
    {
        isTargetLocked = !isTargetLocked; 
        
        // Debug.Log("Target Locked: " + isTargetLocked);
    }

    private void OnJumpInput()
    {
        if (_controller.isGrounded && !_isRolling)
        {
            // Physics formula for jump height
            _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity * gravityMultiplier);

            if (TryGetComponent<AnimationBridge>(out var animBridge))
            {
                animBridge.TriggerJump();
            }
        }
    }
}