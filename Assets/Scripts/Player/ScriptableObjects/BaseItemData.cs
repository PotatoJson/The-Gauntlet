using UnityEngine;

[System.Serializable]
public struct GemModifier
{
  //see definitions script for list of stat types
  public StatModifierType StatType;
  public float Amount;

  [Tooltip("Flat vs Percent Modifier Type")]
  public ModifierMathType MathType;
}

public abstract class BaseItemData : ScriptableObject
{
  [Header("Base Item Data")]
  public string Name;
  public Sprite Icon;
  [TextArea]public string Description;
}
