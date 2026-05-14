using UnityEngine;
/*-- Used for goblal enums that will be used by other scripts (mainly Scriptable Objects) --*/

public enum ModifierMathType
{
    Flat,
    Percentage
}

public enum GauntletRarity
{
    Common,
    Rare,
    UltraRare
}

public enum ElementType
{
    Physical,
    Fire,
    Ice,
    Earth,
    Wind
}

public enum EquipSlot
{
    Primary,
    Secondary,
    None
}

public enum StatModifierType
{
    //passives Calculated once and applied
    PhysicalDamage,
    PoiseDamage,
    MaxHealth,
    MaxStamina,
    MaxPoise,
    HealthRegen,
    CritChance,
    LifeSteal,
    StaminaDelay, //same gem
    StaminaRecovery, //same gem

    //Stat Flags (checked during specific events) These are examples and subject to change
    //These will require more interconnected code within the player scripts so for now these are on the backburner
    //maybe a milestone 3 task ?????
    DamageTakeMult, //+10% damage taken
    HeavyHitStamRestore, //stamina restored on succesful heavy attacks
    DodgeIFrameBonus, //increased I frames on dodge
    ParryAttackBuff, //LOL no parry yet but this could be cool
    Retaliation, //Damage Increases as health lowers
    Executioner, //Killing an enemy gives stamina back
}

public enum SkillCategory
{
    AOE,
    Projectile,
    Buff,
    Parry,
    Wave,
    Cleave
}

public class Definitions : MonoBehaviour
{
    
}
