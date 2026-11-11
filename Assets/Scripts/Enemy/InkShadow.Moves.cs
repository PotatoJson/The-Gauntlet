using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// The Unfinished Knight's footwork and its heavier moves: circling, the backstep and lunge, the turn on a player
// behind it, the grab, the leap; and in phase 2 its mirror powers (break the mirror before its reflection forms,
// find which of three mirrors it's really in, and the ambush from two mirrors that ends in the leap).
// The fight loop that picks between them is in InkShadow.cs.
public partial class InkShadow
{
    private const float LungeTravel = 5.75f;        // how far the lunge clip carries it (4.6 m in Blender, x1.25)
    private const float LungeTipReach = 3.4f;       // past its root, where the blade is sure to land: it aims to stop this far short
    private const float LeapAirSeconds = 18f / 24f; // frames 13 to 31 of the leap clip
    private const float TurnSeconds = 0.35f;        // how long the turn sweep takes to come round to the player
    private const float CloneRise = 0.3f;
    private const float ReflectionScale = 0.62f;     // the boss shrunk to fit in the glass
    private const float GlassBottom = 0.35f, GlassTop = 3.35f, GlassWidth = 2f; // the summoned mirror's glass
    private const float FakeCrack = 0.55f;          // how cracked a fake mirror's glass is: the tell
    private const float FakeBurstRadius = 3f;
    private static readonly int CloneDashState = Animator.StringToHash("Base Layer.Attacks.RunningHeavy");

    [Header("Circling")]
    [SerializeField, Range(0f, 1f), Tooltip("Out of reach but close, how often it circles the player instead of walking in")]
    private float strafeChance = 0.45f;
    [SerializeField] private float strafeSpeed = 1.75f;
    [SerializeField] private float strafeRange = 9f;
    [SerializeField] private Vector2 strafeSeconds = new Vector2(1.4f, 2.4f);

    [Header("Lunge")]
    [SerializeField] private float lungeDamage = 30f;
    [SerializeField] private float lungeCooldown = 6f;
    [SerializeField, Tooltip("Closer than this, it hops back first and lunges from there")]
    private float lungeMinDistance = 5f;
    [SerializeField] private float lungeMaxDistance = 9.5f;

    [Header("Turn sweep")]
    [SerializeField] private float turnSweepDamage = 26f;
    [SerializeField, Tooltip("How long the player can stay behind it before it turns on them")]
    private float behindSeconds = 0.6f;

    [Header("Grab")]
    [SerializeField, Range(0f, 1f), Tooltip("In reach, how often it tries to grab")]
    private float grabChance = 0.15f;
    [SerializeField] private float grabDamage = 45f;
    [SerializeField, Tooltip("How close to its claw the player has to be when it closes")]
    private float grabReach = 2.2f;

    [Header("Leap")]
    [SerializeField] private float leapDamage = 42f;
    [SerializeField] private float leapRadius = 4.5f;
    [SerializeField] private float leapHeight = 7f;

    [Header("Phase 2: mirror powers")]
    [SerializeField] private GameObject summonMirrorPrefab;
    [SerializeField, Tooltip("An ink copy of the knight, posed by the player's own animator controller")]
    private GameObject inkKnightPrefab;
    [SerializeField, Tooltip("The boss's own model with no mind: the reflection in its mirrors")]
    private GameObject shadowReflectionPrefab;
    [SerializeField, Tooltip("Glass that cracks (the Hellbent/MirrorGlass shader)")]
    private Material crackGlassMaterial;
    [SerializeField] private Material mirrorShardMaterial;
    [SerializeField, Tooltip("How long a stagger from beating one of its mirrors lasts")]
    private float staggerSeconds = 3f;

    [Header("Phase 2: break the mirror")]
    [SerializeField, Tooltip("Punches it takes to break the glass (a heavy counts twice)")]
    private int punchesToBreak = 6;
    [SerializeField, Tooltip("How long the reflection takes to form: the time to break it in")]
    private float breakSeconds = 4.5f;
    [SerializeField] private float breakAoeRadius = 11f;
    [SerializeField] private float breakAoeDamage = 50f;

