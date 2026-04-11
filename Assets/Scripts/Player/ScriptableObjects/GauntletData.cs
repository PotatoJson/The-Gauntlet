using UnityEngine;

[CreateAssetMenu(fileName = "GauntletData", menuName = "Scriptable Objects/GauntletData")]
public class GauntletData : ScriptableObject
{
    [Header("Identity")]
    public string GauntletName;

    [Header("Combat Stats")]
    public int Damage;
    public int PoiseDamage;

    [Header("Upgrade Settings")]
    public int MaxGemSlots;

}
