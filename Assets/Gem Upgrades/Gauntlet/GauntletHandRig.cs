using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Keeps the gauntlet on the character screen alive. Most of the time the hand breathes at the wrist, the fingers flex
/// slightly, and now and then it makes a gesture (a fist, a claw, a wrist swing or a finger wave).
///
/// While a reward is waiting to be claimed the whole arm swings upright and the hand clenches into a fist, with energy
/// gathering around it. Claiming a gem releases a pulse as the fist opens and the arm goes back to where it was.
/// Returning the gem to the board starts the charge again.
///
/// Each digit is a pivot at its knuckle. Squashing the pivot along its length reads as the finger curling, because
/// the digits are drawn behind the palm. The gems sit on the palm, so they ride along with the wrist but never move
/// with the fingers. Everything runs on unscaled time (the screen pauses the game), no gesture starts while the
/// player is holding a gem, and all of it stops when the "ReduceMotion" setting is on.
/// </summary>
public class GauntletHandRig : MonoBehaviour
{
    private const int Thumb = 4;
    private const float ClenchedLength = 0.36f;
    private const float ThumbClenchedLength = 0.82f;
    private const float ThumbClenchedBend = -32f;

    private enum Pose { Idle, Charging, Releasing }

    [Tooltip("Everything that turns at the wrist: digits, palm and sockets.")]
    [SerializeField] private RectTransform hand;
    [Tooltip("Knuckle pivots, in order: index, middle, ring, pinky, thumb.")]
    [SerializeField] private RectTransform[] digits;

    [Header("Idle gestures")]
    [SerializeField] private float minGestureGap = 8f;
    [SerializeField] private float maxGestureGap = 16f;

    [Header("Reward pose")]
    [Tooltip("Rotation of the whole arm that stands it upright.")]
    [SerializeField] private float uprightAngle = -55f;
    [Tooltip("Where the upright arm sits, so the fingers clear the banner and the hand clears the reward gems.")]
    [SerializeField] private Vector2 uprightOffset = new Vector2(43f, 70f);

    private float[] _restAngle;
    private float[] _bend;
    private float[] _length;
    private float _swing;
    private float _armAngle;
    private Vector2 _armOffset;
    private float _nextGesture;
    private Sequence _sequence;
    private Pose _pose = Pose.Idle;

    private RectTransform _palm;
    private RectTransform _fxRoot;
    private Image _halo, _rays;
    private Image[] _sparks;
    private float _fxStrength;
    private Color _glow = Color.white;

    private void Awake()
    {
        _restAngle = new float[digits.Length];
        _bend = new float[digits.Length];
        _length = new float[digits.Length];
        for (int i = 0; i < digits.Length; i++) _restAngle[i] = digits[i].localEulerAngles.z;
        _palm = (RectTransform)hand.Find("Palm");
        _glow = GlowColor();
    }

    private void OnEnable()
    {
        ResetPose();
        ScheduleNextGesture();
    }

    private void OnDisable()
    {
        _sequence?.Kill();
        ResetPose();
        Apply(0f);
        UpdateFx();
    }

    private void ResetPose()
    {
        _pose = Pose.Idle;
        _swing = 0f;
        _armAngle = 0f;
        _armOffset = Vector2.zero;
        _fxStrength = 0f;
        ResetDigits();
    }

    private void ResetDigits()
    {
        for (int i = 0; i < digits.Length; i++)
        {
            _bend[i] = 0f;
            _length[i] = 1f;
        }
    }

    private void ScheduleNextGesture()
    {
        _nextGesture = Time.unscaledTime + Random.Range(minGestureGap, maxGestureGap);
    }

    private void Update()
    {
        if (GauntletScreenFx.ReduceMotion)
        {
            Apply(0f);
            return;
        }

        RewardMenuManager reward = RewardMenuManager.Instance;
        bool rewardWaiting = reward != null && reward.IsRewardModeActive() && !reward.RewardClaimed;

        if (rewardWaiting && _pose != Pose.Charging) BeginCharge();
        else if (!rewardWaiting && _pose == Pose.Charging) BeginRelease();

        if (_pose == Pose.Idle && Time.unscaledTime >= _nextGesture && (_sequence == null || !_sequence.IsActive()))
        {
            ScheduleNextGesture();
            if (!PlayerIsBusy()) PlayRandomGesture();
        }

        Apply(1f);
        UpdateFx();
    }

