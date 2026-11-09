using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Localization;
using UnityEngine.Serialization;

/// <summary>
/// The final boss: the knight's own reflection, stepped out of the mirror at the bottom of Hell as a shadow of ink
/// (<see cref="InkShadowIntro"/>). It fights with a sword. In reach it uses a rising cut (it sinks low with the blade
/// trailing, then lunges in and brings it up from the floor: roll to the side), a horizontal cut (the blade drawn back
/// at its waist, then swept round in front: roll back), and now and then a heavy slam.
///
/// A player who keeps their distance gets the sword thrown at them, point first, curving after them. The shadow bursts
/// into ink as it lets go and appears in the air to catch the sword as it closes on them, frozen for a moment in the
/// stance of the cut it is about to make: the moment to read it and roll the right way. Then it drops into the cut.
///
/// Like a reflection it answers the player's moves: a charged heavy attack let go in its reach is sometimes its cue
/// to hop back out of it.
///
/// Below half health the ink spills. It sinks into the floor as a pool, the pool hunts the player and then stops
/// (the moment to roll clear), and it bursts up out of it straight into a rising cut. From then on it dives every few
/// seconds, and moves faster.
///
/// Its idle, walk, death, throw and both cuts are its own (Docs/Art/InkShadow.blend); the slam and the dodge reuse
/// the Elite's clips and their events.
/// </summary>
public class InkShadow : BaseEnemy
{
    // Bosses fight on their own terms: no attack tokens, and no one else's alarm wakes them.
    protected override bool UsesAttackTokens => false;
    protected override bool CanBeAlerted => false;
    protected override string AttackEvent => "event:/Boss/Boss_Attack";
    protected override string DeathEvent => "event:/Boss/Boss_Death";

    /// <summary>What its effects glow: ink, with a violet sheen so that it shows on a dark floor.</summary>
    public static readonly Color InkGlow = new Color(0.5f, 0.42f, 1f, 1f);

    private const string IdleState = "HumanM@Idle01";
    private const float FirstSwingDelay = 1.5f;

    // Where each cut is frozen while it holds its stance after the throw: the blade drawn, just before it moves.
    private const string RisingCutState = "RisingCut";
    private const float RisingCutStance = 13f / 42f;
    private const string HorizontalCutState = "HorizontalCut";
    private const float HorizontalCutStance = 12f / 37f;
    private const float MaxFlightSeconds = 1.6f;
    private const float DropSeconds = 0.15f;
    private const float MaxHangHeight = 0.8f; // its feet off the floor while it holds the stance after the catch
    private const float MinCatchGap = 3f;     // and never closer than this to the player, across the floor

    [Header("Boss")]
    public LocalizedString bossNameString;
    [SerializeField] private SwordHitbox swordHitbox;
    [SerializeField] private float swordSlamDamage = 40f;
    [SerializeField] private float risingCutDamage = 34f;
    [SerializeField] private float horizontalCutDamage = 30f;
    [SerializeField, Range(0f, 1f), Tooltip("In reach, how often it slams; the rest is split between the two cuts")]
    private float slamChance = 0.2f;
    [SerializeField, Range(0f, 1f), Tooltip("Of the cuts, how often it's the rising cut rather than the horizontal one")]
    private float risingCutChance = 0.5f;
    [SerializeField, FormerlySerializedAs("leapStrikeDistance"), Tooltip("How far in front of it the blade meets the floor")]
    private float strikeDistance = 2.4f;
    [SerializeField] private float facePlayerTurnSpeed = 5f;

    [Header("Throw")]
    [SerializeField, Tooltip("It throws its sword at a player at least this far away...")]
    private float throwMinDistance = 8f;
    [SerializeField, Tooltip("...and no further than this")]
    private float throwMaxDistance = 30f;
    [SerializeField] private float throwCooldown = 7f;
    [SerializeField] private float throwDamage = 26f;
    [SerializeField, Tooltip("How fast the sword flies, in metres a second")]
    private float throwSpeed = 20f;
    [SerializeField, Tooltip("How fast the flying sword can turn after the player, in degrees a second")]
    private float throwTurnDegrees = 90f;
    [SerializeField, Tooltip("It catches the sword when it is this close to the player, across the floor")]
    private float catchDistance = 3.6f;
    [SerializeField, Tooltip("How high above the player the sword flies, and so where it is caught")]
    private float catchHeight = 2.2f;
    [SerializeField, Tooltip("How long it holds its stance after catching the sword: the time to read the cut")]
    private float stanceHoldSeconds = 0.5f;

