using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCamera : MonoBehaviour
{
    public static PlayerCamera Instance;

    #region References
    [Header("References")]
    public Transform playerTarget;
    public Transform cameraPivotTransform;
    public Camera cameraObject;
    #endregion

    #region Core Settings
    [Header("Camera Settings")]
    public float cameraSmoothSpeed = 1f;
    public float leftAndRightRotationSpeed = 220f;
    public float upAndDownRotationSpeed = 220f;
    public float minimumPivot = -30f;
    public float maximumPivot = 60f;
    public float cameraCollisionRadius = 0.2f;
    public LayerMask collideWithLayers;

    [Header("Device Sensitivity")]
    [Tooltip("Multiplier for mouse pixel delta.")]
    [Range(0.1f, 2f)]
    [SerializeField] private float mouseSensitivityMultiplier = 0.15f;
    
    [Tooltip("Multiplier for the controller right stick. Leave at 1 for default.")]
    [Range(0.1f, 5f)]
    public float controllerSensitivityMultiplier = 1f;

    [Header("Follow Settings (Velocity Lag)")]
    [Tooltip("How tight the camera is when standing still or walking.")]
    public float baseSmoothTime = 0.1f;
    [Tooltip("How much the camera drags behind when sprinting SIDEWAYS.")]
    public float maxSmoothTimeSideways = 0.4f;
    [Tooltip("How much the camera drags behind when sprinting FORWARD/BACKWARD. Keep this lower")]
    public float maxSmoothTimeForward = 0.15f;
    [Tooltip("How smoothly the camera transitions between tight and relaxed.")]
    public float lagTransitionSpeed = 3f;
    #endregion

    #region Lock-On & Framing Settings
    [Header("Lock-On Settings")]
    public Transform currentLockOnTarget;
    public LayerMask enemyLayer;
    [Tooltip("The layers that block the camera's line of sight (e.g., Ground, Default).")]
    public LayerMask environmentLayer;
    public float maximumLockOnDistance = 30f;
    public float lockOnTrackingSpeed = 15f; // How fast the camera snaps to the enemy
    [Tooltip("How long an enemy can hide behind a wall before the lock-on breaks.")]
    public float timeBeforeLockOnBreaks = 3f;

    [Header("Target Switching")]
    [Tooltip("How far the controller stick must be pushed to switch targets (0 to 1).")]
    public float controllerSwitchThreshold = 0.5f;
    [Tooltip("How fast the mouse must be flicked to switch targets (pixels per frame).")]
    public float mouseSwitchThreshold = 30.0f;
    [Tooltip("How long to wait before allowing another switch (prevents rapid-fire skipping).")]
    public float targetSwitchCooldown = 0.3f;

    [Header("Dynamic Framing")]
    [Tooltip("The distance considered 'point blank' for framing.")]
    public float closeFramingDistance = 2f;
    [Tooltip("The distance considered 'max range' for framing.")]
    public float farFramingDistance = 15f;
    [Tooltip("How high the camera looks when point-blank (Forces reticle up).")]
    public float closeVerticalOffset = -0.25f;
    [Tooltip("How high the camera looks when far away.")]
    public float farVerticalOffset = -1.0f;
    [Tooltip("How much the camera physically drops toward the floor when point-blank.")]
    public float closePivotHeight = 1.0f; 

    [Header("UI Settings")]
    public GameObject lockOnReticle;
    [Tooltip("Pushes the reticle up so it targets the chest/head instead of the feet.")]
    public float targetHeightOffset = 1.5f;
    #endregion

    #region Internal State Variables
    private Vector3 _cameraVelocity;
    private Vector3 _cameraObjectPosition;
    private float _leftAndRightLookAngle;
    private float _upAndDownLookAngle;
    private float _cameraZPosition;
    private float _targetCameraZPosition;
    private Vector2 _cameraInput;
    
    private float _lostSightTimer = 0f;
    private bool _isCameraLockedOn;
    private float _switchTimer;
    
    private float _currentSmoothTime;
    private Vector3 _previousTargetPosition;
    private float _originalPivotHeight;
    #endregion

    #region Setup & Public Methods
    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        DontDestroyOnLoad(gameObject);
        _cameraZPosition = cameraObject.transform.localPosition.z;
        
        if (playerTarget != null) 
            _previousTargetPosition = playerTarget.position; 

        if (cameraPivotTransform != null) 
            _originalPivotHeight = cameraPivotTransform.localPosition.y;
    }

    public void SetMouseSensitivity(float newSensitivity)
    {
        mouseSensitivityMultiplier = newSensitivity;
    }

    public void SetControllerSensitivity(float newSensitivity)
    {
        controllerSensitivityMultiplier = newSensitivity;
    }
    #endregion

    #region Main Update Loop
    public void HandleAllCameraActions(Vector2 input, bool isMouseInput = false)
    {
        if (Time.deltaTime == 0f) return;
        
        _cameraInput = input;

        // Tick down the target switch cooldown every frame
        if (_switchTimer > 0)
        {
            _switchTimer -= Time.deltaTime;
        }

        if (playerTarget != null)
        {
            HandleFollowTarget();
            HandleTargetDisconnection(); 
            HandleTargetSwitching(input, isMouseInput); 
            CheckTargetLineOfSight();
            HandleRotations(isMouseInput);
            HandleCollisions();
            HandleLockOnUI();
        }
    }
    #endregion

    #region Movement & Collision
    private void HandleFollowTarget()
    {
        Vector3 targetMovementDelta = playerTarget.position - _previousTargetPosition;
        float currentTargetSpeed = targetMovementDelta.magnitude / Time.deltaTime;
        float speedPercent = Mathf.Clamp01(currentTargetSpeed / 9f); 

        Vector3 moveDirection = targetMovementDelta.normalized;
        float forwardAlignment = Mathf.Abs(Vector3.Dot(transform.forward, moveDirection));

        float dynamicMaxSmoothTime = Mathf.Lerp(maxSmoothTimeSideways, maxSmoothTimeForward, forwardAlignment);
        float desiredSmoothTime = Mathf.Lerp(baseSmoothTime, dynamicMaxSmoothTime, speedPercent);

        _currentSmoothTime = Mathf.Lerp(_currentSmoothTime, desiredSmoothTime, lagTransitionSpeed * Time.deltaTime);
        transform.position = Vector3.SmoothDamp(transform.position, playerTarget.position, ref _cameraVelocity, _currentSmoothTime);

        _previousTargetPosition = playerTarget.position;
    }

    private void HandleCollisions()
    {
        _targetCameraZPosition = _cameraZPosition;
        RaycastHit hit;
        Vector3 direction = cameraObject.transform.position - cameraPivotTransform.position;
        direction.Normalize();

        if (Physics.SphereCast(cameraPivotTransform.position, cameraCollisionRadius, direction, out hit, Mathf.Abs(_targetCameraZPosition), collideWithLayers))
        {
            float distanceFromHitObject = Vector3.Distance(cameraPivotTransform.position, hit.point);
            _targetCameraZPosition = -(distanceFromHitObject - cameraCollisionRadius);
        }

        if (Mathf.Abs(_targetCameraZPosition) < cameraCollisionRadius)
        {
            _targetCameraZPosition = -cameraCollisionRadius;
        }

        _cameraObjectPosition.z = Mathf.Lerp(cameraObject.transform.localPosition.z, _targetCameraZPosition, 0.2f);
        cameraObject.transform.localPosition = _cameraObjectPosition;
    }
    #endregion

    #region Rotation
    private void HandleRotations(bool isMouseInput)
    {
        // LOCK ON ROTATION
        if (currentLockOnTarget != null)
        {
            Vector3 enemyPosition = GetLockOnTargetPosition(currentLockOnTarget);
            Vector3 playerPosition = playerTarget.position;

            float distanceToTarget = Vector3.Distance(playerPosition, enemyPosition);
            float distancePercent = Mathf.InverseLerp(closeFramingDistance, farFramingDistance, distanceToTarget);

            float dynamicVerticalOffset = Mathf.Lerp(closeVerticalOffset, farVerticalOffset, distancePercent);
            float dynamicPivotHeight = Mathf.Lerp(closePivotHeight, _originalPivotHeight, distancePercent);

            Vector3 pivotPos = cameraPivotTransform.localPosition;
            pivotPos.y = Mathf.Lerp(pivotPos.y, dynamicPivotHeight, 5f * Time.deltaTime);
            cameraPivotTransform.localPosition = pivotPos;

            Vector3 lookAtPoint = Vector3.Lerp(playerPosition, enemyPosition, 0.7f);
            lookAtPoint.y += dynamicVerticalOffset;

            Vector3 targetDirection = lookAtPoint - transform.position;
            targetDirection.Normalize();
            targetDirection.y = 0; 

            if (targetDirection != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, lockOnTrackingSpeed * Time.deltaTime);
            }

            Vector3 pivotDirection = lookAtPoint - cameraPivotTransform.position;
            pivotDirection.Normalize();

            if (pivotDirection != Vector3.zero)
            {
                Quaternion pivotTargetRotation = Quaternion.LookRotation(pivotDirection);
                Vector3 eulerAngle = pivotTargetRotation.eulerAngles;
                eulerAngle.y = 0;
                eulerAngle.z = 0;
                cameraPivotTransform.localRotation = Quaternion.Slerp(cameraPivotTransform.localRotation, Quaternion.Euler(eulerAngle), lockOnTrackingSpeed * Time.deltaTime);
            }

            _leftAndRightLookAngle = transform.eulerAngles.y;
            float currentPivotX = cameraPivotTransform.localEulerAngles.x;
            if (currentPivotX > 180) currentPivotX -= 360; 
            _upAndDownLookAngle = currentPivotX;
            
            return; 
        }

        // NORMAL ROTATION
        float leftRightRotationAmount = 0f;
        float upDownRotationAmount = 0f;

        if (isMouseInput)
        {
            float baseMouseScalar = 0.1f; 
            leftRightRotationAmount = _cameraInput.x * mouseSensitivityMultiplier * baseMouseScalar;
            upDownRotationAmount = _cameraInput.y * mouseSensitivityMultiplier * baseMouseScalar;
        }
        else
        {
            leftRightRotationAmount = _cameraInput.x * leftAndRightRotationSpeed * controllerSensitivityMultiplier * Time.deltaTime;
            upDownRotationAmount = _cameraInput.y * upAndDownRotationSpeed * controllerSensitivityMultiplier * Time.deltaTime;
        }

        _leftAndRightLookAngle += leftRightRotationAmount;
        _upAndDownLookAngle -= upDownRotationAmount;
        _upAndDownLookAngle = Mathf.Clamp(_upAndDownLookAngle, minimumPivot, maximumPivot);

        Vector3 cameraRotation = Vector3.zero;
        cameraRotation.y = _leftAndRightLookAngle;
        transform.rotation = Quaternion.Euler(cameraRotation);

        cameraRotation = Vector3.zero;
        cameraRotation.x = _upAndDownLookAngle;
        cameraPivotTransform.localRotation = Quaternion.Euler(cameraRotation);
    }
    #endregion

    #region Lock-On System
    public bool FindLockOnTarget()
    {
        Collider[] colliders = Physics.OverlapSphere(playerTarget.position, maximumLockOnDistance, enemyLayer);
        
        float bestScore = Mathf.Infinity;
        Transform bestTarget = null;
        Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);

        foreach (Collider collider in colliders)
        {
            Transform potentialTarget = collider.transform;
            Vector3 targetPos = GetLockOnTargetPosition(potentialTarget); 
            Vector3 screenPos = cameraObject.WorldToScreenPoint(targetPos);

            if (screenPos.z > 0)
            {
                Vector3 origin = cameraObject.transform.position;
                Vector3 directionToTarget = targetPos - origin;
                float distanceToTarget = directionToTarget.magnitude;

                if (Physics.Raycast(origin, directionToTarget, out RaycastHit hit, distanceToTarget, environmentLayer)) continue; 

                float worldDistance = Vector3.Distance(playerTarget.position, potentialTarget.position);
                float screenDistance = Vector2.Distance(screenCenter, new Vector2(screenPos.x, screenPos.y));
                float targetScore = screenDistance + (worldDistance * 10f); 

                if (targetScore < bestScore)
                {
                    bestScore = targetScore;
                    bestTarget = potentialTarget;
                }
            }
        }

        if (bestTarget != null)
        {
            currentLockOnTarget = bestTarget;
            _isCameraLockedOn = true;
            if (lockOnReticle != null) lockOnReticle.SetActive(true);
            return true; 
        }

        return false; 
    }

    public void ClearLockOnTarget()
    {
        currentLockOnTarget = null;
        _isCameraLockedOn = false;
        _lostSightTimer = 0f; 
        
        if (lockOnReticle != null) lockOnReticle.SetActive(false);

        // NOTE: Make sure your PlayerMovement script handles this correctly
        PlayerMovement pm = playerTarget.GetComponentInParent<PlayerMovement>();
        if (pm != null)
        {
            pm.isTargetLocked = false;
        }
    }

    private void HandleLockOnUI()
    {
        if (currentLockOnTarget != null && lockOnReticle != null)
        {
            Vector3 targetWorldPos = GetLockOnTargetPosition(currentLockOnTarget);
            Vector3 targetScreenPos = cameraObject.WorldToScreenPoint(targetWorldPos);
            lockOnReticle.transform.position = targetScreenPos;
        }
    }

    private void HandleTargetSwitching(Vector2 input, bool isMouseInput)
    {
        if (currentLockOnTarget == null) return;
        if (_switchTimer > 0) return;

        float currentThreshold = isMouseInput ? mouseSwitchThreshold : controllerSwitchThreshold;

        bool flickedLeft = input.x < -currentThreshold;
        bool flickedRight = input.x > currentThreshold;

        if (!flickedLeft && !flickedRight) return;

        Collider[] colliders = Physics.OverlapSphere(playerTarget.position, maximumLockOnDistance, enemyLayer);
        Transform bestTarget = null;
        float shortestDistance = Mathf.Infinity;
        Vector3 currentTargetScreenPos = cameraObject.WorldToScreenPoint(currentLockOnTarget.position);

        foreach (Collider collider in colliders)
        {
            Transform potentialTarget = collider.transform;
            if (potentialTarget == currentLockOnTarget) continue;

            Vector3 potentialTargetScreenPos = cameraObject.WorldToScreenPoint(potentialTarget.position);

            if (potentialTargetScreenPos.z > 0) 
            {
                Vector3 origin = cameraObject.transform.position;
                Vector3 targetCenter = collider.bounds.center;
                Vector3 directionToTarget = targetCenter - origin;
                
                if (Physics.Raycast(origin, directionToTarget, out RaycastHit hit, directionToTarget.magnitude, environmentLayer)) continue;

                float distanceFromCurrentTarget = Vector2.Distance(currentTargetScreenPos, potentialTargetScreenPos);

                if (flickedLeft && potentialTargetScreenPos.x < currentTargetScreenPos.x)
                {
                    if (distanceFromCurrentTarget < shortestDistance)
                    {
                        shortestDistance = distanceFromCurrentTarget;
                        bestTarget = potentialTarget;
                    }
                }
                else if (flickedRight && potentialTargetScreenPos.x > currentTargetScreenPos.x)
                {
                    if (distanceFromCurrentTarget < shortestDistance)
                    {
                        shortestDistance = distanceFromCurrentTarget;
                        bestTarget = potentialTarget;
                    }
                }
            }
        }

        if (bestTarget != null)
        {
            currentLockOnTarget = bestTarget;
            _switchTimer = targetSwitchCooldown;
        }
    }
    
    private void CheckTargetLineOfSight()
    {
        if (currentLockOnTarget == null)
        {
            _lostSightTimer = 0f;
            return;
        }

        Vector3 lockOnTargetPosition = GetLockOnTargetPosition(currentLockOnTarget);
        Vector3 origin = cameraObject.transform.position;
        Vector3 directionToTarget = lockOnTargetPosition - origin;

        if (Physics.Raycast(origin, directionToTarget, out RaycastHit hit, directionToTarget.magnitude, environmentLayer))
        {
            _lostSightTimer += Time.deltaTime;
            if (_lostSightTimer >= timeBeforeLockOnBreaks)
            {
                ClearLockOnTarget();
            }
        }
        else
        {
            _lostSightTimer = 0f;
        }
    }

    private Vector3 GetLockOnTargetPosition(Transform target)
    {
        if (target == null) return Vector3.zero;

        Collider targetCollider = target.GetComponent<Collider>();
        if (targetCollider != null)
        {
            return targetCollider.bounds.center + (Vector3.up * (targetCollider.bounds.extents.y * 0.5f));
        }

        return target.position + (Vector3.up * targetHeightOffset);
    }

    private void HandleTargetDisconnection()
    {
        if (_isCameraLockedOn)
        {
            bool targetIsDead = currentLockOnTarget == null || 
                                !currentLockOnTarget.gameObject.activeInHierarchy || 
                                !currentLockOnTarget.GetComponent<Collider>().enabled;

            if (targetIsDead)
            {
                bool foundNewTarget = FindLockOnTarget();
                if (!foundNewTarget)
                {
                    ClearLockOnTarget();
                }
            }
        }
    }
    #endregion
}