    private void Apply(float idle)
    {
        float t = Time.unscaledTime;

        transform.localRotation = Quaternion.Euler(0f, 0f, _armAngle);
        transform.localPosition = _armOffset;

        // A clenched fist strains a little as the energy builds.
        float strain = Mathf.Sin(t * 26f) * 0.5f * _fxStrength;
        hand.localRotation = Quaternion.Euler(0f, 0f, _swing + strain + Mathf.Sin(t * 1.1f) * 1.2f * idle);

        for (int i = 0; i < digits.Length; i++)
        {
            float flex = Mathf.Sin(t * 1.7f + i * 0.9f) * 1.3f * idle;
            digits[i].localRotation = Quaternion.Euler(0f, 0f, _restAngle[i] + _bend[i] + flex);
            digits[i].localScale = new Vector3(1f, _length[i], 1f);
        }
    }

    private static bool PlayerIsBusy()
    {
        if (Mouse.current != null && Mouse.current.leftButton.isPressed) return true;
        return GemPopupMenu.Instance != null &&
               (GemPopupMenu.Instance.gameObject.activeInHierarchy || GemPopupMenu.Instance.IsPlacingMode);
    }

    #region Reward pose
    // Stand the arm up, close the fist, and let the energy gather.
    private void BeginCharge()
    {
        _sequence?.Kill();
        _pose = Pose.Charging;
        _glow = GlowColor();
        BuildFx();

        _sequence = DOTween.Sequence();
        _sequence.Insert(0f, ArmTo(uprightAngle, uprightOffset, 0.65f, Ease.OutBack));
        _sequence.Insert(0f, SwingTo(0f, 0.3f, Ease.OutQuad));

        for (int i = 0; i < Thumb; i++)
        {
            float at = 0.25f + (Thumb - 1 - i) * 0.04f;
            _sequence.Insert(at, LengthTo(i, ClenchedLength, 0.18f, Ease.InQuad));
            _sequence.Insert(at, BendTo(i, 0f, 0.18f, Ease.InQuad));
        }

        _sequence.Insert(0.4f, LengthTo(Thumb, ThumbClenchedLength, 0.16f, Ease.InQuad));
        _sequence.Insert(0.4f, BendTo(Thumb, ThumbClenchedBend, 0.16f, Ease.InQuad));
        _sequence.Insert(0.35f, FxTo(1f, 0.45f));
        _sequence.SetUpdate(true).SetLink(gameObject);
    }

    // The reward was claimed: the pulse goes out as the fist springs open, then the arm settles back.
    private void BeginRelease()
    {
        _sequence?.Kill();
        _pose = Pose.Releasing;
        ReleasePulse();

        _sequence = DOTween.Sequence();
        _sequence.Insert(0f, FxTo(0f, 0.2f));

        for (int i = 0; i < digits.Length; i++)
        {
            float at = i == Thumb ? 0.04f : i * 0.025f;
            _sequence.Insert(at, LengthTo(i, 1f, 0.34f, Ease.OutBack));
            _sequence.Insert(at, BendTo(i, 0f, 0.34f, Ease.OutBack));
        }

        _sequence.Insert(0.32f, ArmTo(0f, Vector2.zero, 0.7f, Ease.InOutCubic));
        _sequence.OnComplete(() =>
        {
            _pose = Pose.Idle;
            ScheduleNextGesture();
        });
        _sequence.SetUpdate(true).SetLink(gameObject);
    }

    private Sequence ArmTo(float angle, Vector2 offset, float seconds, Ease ease)
    {
        return DOTween.Sequence()
            .Join(DOTween.To(() => _armAngle, v => _armAngle = v, angle, seconds).SetEase(ease))
            .Join(DOTween.To(() => _armOffset, v => _armOffset = v, offset, seconds).SetEase(ease));
    }

    private Tween FxTo(float value, float seconds)
    {
        return DOTween.To(() => _fxStrength, v => _fxStrength = v, value, seconds).SetEase(Ease.OutQuad);
    }

    // The same colour as the gauntlet's glowing trim.
    private Color GlowColor()
    {
        GauntletManager manager = GetComponent<GauntletManager>();
        ElementType element = manager != null && manager.LinkedGauntletData != null ? manager.LinkedGauntletData.Element : ElementType.Physical;

        switch (element)
        {
            case ElementType.Fire: return new Color32(0xFF, 0x8A, 0x2E, 0xFF);
            case ElementType.Ice: return new Color32(0x6C, 0xD0, 0xFF, 0xFF);
            case ElementType.Earth: return new Color32(0xB8, 0xE8, 0x4C, 0xFF);
            case ElementType.Lightning: return new Color32(0xFF, 0xE8, 0x40, 0xFF);
            case ElementType.Wind: return new Color32(0x70, 0xF0, 0xD8, 0xFF);
            default: return new Color32(0xD4, 0xD8, 0xE0, 0xFF);
        }
    }


    private Image MakeFxImage(Transform parent, string name, Sprite sprite, Material material, float size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(size, size);

        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.material = material;
        image.raycastTarget = false;
        return image;
    }