    [Header("Dodge")]
    [SerializeField, Range(0f, 1f), Tooltip("Chance to hop back when the player lets a charged heavy attack go in its reach")]
    private float dodgeChance = 0.4f;
    [SerializeField] private float dodgeDistance = 4.5f;
    [SerializeField] private float dodgeSeconds = 0.35f;

    [Header("Phase 2: the ink spills")]
    [SerializeField, Range(0f, 1f)] private float phaseTwoThreshold = 0.5f;
    [SerializeField] private float phaseTwoSpeedMultiplier = 1.2f;
    [SerializeField, Tooltip("The time between attacks is multiplied by this")]
    private float phaseTwoCooldownMultiplier = 0.75f;
    [SerializeField] private float diveCooldown = 9f;
    [SerializeField] private float diveRadius = 3.2f;
    [SerializeField] private float diveDamage = 36f;
    [SerializeField] private int divePoiseDamage = 40;
    [SerializeField, Tooltip("How fast the pool follows the player, in metres a second")]
    private float poolSpeed = 8f;
    [SerializeField] private float poolHuntSeconds = 1.4f;
    [SerializeField, Tooltip("How long the pool holds still before it bursts: the time the player has to roll clear")]
    private float poolWarnSeconds = 0.65f;
    [SerializeField] private Material poolMaterial;

    private static readonly int AnimSwordSlam = Animator.StringToHash("SwordSlam");
    private static readonly int AnimDodgeBack = Animator.StringToHash("DodgeBack");
    private static readonly int AnimRisingCut = Animator.StringToHash(RisingCutState);
    private static readonly int AnimHorizontalCut = Animator.StringToHash(HorizontalCutState);
    private static readonly int AnimThrow = Animator.StringToHash("Throw");

    // _underground: sunk into the floor (a dive, or between a throw and rising at the sword). Nothing to hit, and the
    // coroutine doing it moves it.
    // _throwing: from the throw until the cut it rises into is under way.
    private bool _phaseTwo, _underground, _throwing, _dodging, _playerWasCharging, _released;
    private float _nextDodge, _nextDive, _nextThrow;
    private Vector3 _scale;
    private PlayerCombat _playerCombat;
    private TrailRenderer _swordTrail;

    // Where the sword sits in its hand, to put it back after a throw.
    private Transform _swordHand;
    private Vector3 _swordLocalPosition;
    private Quaternion _swordLocalRotation;

    private string BossName => bossNameString.GetLocalizedString();

    protected override void Start()
    {
        base.Start();
        hasOpenedWithCharge = true; // no opening charge: it walks out of the mirror and fights
        attackCooldownTimer = FirstSwingDelay; // a breath after the intro hands back, before it swings
        _scale = transform.localScale;
        _nextThrow = Time.time + throwCooldown * 0.5f;
        if (player != null) _playerCombat = player.GetComponent<PlayerCombat>();

        if (swordHitbox != null)
        {
            Transform sword = swordHitbox.transform;
            _swordHand = sword.parent;
            _swordLocalPosition = sword.localPosition;
            _swordLocalRotation = sword.localRotation;
        }
    }

    protected override void Update()
    {
        if (_underground) return;
        base.Update();
    }

    protected override float FacePlayerTurnSpeed => facePlayerTurnSpeed;

    protected override bool CanPerformAction() => base.CanPerformAction() && !_underground && !_throwing && !_dodging;

    protected override void CheckEngagement()
    {
        if (player == null) return;

        if (GetDistanceToPlayer() <= engagementRange)
        {
            if (!isEngaged && BossHealthBar.Instance != null)
                BossHealthBar.Instance.ShowBossHealthBar(this, currentHealth, maxHealth, BossName);

            isEngaged = true;
            hasOpenedWithCharge = true;
        }
        else if (isAware && !isAttacking)
        {
            ChasePlayer();
            FacePlayer();
        }
    }

