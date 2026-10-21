using DG.Tweening;
using UnityEngine;

/// <summary>
/// A glowing circle on the floor that fades in, slowly turns, can follow a target, and fades out again.
/// Used for the Caster's fireball danger zone (it follows the player) and the summoning circle under the caster.
/// </summary>
public class GroundFx : MonoBehaviour
{
    [Tooltip("Rotates around the vertical axis and is scaled to the radius.")]
    [SerializeField] private Transform spin;
    [SerializeField] private SpriteRenderer[] sprites;
    [SerializeField] private ParticleSystem[] particles;

    [SerializeField] private float fadeIn = 0.35f;
    [SerializeField] private float fadeOut = 0.4f;
    [SerializeField] private float spinDegreesPerSecond = 25f;
    [Tooltip("The glow breathes by this fraction of its brightness.")]
    [SerializeField, Range(0f, 0.6f)] private float pulse = 0.2f;
    [SerializeField] private float followSharpness = 9f;
    [SerializeField] private float groundOffset = 0.05f;

    private Transform _follow;
    private Vector3 _fixedPosition;
    private float[] _baseAlpha;   // each sprite's own opacity from the prefab
    private bool _ending;
    private float _pulseClock;

    private BaseEnemy _followEnemy;
    private bool _hadFollow;
    private float _lift;

    /// <summary>How fast the circle turns, in degrees per second (the Beyblade's spin ring is much faster than a summoning circle).</summary>
    public void SetSpin(float degreesPerSecond) => spinDegreesPerSecond = degreesPerSecond;

    /// <summary>
    /// Starts the effect: a circle of this radius, following the transform (or staying where it is).
    /// An optional tint recolours it; lift raises it off the floor (a ring around a body rather than under it).
    /// If the followed object is an enemy, the circle ends by itself when that enemy dies.
    /// </summary>
    public void Begin(float radius, Transform follow = null, Color? tint = null, float lift = 0f)
    {
        _follow = follow;
        _hadFollow = follow != null;
        _followEnemy = follow != null ? follow.GetComponentInParent<BaseEnemy>() : null;
        _lift = lift;
        _fixedPosition = transform.position;

        if (tint.HasValue)
        {
            foreach (SpriteRenderer sprite in sprites)
            {
                if (sprite == null) continue;
                Color c = tint.Value; c.a = sprite.color.a; sprite.color = c;
            }

            foreach (ParticleSystem system in particles)
            {
                if (system == null) continue;
                var main = system.main;
                main.startColor = tint.Value;
            }
        }

        if (spin != null && sprites != null && sprites.Length > 0 && sprites[0].sprite != null)
        {
            float width = sprites[0].sprite.bounds.size.x;
            if (width > 0.001f) spin.localScale = Vector3.one * (radius * 2f / width);
        }

        foreach (ParticleSystem system in particles)
        {
            if (system == null) continue;

            // The emitter shape is a unit circle: scale it to the radius, but never the particles themselves.
            var main = system.main;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            system.transform.localScale = Vector3.one * radius;
            system.Play();
        }

        // Following a target, or planted on the floor where it was created.
        transform.position = GroundPoint(_follow != null ? _follow.position : transform.position);

        _baseAlpha = new float[sprites.Length];
        for (int i = 0; i < sprites.Length; i++)
        {
            SpriteRenderer sprite = sprites[i];
            if (sprite == null) continue;

            _baseAlpha[i] = sprite.color.a;
            Color c = sprite.color; c.a = 0f; sprite.color = c;
            sprite.DOFade(_baseAlpha[i], fadeIn).SetEase(Ease.OutQuad).SetLink(sprite.gameObject);
        }
    }

    /// <summary>Fades out and removes itself.</summary>
    public void End()
    {
        if (_ending) return;
        _ending = true;

        foreach (SpriteRenderer sprite in sprites)
        {
            if (sprite == null) continue;
            sprite.DOKill();
            sprite.DOFade(0f, fadeOut).SetLink(sprite.gameObject);
        }

        foreach (ParticleSystem system in particles)
        {
            if (system != null) system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        Destroy(gameObject, fadeOut + 1.2f);
    }

    private void LateUpdate()
    {
        if (spin != null) spin.Rotate(0f, spinDegreesPerSecond * Time.deltaTime, 0f, Space.World);

        // The thing it follows is gone (or dying): finish instead of hanging in the air.
        if (!_ending && (_follow == null && _hadFollow || _followEnemy != null && _followEnemy.IsDead()))
        {
            End();
        }

        if (_follow != null)
        {
            Vector3 target = GroundPoint(_follow.position);
            transform.position = Vector3.Lerp(transform.position, target, 1f - Mathf.Exp(-followSharpness * Time.deltaTime));
        }

        if (!_ending && pulse > 0f && _baseAlpha != null)
        {
            _pulseClock += Time.deltaTime;
            float breathe = 1f - pulse + pulse * Mathf.Sin(_pulseClock * 4f);
            for (int i = 0; i < sprites.Length; i++)
            {
                SpriteRenderer sprite = sprites[i];
                if (sprite == null || DOTween.IsTweening(sprite)) continue; // let the fade-in finish first
                Color c = sprite.color; c.a = _baseAlpha[i] * breathe; sprite.color = c;
            }
        }
    }

    private static readonly RaycastHit[] Hits = new RaycastHit[16];

    // The floor under a point. Characters standing there (the player, an enemy) don't count as floor.
    private Vector3 GroundPoint(Vector3 around)
    {
        int count = Physics.RaycastNonAlloc(around + Vector3.up * 2f, Vector3.down, Hits, 12f, ~0, QueryTriggerInteraction.Ignore);

        float best = float.MaxValue;
        Vector3 bestPoint = default;
        bool found = false;

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = Hits[i];
            if (hit.collider.GetComponentInParent<PlayerHealth>() != null) continue;
            if (hit.collider.GetComponentInParent<BaseEnemy>() != null) continue;

            if (hit.distance < best)
            {
                best = hit.distance;
                bestPoint = hit.point;
                found = true;
            }
        }

        if (found) return bestPoint + Vector3.up * (groundOffset + _lift);
        return new Vector3(around.x, _fixedPosition.y + _lift, around.z);
    }
}
