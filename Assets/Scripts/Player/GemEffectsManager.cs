using UnityEngine;

//This script is going to be used to handle the event specific gems 
public class GemEffectsManager : MonoBehaviour
{
    [Header("Main References")]
    [SerializeField] private PlayerCombat _combat;
    [SerializeField] private PlayerMovement _movement;
    [SerializeField] private PlayerHealth _health;
    [SerializeField] private HitboxController _leftHitBox;
    [SerializeField] private HitboxController _rightHitBox;
    
    private void OnEnable()
    {
        //sub to events
    }

    private void OnDisable()
    {
        
    }
}
