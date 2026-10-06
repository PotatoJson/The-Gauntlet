using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using TMPro;

/// <summary>
/// Pixel-art "LEVEL UP!" badge for the XP box: a gold glow around the box, a bobbing chevron and
/// label under it, and a one-off flash + sparkle burst when a level is gained.
/// Everything animates on unscaled time (menus freeze the game) and the idle motion snaps to
/// whole art pixels so it stays crisp instead of shimmering like a scaled sprite.
/// </summary>
public class LevelUpIndicator : MonoBehaviour
{
    [Header("Parts")]
    [SerializeField] private Image glow;
    [SerializeField] private Image flash;
    [SerializeField] private RectTransform badge;       // chevron + label, moves/pops as one
    [SerializeField] private RectTransform chevron;
    [SerializeField] private TMP_Text label;
    [SerializeField] private Image[] sparkles;

    [Header("Tuning")]
    [Tooltip("Size of one art pixel in this object's local units (chevron bob snaps to this).")]
    [SerializeField] private float pixelUnit = 4.6875f;
    [SerializeField] private float burstRadius = 150f;

    private Vector2 _badgeHome;
    private Vector2 _chevronHome;
    private bool _active;
    private int _pending;

    private void Awake()
    {
        _badgeHome = badge.anchoredPosition;
        _chevronHome = chevron.anchoredPosition;
        HideInstant();
    }

    private void OnDestroy()
    {
        badge.DOKill();
        flash.DOKill();
        foreach (Image s in sparkles) if (s != null) s.rectTransform.DOKill();
    }

    /// <summary>Shows or refreshes the badge for this many unclaimed level-ups.</summary>
    public void Show(int pending)
    {
        _pending = pending;
        label.text = pending > 1 ? $"LEVEL UP! x{pending}" : "LEVEL UP!";

        if (_active) return;
        _active = true;

        glow.gameObject.SetActive(true);
        badge.gameObject.SetActive(true);

        // Pop in: drop from slightly above and overshoot into place.
        badge.DOKill();
        badge.localScale = Vector3.one * 0.4f;
        badge.anchoredPosition = _badgeHome + new Vector2(0f, pixelUnit * 4f);
        badge.DOScale(1f, 0.35f).SetEase(Ease.OutBack).SetUpdate(true);
        badge.DOAnchorPos(_badgeHome, 0.35f).SetEase(Ease.OutBack).SetUpdate(true);
    }

    /// <summary>Called once per level gained, as the number ticks over.</summary>
    public void Burst()
    {
        flash.DOKill();
        flash.gameObject.SetActive(true);
        flash.color = new Color(1f, 0.97f, 0.8f, 0.75f);
        flash.DOFade(0f, 0.4f).SetEase(Ease.OutQuad).SetUpdate(true).OnComplete(() => flash.gameObject.SetActive(false));

        for (int i = 0; i < sparkles.Length; i++)
        {
            Image s = sparkles[i];
            RectTransform rt = s.rectTransform;
            rt.DOKill();
            s.DOKill();

            // The HUD sits in the screen's top-left corner, so fan out down and to the right only.
            float t01 = sparkles.Length > 1 ? i / (float)(sparkles.Length - 1) : 0.5f;
            float angle = Mathf.Lerp(-110f, 30f, t01) * Mathf.Deg2Rad + Random.Range(-0.15f, 0.15f);
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            float dist = burstRadius * Random.Range(0.7f, 1.15f);

            s.gameObject.SetActive(true);
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.one * 0.5f;
            s.color = Color.white;

            float t = Random.Range(0.55f, 0.85f);
            rt.DOAnchorPos(dir * dist, t).SetEase(Ease.OutCubic).SetUpdate(true);
            rt.DOScale(1.3f, t * 0.4f).SetLoops(2, LoopType.Yoyo).SetUpdate(true);
            s.DOFade(0f, t).SetEase(Ease.InQuad).SetUpdate(true).OnComplete(() => s.gameObject.SetActive(false));
        }
    }

    /// <summary>Hides the badge once every pending level-up has been claimed.</summary>
    public void Hide()
    {
        if (!_active) return;
        _active = false;

        badge.DOKill();
        badge.DOScale(0f, 0.2f).SetEase(Ease.InBack).SetUpdate(true).OnComplete(HideInstant);
    }

    private void HideInstant()
    {
        _active = false;
        glow.gameObject.SetActive(false);
        badge.gameObject.SetActive(false);
        flash.gameObject.SetActive(false);
        foreach (Image s in sparkles) s.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!_active) return;

        float t = Time.unscaledTime;

        // Chevron hops 0-2 art pixels, in whole-pixel steps.
        float hop = Mathf.Round(Mathf.PingPong(t * 3.5f, 2f));
        chevron.anchoredPosition = _chevronHome + new Vector2(0f, hop * pixelUnit);

        // Glow breathes between four alpha steps (matches the pixel look of the sprite).
        float pulse = Mathf.Round((0.5f + 0.5f * Mathf.Sin(t * 4f)) * 3f) / 3f;
        Color c = glow.color;
        c.a = Mathf.Lerp(0.35f, 1f, pulse);
        glow.color = c;
    }
}
