using System.Collections;
using UnityEngine;

// Brutus up close. Now and then, instead of a blow, he grabs. In phase 1 both hands seize the player, lift them
// overhead and hurl them into the ward behind them (or, where it's broken, over the edge onto the spikes). In phase 2
// he runs them through with a sword, lifts them on it, spins, and flings them away across the floor. Both can be
// rolled out of: the grab closes on whatever is in front of him at that moment.
public partial class DemonBoss
{
    private const float GrabCloses = 13f / 24f;   // frame 13 of the grab-and-hurl clip
    private const float GrabReleases = 38f / 24f; // frame 38
    private const float StabLands = 14f / 24f;    // frame 14 of the stab-and-spin clip
    private const float SpinFrom = 24f / 24f, SpinTo = 44f / 24f, FlingAt = 46f / 24f;
    private const float GrappleClipSeconds = 2.45f;

    [Header("Grapple (close range)")]
    [SerializeField, Range(0f, 1f), Tooltip("In reach, how often he grabs rather than swings")]
    private float grappleChance = 0.3f;
    [SerializeField] private float grappleCooldown = 8f;
    [SerializeField, Tooltip("How far in front of him the player can be and still be caught")]
    private float grappleReach = 5f;
    [SerializeField, Tooltip("Hurled into a ward (phase 1)")]
    private float grabHurlDamage = 45f;
    [SerializeField, Tooltip("The sword going in (phase 2)")]
    private float stabDamage = 20f;
    [SerializeField, Tooltip("Flung off the blade (phase 2)")]
    private float flingDamage = 40f;
    [SerializeField] private float flingDistance = 9f;

    private float _nextGrapple;

    private bool TryGrapple()
    {
        if (Time.time < _nextGrapple || !CanPerformAction() || Random.value >= grappleChance) return false;

        _nextGrapple = Time.time + grappleCooldown;
        _rush = StartCoroutine(isPhase2 ? StabSpin() : GrabHurl());
        return true;
    }

    // Is the player there to be caught, right now: in front of him, close, and not rolling through it?
    private bool InGrasp()
    {
        if (player == null || PlayerHealth.DebugInvincible) return false;
        PlayerMovement movement = player.GetComponent<PlayerMovement>();
        if (movement != null && movement.IsInvincibleViaRoll) return false;
        Vector3 gap = player.position - transform.position;
        if (Mathf.Abs(gap.y) > 3f) return false;
        gap.y = 0f;
        return gap.magnitude <= grappleReach && Vector3.Angle(transform.forward, gap) < 60f;
    }

    private void BeginGrapple(string clip)
    {
        _rushing = true; // shares the charge's guards: nothing else starts, his last swing's timer can't cut it short
        isAttacking = true;
        if (_attackReset != null) StopCoroutine(_attackReset);
        _attackReset = null;
        navAgent.isStopped = true;
        navAgent.velocity = Vector3.zero;
        FacePlayerImmediate();
        animator.CrossFadeInFixedTime(clip, 0.12f);
        PlayAttackSound();
    }

    private void EndGrapple()
    {
        _rushing = false;
        isAttacking = false;
        attackCooldownTimer = attackCooldown;
        if (!IsDead() && !isIntermission) navAgent.isStopped = false;
        _rush = null;
    }

    private IEnumerator GrabHurl()
    {
        BeginGrapple("GrabHurl");
        float clock = 0f;
        for (; clock < GrabCloses; clock += Time.deltaTime)
        {
            if (clock < GrabCloses * 0.6f) FacePlayer();
            yield return null;
        }

        if (!InGrasp())
        {
            yield return new WaitForSeconds(GrappleClipSeconds - clock);
            EndGrapple();
            yield break;
        }

        // Caught: up over his head, held between his hands.
        Transform left = animator.GetBoneTransform(HumanBodyBones.LeftHand), right = animator.GetBoneTransform(HumanBodyBones.RightHand);
        _holdAt = () => (left.position + right.position) * 0.5f - Vector3.up * 1.2f;
        Catch();
        for (; clock < GrabReleases; clock += Time.deltaTime) yield return null;

        // Hurled the way he faces: into the ward there, or over the edge where there's none.
        Vector3 along = transform.forward;
        along.y = 0f;
        WardRing.Pane pane = null;
        Vector3 wardPoint = transform.position + along.normalized * WardRing.EdgeDistance(transform.position, along);
        if (_wards != null) _wards.Cast(transform.position, along, out pane, out wardPoint);
        yield return Throw(along.normalized, pane, wardPoint, false, grabHurlDamage);
        EndGrapple();
    }

