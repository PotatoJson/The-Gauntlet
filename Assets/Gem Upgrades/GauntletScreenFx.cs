using DG.Tweening;
using UnityEngine;

/// <summary>
/// Fades the character screen in when it turns on and out when it is closed through <see cref="Close"/>.
///
/// Opening needs no help from callers: this component starts the fade in OnEnable, whoever switched the screen on.
/// Closing does: the screen has to stay on for the fade, so callers use <see cref="Close"/> instead of SetActive(false)
/// and <see cref="Open"/> instead of SetActive(true) (which also cancels a fade that is still running, so opening again
/// straight after a close can't leave an invisible screen). During the fade the screen ignores the pointer.
///
/// The rest of the motion (cards sliding in, the gauntlet popping) lives in InventoryManager. Everything runs on
/// unscaled time, because the game is paused while the screen is up, and all of it is skipped when the "ReduceMotion"
/// setting is on.
/// </summary>
public class GauntletScreenFx : MonoBehaviour
{
    private const float FadeIn = 0.2f;
    private const float FadeOut = 0.14f;

    /// <summary>Player setting: 1 turns every screen animation into an instant change.</summary>
    public static bool ReduceMotion => PlayerPrefs.GetInt("ReduceMotion", 0) == 1;

    private CanvasGroup _group;
    private Tween _tween;
    private bool _closing;

    public static GauntletScreenFx Ensure(GameObject root)
    {
        var fx = root.GetComponent<GauntletScreenFx>();
        return fx != null ? fx : root.AddComponent<GauntletScreenFx>();
    }

    public static bool IsClosing(GameObject root)
    {
        if (root == null) return false;
        var fx = root.GetComponent<GauntletScreenFx>();
        return fx != null && fx._closing;
    }

    /// <summary>Switches the screen on, cancelling a close that is still fading out.</summary>
    public static void Open(GameObject root)
    {
        if (root == null) return;

        var fx = root.GetComponent<GauntletScreenFx>();
        if (fx != null && fx._closing) fx.CancelClose();

        root.SetActive(true);
    }

    /// <summary>Fades the screen out and switches it off. Safe to call on a screen that is already off or already closing.</summary>
    public static void Close(GameObject root)
    {
        if (root == null || !root.activeSelf) return;
        Ensure(root).BeginClose();
    }

    private void Awake()
    {
        _group = GetComponent<CanvasGroup>();
        if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
    }

    private void OnEnable()
    {
        _tween?.Kill();
        _closing = false;
        ResetGroup();

        if (ReduceMotion) return;

        _group.alpha = 0f;
        _tween = _group.DOFade(1f, FadeIn).SetEase(Ease.OutQuad).SetUpdate(true).SetLink(gameObject);
    }

    private void OnDisable()
    {
        _tween?.Kill();
        _closing = false;
        ResetGroup();
    }

    private void BeginClose()
    {
        if (_closing) return;
        _closing = true;

        _group.interactable = false;
        _group.blocksRaycasts = false;
        _tween?.Kill();

        if (ReduceMotion || !isActiveAndEnabled)
        {
            Finish();
            return;
        }

        _tween = _group.DOFade(0f, FadeOut).SetEase(Ease.InQuad).SetUpdate(true).SetLink(gameObject).OnComplete(Finish);
    }

    private void CancelClose()
    {
        _tween?.Kill();
        _closing = false;
        ResetGroup();
    }

    private void Finish()
    {
        _closing = false;
        ResetGroup();
        gameObject.SetActive(false);
    }

    private void ResetGroup()
    {
        if (_group == null) return;
        _group.alpha = 1f;
        _group.interactable = true;
        _group.blocksRaycasts = true;
    }
}