    [Header("Phase 2: which one is real")]
    [SerializeField, Tooltip("How far from the player its three mirrors rise")]
    private float truthDistance = 7f;
    [SerializeField, Tooltip("How long the player has to find the real one before it lunges out")]
    private float truthSeconds = 3.5f;
    [SerializeField] private float fakeBurstDamage = 30f;
    [SerializeField] private float ambushDashDamage = 24f;
    [SerializeField] private float ambushDashSpeed = 16f;
    [SerializeField, Tooltip("How far to each side of the player its two mirrors rise")]
    private float ambushSide = 6f;

    private bool _strafing, _lunging, _special, _holding, _launched;
    private float _nextStrafe, _nextLunge, _behindFor, _lungeStretch = 1f;
    private readonly List<GameObject> _summoned = new List<GameObject>();

    private Animator _playerAnimator;

    #region Choosing
    // Behind it for long enough: it turns on them with a backhand.
    private bool TryTurnPunish(float distance)
    {
        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        bool behind = distance < 6f && Vector3.Angle(transform.forward, toPlayer) > 110f;
        _behindFor = behind ? _behindFor + Time.deltaTime : 0f;
        if (_behindFor < behindSeconds || !CanPerformAction()) return false;

        _behindFor = 0f;
        StartCoroutine(TurnSweep());
        return true;
    }

    // Close, but out of reach: a lunge if it's ready, otherwise now and then it circles instead of walking in.
    private bool TryMidRange(float distance)
    {
        if (distance <= lungeMaxDistance && Time.time >= _nextLunge && attackCooldownTimer <= 0f && CanPerformAction())
        {
            _nextLunge = Time.time + lungeCooldown;
            StartCoroutine(distance < lungeMinDistance ? BackstepThenLunge() : LungeAt());
            return true;
        }

        if (distance <= strafeRange && Time.time >= _nextStrafe && CanPerformAction())
        {
            _nextStrafe = Time.time + 4f;
            if (Random.value < strafeChance)
            {
                StartCoroutine(Strafe());
                return true;
            }
        }
        return false;
    }

    // Phase 2: one of its ink or mirror moves, every few seconds.
    // Mostly its mirrors: each mirror move is three times as likely as the dive, and none comes twice running.
    private void StartSpecial()
    {
        var moves = new List<(string name, System.Func<IEnumerator> move, int weight)>
        {
            ("Dive", () => Dive(false), 1),
            ("Ambush", MirrorAmbush, 3),
            ("Break", BreakTheMirror, 3),
            ("Real", WhichIsReal, 3),
        };
        moves.RemoveAll(m => m.name == _lastSpecial);

        int total = 0;
        foreach (var m in moves) total += m.weight;
        int roll = Random.Range(0, total);
        foreach (var m in moves)
        {
            roll -= m.weight;
            if (roll >= 0) continue;
            _lastSpecial = m.name;
            StartCoroutine(m.move());
            return;
        }
    }

    private string _lastSpecial;

    // Called whenever it's interrupted for good (phase 2 starting, death): nothing it summoned or holds stays behind.
    private void EndMoves()
    {
        ReleaseGrab(false);
        _strafing = _lunging = _special = _launched = false;
        foreach (GameObject thing in _summoned) if (thing != null) Destroy(thing);
        _summoned.Clear();
    }
    #endregion

    #region Circling
    private IEnumerator Strafe()
    {
        _strafing = true;
        navAgent.isStopped = true;
        navAgent.velocity = Vector3.zero;
        float side = Random.value < 0.5f ? 1f : -1f; // +1: to its right
        animator.CrossFadeInFixedTime(side > 0f ? "StrafeRight" : "StrafeLeft", 0.2f);

        float seconds = Random.Range(strafeSeconds.x, strafeSeconds.y);
        for (float elapsed = 0f; elapsed < seconds && _strafing; elapsed += Time.deltaTime)
        {
            if (player == null || IsDead() || GetDistanceToPlayer() < attackRange * 0.8f) break; // they came to it
            FacePlayerImmediate();
            navAgent.Move(transform.right * (side * strafeSpeed * Time.deltaTime));
            yield return null;
        }

        _strafing = false;
        if (!IsDead()) animator.CrossFadeInFixedTime(IdleState, 0.2f);
    }
    #endregion

    #region Lunge
    private IEnumerator BackstepThenLunge()
    {
        yield return Dodge();
        yield return new WaitForSeconds(0.1f);
        if (!IsDead() && CanPerformAction()) yield return LungeAt();
    }