    private IEnumerator StabSpin()
    {
        BeginGrapple("StabSpin");
        float clock = 0f;
        for (; clock < StabLands; clock += Time.deltaTime)
        {
            if (clock < StabLands * 0.6f) FacePlayer();
            yield return null;
        }

        if (!InGrasp())
        {
            yield return new WaitForSeconds(GrappleClipSeconds - clock);
            EndGrapple();
            yield break;
        }

        // Run through, and lifted on the blade.
        Transform tip = BladeTip(rightSword);
        _holdAt = () => (tip != null ? tip.position : transform.position + transform.forward * 3f + Vector3.up * 4f) - Vector3.up * 1.4f;
        Catch();
        playerHealth.TakeDamage(stabDamage, 0, gameObject);
        EnemyFx.Strike(player.position + Vector3.up, 1.6f, EnemyFx.Hell);
        FMODUnity.RuntimeManager.PlayOneShot("event:/Combat/Impact_Heavy", player.position);

        // The spin: a turn and a quarter, so he lets go facing somewhere new.
        Quaternion from = transform.rotation;
        for (; clock < FlingAt; clock += Time.deltaTime)
        {
            float u = Mathf.InverseLerp(SpinFrom, SpinTo, clock);
            transform.rotation = from * Quaternion.Euler(0f, 450f * Mathf.SmoothStep(0f, 1f, u), 0f);
            if (u > 0f && u < 1f && PlayerCamera.Instance != null) PlayerCamera.Instance.Kick(transform.right, 0.02f);
            yield return null;
        }

        // Flung off the blade, outwards, landing on the floor short of the edge.
        Vector3 away = player.position - transform.position;
        away.y = 0f;
        away.Normalize();
        // Measured along his floor, not from up on the blade.
        Vector3 start = player.position;
        Vector3 under = new Vector3(start.x, transform.position.y, start.z);
        float reach = Mathf.Min(flingDistance, WardRing.EdgeDistance(under, away) - 4.5f);
        float feet = _heldController != null ? _heldController.height * 0.5f - _heldController.center.y : 0.9f;
        Vector3 land = EnemyFx.FloorUnder(under + away * Mathf.Max(0.5f, reach) + Vector3.up * 2f) + Vector3.up * feet;
        ReleasePlayer(true);
        FMODUnity.RuntimeManager.PlayOneShot("event:/Boss/Boss_Attack", transform.position);
        for (float t = 0f; t < 0.5f; t += Time.deltaTime)
        {
            float u = t / 0.5f;
            player.position = Vector3.Lerp(start, land, u) + Vector3.up * (2.5f * 4f * u * (1f - u));
            yield return null;
        }
        player.position = land;
        if (_heldController != null) _heldController.enabled = true;
        EnemyFx.Impact(land, 2.4f, EnemyFx.Hell);
        if (PlayerCamera.Instance != null) PlayerCamera.Instance.Kick(away, 0.2f);
        playerHealth.TakeDamage(flingDamage, 999, gameObject);

        yield return new WaitForSeconds(Mathf.Max(0f, GrappleClipSeconds - FlingAt - 0.5f));
        EndGrapple();
    }

    // The far end of a sword: the blade trail sits there.
    private Transform BladeTip(GameObject sword)
    {
        foreach (TrailRenderer trail in _bladeTrails)
            if (trail != null && sword != null && trail.transform.IsChildOf(sword.transform)) return trail.transform;
        return sword != null ? sword.transform : null;
    }
}
