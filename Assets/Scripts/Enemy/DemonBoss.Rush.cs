using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// Brutus's arena and his charge. The fight raises a ring of fire wards round the edge of his platform, over the spikes.
// He charges in a straight line, very fast: a player who rolls clear leaves him to smash into a ward and stand there
// dazed for a moment; a player who doesn't is caught, carried, and thrown into it. Each ward takes two blows. His roar
// into phase 2 (a short cutscene) breaks every ward, and from then on a throw goes over the edge onto the spikes.
public partial class DemonBoss
{
    private const float BodyRadius = 1.8f;      // how far in front of him the ward stops him
    private const float ThrowRelease = 0.45f;   // frame 11 of the throw clip
    private const float CarryHeight = 2.8f, CarryAhead = 2.2f;
    private const float BounceBack = 4f;        // how far back off the ward a thrown player lands, behind him

    [Header("Wards")]
    [SerializeField] private GameObject wardPostPrefab;
    [SerializeField] private Material wardPostMaterial;
    [SerializeField] private Material wardGlassMaterial;
    [SerializeField] private Material wardShardMaterial;

    [Header("Rush")]
    [SerializeField, Tooltip("How fast he charges, in metres a second")]
    private float rushSpeed = 26f;
    [SerializeField, Tooltip("How long he winds up; for the first part of it he still turns to follow the player")]
    private float rushWindupSeconds = 1f;
    [SerializeField, Range(0f, 1f), Tooltip("Of the wind-up, how much he spends turning before the line is set")]
    private float rushAimShare = 0.55f;
    [SerializeField, Tooltip("How close the player has to be to his path to be caught")]
    private float rushCatchRadius = 2.2f;
    [SerializeField, Tooltip("Thrown into a ward")]
    private float rushThrowDamage = 55f;
    [SerializeField, Tooltip("How long he stands dazed after charging into a ward")]
    private float rushStunSeconds = 2f;
    [SerializeField] private float rushCooldown = 4f;
    [SerializeField, Tooltip("He charges at a player at least this far away")]
    private float rushMinDistance = 5f;

    private WardRing _wards;
    private bool _rushing, _carrying;
    private float _nextRush;
    private PlayerMovement _heldMovement;
    private CharacterController _heldController;
    private Coroutine _rush;

    // The fight starts: the wards come up round the floor he stands on.
    private void RaiseWards()
    {
        if (_wards != null || wardPostPrefab == null || wardGlassMaterial == null) return;

        Vector3 centre = transform.position;
        EnemySpawner spawner = FindFirstObjectByType<EnemySpawner>();
        if (spawner != null)
        {
            ChamberData chamber = spawner.chambers.Find(c => c.activeEnemies.Contains(gameObject));
            if (chamber != null && chamber.chamberTriggers.Count > 0 && chamber.chamberTriggers[0] != null)
                centre = chamber.chamberTriggers[0].bounds.center;
        }
        _wards = WardRing.Raise(centre, wardPostPrefab, wardPostMaterial, wardGlassMaterial, wardShardMaterial);
        _nextRush = Time.time + rushCooldown * 0.5f;
    }

    private bool TryRush(float distance)
    {
        if (distance < rushMinDistance || Time.time < _nextRush || !CanPerformAction() || _wards == null) return false;

        _nextRush = Time.time + rushCooldown;
        _rush = StartCoroutine(Rush());
        return true;
    }

    private IEnumerator Rush()
    {
        if (_attackReset != null) StopCoroutine(_attackReset); // the last swing's timer has nothing to do with this
        _attackReset = null;
        _rushing = true;
        isAttacking = true;
        _outOfReachSeconds = 0f;
        navAgent.isStopped = true;
        navAgent.velocity = Vector3.zero;
        animator.CrossFadeInFixedTime("RushWindup", 0.15f);
        FMODUnity.RuntimeManager.PlayOneShot("event:/Boss/Boss_Roar", transform.position);
        EnemyFx.Impact(transform.position, 3f, isPhase2 ? EnemyFx.Hell : EnemyFx.Fire);

        // He turns to follow the player, then the line is set: a burning stripe on the floor shows where it runs.
        Vector3 along = transform.forward;
        Vector3 stop = transform.position;
        WardRing.Pane pane = null;
        Vector3 wardPoint = Vector3.zero;
        GameObject lane = null;
        Material laneMaterial = null;
        for (float elapsed = 0f; elapsed < rushWindupSeconds; elapsed += Time.deltaTime)
        {
            if (elapsed < rushWindupSeconds * rushAimShare)
            {
                Vector3 toPlayer = player.position - transform.position;
                toPlayer.y = 0f;
                if (toPlayer.sqrMagnitude > 0.01f)
                {
                    along = toPlayer.normalized;
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(along), 540f * Time.deltaTime);
                }
            }
            else if (lane == null)
            {
                transform.rotation = Quaternion.LookRotation(along);
                stop = RushStop(along, out pane, out wardPoint);
                lane = Lane(transform.position, stop, out laneMaterial);
            }
            if (laneMaterial != null)
                laneMaterial.SetFloat("_Rise", Mathf.InverseLerp(rushWindupSeconds * rushAimShare, rushWindupSeconds * 0.85f, elapsed));
            yield return null;
        }
        if (lane == null) stop = RushStop(along, out pane, out wardPoint);
        if (lane != null) Destroy(lane);

