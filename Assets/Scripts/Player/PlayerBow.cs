using UnityEngine;

/// <summary>
/// The Spirit Bow skill gem. Socketed in a gauntlet, that gauntlet's skill key (Q left, E right) toggles a bow of its
/// element, drawn from the gauntlet: left click fires quick arrows, holding right click draws a strong arrow that
/// pierces. While a bow is out the player strafes at a walk (no sprint), can still roll, and pays mana every second.
///
/// One bow out: every arrow is that gauntlet's element. Both gauntlets with the gem and both out: the bow is two-toned
/// (top half one element, bottom half the other) and the arrows alternate between them, the way punches alternate
/// fists, so the pair's element reaction still happens at range. Each arrow counts as its gauntlet's fist for
/// reactions and brings that gauntlet's Attunement with it.
/// </summary>
[DefaultExecutionOrder(100)] // after the camera moves (PlayerMovement.LateUpdate), so the reticle is placed on this frame's view
public class PlayerBow : MonoBehaviour
{
    // Mana
    private const float DrainPerBow = 6f;         // per second, for each bow out
    private const float ManaToDraw = 10f;         // the least it takes to bring a bow out

    // Shots, as multiples of the punch damage (PlayerStatsManager.CurrentDamage)
    private const float QuickDamage = 0.6f;
    private const float QuickCooldown = 0.35f;
    private const float QuickSpeed = 38f;
    private const float ChargeSeconds = 0.9f;     // to full draw
    private const float MinDraw = 0.35f;          // released before this much draw, nothing is loosed (no tap-spam)
    private const float ChargedMaxDamage = 2.5f;  // at full draw; a part-drawn shot scales down to a quick shot's damage
    private const float ChargedCooldown = 0.6f;   // after a drawn shot, before the next can be drawn
    private const float ChargedSpeed = 55f;
    private const float Range = 40f;

    private const string LayerName = "Bow";
    private const string AimState = "Bow_Aim", ShootState = "Bow_Shoot";

    private PlayerCombat _combat;
    private PlayerMovement _movement;
    private PlayerManager _state;
    private PlayerMana _mana;
    private PlayerStatsManager _stats;
    private Animator _animator;
    private int _layer = -1;
    private float _layerWeight;

    private bool _leftOut, _rightOut;
    private bool _nextIsLeft;
    private float _nextShot;
    private float _chargeStart = -1f;

    private SpiritBowVisual _visual;
    private BowReticle _reticle;
    private ChargePulseVFX _chargeFx;
    private bool _fullDrawCued;

    private float Draw => IsCharging ? Mathf.Clamp01((Time.time - _chargeStart) / ChargeSeconds) : 0f;

    public bool IsActive => _leftOut || _rightOut;
    public bool IsCharging => _chargeStart >= 0f;

    private void Awake()
    {
        _combat = GetComponent<PlayerCombat>();
        _movement = GetComponent<PlayerMovement>();
        _state = GetComponent<PlayerManager>();
        _mana = GetComponent<PlayerMana>();
        _stats = GetComponent<PlayerStatsManager>();
        _animator = GetComponentInChildren<Animator>();
        if (_animator != null) _layer = _animator.GetLayerIndex(LayerName);
    }

    /// <summary>Q / E with the bow gem in that gauntlet: its bow comes out, or goes away.</summary>
    public void Toggle(bool left)
    {
        bool turningOn = left ? !_leftOut : !_rightOut;
        if (turningOn && (_mana == null || _mana.CurrentMana < ManaToDraw))
        {
            FMODUnity.RuntimeManager.PlayOneShot("event:/UI/UI_Error"); // not enough mana
            return;
        }

        if (left) _leftOut = turningOn; else _rightOut = turningOn;
        FMODUnity.RuntimeManager.PlayOneShot(turningOn ? SkillSound(ElementOf(left)) : "event:/UI/UI_Back", transform.position);
        Refresh();
    }

    public void PutAway()
    {
        _leftOut = _rightOut = false;
        Refresh();
    }

    private void Refresh()
    {
        if (!IsActive) { _chargeStart = -1f; if (_chargeFx != null) _chargeFx.Cancel(); }
        if (_movement != null) _movement.Aiming = IsActive;

        if (IsActive)
        {
            if (_visual == null) _visual = new GameObject("Spirit Bow").AddComponent<SpiritBowVisual>();
            ElementType top = _rightOut ? ElementOf(false) : ElementOf(true);
            ElementType bottom = _leftOut ? ElementOf(true) : ElementOf(false);
            _visual.Show(_animator, transform, Colour(top), Colour(bottom));
            if (_layer >= 0) _animator.CrossFadeInFixedTime(AimState, 0.15f, _layer);
        }
        else if (_visual != null)
        {
            Destroy(_visual.gameObject);
            _visual = null;
        }
    }

