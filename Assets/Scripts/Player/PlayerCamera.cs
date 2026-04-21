using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCamera : MonoBehaviour
{
    public static PlayerCamera Instance;
    public Transform playerTarget;
    public Transform cameraPivotTransform;
    public Camera cameraObject;

    [Header("Camera Settings")]
    public float cameraSmoothSpeed = 1f;
    public float leftAndRightRotationSpeed = 220f;
    public float upAndDownRotationSpeed = 220f;
    public float minimumPivot = -30f;
    public float maximumPivot = 60f;
    public float cameraCollisionRadius = 0.2f;
    public LayerMask collideWithLayers;

    [Header("Device Sensitivity")]
    [Tooltip("Scales down the raw, high-number mouse delta to match controller speeds.")]
    [Range(0.001f, 0.1f)] 
    [SerializeField] private float mouseSensitivityMultiplier = 0.02f;
    
    [Tooltip("Multiplier for the controller right stick. Leave at 1 for default.")]
    [Range(0.1f, 5f)]
    [SerializeField] private float controllerSensitivityMultiplier = 1f;

    [Header("Camera Values")]
    private Vector3 cameraVelocity;
    private Vector3 cameraObjectPosition;
    private float leftAndRightLookAngle;
    private float upAndDownLookAngle;
    private float cameraZPosition;
    private float targetCameraZPosition;
    private Vector2 cameraInput;

    [Header("Lock-On Settings")]
    public Transform currentLockOnTarget;
    public LayerMask enemyLayer;
    public float maximumLockOnDistance = 30f;
    public float lockOnTrackingSpeed = 15f; // How fast the camera snaps to the enemy
    [Tooltip("How hard you have to flick the stick/mouse to trigger a switch.")]
    public float targetSwitchThreshold = 0.5f; 
    [Tooltip("Prevents the camera from hyper-swapping multiple times in one flick.")]
    public float targetSwitchCooldown = 0.3f;
    [Tooltip("The layers that block the camera's line of sight (e.g., Ground, Default).")]
    public LayerMask environmentLayer;
    [Tooltip("How long an enemy can hide behind a wall before the lock-on breaks.")]
    public float timeBeforeLockOnBreaks = 3f;
    private float _lostSightTimer = 0f;
    private bool _isCameraLockedOn;
    
    private float _switchTimer;

    [Header("UI Settings")]
    public GameObject lockOnReticle;
    [Tooltip("Pushes the reticle up so it targets the chest/head instead of the feet.")]
    public float targetHeightOffset = 1.5f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        DontDestroyOnLoad(gameObject);
        cameraZPosition = cameraObject.transform.localPosition.z;
    }

    public void HandleAllCameraActions(Vector2 input, bool isMouseInput = false)
    {
        cameraInput = input;

        if (playerTarget != null)
        {
            HandleFollowTarget();
            HandleTargetDisconnection();
            HandleTargetSwitching(input);
            CheckTargetLineOfSight();
            HandleRotations(isMouseInput); // Pass the bool here
            HandleCollisions();
            HandleLockOnUI();
        }
    }

    private void HandleFollowTarget()
    {
        Vector3 targetCameraPosition = Vector3.SmoothDamp(transform.position, playerTarget.position, ref cameraVelocity, cameraSmoothSpeed * Time.deltaTime);
        transform.position = targetCameraPosition;
    }

    private void HandleRotations(bool isMouseInput)
    {
        // LOCK ON ROTATION
        if (currentLockOnTarget != null)
        {
            Vector3 lockOnTargetPosition = GetLockOnTargetPosition(currentLockOnTarget);

            // Rotate the main camera parent left/right to face the target
            Vector3 targetDirection = lockOnTargetPosition - transform.position;
            targetDirection.Normalize();
            targetDirection.y = 0; // Keep the parent flat

            if (targetDirection != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, lockOnTrackingSpeed * Time.deltaTime);
            }

            // Rotate the pivot up/down to look at the target's height
            Vector3 pivotDirection = lockOnTargetPosition - cameraPivotTransform.position;
            pivotDirection.Normalize();

            if (pivotDirection != Vector3.zero)
            {
                Quaternion pivotTargetRotation = Quaternion.LookRotation(pivotDirection);
                Vector3 eulerAngle = pivotTargetRotation.eulerAngles;
                eulerAngle.y = 0;
                eulerAngle.z = 0;
                cameraPivotTransform.localRotation = Quaternion.Slerp(cameraPivotTransform.localRotation, Quaternion.Euler(eulerAngle), lockOnTrackingSpeed * Time.deltaTime);
            }

            // Keep internal look angles updated so the camera doesn't snap wildly when you unlock
            leftAndRightLookAngle = transform.eulerAngles.y;
            float currentPivotX = cameraPivotTransform.localEulerAngles.x;
            if (currentPivotX > 180) currentPivotX -= 360; 
            upAndDownLookAngle = currentPivotX;
            
            return; // Exit out of normal rotations
        }

        // NORMAL ROTATION
        float timeStep = isMouseInput ? 1f : Time.deltaTime;
        float deviceSensitivityScale = isMouseInput ? mouseSensitivityMultiplier : controllerSensitivityMultiplier; 

        leftAndRightLookAngle += (cameraInput.x * leftAndRightRotationSpeed * deviceSensitivityScale) * timeStep;
        upAndDownLookAngle -= (cameraInput.y * upAndDownRotationSpeed * deviceSensitivityScale) * timeStep;
        upAndDownLookAngle = Mathf.Clamp(upAndDownLookAngle, minimumPivot, maximumPivot);

        Vector3 cameraRotation = Vector3.zero;
        cameraRotation.y = leftAndRightLookAngle;
        transform.rotation = Quaternion.Euler(cameraRotation);

        cameraRotation = Vector3.zero;
        cameraRotation.x = upAndDownLookAngle;
        cameraPivotTransform.localRotation = Quaternion.Euler(cameraRotation);
    }

    private void HandleCollisions()
    {
        targetCameraZPosition = cameraZPosition;
        RaycastHit hit;
        Vector3 direction = cameraObject.transform.position - cameraPivotTransform.position;
        direction.Normalize();

        if (Physics.SphereCast(cameraPivotTransform.position, cameraCollisionRadius, direction, out hit, Mathf.Abs(targetCameraZPosition), collideWithLayers))
        {
            float distanceFromHitObject = Vector3.Distance(cameraPivotTransform.position, hit.point);
            targetCameraZPosition = -(distanceFromHitObject - cameraCollisionRadius);
        }

        if (Mathf.Abs(targetCameraZPosition) < cameraCollisionRadius)
        {
            targetCameraZPosition = -cameraCollisionRadius;
        }

        cameraObjectPosition.z = Mathf.Lerp(cameraObject.transform.localPosition.z, targetCameraZPosition, 0.2f);
        cameraObject.transform.localPosition = cameraObjectPosition;
    }

    public bool FindLockOnTarget()
    {
        // Find all enemies in range
        Collider[] colliders = Physics.OverlapSphere(playerTarget.position, maximumLockOnDistance, enemyLayer);
        
        float bestScore = Mathf.Infinity;
        Transform bestTarget = null;

        // Find the exact mathematical center of the player's screen
        Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);

        foreach (Collider collider in colliders)
        {
            Transform potentialTarget = collider.transform;
            
            Vector3 targetPos = GetLockOnTargetPosition(potentialTarget); 

            // Convert their 3D world position into a 2D screen coordinate
            Vector3 screenPos = cameraObject.WorldToScreenPoint(targetPos);

            // ONLY consider enemies that are IN FRONT of the camera lens
            if (screenPos.z > 0)
            {
                // Shoot a raycast from the camera to the center of the enemy
                Vector3 origin = cameraObject.transform.position;
                Vector3 directionToTarget = targetPos - origin;
                float distanceToTarget = directionToTarget.magnitude;

                // If the raycast hits something on the environment layer, skip the enemy
                if (Physics.Raycast(origin, directionToTarget, out RaycastHit hit, distanceToTarget, environmentLayer))
                {
                    continue; 
                }
                

                // How far the enemy from the player physically
                float worldDistance = Vector3.Distance(playerTarget.position, potentialTarget.position);

                // How far the enemy from the center of your screen
                float screenDistance = Vector2.Distance(screenCenter, new Vector2(screenPos.x, screenPos.y));

                float targetScore = screenDistance + (worldDistance * 10f); 

                if (targetScore < bestScore)
                {
                    bestScore = targetScore;
                    bestTarget = potentialTarget;
                }
            }
        }

        // 4. Lock onto the winner!
        if (bestTarget != null)
        {
            currentLockOnTarget = bestTarget;
            _isCameraLockedOn = true;
            if (lockOnReticle != null) lockOnReticle.SetActive(true);
            return true; 
        }

        return false; // No valid targets found in front of us
    }

    public void ClearLockOnTarget()
    {
        currentLockOnTarget = null;
        _isCameraLockedOn = false;
        _lostSightTimer = 0f; // Reset timer
        
        if (lockOnReticle != null) lockOnReticle.SetActive(false);

        // Tell the movement script that the target was dropped
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

    private void HandleTargetSwitching(Vector2 input)
    {
        // Only run this if we are currently locked on and the cooldown has finished
        if (currentLockOnTarget == null) return;
        
        if (_switchTimer > 0)
        {
            _switchTimer -= Time.deltaTime;
            return;
        }

        // Check if the input is a hard flick left or right
        bool flickedLeft = input.x < -targetSwitchThreshold;
        bool flickedRight = input.x > targetSwitchThreshold;

        if (!flickedLeft && !flickedRight) return;

        // Find all potential targets in range
        Collider[] colliders = Physics.OverlapSphere(playerTarget.position, maximumLockOnDistance, enemyLayer);
        
        Transform bestTarget = null;
        float shortestDistance = Mathf.Infinity;

        // Where is our current target on the screen
        Vector3 currentTargetScreenPos = cameraObject.WorldToScreenPoint(currentLockOnTarget.position);

        foreach (Collider collider in colliders)
        {
            Transform potentialTarget = collider.transform;

            // Skip if it's the one we are already locked onto
            if (potentialTarget == currentLockOnTarget) continue;

            // Where is this potential target on the screen
            Vector3 potentialTargetScreenPos = cameraObject.WorldToScreenPoint(potentialTarget.position);

            // Make sure the target is actually in front of the camera
            if (potentialTargetScreenPos.z > 0) 
            {
                Vector3 origin = cameraObject.transform.position;
                Vector3 targetCenter = collider.bounds.center;
                Vector3 directionToTarget = targetCenter - origin;
                
                if (Physics.Raycast(origin, directionToTarget, out RaycastHit hit, directionToTarget.magnitude, environmentLayer))
                {
                    continue; // Wall in the way
                }

                // Calculate the 2D distance on the screen between our current target and this new one
                float distanceFromCurrentTarget = Vector2.Distance(currentTargetScreenPos, potentialTargetScreenPos);

                // If we flicked LEFT, we only care about targets whose X screen position is LESS than our current target
                if (flickedLeft && potentialTargetScreenPos.x < currentTargetScreenPos.x)
                {
                    if (distanceFromCurrentTarget < shortestDistance)
                    {
                        shortestDistance = distanceFromCurrentTarget;
                        bestTarget = potentialTarget;
                    }
                }
                // If we flicked RIGHT, we only care about targets whose X screen position is GREATER than our current target
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

        // If we found a valid target in that direction swap to it and start the cooldown
        if (bestTarget != null)
        {
            currentLockOnTarget = bestTarget;
            _switchTimer = targetSwitchCooldown;
        }
    }
    
    private void CheckTargetLineOfSight()
    {
        // Only run this if we actually have a target
        if (currentLockOnTarget == null)
        {
            _lostSightTimer = 0f;
            return;
        }

        Vector3 lockOnTargetPosition = GetLockOnTargetPosition(currentLockOnTarget);

        // Shoot a raycast from the camera lens to the target
        Vector3 origin = cameraObject.transform.position;
        Vector3 directionToTarget = lockOnTargetPosition - origin;

        // Check if we hit a "hard wall" (environmentLayer)
        if (Physics.Raycast(origin, directionToTarget, out RaycastHit hit, directionToTarget.magnitude, environmentLayer))
        {
            // The enemy is hiding. Start ticking the timer up.
            _lostSightTimer += Time.deltaTime;

            if (_lostSightTimer >= timeBeforeLockOnBreaks)
            {
                // Timer maxed out. Break the lock-on
                ClearLockOnTarget();
            }
        }
        else
        {
            // The raycast hit nothing (or hit the enemy). They are visible. Reset the timer.
            _lostSightTimer = 0f;
        }
    }

    private Vector3 GetLockOnTargetPosition(Transform target)
    {
        if (target == null) return Vector3.zero;

        Collider targetCollider = target.GetComponent<Collider>();
        if (targetCollider != null)
        {
            // bounds.center is exactly 50% height.
            // bounds.extents.y is the distance from the center to the very top.
            // Adding half of that distance puts us at exactly 75% height (chest/head level) for ANY sized enemy
            return targetCollider.bounds.center + (Vector3.up * (targetCollider.bounds.extents.y * 0.5f));
        }

        // Fallback for objects without colliders
        return target.position + (Vector3.up * targetHeightOffset);
    }

    private void HandleTargetDisconnection()
    {
        // Only run this if we are actively supposed to be locked on
        if (_isCameraLockedOn)
        {
            // Is it null, deactivated, OR did its collider get turned off
            bool targetIsDead = currentLockOnTarget == null || 
                                !currentLockOnTarget.gameObject.activeInHierarchy || 
                                !currentLockOnTarget.GetComponent<Collider>().enabled;

            if (targetIsDead)
            {
                // Attempt to instantly find the next best target on screen
                bool foundNewTarget = FindLockOnTarget();

                // If nobody else is on screen, completely drop the lock-on
                if (!foundNewTarget)
                {
                    ClearLockOnTarget();
                }
            }
        }
    }
}