    private IEnumerator LungeAt()
    {
        isAttacking = true;
        attackCooldownTimer = attackCooldown * 1.5f;
        navAgent.isStopped = true;
        navAgent.velocity = Vector3.zero;
        FacePlayerImmediate();
        _lungeStretch = 1f;
        _lunging = true;
        animator.CrossFadeInFixedTime("Lunge", 0.12f);
        yield break;
    }

    // From the clip, as the thrust starts: stretch or shorten it so the point ends up just past the player.
    public void LungeStart()
    {
        if (player == null) return;
        FacePlayerImmediate();
        _lungeStretch = Mathf.Clamp((GetDistanceToPlayer() - LungeTipReach) / LungeTravel, 0f, 1.3f);
        PlayAttackSound();
        FMODUnity.RuntimeManager.PlayOneShot("event:/Player/Player_Roll", transform.position);
        EnemyFx.Impact(transform.position, 1.6f, InkGlow);
    }

    public void EnableLungeHitbox()
    {
        Swing(lungeDamage, heavyAttackPoiseDamage);
        Shake(0.06f);
    }

    public void OnLungeEnd()
    {
        _lunging = false;
        OnAttackEnd();
    }

    protected override Vector3 AdjustRootMotion(Vector3 delta)
    {
        if (!_lunging || !isAttacking) return delta;
        return new Vector3(delta.x * _lungeStretch, delta.y, delta.z * _lungeStretch);
    }
    #endregion

    #region Turn sweep
    private IEnumerator TurnSweep()
    {
        isAttacking = true;
        attackCooldownTimer = attackCooldown;
        navAgent.isStopped = true;
        navAgent.velocity = Vector3.zero;
        animator.CrossFadeInFixedTime("TurnSweep", 0.1f);

        Quaternion from = transform.rotation;
        for (float elapsed = 0f; elapsed < TurnSeconds; elapsed += Time.deltaTime)
        {
            Vector3 toPlayer = player.position - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(from, Quaternion.LookRotation(toPlayer), Mathf.SmoothStep(0f, 1f, elapsed / TurnSeconds));
            yield return null;
        }
        FacePlayerImmediate();
    }

    public void EnableTurnSweepHitbox()
    {
        Swing(turnSweepDamage, heavyAttackPoiseDamage);
        EnemyFx.Slash(transform, 4.2f, 1.3f, InkGlow, 130f); // left to right: a backhand
        Shake(0.06f);
    }

    public void OnTurnSweepEnd() => OnAttackEnd();
    #endregion

    #region Grab
    private PlayerMovement _heldMovement;
    private CharacterController _heldController;
    private Animator _heldAnimator;

    private void Grab()
    {
        if (!CanPerformAction()) return;

        isAttacking = true;
        attackCooldownTimer = attackCooldown * 1.5f;
        navAgent.isStopped = true;
        navAgent.velocity = Vector3.zero;
        FacePlayerImmediate();
        animator.CrossFadeInFixedTime("Grab", 0.15f);
    }

    // From the clip, as the claw closes.
    public void GrabCheck()
    {
        Transform claw = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        PlayerMovement movement = player != null ? player.GetComponent<PlayerMovement>() : null;
        Vector3 gap = player != null && claw != null ? player.position - claw.position : Vector3.one * 99f;
        bool rolling = movement != null && movement.IsInvincibleViaRoll;

        if (rolling || Mathf.Abs(gap.y) > 2f || new Vector2(gap.x, gap.z).magnitude > grabReach)
        {
            StartCoroutine(GrabMissed());
            return;
        }

        // Caught. The player is the boss's to move until the slam lets go.
        _holding = true;
        _heldMovement = movement;
        _heldController = player.GetComponent<CharacterController>();
        _heldAnimator = player.GetComponentInChildren<Animator>();
        if (_heldMovement != null) _heldMovement.enabled = false;
        if (_heldController != null) _heldController.enabled = false;
        if (playerManager != null) playerManager.SetPlayerState(PlayerState.Staggered);
        if (GameplayInputGate.Instance != null) GameplayInputGate.Instance.SuspendWhileRunning(this);
        if (_heldAnimator != null) _heldAnimator.CrossFadeInFixedTime("KnockbackHit", 0.1f);
        FMODUnity.RuntimeManager.PlayOneShot("event:/Enemies/Grabber_Grab", claw.position);
        EnemyFx.Strike(claw.position, 1.4f, InkGlow);
        Shake(0.08f);
    }