    protected override void ContinueCombat()
    {
        // A charged heavy attack let go: the charge ends with the player still swinging (a stagger ends it otherwise).
        bool charging = _playerCombat != null && _playerCombat.IsChargingHeavy;
        bool chargeLetGo = _playerWasCharging && !charging
                           && playerManager != null && playerManager.GetCurrentState() == PlayerState.Attacking;
        _playerWasCharging = charging;

        if (isAttacking || isStunned || _dodging || _throwing) return;

        float distance = GetDistanceToPlayer();

        if (chargeLetGo && distance <= attackRange + 2f && Time.time >= _nextDodge && Random.value < dodgeChance)
        {
            StartCoroutine(Dodge());
            return;
        }

        if (_phaseTwo && Time.time >= _nextDive && attackCooldownTimer <= 0f)
        {
            StartCoroutine(Dive(false));
            return;
        }

        if (distance <= attackRange)
        {
            navAgent.isStopped = true;
            FacePlayer();
            if (Random.value < slamChance) SwordSlam();
            else if (Random.value < risingCutChance) Cut(AnimRisingCut);
            else Cut(AnimHorizontalCut);
            return;
        }

        if (distance >= throwMinDistance && distance <= throwMaxDistance && Time.time >= _nextThrow && CanPerformAction())
        {
            StartCoroutine(ThrowAndWarp());
            return;
        }

        ChasePlayer();
        FacePlayer();
    }

    #region Sword
    public override void LightAttack() { } // it doesn't have any
    public override void HeavyAttack() { }

    private void SwordSlam()
    {
        if (!CanPerformAction()) return;

        isAttacking = true;
        attackCooldownTimer = attackCooldown * 1.5f;
        navAgent.isStopped = true;
        navAgent.velocity = Vector3.zero;
        animator.SetTrigger(AnimSwordSlam);
    }

    private void Cut(int trigger)
    {
        if (!CanPerformAction()) return;

        isAttacking = true;
        attackCooldownTimer = attackCooldown * 1.3f;
        navAgent.isStopped = true;
        navAgent.velocity = Vector3.zero;
        animator.SetTrigger(trigger);
    }

    // From the clips' animation events.
    public void EnableSwordSlamHitbox()
    {
        if (swordHitbox != null) swordHitbox.EnableHitbox(swordSlamDamage, heavyAttackPoiseDamage);
        PlayAttackSound();
        StartCoroutine(SlamLands());
    }

    // The blade comes up off the floor: ink where it leaves it.
    public void EnableRisingCutHitbox()
    {
        if (swordHitbox != null) swordHitbox.EnableHitbox(risingCutDamage, heavyAttackPoiseDamage);
        PlayAttackSound();
        EnemyFx.Impact(transform.position + transform.forward * strikeDistance, 1.8f, InkGlow);
    }

    public void EnableHorizontalCutHitbox()
    {
        if (swordHitbox != null) swordHitbox.EnableHitbox(horizontalCutDamage, heavyAttackPoiseDamage);
        PlayAttackSound();
        EnemyFx.Slash(transform, 4.2f, 1.3f, InkGlow, -130f); // negative: right to left, the way the blade goes
    }

    public void DisableSwordHitbox()
    {
        if (swordHitbox != null) swordHitbox.DisableHitbox();
    }

    public void OnSwordSlamEnd() => OnAttackEnd();
    public void OnRisingCutEnd() => OnAttackEnd();
    public void OnHorizontalCutEnd() => OnAttackEnd();
    public void OnDodgeEnd() { } // the clip fires this, but Dodge times the step

    // The blade is still coming down when the hitbox opens: a splash of ink where it meets the floor.
    private IEnumerator SlamLands()
    {
        yield return new WaitForSeconds(0.12f);
        if (IsDead()) yield break;

        Vector3 point = transform.position + transform.forward * strikeDistance;
        FMODUnity.RuntimeManager.PlayOneShot("event:/Boss/Boss_Slam", point);
        EnemyFx.Impact(point, 2.6f, InkGlow);
        EnemyFx.GroundCrack(point, 2.4f, InkGlow);
    }

    public override void OnAttackEnd()
    {
        // Cutting an attack short to go under the floor or to throw leaves its animator state, which calls this: the
        // move it was cut short for isn't done.
        if (_underground || _throwing) return;

        DisableSwordHitbox();
        base.OnAttackEnd();
    }
    #endregion

    #region Throw
    // From the throw clip, as the hand comes over.
    public void ThrowRelease() => _released = true;

