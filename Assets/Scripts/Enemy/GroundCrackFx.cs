using DG.Tweening;
using UnityEngine;

/// <summary>
/// A patch of cracked floor where a heavy blow hit the ground. The cracks glow as they open, then stay for a
/// moment before fading away.
/// </summary>
public class GroundCrackFx : MonoBehaviour
{
    [SerializeField] private SpriteRenderer crack;
    [SerializeField] private SpriteRenderer glow;

    [SerializeField] private float openSeconds = 0.12f;
    [SerializeField] private float glowSeconds = 0.7f;
    [SerializeField] private float holdSeconds = 2.5f;
    [SerializeField] private float fadeSeconds = 1f;

    public void Play(float radius, Color? glowTint = null)
    {
        if (glowTint.HasValue) glow.color = glowTint.Value;

        float full = radius * 2f / Mathf.Max(0.001f, crack.sprite.bounds.size.x);
        transform.localScale = Vector3.one * full * 0.7f;
        transform.DOScale(Vector3.one * full, openSeconds).SetEase(Ease.OutCubic).SetLink(gameObject);

        glow.DOFade(0f, glowSeconds).SetEase(Ease.InQuad).SetLink(gameObject);
        crack.DOFade(0f, fadeSeconds).SetDelay(holdSeconds).SetLink(gameObject);

        Destroy(gameObject, holdSeconds + fadeSeconds + 0.1f);
    }
}
