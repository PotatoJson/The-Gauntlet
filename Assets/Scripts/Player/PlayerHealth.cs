using UnityEngine;
using System;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using System.Collections;
using Unity.VisualScripting;

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

    [Header("Potion Settings")]
    public int MaxPotions = 5;
    public int CurrentPotions;
    public float PotionHealAmount = 35f;
    public event Action<int, int> OnPotionCountChanged;

    [Header("VFX")]
    [Tooltip("Drag your healing VFX prefab here.")]
    [SerializeField] private GameObject healingVfxPrefab;
    [Tooltip("Optional: Drag a specific transform here (like the chest/feet). If empty, spawns at the player's base.")]
    [SerializeField] private Transform vfxSpawnPoint;

    [Header("Haptic Feedback")]
    [Tooltip("Low frequency motor (left side). Heavy, deep rumble.")]
    [Range(0f, 1f)] [SerializeField] private float damageRumbleLow = 0.5f;
    
    [Tooltip("High frequency motor (right side). Sharp, light vibration.")]
    [Range(0f, 1f)] [SerializeField] private float damageRumbleHigh = 0.8f;
    
    [Tooltip("How long the controller vibrates when taking damage.")]
    [SerializeField] private float damageRumbleDuration = 0.25f;

    // Events for UI or other systems to subscribe to
    public event Action<float, float> OnHealthChanged; // _currentHealth, maxHealth
    public event Action<float, float> OnPoiseChanged; //CurrentPoise, MaxPoise
    public event Action OnPlayerDeath;

    public bool IsDead => _currentHealth <= 0;
    public float HealthPercentage => _currentHealth / maxHealth;
    public float CurrentHealth => _currentHealth;

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

        Gamepad gamepad = Gamepad.current;
        if (gamepad != null)
        {
            gamepad.SetMotorSpeeds(0f, 0f);
        }
    }

    private void Start()
    {
        CurrentPotions = MaxPotions;
        _currentHealth = maxHealth;
        CurrentPoise = 0;
        OnPoiseChanged?.Invoke(CurrentPoise, _maxPoise);
        OnHealthChanged?.Invoke(_currentHealth, maxHealth);
        OnPotionCountChanged?.Invoke(CurrentPotions, MaxPotions);
    }

    private void Update()
    {
        HandleInvincibility();
        HandlePoiseRecovery();
        HandleHealthRecovery();
    }

#region StatChangesFromGems
    private void HandleMaxPoiseChange()
    {
        _maxPoise = _statsManager.CurrentMaxPoise;
        OnPoiseChanged?.Invoke(CurrentPoise, _maxPoise);
    }

    private void HandleMaxHealthChange()
    {
        Debug.Log("Pre Health gem - " + maxHealth);
        float newMax = _statsManager.CurrentMaxHealth;
        float differnece = newMax - maxHealth;

        maxHealth = newMax;

        if(differnece > 0) _currentHealth += differnece;
        else _currentHealth = Mathf.Min(_currentHealth, maxHealth);
        Debug.Log("Post Health gem - " + maxHealth);
        OnHealthChanged?.Invoke(_currentHealth, maxHealth);
    }
#endregion

#region PotionHandling
    public bool TryConsumePotion()
    {
        if(CurrentPotions > 0 && _currentHealth < maxHealth)
        {
            CurrentPotions--;

            OnPotionCountChanged?.Invoke(CurrentPotions, MaxPotions);

            return true;
        }
        return false;
    }

    public void AddPotion()
    {
        // Only add a potion if we aren't already at the maximum limit!
        if (CurrentPotions < MaxPotions)
        {
            CurrentPotions++;

            // Tell the UI to draw a new potion bottle!
            OnPotionCountChanged?.Invoke(CurrentPotions, MaxPotions);

            Debug.Log($"Debug: Added 1 Potion! Total: {CurrentPotions}");
        }
        else
        {
            Debug.Log("Debug: Potion count is already at max!");
        }
    }

    public void ExecutePotionHeal()
    {
        Heal(PotionHealAmount);
        Debug.Log($"Healed Potions remaining: {CurrentPotions}");

        _stateManager.SetInCombat();

        if (healingVfxPrefab != null)
        {
            // Figure out where to spawn it (use the custom point, or just the player's position)
            Transform spawnLocation = (vfxSpawnPoint != null) ? vfxSpawnPoint : transform;

            // Spawn the prefab as a child of the player so it moves WITH the player!
            GameObject spawnedVFX = Instantiate(healingVfxPrefab, spawnLocation.position, spawnLocation.rotation, transform);
        }

    }
#endregion

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

    public void TakeDamage(float damage, int poiseDamage = 0, GameObject attacker = null)
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

        TriggerHaptics(damageRumbleLow, damageRumbleHigh, damageRumbleDuration);

        OnHealthChanged?.Invoke(_currentHealth, maxHealth);
        Debug.Log("TakeDamage Test " + _currentHealth);
        if (_currentHealth <= 0)
        {
            if (MetricsTracker.Instance != null)
            {
                // Fallback to "Unknown" if the attacker wasn't passed or was a trap
                string killerName = attacker != null ? attacker.name : "Unknown/Environment";
                
                // Clean up clone tags in the name (e.g., "BasicEnemy(Clone)" -> "BasicEnemy")
                killerName = killerName.Replace("(Clone)", "").Trim(); 

                MetricsTracker.Instance.EndChamber(Mathf.RoundToInt(_currentHealth), true, killerName);
            }
            HandleDeath();
            return;
        }

        if (poiseDamage > 0)
        {
            HandleStagger(poiseDamage);
        }
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
        _animator.Play("Empty", 1);
        _animator.SetTrigger("KnockbackHit");
        _stateManager.CurrentLungeSpeed = -15;
    }

    private void TriggerLargeStumble()
    {
        _stateManager.SetPlayerState(PlayerState.Staggered);
        _animator.Play("Empty", 1);
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
        // 1. Turn off the Character Controller so the player is frozen in place where they died
        CharacterController cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        // (Optional) If you have a death animation, trigger it here!
        // _animator.SetTrigger("Die");


        // 3. Trigger the screen falling apart and the Game Over menu!
        DeathScreenShatter.Instance.TriggerDeathShatter();

        Debug.Log("Player died. Awaiting Restart...");

        //transform.position = PlayerSpawn.transform.position;
        //if (cc != null) cc.enabled = true; // Re-enable the Character Controller so it can move again after respawn
        //if (playerCamera != null) playerCamera.SnapToTarget();

        // Notice we completely deleted the transform.position teleport code!
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

        if (PlayerCamera.Instance != null) PlayerCamera.Instance.SnapToTarget();
    }

    #region Haptics
    private void TriggerHaptics(float lowFrequency, float highFrequency, float duration)
    {
        // Check if there is an active gamepad connected
        Gamepad gamepad = Gamepad.current;
        if (gamepad != null)
        {
            // Start the motors
            gamepad.SetMotorSpeeds(lowFrequency, highFrequency);
            
            // Start a coroutine to stop the motors after 'duration'
            StartCoroutine(StopHapticsCoroutine(gamepad, duration));
        }
    }

    private IEnumerator StopHapticsCoroutine(Gamepad gamepad, float duration)
    {
        // Using WaitForSecondsRealtime ensures the rumble stops even if Time.timeScale == 0 (e.g., hit pause or pausing the game)
        yield return new WaitForSecondsRealtime(duration);

        // Double check if the gamepad is still connected/current before stopping
        if (gamepad != null)
        {
            gamepad.SetMotorSpeeds(0f, 0f);
        }
    }
    #endregion

}