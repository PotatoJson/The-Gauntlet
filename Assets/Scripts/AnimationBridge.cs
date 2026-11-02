using UnityEngine;

public class AnimationBridge : MonoBehaviour
{
    private Animator _animator;
    private PlayerMovement _movement;
    private CharacterController _controller;
    private PlayerManager _stateManager;

    void Start()
    {
        _animator = GetComponentInChildren<Animator>();
        _movement = GetComponent<PlayerMovement>();
        _controller = GetComponent<CharacterController>();
        _stateManager = GetComponent<PlayerManager>();
    }

    void Update()
    {
        float moveX = 0f;
        float moveZ = 0f;

        // A disabled controller keeps reporting its last velocity, which would leave the player jogging on the spot.
        Vector3 velocity = _controller.enabled ? _controller.velocity : Vector3.zero;

        if (_movement.isTargetLocked)
        {
            Vector3 localVelocity = transform.InverseTransformDirection(velocity);
            moveX = localVelocity.x / _movement.walkSpeed;
            moveZ = localVelocity.z / _movement.walkSpeed;
        }
        else
        {
            Vector3 horizontalVelocity = new Vector3(velocity.x, 0, velocity.z);

            moveZ = horizontalVelocity.magnitude / _movement.walkSpeed;
            
            moveX = 0f; 
        }

        _animator.SetFloat("MoveX", moveX, 0.1f, Time.deltaTime);
        _animator.SetFloat("MoveZ", moveZ, 0.1f, Time.deltaTime);
        
        float currentSpeed = velocity.magnitude;
        _animator.SetFloat("Speed", currentSpeed, 0.1f, Time.deltaTime);
        _animator.SetBool("IsGrounded", _controller.isGrounded);

        bool isFalling = !_controller.isGrounded && velocity.y < -2f && !_movement.IsRolling;
        _animator.SetBool("IsFalling", isFalling);

        
    }

    public void PlayAttack(string moveName){
        _animator.CrossFadeInFixedTime(moveName, 0.15f);
    }
    
    public void PlayRoll() {
        _animator.CrossFadeInFixedTime("Roll", 0.1f);
    }

    public void PlayBlock(float animPoint){
        _animator.Play("Standing Block", 1, animPoint);
    }

    public void StopBlock(){
        _animator.Play("EmptyState", 1, 0f);
    }

    public void BackToLocomotion(){
        _animator.CrossFadeInFixedTime("Locomotion", 0.25f); 
    }

    public void HitStopAnim(){
        _animator.speed = 0;
    }

    public void ResumeAnim(){
        _animator.speed = 1;
    }

    public void TriggerJump()
    {
        _animator.SetTrigger("Jump");
    }
}