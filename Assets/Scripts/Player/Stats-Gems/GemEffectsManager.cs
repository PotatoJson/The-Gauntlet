using UnityEngine;

//This script creates and listens for events that get called then tells
//the appropriate scripts that said event happened
public class GemEffectsManager : MonoBehaviour
{
    [Header("Main References")]
    [SerializeField] private PlayerCombat _combat;
    [SerializeField] private PlayerMovement _movement;
    [SerializeField] private PlayerHealth _healthScript;
    [SerializeField] private PlayerStamina _staminaScript;
    [SerializeField] private HitboxController _leftHitBox;
    [SerializeField] private HitboxController _rightHitBox;
    [SerializeField] private PlayerStatsManager _statsManager;

    [Header("Flags")]
    public bool hasExecutionerGem;
    public bool hasBerserkerGem;

    private void Awake()
    {
        _healthScript = GetComponent<PlayerHealth>(); 
        _staminaScript = GetComponent<PlayerStamina>(); 
    }

    private void OnEnable()
    {
        //sub to events
        BaseEnemy.OnAnyEnemyDied += HandleEnemyDeath;
        if(_healthScript != null) _healthScript.OnHealthChanged += HandleHealthChanged;
    }

    private void OnDisable()
    {
        BaseEnemy.OnAnyEnemyDied -= HandleEnemyDeath;
        if(_healthScript != null) _healthScript.OnHealthChanged -= HandleHealthChanged;
    }

    private void HandleEnemyDeath(BaseEnemy deadEnemy)
    {
        if (_statsManager != null && _statsManager.executionerTotal > 0)
        {
            float staminaToRestore = _statsManager.executionerTotal;
            
            if (_staminaScript != null)
            {
                _staminaScript.RestoreStamina(staminaToRestore);
                Debug.Log($"Executioner Triggered: Restored {staminaToRestore} Stamina");
            }
        }
    }

    private void HandleHealthChanged(float currentHealth, float maxHealth)
    {
        if (_statsManager != null && _statsManager.retaliationTotal > 0)
        {
            float healthPercentage = currentHealth / maxHealth;

            if (healthPercentage <= 0.3f)
            {
                // Dynamic Multiplier: (1 + 0.4 = 1.4x)
                float activeMult = 1f + _statsManager.retaliationTotal;
                
                if (_statsManager.berserkerDamageMultiplier != activeMult)
                {
                    _statsManager.berserkerDamageMultiplier = activeMult;
                    _statsManager.RecalculateGlobalStats();
                    Debug.Log($"Retaliation Active! Multiplier: {activeMult}x");
                }
            }
            else if (_statsManager.berserkerDamageMultiplier != 1f)
            {
                _statsManager.berserkerDamageMultiplier = 1f;
                _statsManager.RecalculateGlobalStats();
            }
        }
    }
}
