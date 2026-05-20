using UnityEngine;
using System.Collections.Generic;
using System;

public class PlayerStatsManager : MonoBehaviour
{
    [Header("Base Stats")]
    public float BaseMaxHealth = 100f;
    public float BaseDamage = 10f;
    public float BaseStamina = 100f;
    public float BasePoiseDamage = 10f;
    public float BaseMaxPoise = 100f;
    public float BaseStaminaRegen = 60f;
    public float BaseStaminaDelay = 3f;
    [HideInInspector] public float berserkerDamageMultiplier = 1f;
    

    [Header("Current Global Stats (Read Only)")]
    public float CurrentMaxHealth {get; private set;}
    public float CurrentDamage {get; private set;}
    public float CurrentStamina {get; private set;}
    public float CurrentPoiseDamage {get; private set;}
    public float CurrentMaxPoise {get; private set;}
    public float CurrentStaminaRegen {get; private set;}
    public float CurrentStaminaDelay {get; private set;}

    public RunTimeGauntlet PrimaryGauntlet;
    public RunTimeGauntlet SecondaryGauntlet;

    [Header("Starter Equipment")]
    public GauntletData DefaultPrimary;
    public GauntletData DefaultSecondary;

    [Header("Dynamic Modifier Values")]
    // These store the SUM of all amounts found on equipped gems
    [HideInInspector] public float retaliationTotal = 0f; 
    [HideInInspector] public float executionerTotal = 0f;

    //Dictionaries for different stats depending on stat bonus type
    private Dictionary<StatModifierType, float> _flatBonuses = new Dictionary<StatModifierType, float>();
    private Dictionary<StatModifierType, float> _percentBonuses = new Dictionary<StatModifierType, float>();

    // no need to link scripts becasue of events
    public event Action OnStatsCalculated;

    private void Start()
    {
        if(PrimaryGauntlet == null && DefaultPrimary != null)
        {
            PrimaryGauntlet = new RunTimeGauntlet(DefaultPrimary, EquipSlot.Primary);
        }
        if(SecondaryGauntlet == null && DefaultSecondary != null)
        {
            SecondaryGauntlet = new RunTimeGauntlet(DefaultSecondary, EquipSlot.Secondary);
        }
        RecalculateGlobalStats();
    }

    public void SyncWithUI(Transform primaryUIParent, Transform secondaryUIParent)
    {
        PrimaryGauntlet = BuildGauntletFromUI(primaryUIParent, EquipSlot.Primary);
        SecondaryGauntlet = BuildGauntletFromUI(secondaryUIParent, EquipSlot.Secondary);
        RecalculateGlobalStats();
    }

    private RunTimeGauntlet BuildGauntletFromUI(Transform uiParent, EquipSlot slotContext)
    {
        GauntletManager gManager = uiParent.GetComponentInChildren<GauntletManager>();
        if(gManager == null || gManager.LinkedGauntletData == null) return null;

        //create the actual gauntlet from data
        RunTimeGauntlet newGauntlet = new RunTimeGauntlet(gManager.LinkedGauntletData, slotContext);

        for (int i = 0; i < gManager.currentActiveSlots; i++)
        {
            Transform slot = gManager.fingerSlots[i].transform;
            DraggableGem gemUI = slot.GetComponentInChildren<DraggableGem>();
            //check for valid gem
            if(gemUI != null && gemUI.LinkedGemData is StatGemData statGem)
            {
                if(newGauntlet.SocketedStatGems != null && i < newGauntlet.SocketedStatGems.Length)
                {
                    newGauntlet.SocketedStatGems[i] = statGem;
                }
                else
                {
                    Debug.LogError($"[Backend Mismatch] UI slot {i} has a gem, but the backend array only has room for {newGauntlet.SocketedStatGems?.Length}! Check RunTimeGauntlet.cs");
                }
            }
        }
        return newGauntlet;
    }

    public void RecalculateGlobalStats()
    {
        _flatBonuses.Clear();
        _percentBonuses.Clear();

        ExtractGemModifiers();
        CalculateOffensiveStats();
        CalculateDefensiveStats();
        CalculateUtilityStats();

        OnStatsCalculated?.Invoke();
    }

