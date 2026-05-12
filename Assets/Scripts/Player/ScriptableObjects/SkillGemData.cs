using UnityEngine;

[CreateAssetMenu(fileName = "New Skill Gem", menuName = "Items/Gems/Skill Gem")]
public class SkillGemData : GemData
{
    [Header("Skill Properties")]
    public GameObject SkillPrefab;
    public float BaseCooldown;
    public float StaminaCost;

    //example of general skill logic (see definitions script for list of categories)
    public SkillCategory Category;
}