    private IEnumerator GrabMissed()
    {
        yield return new WaitForSeconds(0.45f);
        if (IsDead()) yield break;
        animator.CrossFadeInFixedTime(IdleState, 0.35f);
        OnAttackEnd();
    }

    // Held up by the claw, facing it.
    private void LateUpdate()
    {
        if (!_holding || player == null) return;

        Transform claw = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        if (claw == null) return;
        player.position = claw.position - Vector3.up * 0.9f;
        Vector3 toBoss = transform.position - player.position;
        toBoss.y = 0f;
        if (toBoss.sqrMagnitude > 0.01f) player.rotation = Quaternion.LookRotation(toBoss);
    }

    // From the clip, as the claw hits the floor.
    public void GrabSlam() => ReleaseGrab(true);

    private void ReleaseGrab(bool slammed)
    {
        if (!_holding) return;
        _holding = false;

        // Down on the floor where the claw is.
        Vector3 at = player.position;
        float feetBelowRoot = _heldController != null ? _heldController.height * 0.5f - _heldController.center.y : 0.9f;
        player.position = EnemyFx.FloorUnder(at + Vector3.up) + Vector3.up * feetBelowRoot;

        if (_heldController != null) _heldController.enabled = true;
        if (_heldMovement != null) _heldMovement.enabled = true;
        if (playerManager != null) playerManager.SetPlayerState(PlayerState.Idle);
        if (GameplayInputGate.Instance != null) GameplayInputGate.Instance.RestoreWhenReleased(this);

        if (!slammed || playerHealth == null) return;

        FMODUnity.RuntimeManager.PlayOneShot("event:/Boss/Boss_Slam", player.position);
        EnemyFx.Impact(player.position, 2.6f, InkGlow);
        EnemyFx.GroundCrack(player.position, 2.2f, InkGlow);
        Shake(0.2f);
        playerHealth.TakeDamage(grabDamage, 999, gameObject); // thrown down: a knockdown
    }

    public void OnGrabEnd() => OnAttackEnd();

    private void OnDisable() => ReleaseGrab(false);
    #endregion

    #region Leap
    // Up, across, and point first into the floor where the target was. The clip sends LeapLaunch as it springs.
    private IEnumerator LeapAt(Vector3 target)
    {
        isAttacking = true;
        attackCooldownTimer = attackCooldown * 1.5f;
        navAgent.isStopped = true;
        navAgent.velocity = Vector3.zero;
        if (NavMesh.SamplePosition(target, out NavMeshHit onFloor, 4f, NavMesh.AllAreas)) target = onFloor.position;
        Vector3 toTarget = target - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(toTarget);

        // Where it will come down, marked on the floor from the start.
        Transform mark = new GameObject("LeapMark").transform;
        mark.position = EnemyFx.FloorUnder(target + Vector3.up);
        _summoned.Add(mark.gameObject);
        GroundFx ring = EnemyFx.Ring(mark, leapRadius, InkGlow);

        _launched = false;
        animator.CrossFadeInFixedTime("Leap", 0.12f);
        for (float waited = 0f; !_launched && waited < 1.5f; waited += Time.deltaTime) yield return null;

        FMODUnity.RuntimeManager.PlayOneShot("event:/Player/Player_Jump", transform.position);
        EnemyFx.Impact(transform.position, 2.2f, InkGlow);
        Shake(0.06f);
        Vector3 from = transform.position;
        for (float flown = 0f; flown < LeapAirSeconds; flown += Time.deltaTime)
        {
            float u = flown / LeapAirSeconds;
            transform.position = Vector3.Lerp(from, mark.position, u) + Vector3.up * (leapHeight * 4f * u * (1f - u));
            yield return null;
        }

        navAgent.Warp(mark.position);
        transform.position = mark.position; // root motion holds the transform while it's attacking, so move it too
        FMODUnity.RuntimeManager.PlayOneShot("event:/Boss/Boss_Slam", mark.position);
        EnemyFx.Impact(mark.position, leapRadius, InkGlow);
        EnemyFx.GroundCrack(mark.position, leapRadius, InkGlow);
        Shake(0.25f);
        if (player != null && playerHealth != null)
        {
            Vector3 offset = player.position - mark.position;
            if (Mathf.Abs(offset.y) < 2.5f && new Vector2(offset.x, offset.z).magnitude <= leapRadius)
                playerHealth.TakeDamage(leapDamage, heavyAttackPoiseDamage, gameObject);
        }
        if (ring != null) ring.End();
        Destroy(mark.gameObject);
    }

