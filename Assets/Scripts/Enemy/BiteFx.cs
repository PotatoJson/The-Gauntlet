using DG.Tweening;
using UnityEngine;

/// <summary>
/// Two rows of fangs that slam shut, with a red flash as they meet. It always faces the camera, so the bite
/// reads from any angle. Plays itself the moment it is created.
/// </summary>
public class BiteFx : MonoBehaviour
{
    [Tooltip("Teeth point down.")]
    [SerializeField] private SpriteRenderer upper;
    [Tooltip("Same sprite flipped, teeth point up.")]
    [SerializeField] private SpriteRenderer lower;
    [SerializeField] private SpriteRenderer flash;

    [SerializeField] private float width = 1.3f;
    [SerializeField] private float openGap = 0.5f;
    [SerializeField] private float closedGap = 0.18f;
    [SerializeField] private float snapSeconds = 0.09f;

    private void Start()
    {
        float scale = width / Mathf.Max(0.001f, upper.sprite.bounds.size.x);
        upper.transform.localScale = lower.transform.localScale = Vector3.one * scale;

        float flashScale = width * 1.7f / Mathf.Max(0.001f, flash.sprite.bounds.size.x);
        flash.transform.localScale = Vector3.one * flashScale * 0.5f;

        upper.transform.localPosition = new Vector3(0f, openGap, 0f);
        lower.transform.localPosition = new Vector3(0f, -openGap, 0f);
        SetAlpha(upper, 0f);
        SetAlpha(lower, 0f);
        SetAlpha(flash, 0f);

        FaceCamera();

        Sequence bite = DOTween.Sequence().SetLink(gameObject);

        // Jaws appear wide open...
        bite.Append(upper.DOFade(1f, 0.05f));
        bite.Join(lower.DOFade(1f, 0.05f));

        // ...then slam shut.
        bite.Append(upper.transform.DOLocalMoveY(closedGap, snapSeconds).SetEase(Ease.InQuad));
        bite.Join(lower.transform.DOLocalMoveY(-closedGap, snapSeconds).SetEase(Ease.InQuad));

        // The impact: a red flash, a shudder, then everything fades.
        bite.Append(flash.DOFade(0.95f, 0.04f));
        bite.Join(flash.transform.DOScale(Vector3.one * flashScale, 0.18f).SetEase(Ease.OutQuad));
        bite.Join(upper.transform.DOPunchPosition(Vector3.down * 0.08f, 0.15f, 12, 0.5f));
        bite.Join(lower.transform.DOPunchPosition(Vector3.up * 0.08f, 0.15f, 12, 0.5f));
        bite.AppendInterval(0.06f);
        bite.Append(flash.DOFade(0f, 0.2f));
        bite.Join(upper.DOFade(0f, 0.2f));
        bite.Join(lower.DOFade(0f, 0.2f));

        Destroy(gameObject, 1f);
    }

    private void LateUpdate() => FaceCamera();

    private void FaceCamera()
    {
        Camera cam = Camera.main;
        if (cam != null) transform.rotation = cam.transform.rotation;
    }

    private static void SetAlpha(SpriteRenderer sprite, float alpha)
    {
        Color c = sprite.color;
        c.a = alpha;
        sprite.color = c;
    }
}
