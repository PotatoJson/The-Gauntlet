using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New Gauntlet", menuName = "Items/Gauntlet")]
public class GauntletData : BaseItemData
{
    [Header("Combat Stats")]
    public int Damage;
    public int PoiseDamage;

    [Header("Identity")]
    //see definitions script for list of Rarities/Elements
    public GauntletRarity Rarity;
    public ElementType Element;

    [Header("Inherent Passives in Gauntlets")]
    [Tooltip("These buffs/debuffs ONLY apply if this gauntlet is in the Primary Slot")]
    public List<GemModifier> InherentPassives = new List<GemModifier>();

    [Header("Socket Settings")]
    public int MaxPrimaryGemSlots;
    public int MaxSecondaryGemSlots;
    public bool HasSkillSlot;

}
