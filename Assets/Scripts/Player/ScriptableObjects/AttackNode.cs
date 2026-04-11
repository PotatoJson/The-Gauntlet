using UnityEngine;

[CreateAssetMenu(fileName = "AttackNode", menuName = "Scriptable Objects/AttackNode")]

public enum StrikeHand
{
    Left,
    Right,
    Both
}

public class AttackNode : ScriptableObject
{
    [Header("The Actions")]
    public string AnimationTrigger;
    public float StaminaCost;

    [Header("Combo Web")]
    public AttackNode NextLightAttack;
    public AttackNode NextHeavyAttack;

    public StrikeHand StrikingHand;
}
