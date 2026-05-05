using UnityEngine;
using System.Collections.Generic;

public enum StatModifierType
{
  PhysicalDamage,
  PoiseDamage,
  MaxHealth,
  MaxStamina,
  HealthRegen,
  StaminaRegen,
  CritChance,
}

public struct GemModifier
{
  public StatModifierType StatType;
  public float Amount;
}

[CreateAssetMenu(fileName = "New_Gem", menuName = "Scriptable Objects/GemData")]
public class GemData : ScriptableObject
{
    [Header("Gem Identification")]
    public string GemName;
    public Sprite GemIcon;

    [Header("Stat Bonuses")]
    public List<GemModifier> Modifiers = new List<GemModifier>();
}