    public void LeapLaunch() => _launched = true;
    public void OnLeapEnd() => OnAttackEnd();
    #endregion

    #region Mirror powers
    private void BeginSpecial()
    {
        _special = true;
        isAttacking = true;
        navAgent.isStopped = true;
        navAgent.velocity = Vector3.zero;
        FacePlayerImmediate();
        animator.CrossFadeInFixedTime("Summon", 0.15f);
        FMODUnity.RuntimeManager.PlayOneShot("event:/Boss/Boss_Roar", transform.position);
    }

    private void EndSpecial()
    {
        _special = false;
        _nextDive = Time.time + diveCooldown;
        isAttacking = false;
        OnAttackEnd();
    }

    // A mirror up out of the ink, facing the player.
    private Transform RaiseMirror(Vector3 at)
    {
        if (NavMesh.SamplePosition(at, out NavMeshHit onFloor, 3f, NavMesh.AllAreas)) at = onFloor.position;
        at = EnemyFx.FloorUnder(at + Vector3.up);
        Vector3 toPlayer = player.position - at;
        toPlayer.y = 0f;
        GameObject mirror = Instantiate(summonMirrorPrefab, at - Vector3.up * 0.05f, Quaternion.LookRotation(toPlayer));
        _summoned.Add(mirror);
        StartCoroutine(Grow(mirror.transform, 0f, 1f, 0.35f));
        EnemyFx.Impact(at, 2f, InkGlow);
        FMODUnity.RuntimeManager.PlayOneShot("event:/Environment/Teleport", at);
        return mirror.transform;
    }

    // An ink copy of the knight, stepping out of the glass.
    private Animator StepOut(Transform mirror)
    {
        if (_playerAnimator == null && player != null) _playerAnimator = player.GetComponentInChildren<Animator>();
        Vector3 at = mirror.position + mirror.forward * 0.6f;
        GameObject clone = Instantiate(inkKnightPrefab, at, mirror.rotation);
        _summoned.Add(clone);
        Transform model = _playerAnimator != null ? _playerAnimator.transform : null;
        Vector3 size = model != null ? model.lossyScale : Vector3.one;
        clone.transform.localScale = size;
        StartCoroutine(Grow(clone.transform, 0.05f, 1f, CloneRise, size));

        Animator pose = clone.GetComponent<Animator>();
        pose.fireEvents = false; // the knight's clips call the player's scripts; the copy has none
        pose.applyRootMotion = false;
        return pose;
    }

    private IEnumerator Grow(Transform thing, float from, float to, float seconds, Vector3? full = null)
    {
        Vector3 size = full ?? thing.localScale;
        for (float elapsed = 0f; elapsed < seconds && thing != null; elapsed += Time.deltaTime)
        {
            thing.localScale = new Vector3(size.x, size.y * Mathf.Lerp(from, to, elapsed / seconds), size.z);
            yield return null;
        }
        if (thing != null) thing.localScale = new Vector3(size.x, size.y * to, size.z);
    }

    // Back into the ink: the copy melts, the mirror sinks.
    private IEnumerator Melt(Transform thing, float seconds)
    {
        if (thing == null) yield break;
        EnemyFx.Impact(thing.position, 1.6f, InkGlow);
        yield return Grow(thing, 1f, 0.02f, seconds);
        if (thing != null) Destroy(thing.gameObject);
    }

    // The reflection in one of its mirrors: its own model, smaller, standing in the frame, without a mind of its own.
    private Animator Reflect(Transform mirror, string state, float at)
    {
        GameObject image = Instantiate(shadowReflectionPrefab, mirror.position + mirror.forward * 0.2f, mirror.rotation);
        image.transform.localScale = Vector3.one * ReflectionScale;
        _summoned.Add(image);

        Animator pose = image.GetComponent<Animator>();
        pose.fireEvents = false; // its clips call the boss's scripts; the reflection has none
        pose.applyRootMotion = false;
        pose.speed = 0f;
        pose.Play(state, 0, at);
        pose.Update(0f);
        return pose;
    }

