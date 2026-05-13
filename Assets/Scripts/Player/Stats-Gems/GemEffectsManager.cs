using UnityEngine;

//This script creates and listens for events that get called then tells
//the appropriate scripts that said event happened
public class GemEffectsManager : MonoBehaviour
{
    [Header("Main References")]
    [SerializeField] private PlayerCombat _combat;
    [SerializeField] private PlayerMovement _movement;
    [SerializeField] private PlayerHealth _health;
    [SerializeField] private HitboxController _leftHitBox;
    [SerializeField] private HitboxController _rightHitBox;
    [SerializeField] private PlayerStatsManager _statsManager;

    //Events
    


    private void OnEnable()
    {
        //sub to events
    }

    private void OnDisable()
    {
        
    }
}