        // The charge.
        animator.CrossFadeInFixedTime("RushRun", 0.06f);
        FMODUnity.RuntimeManager.PlayOneShot("event:/Boss/Brutus_Rush", transform.position);
        PlayerMovement movement = player.GetComponent<PlayerMovement>();
        float stepClock = 0f;
        while (Vector3.Distance(Flat(transform.position), Flat(stop)) > 0.05f)
        {
            transform.position = Vector3.MoveTowards(transform.position, new Vector3(stop.x, transform.position.y, stop.z), rushSpeed * Time.deltaTime);
            navAgent.nextPosition = transform.position;

            stepClock += Time.deltaTime;
            if (stepClock > 0.16f)
            {
                stepClock = 0f;
                FMODUnity.RuntimeManager.PlayOneShot("event:/Enemies/Grunt_Footstep", transform.position);
                EnemyFx.Impact(transform.position, 1.2f, isPhase2 ? EnemyFx.Hell : EnemyFx.Fire);
                if (PlayerCamera.Instance != null && GetDistanceToPlayer() < 12f) PlayerCamera.Instance.Kick(along, 0.04f);
            }

            if (!_carrying)
            {
                Vector3 gap = player.position - transform.position;
                bool rolling = movement != null && movement.IsInvincibleViaRoll;
                if (!rolling && !PlayerHealth.DebugInvincible && Mathf.Abs(gap.y) < 3f && Flat(gap).magnitude < rushCatchRadius)
                {
                    Catch();
                    animator.CrossFadeInFixedTime("RushCarry", 0.08f);
                }
            }
            yield return null;
        }
        navAgent.Warp(transform.position);

        if (_carrying) yield return Throw(along, pane, wardPoint);
        else yield return Crash(pane, wardPoint);

