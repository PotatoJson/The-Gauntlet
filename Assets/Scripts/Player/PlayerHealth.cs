using UnityEngine;
using System;
using UnityEngine.SceneManagement;

public class PlayerHealth : MonoBehaviour
{
    private PlayerStatsManager _statsManager;
    

    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;
    private float _currentHealth;
    [Tooltip("How much health recovers per second after the delay.")]
    [SerializeField] private float healthRecoveryRate = 1f; // 1 HP per second = REAL slow

    [Tooltip("How long to wait after taking damage before health starts recovering.")]
    [SerializeField] private float healthRecoveryDelay = 5.0f; // 5 seconds of safety needed
    private float healthRecoveryTimer = 0f;

    [Header("Defense & Posture")]
    public bool isBlocking;

    [Header("Invincibility")]
    [SerializeField] private float invincibilityDuration = 0.5f;
    private float invincibilityTimer;
    private bool isInvincible;

    [Header("References")]
    private PlayerManager _stateManager;
    private Animator _animator;

    [Header("Poise")]
    public float _maxPoise;
    public float CurrentPoise;
    //public Slider PoiseBar; //If we want to visualize the poise and stagger on the player
    public float PoiseRecoveryRate;
    public float PoiseRecoveryDelay;
    private float PoiseRecoveryTimer;

    [Header("Stagger stuff")]//these will be used to trigger a large knockback throwing the player
    public float InstantKnockback;
    public float OvercapKnockback;

    // Events for UI or other systems to subscribe to
    public event Action<float, float> OnHealthChanged; // _currentHealth, maxHealth
    public event Action<float, float> OnPoiseChanged; //CurrentPoise, MaxPoise
    public event Action OnPlayerDeath;

    public bool IsDead => _currentHealth <= 0;
    public float HealthPercentage => _currentHealth / maxHealth;

    public GameObject LastCheckPoint;
    public GameObject PlayerSpawn;

    private void Awake()
    {
        _statsManager = GetComponent<PlayerStatsManager>();
        _stateManager = GetComponent<PlayerManager>();
        _animator = GetComponentInChildren<Animator>();
    }

    private void OnEnable()
    {
        if(_statsManager != null)
        {
            _statsManager.OnStatsCalculated += HandleMaxHealthChange;  
            _statsManager.OnStatsCalculated += HandleMaxPoiseChange;
        } 

    }

    private void OnDisable()
    {
        if(_statsManager != null)
        {
            _statsManager.OnStatsCalculated -= HandleMaxHealthChange;
            _statsManager.OnStatsCalculated += HandleMaxPoiseChange;
        }
    }

    private void Start()
    {
        _currentHealth = maxHealth;
        CurrentPoise = 0;
        OnPoiseChanged?.Invoke(CurrentPoise, _maxPoise);
        OnHealthChanged?.Invoke(_currentHealth, maxHealth);
    }

    private void HandleMaxPoiseChange()
    {
        _maxPoise = _statsManager.CurrentMaxPoise;
        OnPoiseChanged?.Invoke(CurrentPoise, _maxPoise);
    }

    private void HandleMaxHealthChange()
    {
        float newMax = _statsManager.CurrentMaxHealth;
        float differnece = newMax - maxHealth;

        maxHealth = newMax;

        if(differnece > 0) _currentHealth += differnece;
        else _currentHealth = Mathf.Min(_currentHealth, maxHealth);

        OnHealthChanged?.Invoke(_currentHealth, maxHealth);
    }


    private void Update()
    {
        HandleInvincibility();
        HandlePoiseRecovery();
        HandleHealthRecovery();
    }

    #region UpdateClarity
    private void HandleInvincibility()
    {
        if (isInvincible)
        {
            invincibilityTimer -= Time.deltaTime;
            if (invincibilityTimer <= 0) isInvincible = false;
        }
    }