    private void ExtractGemModifiers()
    {
        List<GemModifier> allMods = new List<GemModifier>();
        
        if (PrimaryGauntlet != null) allMods.AddRange(PrimaryGauntlet.GetAllActiveModifiers());
        if (SecondaryGauntlet != null) allMods.AddRange(SecondaryGauntlet.GetAllActiveModifiers());

        retaliationTotal = 0f;
        executionerTotal = 0f;
        Debug.Log($"[Backend Check] The Stats Manager found {allMods.Count} total modifiers equipped!");
        foreach (GemModifier mod in allMods)
        {
            if (mod.StatType == StatModifierType.Retaliation)
            {
                retaliationTotal += mod.Amount; // Sums up for multiple gems
                continue; 
            }
            if (mod.StatType == StatModifierType.Executioner)
            {
                executionerTotal += mod.Amount;
                continue;
            }
            
            // Sort them into the correct dictionary based on their Math Type
            if (mod.MathType == ModifierMathType.Flat)
            {
                if (_flatBonuses.ContainsKey(mod.StatType)) _flatBonuses[mod.StatType] += mod.Amount;
                else _flatBonuses.Add(mod.StatType, mod.Amount);
            }
            else if (mod.MathType == ModifierMathType.Percentage)
            {
                // Note: Percentages usually stack additively in RPGs (e.g., two 10% buffs = +20% total, not 1.1 * 1.1)
                // So an "Amount" of 0.1 means +10%
                if (_percentBonuses.ContainsKey(mod.StatType)) _percentBonuses[mod.StatType] += mod.Amount;
                else _percentBonuses.Add(mod.StatType, mod.Amount);
            }
        }
    }

    private void CalculateOffensiveStats()
    {
        // 1. Get Base from Player + Gauntlet
        float primaryWeaponDamage = (PrimaryGauntlet != null && PrimaryGauntlet.BaseGauntlet != null) ? PrimaryGauntlet.BaseGauntlet.Damage : 0f;
        float primaryWeaponPoise = (PrimaryGauntlet != null && PrimaryGauntlet.BaseGauntlet != null) ? PrimaryGauntlet.BaseGauntlet.PoiseDamage : 0f;

        float totalDamage = BaseDamage + primaryWeaponDamage;
        float totalPoiseDamage = BasePoiseDamage + primaryWeaponPoise;

        // 2. Add Flat Gem Bonuses
        totalDamage += GetFlatBonus(StatModifierType.PhysicalDamage);
        totalPoiseDamage += GetFlatBonus(StatModifierType.PoiseDamage);

        // 3. Multiply by Persistent Gem Percentages (e.g., a gem that gives a permanent +10% damage)
        totalDamage *= GetPercentMultiplier(StatModifierType.PhysicalDamage);
        totalPoiseDamage *= GetPercentMultiplier(StatModifierType.PoiseDamage);

        // 4. Multiply by Dynamic/Event Multipliers (The Berserker Gem!)
        totalDamage *= berserkerDamageMultiplier;

        // 5. Finalize
        CurrentDamage = totalDamage;
        CurrentPoiseDamage = totalPoiseDamage;
    }

    private void CalculateDefensiveStats()
    {
        float totalHealth = BaseMaxHealth;
        
        totalHealth += GetFlatBonus(StatModifierType.MaxHealth);
        totalHealth *= GetPercentMultiplier(StatModifierType.MaxHealth);
        
        // Example of adding a Vanguard Gem (Damage reduction) logic here later!

        CurrentMaxHealth = totalHealth;
    }

    private void CalculateUtilityStats()
    {
        float totalStamina = BaseStamina;
        float totalPoise = BaseMaxPoise;
        float totalDelay = BaseStaminaDelay;
        float totalRecovery = BaseStaminaRegen;

        totalDelay += GetFlatBonus(StatModifierType.StaminaDelay);
        totalDelay *= GetPercentMultiplier(StatModifierType.StaminaDelay);

        totalRecovery += GetFlatBonus(StatModifierType.StaminaRecovery);
        totalRecovery *= GetPercentMultiplier(StatModifierType.StaminaRecovery);

        // Prevent the delay from accidentally going below 0 seconds!
        CurrentStaminaDelay = Mathf.Max(0f, totalDelay); 
        CurrentStaminaRegen = totalRecovery;

        totalStamina += GetFlatBonus(StatModifierType.MaxStamina);
        totalStamina *= GetPercentMultiplier(StatModifierType.MaxStamina);

        totalPoise += GetFlatBonus(StatModifierType.MaxPoise);
        totalPoise *= GetPercentMultiplier(StatModifierType.MaxPoise);

        CurrentStamina = totalStamina;
        CurrentMaxPoise = totalPoise;
    }

    private float GetFlatBonus(StatModifierType statType)
    {
        if (_flatBonuses.TryGetValue(statType, out float bonus))
        {
            return bonus;
        }
        return 0f;
    }

    private float GetPercentMultiplier(StatModifierType statType)
    {
        // Returns a ready-to-multiply format. 
        // Example: If the dictionary holds 0.2 (representing 20%), this returns 1.2f.
        if (_percentBonuses.TryGetValue(statType, out float percent))
        {
            return 1f + percent; 
        }
        return 1f; // If no percentage modifier, multiplying by 1 changes nothing.
    }
}
