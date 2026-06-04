using UnityEngine;
using System.Collections.Generic;

public class HitboxController : MonoBehaviour
{
    private Collider _collider;

    [Header("Visuals")]
    public TrailRenderer PunchTrail;


    //states passed from combat manager
    public int CurrentDamage => _currentDamage;
    private int _currentDamage;
    private int _currentPoiseDamage;
    private CombatInput _currentAttackType;
    [SerializeField] private Animator _playerAnimator;

    private HashSet<GameObject> _alreadyHit = new HashSet<GameObject>();

    private void Awake()
    {
        /*if(bloodEffectPrefab == null)
        {
            Debug.LogError("Blood effect prefab not assigned in HitboxController.");
        }*/
        _collider = GetComponent<Collider>();
        _collider.isTrigger = true;
        _collider.enabled = false;
    }

    public void EnableCollider(int damage, int poise, CombatInput attackType, ElementType element)
    {
        _alreadyHit.Clear();
        _currentDamage = damage;
        _currentPoiseDamage = poise;
        _currentAttackType = attackType;
        _collider.enabled = true;

        if(PunchTrail != null)
        {
            SetTrailColor(element);
            PunchTrail.Clear();
            PunchTrail.emitting = true;
        }
    }

    public void DisableCollider()
    {
        Debug.Log("hitbox Disabled");
        _collider.enabled = false;

        if (PunchTrail != null)
        {
            PunchTrail.emitting = false;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Don't hit ourselves
        if(other.transform.root == transform.root) return;

        ShieldHealth shieldHealth = other.GetComponentInParent<ShieldHealth>();
        if(shieldHealth != null)
        {
            shieldHealth.RegisterShieldHit(this);
            return;
        }

        // THE FIX: Search the object and its parents for the BaseEnemy script
        BaseEnemy enemyScript = other.GetComponentInParent<BaseEnemy>();

        if(enemyScript != null)
        {
            ShieldEnemy shieldEnemy = enemyScript as ShieldEnemy;
            if(shieldEnemy != null)
            {
                ShieldHealth blockingShield = shieldEnemy.GetComponentInChildren<ShieldHealth>();
                if(blockingShield != null && blockingShield.IsHitboxBlocked(_collider))
                {
                    blockingShield.RegisterShieldHit(this);
                    return;
                }

                if(shieldEnemy.IsShieldBlockingHit(this)) return;
            }

            GameObject enemyRoot = other.transform.root.gameObject;
            
            if(_alreadyHit.Add(enemyRoot))
            {
                enemyScript.TakeDamage(_currentDamage);
                
                Vector3 spawnPosition = other.ClosestPoint(transform.position);
                Vector3 punchDirection = (other.transform.position - transform.position).normalized;
                punchDirection += new Vector3(Random.Range(-0.1f, 0.1f), 1.1f, Random.Range(-0.1f, 0.1f));
                
                enemyScript.SpawnHitVFX(spawnPosition, punchDirection);

                if (_currentAttackType == CombatInput.Heavy)
                {
                    FMODUnity.RuntimeManager.PlayOneShot("event:/Combat/Impact_Heavy", other.transform.position);
                    StartCoroutine(HitstopRoutine(0.1f));
                }
                else if (_currentAttackType == CombatInput.Light)
                {
                    FMODUnity.RuntimeManager.PlayOneShot("event:/Combat/Impact_Light", other.transform.position);
                    StartCoroutine(HitstopRoutine(0.05f));
                }

                if (MetricsTracker.Instance != null)
                {
                    MetricsTracker.Instance.RecordMeleeHit(_currentAttackType, _currentDamage);
                }
                
                Debug.Log($"<color=orange>Successfully Damaged {enemyRoot.name} for {_currentDamage}</color>");
            }
        }
    }

    private System.Collections.IEnumerator HitstopRoutine(float duration)
    {
        FreezeForHitstop();
        yield return new WaitForSeconds(duration);
        UnfreezeFromHitstop();
    }

    private void FreezeForHitstop()
    {
        if(_playerAnimator != null) _playerAnimator.speed = 0f;
    }

    private void UnfreezeFromHitstop()
    {
        if(_playerAnimator != null) _playerAnimator.speed = 1f;
    }

    private void SetTrailColor(ElementType element)
    {
        // Adjust these colors to match your game's art style!
        Color trailColor = Color.white; // Default

        switch (element)
        {
            case ElementType.Fire: trailColor = new Color(1f, 0.3f, 0f); break; // Orange-Red
            case ElementType.Ice: trailColor = new Color(0f, 0.5f, 1f); break; // Blue
            case ElementType.Earth: trailColor = new Color(0.6f, 0.3f, 0.1f); break; // Brown/Green
            case ElementType.Lightning: trailColor = new Color(1f, 0.9f, 0.1f); break; // Yellow
            case ElementType.Wind: trailColor = new Color(0.6f, 0.9f, 0.9f); break; // Cyan/White
        }

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] { new GradientColorKey(trailColor, 0.0f), new GradientColorKey(trailColor, 1.0f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) } // Fades to 0 alpha at the tail
        );
        PunchTrail.colorGradient = gradient;
    }

    
}