    private void Update()
    {
        if (IsActive)
        {
            // The gem taken out of a gauntlet (or the gauntlet swapped) puts that bow away.
            if (_leftOut && !HasBowGem(true)) { _leftOut = false; Refresh(); }
            if (_rightOut && !HasBowGem(false)) { _rightOut = false; Refresh(); }
        }

        if (IsActive && Time.timeScale > 0f)
        {
            int bows = (_leftOut ? 1 : 0) + (_rightOut ? 1 : 0);
            _mana.ConsumeMana(DrainPerBow * bows * Time.deltaTime);
            if (_mana.CurrentMana <= 0f) PutAway();
        }

        // The arms raise the bow; they drop it for anything that needs them (a potion, a stagger, a roll).
        bool armsFree = IsActive && CanAct();
        _layerWeight = Mathf.MoveTowards(_layerWeight, armsFree ? 1f : 0f, Time.deltaTime * 8f);
        if (_layer >= 0) _animator.SetLayerWeight(_layer, _layerWeight);
        if (_visual != null) _visual.SetDrawn(Draw, _layerWeight);

        // Over the right shoulder while a bow is out, and a little zoom as a shot is drawn.
        if (PlayerCamera.Instance != null)
        {
            PlayerCamera.Instance.AimTarget = IsActive ? 1f : 0f;
            PlayerCamera.Instance.DrawTarget = Draw;
        }

        UpdateCharge();
        _armsFree = armsFree;

        // Standing still, he still turns to where the camera aims.
        if (IsActive && CanAct() && _movement.enabled && Camera.main != null)
        {
            Vector3 aim = Camera.main.transform.forward;
            aim.y = 0f;
            if (aim.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(aim), 12f * Time.deltaTime);
        }
    }

    private bool _armsFree;

    private void LateUpdate() => UpdateReticle(_armsFree);

    private void OnDisable() => PutAway();

    private bool CanAct()
    {
        PlayerState state = _state.GetCurrentState();
        return state == PlayerState.Idle || state == PlayerState.Walking || state == PlayerState.Attacking;
    }

    // ---------------- Shooting ----------------
    public void QuickShot()
    {
        if (!IsActive || IsCharging || Time.time < _nextShot || !CanAct()) return;
        _nextShot = Time.time + QuickCooldown;
        Fire(QuickDamage, QuickSpeed, false, 1f);
    }

    public void BeginCharge()
    {
        if (!IsActive || !CanAct() || Time.time < _nextShot) return;
        _chargeStart = Time.time;
        _fullDrawCued = false;
        FMODUnity.RuntimeManager.PlayOneShot("event:/Combat/Charge_Start", transform.position);
        if (_chargeFx == null) _chargeFx = ChargePulseVFX.Create(new GameObject("Bow Charge").transform);
        _chargeFx.Begin(Colour(ElementOf(NextIsLeft())));
    }

    public void ReleaseCharge()
    {
        if (!IsCharging) return;
        float draw = Draw;
        _chargeStart = -1f;
        if (!IsActive || !CanAct()) { if (_chargeFx != null) _chargeFx.Cancel(); return; }
        if (draw < MinDraw)
        {
            // Let go too soon: the arrow is dropped, not loosed.
            if (_chargeFx != null) _chargeFx.Cancel();
            return;
        }
        if (_chargeFx != null) _chargeFx.Release();
        _nextShot = Time.time + ChargedCooldown;
        float power = Mathf.InverseLerp(MinDraw, 1f, draw);
        Fire(Mathf.Lerp(QuickDamage, ChargedMaxDamage, power), ChargedSpeed, draw >= 0.99f, 1f + draw * 0.6f);
    }

    private void Fire(float damageScale, float speed, bool pierce, float size)
    {
        // Which gauntlet looses this one: the only bow out, or each in turn when both are.
        bool left = _leftOut && _rightOut ? (_nextIsLeft = !_nextIsLeft) : _leftOut;
        ElementType element = ElementOf(left);
        HitboxController fist = left ? _combat.LeftFist : _combat.RightFist;
        AttackPassive attunement = _combat.AttunementFor(!left);
        int damage = Mathf.Max(1, Mathf.RoundToInt(_stats.CurrentDamage * damageScale));

        Vector3 from = _visual != null ? _visual.Nock : transform.position + Vector3.up * 1.4f + transform.forward * 0.6f;
        Vector3 direction = Aim(from);
        PlayerArrow.Shoot(this, from, direction, speed, Range / speed, damage, element, Colour(element), fist, attunement, pierce, size);

        if (_layer >= 0 && _layerWeight > 0.5f)
        {
            _animator.CrossFadeInFixedTime(ShootState, 0.05f, _layer);
        }
        FMODUnity.RuntimeManager.PlayOneShot(SkillSound(element), from);
    }