    private void HandlePoiseRecovery()
    {
        if (CurrentPoise > 0)
        {
            if (PoiseRecoveryTimer > 0)
            {
                PoiseRecoveryTimer -= Time.deltaTime;
            }
            else
            {
                CurrentPoise -= PoiseRecoveryRate * Time.deltaTime;
                CurrentPoise = Mathf.Max(0, CurrentPoise);
                OnPoiseChanged?.Invoke(CurrentPoise, _maxPoise);
            }
        }
    }

    private void HandleHealthRecovery()
    {
        if (_currentHealth < maxHealth && !IsDead)
        {
            if (healthRecoveryTimer > 0)
            {
                healthRecoveryTimer -= Time.deltaTime;
            }
            else
            {
                _currentHealth += healthRecoveryRate * Time.deltaTime;
                _currentHealth = Mathf.Min(_currentHealth, maxHealth);
                OnHealthChanged?.Invoke(_currentHealth, maxHealth);
            }
        }
    }
    #endregion

    public void TakeDamage(float damage/*, int poiseDamage*/, GameObject attacker = null)
    {
        Debug.Log("TakeDamage Test");
        if (IsDead || isInvincible) return;

        // ... Existing Parry/Block/Dodge logic would go here if uncommented ...
        _currentHealth -= damage;
        _currentHealth = Mathf.Max(_currentHealth, 0);

        healthRecoveryTimer = healthRecoveryDelay;

        // Brief invincibility to prevent multiple hits from same attack
        isInvincible = true;
        invincibilityTimer = invincibilityDuration;

        OnHealthChanged?.Invoke(_currentHealth, maxHealth);
        Debug.Log("TakeDamage Test " + _currentHealth);
        if (_currentHealth <= 0)
        {
            HandleDeath();
            return;
        }
        //HandleStagger(poiseDamage);
    }

    #region Stagger Handling 
    private void HandleStagger(int poiseDamage)
    {
        //see if player gets thrown from attack
        bool isKnockBack = poiseDamage >= InstantKnockback || (CurrentPoise + poiseDamage) >= (_maxPoise + OvercapKnockback);
        //check for large stagger for player stumble+staggered state
        bool isHeavyStagger = (CurrentPoise + poiseDamage) >= _maxPoise;

        if (isKnockBack)
        {
            CurrentPoise = 0;
            TriggerKnockback();
        }
        else if (isHeavyStagger)
        {
            CurrentPoise = 0;
            TriggerLargeStumble();
        }
        else
        {
            CurrentPoise += poiseDamage;
            TriggerSmallFlinch();
        }

        OnPoiseChanged?.Invoke(CurrentPoise, _maxPoise);
    }

    private void TriggerKnockback()
    {
        _stateManager.SetPlayerState(PlayerState.Staggered);
        _animator.SetTrigger("KnockbackHit");
        _stateManager.CurrentLungeSpeed = -20;
    }

    private void TriggerLargeStumble()
    {
        _stateManager.SetPlayerState(PlayerState.Staggered);
        _animator.SetTrigger("LargeStumble");
        _stateManager.CurrentLungeSpeed = -4f;
    }

    private void TriggerSmallFlinch()
    {//this doesn't change state at all just some extra feedback for player
        _animator.SetTrigger("SmallFlinch");
    }

    #endregion

    private void HandleDeath()
    {
        CharacterController cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        transform.position = PlayerSpawn.transform.position;
        if (cc != null) cc.enabled = true;
        Debug.Log("Death Test");
    }

    public void Heal(float amount)
    {
        //if (IsDead) return;

        _currentHealth += amount;
        _currentHealth = Mathf.Min(_currentHealth, maxHealth);

        OnHealthChanged?.Invoke(_currentHealth, maxHealth);
    }

    public void PlayerRespawnSpikes()
    {
        CharacterController cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false; // Turn it off so it stops "holding" the position

        transform.position = LastCheckPoint.transform.position;

        if (cc != null) cc.enabled = true; // Turn it back on
    }

}