    // How much of the reflection has formed: 1 not at all, 0 all of it.
    private static void SetDissolve(Animator image, float amount)
    {
        if (image == null) return;
        foreach (Renderer part in image.GetComponentsInChildren<Renderer>())
        {
            foreach (Material material in part.materials) if (material.HasProperty("_Dissolve")) material.SetFloat("_Dissolve", amount);
        }
    }

    // Glass that cracks, on one of its mirrors, and a way for the player's fists to reach it.
    private Material CrackableGlass(Transform mirror, out PunchTarget target)
    {
        Renderer glass = null;
        foreach (Renderer part in mirror.GetComponentsInChildren<Renderer>()) if (part.name == "Glass") glass = part;

        Material material = new Material(crackGlassMaterial);
        material.SetTexture("_CrackTex", GlassFx.DrawCracks(new Vector2(0.5f, 0.55f)));
        material.SetVector("_CrackCentre", new Vector4(0.5f, 0.55f, 0f, 0f));
        material.SetVector("_GlassSize", new Vector4(GlassWidth, GlassTop - GlassBottom, 0f, 0f));
        material.SetFloat("_Crack", 0f);
        if (glass != null) glass.material = material;

        BoxCollider body = mirror.gameObject.AddComponent<BoxCollider>();
        body.center = new Vector3(0f, (GlassBottom + GlassTop) * 0.5f, 0f);
        body.size = new Vector3(GlassWidth + 0.3f, GlassTop - GlassBottom + 0.3f, 0.4f);
        target = mirror.gameObject.AddComponent<PunchTarget>();
        return material;
    }

    private void ShatterMirror(Transform mirror, Animator image, bool loud = true)
    {
        if (mirror == null) return;
        foreach (Renderer part in mirror.GetComponentsInChildren<Renderer>()) if (part.name == "Glass") part.enabled = false;
        foreach (Collider body in mirror.GetComponents<Collider>()) body.enabled = false;
        GlassFx.Shatter(mirror, GlassBottom, GlassTop, GlassWidth, mirrorShardMaterial, loud ? 34 : 18);
        Vector3 centre = mirror.position + Vector3.up * (GlassBottom + GlassTop) * 0.5f;
        FMODUnity.RuntimeManager.PlayOneShot(loud ? "event:/Boss/Glass_Shatter" : "event:/Combat/Impact_Light", centre);
        EnemyFx.Impact(mirror.position + mirror.forward * 0.6f, loud ? 2.6f : 1.6f, new Color(0.75f, 0.8f, 1f, 1f));
        if (image != null) Destroy(image.gameObject);
        StartCoroutine(Melt(mirror, 0.5f));
    }

    // On one knee, leaning on its sword: the reward for beating one of its mirrors.
    private IEnumerator Stagger(float seconds)
    {
        isAttacking = true;
        navAgent.isStopped = true;
        navAgent.velocity = Vector3.zero;
        FMODUnity.RuntimeManager.PlayOneShot("event:/Combat/Impact_Heavy", transform.position);
        EnemyFx.Strike(transform.position + Vector3.up * 3f, 2f, InkGlow);
        Shake(0.12f);

        DizzyStars.Show(animator.GetBoneTransform(HumanBodyBones.Head), seconds, 1.5f);
        animator.CrossFadeInFixedTime("Die", 0.15f); // the kneel, from its death
        animator.speed = 0.6f;
        yield return new WaitForSeconds(1.4f);
        animator.speed = 0f;
        yield return new WaitForSeconds(Mathf.Max(0f, seconds - 1.9f));
        animator.speed = 1f;
        animator.CrossFadeInFixedTime(IdleState, 0.5f);
        yield return new WaitForSeconds(0.5f);
    }