    // Halo, slow rays and sparks drawn in toward the fist, all behind the arm.
    private void BuildFx()
    {
        if (_fxRoot != null) return;

        var go = new GameObject("Reward Fx", typeof(RectTransform));
        _fxRoot = (RectTransform)go.transform;
        _fxRoot.SetParent(transform, false);
        _fxRoot.SetAsFirstSibling();
        _fxRoot.anchorMin = _fxRoot.anchorMax = new Vector2(0.5f, 0.5f);
        _fxRoot.sizeDelta = Vector2.zero;

        Material additive = Resources.Load<Material>("GemFx/AuraAdditive");
        _rays = MakeFxImage(_fxRoot, "Rays", Resources.Load<Sprite>("GemFx/aura_rays"), additive, 780f);
        _halo = MakeFxImage(_fxRoot, "Halo", Resources.Load<Sprite>("GemFx/aura_halo"), additive, 640f);

        Sprite spark = Resources.Load<Sprite>("GemFx/aura_spark");
        _sparks = new Image[16];
        for (int i = 0; i < _sparks.Length; i++) _sparks[i] = MakeFxImage(_fxRoot, "Spark", spark, additive, 30f);

        go.SetActive(false);
    }

    private void UpdateFx()
    {
        if (_fxRoot == null) return;

        bool visible = _fxStrength > 0.01f;
        if (_fxRoot.gameObject.activeSelf != visible) _fxRoot.gameObject.SetActive(visible);
        if (!visible) return;

        float t = Time.unscaledTime;
        _fxRoot.position = _palm.position;
        _halo.color = WithAlpha(_glow, (0.5f + 0.14f * Mathf.Sin(t * 5f)) * _fxStrength);
        _rays.color = WithAlpha(_glow, 0.2f * _fxStrength);
        _rays.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -t * 22f);

        for (int i = 0; i < _sparks.Length; i++)
        {
            float phase = Mathf.Repeat(t * 0.5f + i / (float)_sparks.Length, 1f);
            float radius = Mathf.Lerp(340f, 90f, phase * phase);
            float angle = (i * 137.5f + t * 35f) * Mathf.Deg2Rad;

            _sparks[i].rectTransform.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            _sparks[i].color = WithAlpha(_glow, Mathf.Sin(phase * Mathf.PI) * _fxStrength);
        }
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    // Two rings and a burst of sparks racing outward, and the hand gives a little kick.
    private void ReleasePulse()
    {
        if (_fxRoot == null) return;

        Material additive = Resources.Load<Material>("GemFx/AuraAdditive");
        Sprite halo = Resources.Load<Sprite>("GemFx/aura_halo");
        Sprite ring = Resources.Load<Sprite>("GemFx/hell_fire_ring");
        Sprite spark = Resources.Load<Sprite>("GemFx/aura_spark");

        var host = new GameObject("Release Pulse", typeof(RectTransform));
        var hostRect = (RectTransform)host.transform;
        hostRect.SetParent(transform, false);
        hostRect.SetAsLastSibling(); // in front of the hand, so the flash reads over the gems
        hostRect.anchorMin = hostRect.anchorMax = new Vector2(0.5f, 0.5f);
        hostRect.sizeDelta = Vector2.zero;
        hostRect.position = _palm.position;

        Image flash = MakeFxImage(hostRect, "Flash", halo, additive, 760f);
        flash.color = WithAlpha(Color.Lerp(_glow, Color.white, 0.5f), 1f);
        flash.rectTransform.localScale = Vector3.one * 0.5f;
        flash.rectTransform.DOScale(1.2f, 0.3f).SetEase(Ease.OutQuad).SetUpdate(true).SetLink(host);
        flash.DOFade(0f, 0.34f).SetEase(Ease.InQuad).SetUpdate(true).SetLink(host);

        Image shockwave = MakeFxImage(hostRect, "Ring", ring != null ? ring : halo, additive, 1000f);
        shockwave.color = WithAlpha(_glow, 1f);
        shockwave.rectTransform.localScale = Vector3.one * 0.15f;
        shockwave.rectTransform.DOScale(1.25f, 0.55f).SetEase(Ease.OutCubic).SetUpdate(true).SetLink(host);
        shockwave.DOFade(0f, 0.55f).SetEase(Ease.InQuad).SetUpdate(true).SetLink(host);

        for (int i = 0; i < 18; i++)
        {
            float angle = (i * 20f + Random.Range(-6f, 6f)) * Mathf.Deg2Rad;
            float distance = Random.Range(320f, 520f);
            float seconds = Random.Range(0.4f, 0.65f);

            Image burst = MakeFxImage(hostRect, "Burst", spark, additive, Random.Range(26f, 44f));
            burst.color = WithAlpha(Color.Lerp(_glow, Color.white, 0.3f), 1f);
            burst.rectTransform.DOAnchorPos(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance, seconds).SetEase(Ease.OutCubic).SetUpdate(true).SetLink(host);
            burst.DOFade(0f, seconds).SetEase(Ease.InQuad).SetUpdate(true).SetLink(host);
        }

        hand.DOKill(true);
        hand.DOPunchScale(Vector3.one * 0.06f, 0.35f, 6, 0.6f).SetUpdate(true).SetLink(hand.gameObject);

        GemSwapFx.PlaySound();

        DOVirtual.DelayedCall(0.8f, () => { if (host != null) Destroy(host); }).SetUpdate(true).SetLink(host);
    }
    #endregion