        _rushing = false;
        isAttacking = false;
        attackCooldownTimer = attackCooldown * 0.5f;
        if (!IsDead() && !isIntermission) navAgent.isStopped = false;
        _rush = null;
    }

    // The far end of the charge: just short of the ward in the way, or of the edge of the floor where there's none.
    private Vector3 RushStop(Vector3 along, out WardRing.Pane pane, out Vector3 wardPoint)
    {
        bool ward = _wards.Cast(transform.position, along, out pane, out wardPoint);
        float reach = ward && !pane.Broken
            ? Vector3.Distance(Flat(transform.position), Flat(wardPoint)) - BodyRadius
            : WardRing.EdgeDistance(transform.position, along) - BodyRadius * 0.6f;
        Vector3 stop = transform.position + along * Mathf.Max(0f, reach);
        if (NavMesh.SamplePosition(stop, out NavMeshHit onFloor, 2f, NavMesh.AllAreas)) stop = onFloor.position;
        return stop;
    }

    // The line it will run, burning on the floor.
    private GameObject Lane(Vector3 from, Vector3 to, out Material material)
    {
        Vector3 along = Flat(to - from);
        float length = along.magnitude;
        GameObject lane = new GameObject("RushLane", typeof(MeshFilter), typeof(MeshRenderer));
        lane.transform.SetPositionAndRotation(EnemyFx.FloorUnder(from + Vector3.up) + Vector3.up * 0.05f,
            Quaternion.LookRotation(Vector3.down, along.normalized)); // its local y runs along the floor
        float half = BodyRadius;
        Mesh mesh = new Mesh();
        mesh.vertices = new[] { new Vector3(-half, 0f, 0f), new Vector3(half, 0f, 0f), new Vector3(half, length, 0f), new Vector3(-half, length, 0f) };
        mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
        mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        mesh.RecalculateNormals();
        lane.GetComponent<MeshFilter>().sharedMesh = mesh;
        material = new Material(wardGlassMaterial);
        material.SetFloat("_Rise", 0f);
        material.SetFloat("_Strength", 0.9f);
        material.SetColor("_Color", isPhase2 ? EnemyFx.Hell : EnemyFx.Fire);
        lane.GetComponent<MeshRenderer>().sharedMaterial = material;
        Destroy(lane, 5f);
        return lane;
    }

    // In his arms, carried along in front of him.
    private void Catch()
    {
        _carrying = true;
        _heldMovement = player.GetComponent<PlayerMovement>();
        _heldController = player.GetComponent<CharacterController>();
        if (_heldMovement != null) _heldMovement.enabled = false;
        if (_heldController != null) _heldController.enabled = false;
        if (playerManager != null) playerManager.SetPlayerState(PlayerState.Staggered);
        if (GameplayInputGate.Instance != null) GameplayInputGate.Instance.SuspendWhileRunning(this);
        Animator held = player.GetComponentInChildren<Animator>();
        if (held != null) held.CrossFadeInFixedTime("KnockbackHit", 0.1f);
        FMODUnity.RuntimeManager.PlayOneShot("event:/Enemies/Grabber_Grab", player.position);
        EnemyFx.Strike(player.position + Vector3.up, 1.6f, EnemyFx.Fire);
        if (PlayerCamera.Instance != null) PlayerCamera.Instance.Kick(transform.forward, 0.12f);
    }

    // Where a held player goes, if not out in front of him at chest height (a grab overhead, a blade).
    private System.Func<Vector3> _holdAt;

    private void LateUpdate()
    {
        if (!_carrying || player == null) return;
        player.position = _holdAt != null ? _holdAt() : transform.position + transform.forward * CarryAhead + Vector3.up * CarryHeight;
        player.rotation = Quaternion.LookRotation(-transform.forward);
    }

    // Lets go of the player wherever they are, controls back. Also for anything that cuts the charge short.
    private void ReleasePlayer(bool keepFalling = false)
    {
        if (!_carrying) return;
        _carrying = false;
        _holdAt = null;
        if (!keepFalling && _heldController != null) _heldController.enabled = true;
        if (_heldMovement != null) _heldMovement.enabled = true;
        if (playerManager != null) playerManager.SetPlayerState(PlayerState.Idle);
        if (GameplayInputGate.Instance != null) GameplayInputGate.Instance.RestoreWhenReleased(this);
    }

    private IEnumerator Throw(Vector3 along, WardRing.Pane pane, Vector3 wardPoint, bool ownClip = true, float damage = -1f)
    {
        if (damage < 0f) damage = rushThrowDamage;
        if (ownClip)
        {
            animator.CrossFadeInFixedTime("RushThrow", 0.05f);
            yield return new WaitForSeconds(ThrowRelease);
        }
        if (player == null) yield break;

        bool ward = pane != null && !pane.Broken;
        FMODUnity.RuntimeManager.PlayOneShot("event:/Boss/Boss_Attack", transform.position);
        if (ward)
        {
            // Into the ward: it holds, and the player takes the whole of it.
            Vector3 against = new Vector3(wardPoint.x, transform.position.y, wardPoint.z) - along * 0.7f;
            Vector3 from = player.position;
            ReleasePlayer(true);
            for (float t = 0f; t < 0.12f; t += Time.deltaTime)
            {
                player.position = Vector3.Lerp(from, against + Vector3.up * 1.5f, t / 0.12f);
                yield return null;
            }
            _wards.Hit(pane, wardPoint + Vector3.up * 1.5f);
            FMODUnity.RuntimeManager.PlayOneShot("event:/Boss/Boss_Slam", wardPoint);
            if (PlayerCamera.Instance != null) PlayerCamera.Instance.Kick(along, 0.25f);

            // Off the ward and back past him onto the floor, so the knockdown (away from him) carries them inwards,
            // not out through the gap if this was the blow that broke it.
            float feet = _heldController != null ? _heldController.height * 0.5f - _heldController.center.y : 0.9f;
            Vector3 landing = EnemyFx.FloorUnder(new Vector3(wardPoint.x, transform.position.y, wardPoint.z) - along * BounceBack + Vector3.up) + Vector3.up * feet;
            Vector3 off = player.position;
            for (float t = 0f; t < 0.25f; t += Time.deltaTime)
            {
                float u = t / 0.25f;
                player.position = Vector3.Lerp(off, landing, u) + Vector3.up * (1.5f * 4f * u * (1f - u));
                yield return null;
            }
            player.position = landing;
            if (_heldController != null) _heldController.enabled = true;
            playerHealth.TakeDamage(damage, 999, gameObject); // a knockdown
        }
        else
        {
            // No ward to stop them: over the edge and down onto the spikes.
            Vector3 from = player.position;
            Vector3 over = new Vector3(from.x, from.y, from.z) + along * 9f + Vector3.up * 2f;
            ReleasePlayer(true);
            for (float t = 0f; t < 0.5f; t += Time.deltaTime)
            {
                float u = t / 0.5f;
                player.position = Vector3.Lerp(from, over, u) + Vector3.up * (2.5f * 4f * u * (1f - u));
                yield return null;
            }
            Vector3 fallFrom = player.position;
            Vector3 spikes = fallFrom + along * 2f + Vector3.down * 14f;
            for (float t = 0f; t < 0.55f; t += Time.deltaTime)
            {
                player.position = Vector3.Lerp(fallFrom, spikes, (t / 0.55f) * (t / 0.55f));
                yield return null;
            }
            EnemyFx.Impact(player.position, 2.5f, EnemyFx.Hell);
            FMODUnity.RuntimeManager.PlayOneShot("event:/Environment/Spike_Trap", player.position);
            if (_heldController != null) _heldController.enabled = true;
            playerHealth.Kill(gameObject); // the spikes: no surviving that
        }
        yield return new WaitForSeconds(0.8f);
    }

    // Nobody in his arms: he ploughs into the ward and reels, or skids to a stop at the edge where there's none left.
    private IEnumerator Crash(WardRing.Pane pane, Vector3 wardPoint)
    {
        if (pane != null && !pane.Broken)
        {
            _wards.Hit(pane, wardPoint + Vector3.up * 2f);
            FMODUnity.RuntimeManager.PlayOneShot("event:/Boss/Boss_Slam", wardPoint);
            EnemyFx.Impact(wardPoint, 3f, EnemyFx.Fire);
            if (PlayerCamera.Instance != null && GetDistanceToPlayer() < 25f) PlayerCamera.Instance.Kick(transform.forward, 0.18f);
            animator.CrossFadeInFixedTime("RushStunned", 0.05f);
            EnemyFx.Strike(transform.position + Vector3.up * 6f, 2.4f, Color.white);
            DizzyStars.Show(animator.GetBoneTransform(HumanBodyBones.Head), rushStunSeconds, 3f);
            yield return new WaitForSeconds(rushStunSeconds);
            animator.CrossFadeInFixedTime("HumanM@Idle01", 0.3f);
            yield break;
        }

        animator.CrossFadeInFixedTime("HumanM@Idle01", 0.2f);
        EnemyFx.Impact(transform.position, 2.4f, EnemyFx.Hell);
        yield return new WaitForSeconds(0.7f);
    }

    // Anything that cuts the charge off (the roar, death) drops whoever he's carrying and clears up.
    private void StopRush()
    {
        if (_rush != null) StopCoroutine(_rush);
        _rush = null;
        ReleasePlayer();
        _rushing = false;
    }

    // Phase 2: his roar, seen in a short cutscene. It blasts out across the floor, and every ward breaks.
    private IEnumerator Enrage()
    {
        CutsceneCamera cut = CutsceneCamera.Begin();
        Vector3 feet = transform.position;
        Vector3 head = feet + Vector3.up * 5.5f;
        Vector3 front = transform.forward;
        Vector3 side = transform.right;

        // Close, from low in front: he draws himself up.
        Vector3 close = InTheClear(feet + front * 8f + side * 2.5f + Vector3.up * 2.2f, head);
        yield return cut.Shot(InTheClear(close + front * 1.5f, head), close, () => head, () => head, 1.3f);

        // The roar.
        FMODUnity.RuntimeManager.PlayOneShot("event:/Boss/Boss_Roar", feet);
        cut.Shake = 0.25f;
        for (int ring = 0; ring < 5; ring++)
        {
            EnemyFx.Impact(feet, 4f + ring * 4f, EnemyFx.Rage);
            if (ring == 1) EnemyFx.GroundCrack(feet, 7f, EnemyFx.Rage);
            yield return new WaitForSecondsRealtime(0.14f);
        }
        cut.Shake = 0.1f;

        // Wide, from up and back across the floor: the blast reaches the edge and the wards go, one after another.
        Vector3 high = InTheClear(feet + front * 9f + side * 7f + Vector3.up * 9f, feet + Vector3.up * 2f);
        StartCoroutine(cut.Shot(cut.Position, high, () => head, () => feet, 0.6f));
        yield return new WaitForSecondsRealtime(0.3f);
        if (_wards != null) yield return _wards.ShatterAll(feet, 1.4f);
        cut.Shake = 0f;
        yield return new WaitForSecondsRealtime(1.1f);
        cut.End();
    }

    // A camera spot with a clear view: kept under the ceiling, and pulled in front of any wall between it and what it looks at.
    private static Vector3 InTheClear(Vector3 spot, Vector3 lookAt)
    {
        if (Physics.Raycast(lookAt, Vector3.up, out RaycastHit roof, spot.y - lookAt.y + 2f, ~0, QueryTriggerInteraction.Ignore))
            spot.y = Mathf.Min(spot.y, roof.point.y - 1.5f);
        Vector3 toSpot = spot - lookAt;
        if (Physics.Raycast(lookAt, toSpot.normalized, out RaycastHit wall, toSpot.magnitude, ~0, QueryTriggerInteraction.Ignore))
            spot = wall.point - toSpot.normalized * 0.8f;
        return spot;
    }

    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
}