    // A mirror close in front of the player. In it the boss's reflection gathers itself, over a few seconds, for a blow
    // that fills the floor around it. Punch the glass enough times first and it breaks, and the boss drops to a knee.
    private IEnumerator BreakTheMirror()
    {
        BeginSpecial();
        yield return new WaitForSeconds(0.45f);

        Vector3 toBoss = transform.position - player.position;
        toBoss.y = 0f;
        Transform mirror = RaiseMirror(player.position + toBoss.normalized * 3.5f);
        Material glass = CrackableGlass(mirror, out PunchTarget target);
        Animator image = Reflect(mirror, "Leap", 13f / 62f); // springing up, the sword overhead
        SetDissolve(image, 1f);

        int punches = 0;
        target.Punched += (at, heavy) =>
        {
            if (punches >= punchesToBreak) return;
            punches = Mathf.Min(punchesToBreak, punches + (heavy ? 2 : 1));
            glass.SetFloat("_Crack", (float)punches / punchesToBreak);
            EnemyFx.Strike(at, 0.8f, new Color(0.8f, 0.85f, 1f, 1f));
            Shake(0.03f);
        };

        GroundFx ring = null;
        for (float t = 0f; t < breakSeconds && punches < punchesToBreak; t += Time.deltaTime)
        {
            SetDissolve(image, 1f - t / breakSeconds);
            if (ring == null && t > breakSeconds - 1.5f) ring = EnemyFx.Ring(mirror, breakAoeRadius, InkGlow);
            yield return null;
        }
        if (ring != null) ring.End();

        if (punches >= punchesToBreak)
        {
            ShatterMirror(mirror, image);
            yield return Stagger(staggerSeconds);
            EndSpecial();
            yield break;
        }

        // Too slow: the reflection brings its sword down, and the whole floor around the mirror erupts.
        SetDissolve(image, 0f);
        image.speed = 1f;
        image.Play("Leap", 0, 29f / 62f);
        yield return new WaitForSeconds(0.14f);
        Vector3 at = EnemyFx.FloorUnder(mirror.position + Vector3.up);
        FMODUnity.RuntimeManager.PlayOneShot("event:/Boss/Boss_Slam", at);
        EnemyFx.Impact(at, breakAoeRadius, InkGlow);
        EnemyFx.GroundCrack(at, breakAoeRadius * 0.6f, InkGlow);
        Shake(0.3f);
        Vector3 offset = player.position - at;
        if (Mathf.Abs(offset.y) < 3f && new Vector2(offset.x, offset.z).magnitude <= breakAoeRadius)
            playerHealth.TakeDamage(breakAoeDamage, heavyAttackPoiseDamage, gameObject);

        yield return new WaitForSeconds(0.6f);
        ShatterMirror(mirror, image, false);
        yield return new WaitForSeconds(0.4f);
        EndSpecial();
    }