    // Locked on: at the locked-on enemy (the reticle sits on it). Otherwise straight at whatever is under the reticle
    // in the middle of the screen, with no help at all: the arrow goes exactly where you point.
    private Vector3 Aim(Vector3 from)
    {
        if (PlayerCamera.Instance != null && PlayerCamera.Instance.LockOnPoint(out Vector3 locked, out _))
            return (locked - from).normalized;
        return (AimPoint(out _) - from).normalized;
    }

    // The first thing (not the player) under the middle of the screen, or a point far off along that line.
    private Vector3 AimPoint(out BaseEnemy enemy)
    {
        enemy = null;
        Camera view = Camera.main;
        if (view == null) return transform.position + transform.forward * Range;

        Ray ray = view.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        RaycastHit[] hits = Physics.RaycastAll(ray, Range + 10f, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.IsChildOf(transform)) continue;
            BaseEnemy found = hit.collider.GetComponentInParent<BaseEnemy>();
            if (found != null && found.IsDead()) continue;
            enemy = found;
            return hit.point;
        }
        return ray.origin + ray.direction * Range;
    }

    // ---------------- Charge glow and reticle ----------------
    // Which gauntlet looses the next arrow.
    private bool NextIsLeft() => _leftOut && _rightOut ? !_nextIsLeft : _leftOut;

    private void UpdateCharge()
    {
        if (_chargeFx == null) return;
        if (_visual != null) _chargeFx.transform.parent.position = _visual.Nock;
        if (!IsCharging) return;

        float draw = Draw;
        _chargeFx.SetCharge(draw);
        if (draw >= 0.99f && !_fullDrawCued)
        {
            // Fully drawn: a flash at the nock and a nudge, so you feel it's ready.
            _fullDrawCued = true;
            EnemyFx.Impact(_visual != null ? _visual.Nock : transform.position, 0.6f, Colour(ElementOf(NextIsLeft())));
            FMODUnity.RuntimeManager.PlayOneShot("event:/Combat/Impact_Light", transform.position);
            if (PlayerCamera.Instance != null) PlayerCamera.Instance.Kick(transform.forward, 0.04f);
        }
    }

    private void UpdateReticle(bool ready)
    {
        if (_reticle == null)
        {
            if (!IsActive) return;
            _reticle = BowReticle.Create();
        }
        // Locked on, the reticle sits on the lock-on marker (and is always on an enemy); else in the middle.
        // Locked on: exactly where the lock-on marker is drawn (the camera placed it this frame).
        PlayerCamera cam = PlayerCamera.Instance;
        Vector3 screen = Vector3.zero;
        bool locked = cam != null && cam.LockOnPoint(out _, out screen);
        if (locked && cam.lockOnReticle != null && cam.lockOnReticle.activeInHierarchy) screen = cam.lockOnReticle.transform.position;
        bool onEnemy = locked;
        if (ready && !locked) { AimPoint(out BaseEnemy under); onEnemy = under != null; }
        _reticle.Place(locked ? (Vector2?)new Vector2(screen.x, screen.y) : null);
        _reticle.Set(ready && Time.timeScale > 0f, Draw, Colour(ElementOf(NextIsLeft())), onEnemy);
    }

    private void OnDestroy()
    {
        // The camera outlives the player (it carries over between levels): leave it as it was.
        if (PlayerCamera.Instance != null) { PlayerCamera.Instance.AimTarget = 0f; PlayerCamera.Instance.DrawTarget = 0f; }
        if (_reticle != null) Destroy(_reticle.gameObject);
        if (_chargeFx != null) Destroy(_chargeFx.transform.parent.gameObject);
    }

    private static Vector3 Centre(Transform target)
    {
        Collider body = target.GetComponentInChildren<Collider>();
        return body != null ? body.bounds.center : target.position + Vector3.up;
    }

    // ---------------- Gauntlets ----------------
    private RunTimeGauntlet Gauntlet(bool left) => left ? _stats.SecondaryGauntlet : _stats.PrimaryGauntlet;

    private bool HasBowGem(bool left)
    {
        RunTimeGauntlet g = Gauntlet(left);
        return g != null && g.ActiveSkillGem != null && g.ActiveSkillGem.Category == SkillCategory.Bow;
    }

    private ElementType ElementOf(bool left)
    {
        RunTimeGauntlet g = Gauntlet(left);
        return g != null && g.BaseGauntlet != null ? g.BaseGauntlet.Element : ElementType.Physical;
    }

    private static string SkillSound(ElementType element) =>
        element == ElementType.Physical ? "event:/Skills/Skill_Impact" : "event:/Skills/Skill_" + element;

    public static Color Colour(ElementType element) => element switch
    {
        ElementType.Fire => ElementReactions.FireColour,
        ElementType.Ice => ElementReactions.IceColour,
        ElementType.Lightning => ElementReactions.LightningColour,
        ElementType.Wind => ElementReactions.WindColour,
        ElementType.Earth => ElementReactions.EarthColour,
        _ => new Color(0.85f, 0.85f, 0.95f),
    };
}
