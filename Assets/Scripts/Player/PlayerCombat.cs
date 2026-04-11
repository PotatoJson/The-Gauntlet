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
    [Header("Universal Combo Tree")]
    public AttackNode StartingLightAttack;
    public AttackNode StartingHeavyAttack;

    [Header("References")]
    public GauntletData LeftGauntletData;
    public GauntletData RightGauntletData;
    private PlayerManager _stateManager;
    private Animator _animator;
    private PlayerControls _input;

    [Header("Active Weapon (Wrapper)")]
    private RunTimeGauntlet _leftGauntlet;
    private RunTimeGauntlet _rightGauntlet;

    [Header("Combat Tracking")]
    private AttackNode _currentAttackNode;
    private bool _canCombo = false;

    [Header("Player Stats")]
    public int CurrentStamina = 100;
    public int MaxStamina = 100;
    
    [Header("Input Buffer Things")]
    public float BufferDuration;
    private CombatInput _currentBuffer = CombatInput.None;
    private float BufferTimer = 0f;    
    
    void Awake()
    {
        _animator = GetComponent<Animator>();
        _stateManager = GetComponent<PlayerManager>();

        _input = new PlayerControls();

        _input.Player.LightAttack.started += ctx => OnLightAttackInput();
        _input.Player.HeavyAttack.started += ctx => OnHeavyAttackInput();
    }

    private void OnEnable()
    {
        _input.Enable();
    }
    private void OnDisable()
    {
        _input.Disable();
    }
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
        HandleInputBuffer();
        ProcessCombatLogic();
    }

    private void OnHeavyAttackInput()
    {
        _currentBuffer = CombatInput.Heavy;
        BufferTimer = BufferDuration;
    }

    private void OnLightAttackInput()
    {
        _currentBuffer = CombatInput.Light;
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

    private void ProcessCombatLogic()
    {
        if(_currentBuffer == CombatInput.None) return;
        PlayerState currentState = _stateManager.GetCurrentState();

        if(currentState == PlayerState.Idle || currentState == PlayerState.Moving)
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

        CurrentStamina -= node.StaminaCost;
        _currentAttackNode = node;
        _canCombo = false;
        ConsumeBuffer();

        _stateManager.SetPlayerState(PlayerState.Attacking);
        _animator.SetTrigger(node.AnimationTrigger);
    }

    /* Animation Events */
    public void ActivateHitbox(int handID)
    {
        RunTimeGauntlet activeWeapon = _leftGauntlet;

        if(_currentAttackNode.StrikingHand == StrikeHand.Right)
        {
            activeWeapon = _rightGauntlet;
        }
        else if(_currentAttackNode.StrikingHand == StrikeHand.Both)
        {
            //future dual hand attack
        }
        int currntDamage = activeWeapon.GetCurrentDamage();
        int currentPoise = activeWeapon.GetCurrentPoise();

        //need to add the actual hitbox
    }

    public void DeactivateHitbox()
    {
        //turn off collider TODO later
    }

    public void OpenComboWindow()
    {
        _canCombo = true;
    }

    public void EndAttack()
    {
        _currentAttackNode = null;
        _canCombo = false;

        _stateManager.SetPlayerState(PlayerState.Idle);
    }
}
