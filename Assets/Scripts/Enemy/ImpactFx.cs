using DG.Tweening;
using UnityEngine;

/// <summary>
/// A one-shot burst: a flash, a shockwave ring racing out along the ground and a spray of embers.
/// Used where a Caster fireball lands, and (bigger and in another colour) for the Caster's buff pulse.
/// </summary>
public class ImpactFx : MonoBehaviour
{
    [SerializeField] private SpriteRenderer flash;
    [SerializeField] private SpriteRenderer ring;
    [SerializeField] private ParticleSystem embers;

    [SerializeField] private float ringRadius = 2.5f;
    [SerializeField] private float ringSeconds = 0.45f;
    [SerializeField] private float flashSeconds = 0.18f;
    [SerializeField] private float lifetime = 1.6f;

    /// <summary>Plays the burst. Radius and colour can be changed per use.</summary>
    public void Play(float? radius = null, Color? tint = null)
    {
        _played = true;
        float r = radius ?? ringRadius;

        if (tint.HasValue)
        {
            if (flash != null) flash.color = tint.Value;
            if (ring != null) ring.color = tint.Value;
            if (embers != null)
            {
                var main = embers.main;
                main.startColor = tint.Value;
            }
        }

        if (ring != null && ring.sprite != null)
        {
            float width = ring.sprite.bounds.size.x;
            float full = r * 2f / Mathf.Max(0.001f, width);
            Color c = ring.color; c.a = 1f; ring.color = c;
            ring.transform.localScale = Vector3.one * full * 0.15f;
            ring.transform.DOScale(Vector3.one * full, ringSeconds).SetEase(Ease.OutCubic).SetLink(ring.gameObject);
            ring.DOFade(0f, ringSeconds).SetEase(Ease.InQuad).SetLink(ring.gameObject);
        }

        if (flash != null && flash.sprite != null)
        {
            float width = flash.sprite.bounds.size.x;
            float full = Mathf.Min(r, 3f) * 1.3f / Mathf.Max(0.001f, width);
            Color c = flash.color; c.a = 1f; flash.color = c;
            flash.transform.localScale = Vector3.one * full * 0.5f;
            flash.transform.DOScale(Vector3.one * full, flashSeconds).SetEase(Ease.OutQuad).SetLink(flash.gameObject);
            flash.DOFade(0f, flashSeconds * 1.6f).SetLink(flash.gameObject);
        }

        if (embers != null) embers.Play();

        Destroy(gameObject, lifetime);
    }

    private bool _played;

    private void Start()
    {
        // Spawned straight from a prefab with nothing calling Play: still burst once.
        if (!_played) Play();
    }
}
