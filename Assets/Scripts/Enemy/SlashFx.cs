using DG.Tweening;
using UnityEngine;

/// <summary>
/// A glowing crescent that sweeps around the vertical axis in front of an enemy: the visible arc of a sword swing.
/// The sprite is a flat arc pointing "up", so turning it flat puts the middle of the arc straight ahead.
/// </summary>
public class SlashFx : MonoBehaviour
{
    [Tooltip("Turns around the vertical axis during the swing.")]
    [SerializeField] private Transform pivot;
    [SerializeField] private SpriteRenderer arc;

    public void Play(float radius, Color tint, float sweepDegrees, float seconds)
    {
        if (arc.sprite != null)
        {
            float width = arc.sprite.bounds.size.x;
            arc.transform.localScale = Vector3.one * (radius * 2f / Mathf.Max(0.001f, width));
        }

        float alpha = tint.a;
        tint.a = 0f;
        arc.color = tint;

        float half = sweepDegrees * 0.5f;
        pivot.localRotation = Quaternion.Euler(0f, -half, 0f);
        pivot.DOLocalRotate(new Vector3(0f, half, 0f), seconds).SetEase(Ease.OutCubic).SetLink(gameObject);

        // Flares up as the swing starts and burns out as it ends.
        DOTween.Sequence().SetLink(gameObject)
            .Append(arc.DOFade(alpha, seconds * 0.25f))
            .Append(arc.DOFade(0f, seconds * 0.9f).SetEase(Ease.InQuad));

        Destroy(gameObject, seconds * 2f + 0.2f);
    }
}