    // The gap-closer. The sword thrown point first, curving after the player; the shadow bursts into ink as it lets go
    // and, as the sword closes on them, appears in the air and catches it, frozen in the stance of the cut to come.
    // It hangs there a moment (the time to read the cut and roll the right way), then drops into it.
    private IEnumerator ThrowAndWarp()
    {
        _throwing = true;
        isAttacking = true;
        _nextThrow = Time.time + throwCooldown;
        navAgent.isStopped = true;
        navAgent.velocity = Vector3.zero;
        FacePlayerImmediate();
        _released = false;
        animator.SetTrigger(AnimThrow);

        for (float waited = 0f; !_released && waited < 1.5f; waited += Time.deltaTime)
        {
            FacePlayer();
            yield return null;
        }

        // Gone as it lets go: the sword leaves the hand, the shadow bursts into ink.
        Transform sword = swordHitbox != null ? swordHitbox.transform : null;
        if (sword != null) sword.SetParent(null, true);
        _underground = true;
        Collider body = GetComponent<Collider>();
        if (body != null) body.enabled = false;
        SetVisible(false);
        PlayAttackSound();
        EnemyFx.Impact(transform.position, 2.4f, InkGlow);
        EnemyFx.Strike(transform.position + Vector3.up * 2.5f, 2f, InkGlow);

        Vector3 catchPoint = sword != null ? sword.position : transform.position + Vector3.up * catchHeight;
        if (sword != null) yield return FlySword(sword, result => catchPoint = result);

        // Caught in the air: the stance of the cut it's about to make, hand where the sword was.
        bool rising = Random.value < risingCutChance;
        foreach (int trigger in new[] { AnimRisingCut, AnimHorizontalCut, AnimSwordSlam, AnimThrow }) animator.ResetTrigger(trigger);
        Vector3 toPlayer = player.position - catchPoint;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(toPlayer);
        animator.speed = 0f;
        animator.Play(rising ? RisingCutState : HorizontalCutState, 0, rising ? RisingCutStance : HorizontalCutStance);
        animator.Update(0f);
        Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        if (hand != null) transform.position += catchPoint - hand.position;
        transform.position = HangPoint(transform.position);
        ReclaimSword();
        SetVisible(true);
        FMODUnity.RuntimeManager.PlayOneShot("event:/Combat/Impact_Light", catchPoint);
        EnemyFx.Strike(catchPoint, 1.6f, InkGlow);

        yield return new WaitForSeconds(stanceHoldSeconds);

        // Down to the floor below, and into the cut.
        Vector3 from = transform.position;
        Vector3 ground = EnemyFx.FloorUnder(from);
        if (NavMesh.SamplePosition(ground, out NavMeshHit hit, 3f, NavMesh.AllAreas)) ground = hit.position;
        for (float elapsed = 0f; elapsed < DropSeconds; elapsed += Time.deltaTime)
        {
            float u = elapsed / DropSeconds;
            transform.position = Vector3.Lerp(from, ground, u * u);
            yield return null;
        }
        navAgent.Warp(ground);
        transform.position = ground; // root motion holds the transform while it's attacking, so move it too
        EnemyFx.Impact(ground, 2f, InkGlow);
        if (body != null) body.enabled = true;
        _underground = false;

        animator.speed = 1f;
        attackCooldownTimer = attackCooldown * 1.3f;
        _throwing = false;
        // The clip carries on from the stance and its events finish the attack (OnRisingCutEnd / OnHorizontalCutEnd).
    }

    // Where it hangs after the catch: a leap, not a perch. No higher than MaxHangHeight off the floor, and pushed back
    // to MinCatchGap from the player if the catch came closer than that.
    private Vector3 HangPoint(Vector3 at)
    {
        Vector3 away = at - player.position;
        away.y = 0f;
        if (away.magnitude < MinCatchGap)
        {
            Vector3 back = away.sqrMagnitude > 0.01f ? away.normalized : -transform.forward;
            at = new Vector3(player.position.x, at.y, player.position.z) + back * MinCatchGap;
        }

        float floor = EnemyFx.FloorUnder(at + Vector3.up * 3f).y;
        at.y = Mathf.Clamp(at.y, floor, floor + MaxHangHeight);
        return at;
    }

