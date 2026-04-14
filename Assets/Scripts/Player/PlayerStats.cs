using UnityEngine;
using UnityEngine.UI;

public enum HitReactionType
{
    Light,
    Heavy,
    None
}

public class PlayerStats : MonoBehaviour
{
    [Header("References")]
    private PlayerManager _stateManager;
    private Animator _animator;

    [Header("Health")]
    public int MaxHealth;
    public int CurrentHealth;
    public Slider HealthBar; 

    [Header("Stamina")]
    public int MaxStamina;
    public int CurrentStamina;
    public Slider StaminaBar;

    [Header("Poise")]
    public int StunMax;
    public float CurrentPoise;
    public Slider PoiseBar; //If we want to visualize the poise and stagger on the player
    public float PoiseDecayDelay;
    public int PoiseDecayRate;
    private float _timeSinceLastHit;

    void Awake()
    {
        _stateManager.GetComponent<PlayerManager>();
        _animator = GetComponentInChildren<Animator>();
    }
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        CurrentHealth = MaxHealth;
        CurrentStamina = MaxStamina;
        CurrentPoise = 0;
        //UpdateUI();
    }

    void Update()
    {
        //poise decay
        _timeSinceLastHit += Time.deltaTime;

        if (_timeSinceLastHit >= PoiseDecayDelay && CurrentPoise > 0)
        {
            CurrentPoise -= (float)PoiseDecayRate * Time.deltaTime;
            CurrentPoise = Mathf.Max(CurrentPoise, 0);
        }
    }


    /*public void UpdateUI()
    {
        if(HealthSlider != null) HealthSlider.value = CurrentHealth / MaxHealth;
        if(StaminaSlider != null) StaminaSlider.value = CurrentStamina / MaxStamina;
        //if(PoiseSlider != null) PoiseSlider.value = (float)CurrentPoiseHealth / StunMax;
    }*/

    public void TakeDamage(int damage, int poiseDamage, int HitReactionType)
    {
        CurrentHealth -= damage;
        CurrentPoise += poiseDamage;
        _timeSinceLastHit = 0f;

        if(CurrentPoise >= StunMax)
        {
            TriggerLargeStumble();
            CurrentPoise = 0;
        }
        else
        {
            TriggerSmallFlinch();
        }
        //UpdateUI();
    }

    private void TriggerSmallFlinch()
    {
        _animator.SetTrigger("LightHit");
    }
    private void TriggerLargeStumble()
    {
        _stateManager.SetPlayerState(PlayerState.Staggered);
        _animator.SetTrigger("LargeStumble");
        _stateManager.CurrentLungeSpeed = -4f;
    }
}