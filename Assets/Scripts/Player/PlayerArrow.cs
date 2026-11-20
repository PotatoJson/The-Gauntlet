using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// An arrow from the Spirit Bow: a glowing bolt with a trail in its element's colour. Each frame it sweeps a sphere
/// along its path, so fast arrows can't skip past an enemy. A hit deals damage with the usual hit feedback, applies
/// the gauntlet's Attunement, and counts as that gauntlet's fist for element reactions. A fully drawn arrow pierces
/// through enemies; anything else stops at the first one. Walls stop both.
/// </summary>
public class PlayerArrow : MonoBehaviour
{
    private const float Radius = 0.25f;
    private const string HitSound = "event:/Bow/Arrow_Hit";

    private MonoBehaviour _runner;     // the bow: chain lightning and the like run on it, not on this short-lived arrow
    private Vector3 _velocity;
    private float _life;
    private int _damage;
    private ElementType _element;
    private Color _colour;
    private HitboxController _fist;
    private AttackPassive _attunement;
    private bool _pierce;
    private float _radius;
    private Transform _shooter;
    private readonly HashSet<BaseEnemy> _hit = new HashSet<BaseEnemy>();

    public static void Shoot(MonoBehaviour runner, Vector3 from, Vector3 direction, float speed, float seconds, int damage,
                             ElementType element, Color colour, HitboxController fist, AttackPassive attunement, bool pierce, float size)
    {
        var arrow = new GameObject("Spirit Arrow").AddComponent<PlayerArrow>();
        arrow.transform.SetPositionAndRotation(from, Quaternion.LookRotation(direction));
        arrow._runner = runner;
        arrow._shooter = runner.transform;
        arrow._velocity = direction.normalized * speed;
        arrow._life = seconds;
        arrow._damage = damage;
        arrow._element = element;
        arrow._colour = colour;
        arrow._fist = fist;
        arrow._attunement = attunement;
        arrow._pierce = pierce;
        arrow._radius = Radius * size;
        arrow.Build(size);
    }

    private void Build(float size)
    {
        Color hdr = _colour * 1.5f; // bright enough to bloom, not so bright it turns white
        var material = new Material(Shader.Find("Sprites/Default")) { color = hdr };

        // The bolt: a short bright line along the flight.
        var bolt = gameObject.AddComponent<LineRenderer>();
        bolt.sharedMaterial = material;
        bolt.useWorldSpace = false;
        bolt.positionCount = 2;
        bolt.SetPosition(0, Vector3.back * 0.7f * size);
        bolt.SetPosition(1, Vector3.zero);
        bolt.startWidth = 0.02f * size;
        bolt.endWidth = 0.12f * size;
        bolt.numCapVertices = 2;
        bolt.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        var trail = new GameObject("Trail").AddComponent<TrailRenderer>();
        trail.transform.SetParent(transform, false);
        trail.sharedMaterial = material;
        trail.time = 0.18f;
        trail.startWidth = 0.1f * size;
        trail.endWidth = 0f;
        trail.minVertexDistance = 0.1f;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        var light = gameObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = _colour;
        light.range = 2.5f * size;
        light.intensity = 2f;
        light.shadows = LightShadows.None;
    }

    private void Update()
    {
        _life -= Time.deltaTime;
        if (_life <= 0f) { Destroy(gameObject); return; }

        Vector3 step = _velocity * Time.deltaTime;
        float distance = step.magnitude;
        RaycastHit[] hits = Physics.SphereCastAll(transform.position, _radius, step.normalized, distance, ~0, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.IsChildOf(_shooter)) continue; // the player, and his own hitboxes

            BaseEnemy enemy = hit.collider.GetComponentInParent<BaseEnemy>();
            if (enemy != null)
            {
                if (enemy.IsDead() || !_hit.Add(enemy)) continue;
                Vector3 at = hit.point == Vector3.zero ? transform.position : hit.point;
                Strike(enemy, at);
                ArrowHitFx.Play(at, _velocity, _colour, _pierce);
                if (!_pierce) { Destroy(gameObject); return; }

                // Through it, and on behind it. The bow is held above an enemy's middle, so the shot comes in a
                // little downward and would bury itself in the floor just past them; it levels out instead.
                if (_velocity.y < 0f)
                {
                    float speed = _velocity.magnitude;
                    _velocity.y = 0f;
                    _velocity = _velocity.normalized * speed;
                    step = _velocity * Time.deltaTime;
                    transform.rotation = Quaternion.LookRotation(_velocity);
                    break; // what else this sweep found lay along the old, downward path (the floor, usually)
                }
                continue;
            }

            // Scenery stops it; other triggers (rooms, pickups) don't.
            if (!hit.collider.isTrigger)
            {
                ArrowHitFx.Play(hit.point == Vector3.zero ? transform.position : hit.point, _velocity, _colour, _pierce, solid: true);
                FMODUnity.RuntimeManager.PlayOneShot(HitSound, transform.position);
                Destroy(gameObject);
                return;
            }
        }

        transform.position += step;
    }

    private void Strike(BaseEnemy enemy, Vector3 at)
    {
        CombatInput kind = _pierce ? CombatInput.Heavy : CombatInput.Light;

        HitFeel.InMelee = true; // play the hit feedback below, not BaseEnemy's own
        try { enemy.TakeDamage(_damage); }
        finally { HitFeel.InMelee = false; }
        HitFeel.EnemyHit(enemy, enemy.IsDead() ? HitStrength.Kill : _pierce ? HitStrength.Heavy : HitStrength.Light);
        FMODUnity.RuntimeManager.PlayOneShot(HitSound, at);

        // As if the gauntlet that shot it had punched: its Attunement, and its half of a reaction.
        AttackPassiveEffects.OnHit(_attunement, enemy, _damage, kind, _runner);
        if (_fist != null) ElementReactions.OnPunch(_fist, _element, _attunement, enemy, _damage);
    }

}