    // Point first, turning after a point above the player, but only so fast: a roll to the side bends its path, it
    // doesn't lose it. Ends where the shadow catches it, close to the player, or when it's flown long enough.
    private IEnumerator FlySword(Transform sword, System.Action<Vector3> caught)
    {
        swordHitbox.EnableHitbox(throwDamage, lightAttackPoiseDamage);
        if (_swordTrail == null) _swordTrail = EnemyFx.BladeTrail(sword.gameObject);
        if (_swordTrail != null) { _swordTrail.Clear(); _swordTrail.emitting = true; }

        Vector3 Aim() => player.position + Vector3.up * catchHeight;
        Vector3 velocity = (Aim() - sword.position).normalized * throwSpeed;
        for (float flown = 0f; flown < MaxFlightSeconds; flown += Time.deltaTime)
        {
            Vector3 wanted = (Aim() - sword.position).normalized * throwSpeed;
            velocity = Vector3.RotateTowards(velocity, wanted, throwTurnDegrees * Mathf.Deg2Rad * Time.deltaTime, 0f);
            sword.position += velocity * Time.deltaTime;
            sword.rotation = Quaternion.LookRotation(velocity); // the blade runs along the sword's Z

            Vector3 gap = player.position - sword.position;
            gap.y = 0f;
            if (gap.magnitude <= catchDistance) break;
            yield return null;
        }

        swordHitbox.DisableHitbox();
        if (_swordTrail != null) _swordTrail.emitting = false;
        caught(sword.position);
    }

    // Back into its hand, wherever it is.
    private void ReclaimSword()
    {
        if (swordHitbox == null || _swordHand == null) return;

        Transform sword = swordHitbox.transform;
        if (sword.parent != _swordHand) sword.SetParent(_swordHand, false);
        sword.localPosition = _swordLocalPosition;
        sword.localRotation = _swordLocalRotation;
    }
    #endregion

    #region Dodge
    // A hop back out of the player's swing. The clip's own root motion is too small, so the nav agent carries it,
    // which also stops it at walls and ledges.
    private IEnumerator Dodge()
    {
        _dodging = true;
        _nextDodge = Time.time + 1f; // never twice for one swing
        navAgent.isStopped = true;
        navAgent.velocity = Vector3.zero;
        animator.SetTrigger(AnimDodgeBack);

        Vector3 away = transform.position - player.position;
        away.y = 0f;
        away.Normalize();

        for (float elapsed = 0f; elapsed < dodgeSeconds && _dodging; elapsed += Time.deltaTime)
        {
            navAgent.Move(away * (dodgeDistance / dodgeSeconds * Time.deltaTime));
            yield return null;
        }

        _dodging = false;
        if (!isStunned && !IsDead()) navAgent.isStopped = false;
    }
    #endregion

    #region Ink dive
    private void StartPhaseTwo()
    {
        _phaseTwo = true;
        chaseSpeed *= phaseTwoSpeedMultiplier;
        attackCooldown *= phaseTwoCooldownMultiplier;
        StoryDialogue.Say("Story_A3_P2_01", Speaker.Shadow, 1.2f);

        // Whatever it was doing, it spills now; a sword out of its hand comes back to it.
        StopAllCoroutines();
        _dodging = false;
        _throwing = false;
        animator.speed = 1f;
        ReclaimSword();
        StartCoroutine(Dive(true));
    }

