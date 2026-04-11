using UnityEngine;

public enum StrikeHand
{
    Left,
    Right,
    Both
}
[CreateAssetMenu(fileName = "AttackNode", menuName = "Scriptable Objects/AttackNode")]
public class AttackNode : ScriptableObject
{
    [Header("The Actions")]
    public string AnimationTrigger;
    public int StaminaCost;

    [Header("Combo Web")]
    public AttackNode NextLightAttack;
    public AttackNode NextHeavyAttack;

    public StrikeHand StrikingHand;
}
