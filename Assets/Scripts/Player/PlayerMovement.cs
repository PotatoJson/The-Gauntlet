using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 5f;
    public float sprintSpeed = 9f;
    
    [Header("Dash Settings")]
    public float dashSpeed = 15f;
    public float dashDuration = 0.2f;
    public float dashCooldown = 0.5f; 
    public float dashIFrames = 0.15f; 
    
    [Tooltip("How fast the character turns while sprinting")]
    public float rotationSpeed = 15f; 
    
    [Tooltip("How fast the character aligns with the camera while strafing")]
    public float strafeTurnSpeed = 20f; 

    [Header("Buffering")]
    public float bufferWindow = 0.2f; 
    private float _dashBufferTimer;
    private bool _hasBufferedDash;

    [Header("Physics")]
    public float gravity = -9.81f;
    public float gravityMultiplier = 2.0f;

    // Internal Variables
    private CharacterController _controller;
    private PlayerControls _input;
    private Transform _cameraTransform;
    
    private Vector3 _velocity;
    private Vector2 _moveInput;
    private float _smoothSpeed;
    private float _targetSpeed;
    private float _dashCooldownTimer;
    private float _iFrameTimer;
    
    // Dash Logic
    private bool _isDashing;
    public bool IsDashing => _isDashing;
    private float _dashTimer;
    public bool IsInvincibleViaDash => _iFrameTimer > 0;
    private Vector3 _dashDirection;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        
        // Failsafe check to ensure we have a Main Camera
        if (Camera.main != null)
        {
            _cameraTransform = Camera.main.transform;
        }
        else
        {
            Debug.LogError("PlayerMovement: No camera tagged 'MainCamera' found in the scene!");
        }
        
        _input = new PlayerControls();
        
        _input.Player.Move.performed += ctx => _moveInput = ctx.ReadValue<Vector2>();
        _input.Player.Move.canceled += ctx => _moveInput = Vector2.zero;
        
        _input.Player.Dash.performed += ctx => OnDashInput();
    }

    private void OnEnable() => _input.Enable();
    private void OnDisable() => _input.Disable();

    private void OnDashInput()
    {
        // If we are currently dashing or on cooldown, buffer the input
        if (_isDashing || _dashCooldownTimer > 0)
        {
            _hasBufferedDash = true;
            _dashBufferTimer = bufferWindow;
        }
        else
        {
            AttemptDash(); 
        }
    }

    private void Update()
    {
        // Handle Cooldowns & Timers
        if (_dashCooldownTimer > 0) _dashCooldownTimer -= Time.deltaTime;
        if (_iFrameTimer > 0) _iFrameTimer -= Time.deltaTime;

        // Handle Input Buffering for Dashes
        if (_hasBufferedDash)
        {
            _dashBufferTimer -= Time.deltaTime;
            
            if (_dashBufferTimer <= 0) 
            {
                _hasBufferedDash = false;
            }
            // If the buffer is active and we are clear to dash, execute it
            else if (!_isDashing && _dashCooldownTimer <= 0)
            {
                _hasBufferedDash = false;
                AttemptDash();
            }
        }

        ApplyGravity();

        // Mouse Lock Toggle (Alt key)
        if (Keyboard.current.leftAltKey.wasPressedThisFrame)
        {
            ToggleCursorLock();
        }
        
        // If Dashing, override all other movement
        if (_isDashing)
        {
            HandleDash();
            return;
        }

        // Otherwise, handle standard movement
        HandleMovement();
    }

    private void HandleMovement()
    {
        // If no input, smoothly decelerate to 0
        if (_moveInput.magnitude < 0.1f) 
        {
            _smoothSpeed = Mathf.Lerp(_smoothSpeed, 0f, 10f * Time.deltaTime);
            return;
        }

        // Note: Currently using the Dash button for sprinting based on the original code
        bool isSprinting = _input.Gameplay.Dash.IsPressed(); 

        // Calculate World Direction relative to Camera
        Vector3 camForward = _cameraTransform.forward;
        Vector3 camRight = _cameraTransform.right;
        camForward.y = 0;
        camRight.y = 0;
        camForward.Normalize();
        camRight.Normalize();

        Vector3 moveDir = (camForward * _moveInput.y + camRight * _moveInput.x).normalized;

        if (isSprinting)
        {
            // Sprinting: Face the direction we are moving
            Quaternion targetRotation = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
        else
        {
            // Walking: Strafe relative to camera
            if (camForward != Vector3.zero)
            {
                Quaternion strafeRotation = Quaternion.LookRotation(camForward);
                transform.rotation = Quaternion.Slerp(transform.rotation, strafeRotation, strafeTurnSpeed * Time.deltaTime);
            }
        }

        _targetSpeed = isSprinting ? sprintSpeed : walkSpeed;
        _smoothSpeed = Mathf.Lerp(_smoothSpeed, _targetSpeed, 10f * Time.deltaTime);
        
        _controller.Move(moveDir * _smoothSpeed * Time.deltaTime);
    }

    private void AttemptDash()
    {
        if (_isDashing || _dashCooldownTimer > 0) return;
        
        _hasBufferedDash = false;
        _isDashing = true;
        _dashTimer = dashDuration;
        _iFrameTimer = dashIFrames; 
        _dashCooldownTimer = dashCooldown; 

        // Determine dash direction based on input or current facing direction
        if (_moveInput.magnitude > 0.1f)
        {
            Vector3 camForward = _cameraTransform.forward;
            Vector3 camRight = _cameraTransform.right;
            camForward.y = 0; 
            camRight.y = 0;
            _dashDirection = (camForward * _moveInput.y + camRight * _moveInput.x).normalized;
        }
        else
        {
            _dashDirection = transform.forward;
        }
    }

    private void HandleDash()
    {
        _controller.Move(_dashDirection * dashSpeed * Time.deltaTime);
        
        if (_dashDirection != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(_dashDirection);
        }

        _dashTimer -= Time.deltaTime;
        if (_dashTimer <= 0)
        {
            _isDashing = false;
        }
    }

    private void ApplyGravity()
    {
        if (_controller.isGrounded && _velocity.y < 0)
        {
            _velocity.y = -2f; // Keeps the player grounded smoothly
        }

        _velocity.y += gravity * gravityMultiplier * Time.deltaTime;
        _controller.Move(_velocity * Time.deltaTime);
    }

    private void ToggleCursorLock()
    {
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}