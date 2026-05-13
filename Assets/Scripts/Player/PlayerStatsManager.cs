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
    

    [Header("Current Global Stats (Read Only)")]
    public float CurrentMaxHealth {get; private set;}
    public float CurrentDamage {get; private set;}
    public float CurrentStamina {get; private set;}
    public float CurrentPoiseDamage {get; private set;}
    public float CurrentMaxPoise {get; private set;}

    public RunTimeGauntlet PrimaryGauntlet;
    public RunTimeGauntlet SecondaryGauntlet;

    [Header("Starter Equipment")]
    public GauntletData DefaultPrimary;
    public GauntletData DefaultSecondary;


    //safe gaurd that holds the sum of all active bonuses making sure there are no duplicating stats
    private Dictionary<StatModifierType, float> _activeBonuses = new Dictionary<StatModifierType, float>();

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
        _activeBonuses.Clear();

        //get all modifiers
        List<GemModifier> allMods = new List<GemModifier>();
        if(PrimaryGauntlet != null) allMods.AddRange(PrimaryGauntlet.GetAllActiveModifiers());
        if(SecondaryGauntlet != null) allMods.AddRange(SecondaryGauntlet.GetAllActiveModifiers());
        Debug.Log($"Total active modifiers found across both hands: {allMods.Count}");
        foreach(GemModifier mod in allMods)
        {
            if(_activeBonuses.ContainsKey(mod.StatType)) _activeBonuses[mod.StatType] += mod.Amount;
            else
            {
                _activeBonuses.Add(mod.StatType, mod.Amount);
            }
        }

        float primaryWeaponDamage = (PrimaryGauntlet != null && PrimaryGauntlet.BaseGauntlet != null) ? PrimaryGauntlet.BaseGauntlet.Damage : 0f;
        float primaryPoiseDamage = (PrimaryGauntlet != null && PrimaryGauntlet.BaseGauntlet != null) ? PrimaryGauntlet.BaseGauntlet.PoiseDamage : 0f;

        CurrentMaxHealth = BaseMaxHealth + GetBonus(StatModifierType.MaxHealth);
        CurrentDamage = BaseDamage + primaryWeaponDamage + GetBonus(StatModifierType.PhysicalDamage);
        CurrentStamina = BaseStamina + GetBonus(StatModifierType.MaxStamina);
        CurrentPoiseDamage = BasePoiseDamage + primaryPoiseDamage + GetBonus(StatModifierType.PoiseDamage);
        CurrentMaxPoise = BaseMaxPoise + GetBonus(StatModifierType.MaxPoise);
  
        //invoke event to tell other scripts stats have changed 
        OnStatsCalculated?.Invoke();
    }

    //this is a universal function that other scripts will need to call in order to know what 
    //needs to change on their end
    public float GetBonus(StatModifierType statType)
    {
        if (_activeBonuses.ContainsKey(statType))
        {
            return _activeBonuses[statType];
        }
        return 0f;
    }
}
