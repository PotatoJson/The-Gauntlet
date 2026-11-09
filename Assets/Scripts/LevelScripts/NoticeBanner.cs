using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

/// <summary>
/// A short notice at the top of the screen on a paper banner (the Humble Paper UI pack's "4 Notification"). The
/// paper forms (the pack's burn-away frames played backwards), the line fades in, waits, and the paper burns away.
/// The prefab lives in Resources/Highlight/NoticeBanner; Show creates it the first time.
/// </summary>
public class NoticeBanner : MonoBehaviour
{
    private const string PrefabPath = "Highlight/NoticeBanner";

    [SerializeField] private Image paper;
    [SerializeField] private TMP_Text text;
    [Tooltip("The pack's notification frames in order: whole paper first, burnt away last.")]
    [SerializeField] private Sprite[] frames = new Sprite[0];
    [Tooltip("The pack animates them at 60 frames a second.")]
    [SerializeField] private float frameSeconds = 1f / 60f;

    private static NoticeBanner _instance;
    private LocalizedTmpFont _font;

    /// <summary>Shows a line for this many seconds (unscaled), replacing any notice already up.</summary>
    public static void Show(string message, float seconds)
    {
        if (_instance == null)
        {
            NoticeBanner prefab = Resources.Load<NoticeBanner>(PrefabPath);
            if (prefab == null) return;
            _instance = Instantiate(prefab);
            DontDestroyOnLoad(_instance.gameObject);
        }

        _instance.StopAllCoroutines();
        _instance.StartCoroutine(_instance.Play(message, seconds));
    }

    private void Awake()
    {
        paper.enabled = false;
        text.alpha = 0f;

        // The game's pixel font, swapped for the right one in Japanese, Chinese and German.
        _font = new LocalizedTmpFont { TableReference = "UIFonts", TableEntryReference = "MainFont" };
        _font.AssetChanged += font => { if (font != null) text.font = font; };
    }

    private IEnumerator Play(string message, float seconds)
    {
        text.text = message;
        text.alpha = 0f;
        paper.enabled = true;

        for (int i = frames.Length - 1; i >= 0; i--) // forms
        {
            paper.sprite = frames[i];
            yield return new WaitForSecondsRealtime(frameSeconds);
        }

        yield return Fade(0f, 1f, 0.15f);
        yield return new WaitForSecondsRealtime(seconds);
        yield return Fade(1f, 0f, 0.15f);

        foreach (Sprite frame in frames) // burns away
        {
            paper.sprite = frame;
            yield return new WaitForSecondsRealtime(frameSeconds);
        }
        paper.enabled = false;
    }

    private IEnumerator Fade(float from, float to, float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            text.alpha = Mathf.Lerp(from, to, t / seconds);
            yield return null;
        }
        text.alpha = to;
    }
}