    #region Idle gestures
    private void PlayRandomGesture()
    {
        _sequence?.Kill();
        _swing = 0f;
        ResetDigits();

        switch (Random.Range(0, 4))
        {
            case 0: Fist(); break;
            case 1: Claw(); break;
            case 2: Swing(); break;
            default: Wave(); break;
        }

        _sequence.SetUpdate(true).SetLink(gameObject);
    }

    // Fingers roll in from the pinky, the thumb folds over, it holds, then everything springs back open.
    private void Fist()
    {
        _sequence = DOTween.Sequence();
        for (int i = 0; i < Thumb; i++)
        {
            float at = (Thumb - 1 - i) * 0.035f;
            _sequence.Insert(at, LengthTo(i, ClenchedLength, 0.15f, Ease.InQuad));
            _sequence.Insert(0.62f + i * 0.03f, LengthTo(i, 1f, 0.3f, Ease.OutBack));
        }

        _sequence.Insert(0.1f, LengthTo(Thumb, ThumbClenchedLength, 0.14f, Ease.InQuad));
        _sequence.Insert(0.1f, BendTo(Thumb, ThumbClenchedBend, 0.14f, Ease.InQuad));
        _sequence.Insert(0.68f, LengthTo(Thumb, 1f, 0.28f, Ease.OutBack));
        _sequence.Insert(0.68f, BendTo(Thumb, 0f, 0.28f, Ease.OutBack));

        _sequence.Insert(0f, SwingTo(-3f, 0.15f, Ease.OutQuad));
        _sequence.Insert(0.62f, SwingTo(0f, 0.3f, Ease.OutBack));
    }

    // A quick snap: the fingers shorten and splay apart like talons, then relax.
    private void Claw()
    {
        float[] splay = { 6f, 1.5f, -3f, -8f, 8f };

        _sequence = DOTween.Sequence();
        for (int i = 0; i < digits.Length; i++)
        {
            _sequence.Insert(0f, LengthTo(i, i == Thumb ? 0.88f : 0.72f, 0.09f, Ease.OutBack));
            _sequence.Insert(0f, BendTo(i, splay[i], 0.09f, Ease.OutBack));
            _sequence.Insert(0.32f, LengthTo(i, 1f, 0.3f, Ease.InOutSine));
            _sequence.Insert(0.32f, BendTo(i, 0f, 0.3f, Ease.InOutSine));
        }
    }

    // The hand swings at the wrist, with the fingers trailing a little behind.
    private void Swing()
    {
        _sequence = DOTween.Sequence();
        _sequence.Append(SwingTo(5f, 0.24f, Ease.InOutSine));
        _sequence.Append(SwingTo(-5f, 0.42f, Ease.InOutSine));
        _sequence.Append(SwingTo(0f, 0.3f, Ease.OutBack));

        for (int i = 0; i < Thumb; i++)
        {
            _sequence.Insert(0.04f, BendTo(i, -3f, 0.24f, Ease.InOutSine));
            _sequence.Insert(0.3f, BendTo(i, 3f, 0.42f, Ease.InOutSine));
            _sequence.Insert(0.74f, BendTo(i, 0f, 0.26f, Ease.OutSine));
        }
    }

    // Each finger dips in turn, from the pinky to the index.
    private void Wave()
    {
        _sequence = DOTween.Sequence();
        for (int i = 0; i < Thumb; i++)
        {
            float at = (Thumb - 1 - i) * 0.09f;
            _sequence.Insert(at, LengthTo(i, 0.68f, 0.12f, Ease.InOutSine));
            _sequence.Insert(at + 0.12f, LengthTo(i, 1f, 0.2f, Ease.OutBack));
        }
    }

    private Tween LengthTo(int digit, float value, float seconds, Ease ease)
    {
        return DOTween.To(() => _length[digit], v => _length[digit] = v, value, seconds).SetEase(ease);
    }

    private Tween BendTo(int digit, float value, float seconds, Ease ease)
    {
        return DOTween.To(() => _bend[digit], v => _bend[digit] = v, value, seconds).SetEase(ease);
    }

    private Tween SwingTo(float value, float seconds, Ease ease)
    {
        return DOTween.To(() => _swing, v => _swing = v, value, seconds).SetEase(ease);
    }
    #endregion
}
