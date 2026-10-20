using DG.Tweening;
using UnityEngine;

/// <summary>
/// The small danger marker on the floor where a Caster fireball is about to land: an outline ring plus a glow
/// that fills up until the moment of impact, flickering faster as the fireball gets closer.
/// </summary>
public class FireballWarningFx : MonoBehaviour
{
    [SerializeField] private SpriteRenderer ring;
    [SerializeField] private SpriteRenderer fill;
    [SerializeField] private Color startColor = new Color(0.9f, 0.1f, 0.05f, 0.45f);
    [SerializeField] private Color endColor = new Color(1f, 0.75f, 0.25f, 0.95f);
    [SerializeField] private float startFill = 0.15f;

    private float _duration = 1f;
    private float _age;
    private float _ringAlpha = 0.9f;
    private bool _ending;

    /// <summary>Starts the marker; it fills up over <paramref name="seconds"/> (the time until impact).</summary>
    public void Begin(float radius, float seconds)
    {
        _duration = Mathf.Max(0.1f, seconds);

        if (ring != null && ring.sprite != null)
        {
            float full = radius * 2f / Mathf.Max(0.001f, ring.sprite.bounds.size.x);
            ring.transform.localScale = Vector3.one * full * 0.8f;
            ring.transform.DOScale(Vector3.one * full, 0.2f).SetEase(Ease.OutBack).SetLink(ring.gameObject);
            _ringAlpha = ring.color.a;
        }

        if (fill != null && fill.sprite != null)
        {
            float full = radius * 2f / Mathf.Max(0.001f, fill.sprite.bounds.size.x);
            fill.color = startColor;
            fill.transform.localScale = Vector3.one * full * startFill;
            fill.transform.DOScale(Vector3.one * full, _duration).SetEase(Ease.InQuad).SetLink(fill.gameObject);
            fill.DOColor(endColor, _duration).SetEase(Ease.InQuad).SetLink(fill.gameObject);
        }

        Destroy(gameObject, _duration + 1.5f); // safety net if the caster dies before calling End
    }

    /// <summary>The fireball landed: a last bright flash, then gone.</summary>
    public void End()
    {
        if (_ending) return;
        _ending = true;

        if (fill != null)
        {
            fill.DOKill();
            fill.color = new Color(1f, 0.95f, 0.7f, 1f);
            fill.DOFade(0f, 0.16f).SetLink(fill.gameObject);
        }

        if (ring != null)
        {
            ring.DOKill();
            ring.DOFade(0f, 0.16f).SetLink(ring.gameObject);
        }

        Destroy(gameObject, 0.3f);
    }

    private void Update()
    {
        if (_ending || ring == null) return;

        _age += Time.deltaTime;
        float urgency = Mathf.Clamp01(_age / _duration);

        // The ring flickers faster and brighter the closer the impact.
        float flicker = 0.5f + 0.5f * Mathf.Sin(_age * Mathf.Lerp(5f, 22f, urgency));
        Color c = ring.color;
        c.a = _ringAlpha * Mathf.Lerp(0.55f, 1f, flicker);
        ring.color = c;
    }
}