    private IEnumerator Dive(bool spilling)
    {
        _underground = true;
        isAttacking = true;
        DisableSwordHitbox();
        navAgent.isStopped = true;
        navAgent.velocity = Vector3.zero;
        Collider body = GetComponent<Collider>();
        if (body != null) body.enabled = false;
        animator.CrossFadeInFixedTime(IdleState, 0.1f);

        // Down into the floor: it flattens into a pool of itself.
        EnemyFx.Impact(transform.position, 3f, InkGlow);
        yield return Squash(transform.localScale.y / _scale.y, 0.04f, 0.45f);
        SetVisible(false);

        Transform pool = CreatePool(EnemyFx.FloorUnder(transform.position));
        GroundFx ring = EnemyFx.Ring(pool, diveRadius, InkGlow);
        if (spilling) yield return new WaitForSeconds(1.2f); // a moment for its line

        // The pool hunts the player, then holds still: the cue to roll clear.
        for (float elapsed = 0f; elapsed < poolHuntSeconds; elapsed += Time.deltaTime)
        {
            if (player != null)
            {
                Vector3 target = EnemyFx.FloorUnder(player.position);
                pool.position = Vector3.MoveTowards(pool.position, target, poolSpeed * Time.deltaTime);
            }
            yield return null;
        }
        yield return new WaitForSeconds(poolWarnSeconds);

        // Up out of it.
        Vector3 at = pool.position;
        if (NavMesh.SamplePosition(at, out NavMeshHit hit, 3f, NavMesh.AllAreas)) at = hit.position;
        navAgent.Warp(at);
        transform.position = at; // root motion holds the transform while it's attacking, so move it too
        FacePlayerImmediate();
        SetVisible(true);

        FMODUnity.RuntimeManager.PlayOneShot("event:/Boss/Boss_Slam", at);
        EnemyFx.Impact(at, diveRadius, InkGlow);
        EnemyFx.GroundCrack(at, diveRadius, InkGlow);
        HurtPlayerWithin(at, diveRadius);
        if (ring != null) ring.End();
        Destroy(pool.gameObject);
        yield return Squash(0.04f, 1f, 0.18f);

        if (body != null) body.enabled = true;
        _underground = false;
        _nextDive = Time.time + diveCooldown;

        // ...and straight into a rising cut.
        isAttacking = false;
        attackCooldownTimer = 0f;
        Cut(AnimRisingCut);
    }

    private IEnumerator Squash(float from, float to, float seconds)
    {
        for (float elapsed = 0f; elapsed < seconds; elapsed += Time.deltaTime)
        {
            transform.localScale = new Vector3(_scale.x, _scale.y * Mathf.Lerp(from, to, elapsed / seconds), _scale.z);
            yield return null;
        }
        transform.localScale = new Vector3(_scale.x, _scale.y * to, _scale.z);
    }

    private void SetVisible(bool visible)
    {
        foreach (Renderer part in GetComponentsInChildren<Renderer>(true)) part.enabled = visible;
    }

    private Transform CreatePool(Vector3 at)
    {
        GameObject pool = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pool.name = "InkPool";
        Destroy(pool.GetComponent<Collider>());
        pool.GetComponent<Renderer>().sharedMaterial = poolMaterial;
        pool.transform.position = at;
        pool.transform.localScale = new Vector3(diveRadius * 2f, 0.01f, diveRadius * 2f);
        return pool.transform;
    }

    private void HurtPlayerWithin(Vector3 point, float radius)
    {
        if (player == null || playerHealth == null) return;

        Vector3 offset = player.position - point;
        if (Mathf.Abs(offset.y) > 2.5f) return;
        offset.y = 0f;
        if (offset.sqrMagnitude <= radius * radius) playerHealth.TakeDamage(diveDamage, divePoiseDamage, gameObject);
    }
    #endregion

    #region Health
    public override void TakeDamage(float damage)
    {
        if (IsDead() || _underground) return; // nothing to hit under the floor
        if (_dodging && !IsDotTick) return;

        if (!isAware)
        {
            isAware = true;
            navAgent.isStopped = false;
        }
        if (!isEngaged && BossHealthBar.Instance != null)
            BossHealthBar.Instance.ShowBossHealthBar(this, currentHealth, maxHealth, BossName);
        isEngaged = true;
        hasOpenedWithCharge = true;

        currentHealth -= damage;
        if (BossHealthBar.Instance != null) BossHealthBar.Instance.UpdateHealth(this, currentHealth, maxHealth, BossName);

        if (currentHealth <= 0)
        {
            if (BossHealthBar.Instance != null) BossHealthBar.Instance.HideBossHealthBar(this);
            Die();
            return;
        }

        if (!_phaseTwo && currentHealth / maxHealth <= phaseTwoThreshold) StartPhaseTwo();
    }

    protected override void EnterHitStun() { } // it doesn't flinch

    protected override void Die()
    {
        StoryDialogue.ClearLines();
        StoryDialogue.Say("Story_A3_Win_01", Speaker.Shadow);
        StoryDialogue.Say("Story_A3_Win_02");

        DisableSwordHitbox();
        ReclaimSword(); // so that it dissolves with it
        animator.speed = 1f;
        base.Die();
    }
    #endregion
}