    // It sinks into the ink and shows itself in three mirrors around the player, each ready to lunge. Only one is
    // really it: the other two have cracked glass. Punch the real one and it falls out of it onto one knee; punch a
    // fake and it bursts in your face; wait, and the real one lunges out of its glass.
    private IEnumerator WhichIsReal()
    {
        BeginSpecial();
        yield return new WaitForSeconds(0.45f);

        _underground = true;
        Collider body = GetComponent<Collider>();
        if (body != null) body.enabled = false;
        EnemyFx.Impact(transform.position, 2.6f, InkGlow);
        FMODUnity.RuntimeManager.PlayOneShot("event:/Environment/Teleport", transform.position);
        yield return Squash(1f, 0.04f, 0.35f);
        SetVisible(false);

        const int count = 3;
        int real = Random.Range(0, count);
        float turn = Random.value * 360f;
        var mirrors = new Transform[count];
        var images = new Animator[count];
        int hit = -1;
        for (int i = 0; i < count; i++)
        {
            Vector3 at = player.position + Quaternion.Euler(0f, turn + i * 360f / count, 0f) * Vector3.forward * truthDistance;
            mirrors[i] = RaiseMirror(at);
            Material glass = CrackableGlass(mirrors[i], out PunchTarget target);
            if (i != real) glass.SetFloat("_Crack", FakeCrack);
            images[i] = Reflect(mirrors[i], "Lunge", 14f / 44f); // drawn back, about to thrust
            SetDissolve(images[i], 0f);
            int which = i;
            target.Punched += (point, heavy) => { if (hit < 0) hit = which; };
            yield return new WaitForSeconds(0.12f);
        }

        // Fakes that get punched burst; the real one, punched or not, ends it.
        bool found = false;
        for (float t = 0f; t < truthSeconds && !found; t += Time.deltaTime)
        {
            if (hit >= 0)
            {
                if (hit == real) found = true;
                else if (mirrors[hit] != null)
                {
                    Vector3 burst = mirrors[hit].position + mirrors[hit].forward * 0.8f;
                    ShatterMirror(mirrors[hit], images[hit]);
                    EnemyFx.Impact(burst, FakeBurstRadius, InkGlow);
                    Vector3 gap = player.position - burst;
                    if (Mathf.Abs(gap.y) < 2.5f && new Vector2(gap.x, gap.z).magnitude <= FakeBurstRadius)
                        playerHealth.TakeDamage(fakeBurstDamage, heavyAttackPoiseDamage, gameObject);
                    mirrors[hit] = null;
                    hit = -1;
                }
            }
            yield return null;
        }

        // Out of the real mirror; the fakes go with it.
        Transform exit = mirrors[real];
        Vector3 toPlayer = player.position - exit.position;
        toPlayer.y = 0f;
        Vector3 standAt = exit.position + exit.forward * 0.9f;
        if (NavMesh.SamplePosition(standAt, out NavMeshHit onFloor, 3f, NavMesh.AllAreas)) standAt = onFloor.position;
        for (int i = 0; i < count; i++) if (mirrors[i] != null) ShatterMirror(mirrors[i], images[i], i == real);

        navAgent.Warp(standAt);
        transform.position = standAt; // root motion holds the transform while it's attacking, so move it too
        if (toPlayer.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(toPlayer);
        transform.localScale = _scale;
        SetVisible(true);
        if (body != null) body.enabled = true;
        _underground = false;

        if (found)
        {
            yield return Stagger(staggerSeconds);
            EndSpecial();
            yield break;
        }

        // Nobody found it: out of the glass with the lunge it was holding.
        _special = false;
        _nextDive = Time.time + diveCooldown;
        isAttacking = false;
        attackCooldownTimer = 0f;
        yield return LungeAt();
    }

    // Two mirrors either side of the player; a copy of the knight dashes out of each, one after the other; then the
    // leap, down on where the player is.
    private IEnumerator MirrorAmbush()
    {
        BeginSpecial();
        yield return new WaitForSeconds(0.45f);

        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        Vector3 side = Vector3.Cross(Vector3.up, toPlayer.normalized);
        Transform left = RaiseMirror(player.position - side * ambushSide);
        Transform right = RaiseMirror(player.position + side * ambushSide);
        yield return new WaitForSeconds(0.25f);

        Animator[] copies = { StepOut(left), StepOut(right) };
        foreach (Animator copy in copies)
        {
            copy.speed = 0f;
            copy.Play(CloneDashState, 0, 0.1f); // crouched, ready to go
        }
        yield return new WaitForSeconds(CloneRise + 0.45f);

        Coroutine first = StartCoroutine(Dash(copies[0]));
        yield return new WaitForSeconds(0.45f);
        yield return Dash(copies[1]);
        yield return first;

        StartCoroutine(Melt(left, 0.4f));
        StartCoroutine(Melt(right, 0.4f));
        yield return new WaitForSeconds(0.15f);

        yield return LeapAt(player.position);
        _special = false;
        _nextDive = Time.time + diveCooldown;
        // The leap clip's own end (OnLeapEnd) finishes the attack.
    }

    // Straight at where the player is as it sets off, and on past: a roll forward or back clears it.
    private IEnumerator Dash(Animator copy)
    {
        if (copy == null || player == null) yield break;

        Transform body = copy.transform;
        Vector3 from = body.position;
        Vector3 aim = player.position - from;
        aim.y = 0f;
        Vector3 to = from + aim + aim.normalized * 4f;
        body.rotation = Quaternion.LookRotation(aim);
        copy.speed = 1.6f;
        copy.Play(CloneDashState, 0, 0.15f);
        FMODUnity.RuntimeManager.PlayOneShot("event:/Player/Player_Heavy_Attack", from);

        bool hit = false;
        float seconds = Vector3.Distance(from, to) / ambushDashSpeed;
        for (float elapsed = 0f; elapsed < seconds && body != null; elapsed += Time.deltaTime)
        {
            body.position = Vector3.Lerp(from, to, elapsed / seconds);
            Vector3 gap = player.position - body.position;
            if (!hit && Mathf.Abs(gap.y) < 2f && new Vector2(gap.x, gap.z).magnitude < 1.2f)
            {
                hit = true;
                playerHealth.TakeDamage(ambushDashDamage, heavyAttackPoiseDamage, gameObject);
                EnemyFx.Strike(player.position + Vector3.up, 1.2f, InkGlow);
            }
            yield return null;
        }
        if (body != null) StartCoroutine(Melt(body, 0.3f));
    }
    #endregion
}
