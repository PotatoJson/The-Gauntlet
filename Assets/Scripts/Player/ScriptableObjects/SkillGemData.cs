using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public struct SkillVariation
{
    public ElementType RequiredElement;
    public GameObject SkillPrefab;
    public AnimationClip LeftGauntletAnim;
    public AnimationClip RightGauntletAnim;

    [Header("Pre-Cast Visual")]
    public GameObject WindUpVFXPrefab;
}

[CreateAssetMenu(fileName = "New Skill Gem", menuName = "Items/Gems/Skill Gem")]
public class SkillGemData : GemData
{
    [Header("Skill Properties")]
    public SkillCategory Category;
    public float Cooldown;
    public float StaminaCost; //womp


    [Header("Elemental Variation")]
    [Tooltip("Add prefabs for each element the skill supports")]
    public List<SkillVariation> Variations = new List<SkillVariation>();

    public GameObject GetPrefabForElement(ElementType gauntletElement)
    {
        foreach(SkillVariation variant in Variations)
        {
            if(variant.RequiredElement == gauntletElement)
            {
                return variant.SkillPrefab;
            }
        }
        Debug.LogWarning($"[SkillGem] No prefab found for element {gauntletElement} on {Name}. Using default.");
        return Variations.Count > 0 ? Variations[0].SkillPrefab : null;
    }

    public SkillVariation? GetVariationForElement(ElementType gauntletElement)
    {
        foreach (SkillVariation variant in Variations)
        {
            if (variant.RequiredElement == gauntletElement)
            {
                return variant;
            }
        }
        
        Debug.LogWarning($"[SkillGem] No variation found for element {gauntletElement} on {Name}. Using default.");
        if (Variations.Count > 0) return Variations[0];
        
        return null; 
    }
}