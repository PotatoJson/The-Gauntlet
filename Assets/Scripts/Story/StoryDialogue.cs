using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using DG.Tweening;
using FMODUnity;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Who says a line. Echo is the whispering voices of the knight's family.</summary>
public enum Speaker { Narrator, Knight, Echo, Brutus, Pergius, Shrine }

/// <summary>
/// The story's voice (see Docs/Story.md). Two ways to show a line:
/// <list type="bullet">
/// <item><see cref="Say"/>: one line at the bottom of the screen that types out, waits and fades. The player keeps
/// control, so it can run over gameplay, cutscenes and the death screen.</item>
/// <item><see cref="Tell"/>: a black page the player clicks through (the prologue and the ending). Time stops under it.</item>
/// </list>
/// Lines come from the "Story" localization table. Every speaker "talks" in blips, a short tone per letter at their
/// own pitch. Creates itself, so no scene needs it.
/// </summary>
public class StoryDialogue : MonoBehaviour
{
    private const string Table = "Story";
    private const float LetterSeconds = 0.03f;
    private const float ReadSeconds = 1.6f;          // a line stays this long after typing...
    private const float ReadSecondsPerLetter = 0.04f; // ...plus this per letter
    private const float SkipHoldSeconds = 2f;
    private const float BlipVolume = 0.3f;

    private struct Line
    {
        public string Key;
        public Speaker Who;
        public float Hold; // < 0: reading time
    }

    private static StoryDialogue _instance;
    private static readonly HashSet<string> ToldThisRun = new HashSet<string>();
    private static readonly string[] DeathDeck =
    {
        "Story_Death_01", "Story_Death_02", "Story_Death_03", "Story_Death_04", "Story_Death_05", "Story_Death_06",
        "Story_Death_07", "Story_Death_08", "Story_Death_09", "Story_Death_10", "Story_Death_11", "Story_Death_12"
    };
    private static int _nextDeathCard = DeathDeck.Length; // shuffled on first use
    private static bool _diedInThisLevel;
    private static bool _finishedRun;

    private readonly Queue<Line> _queue = new Queue<Line>();
    private Coroutine _lines;
    private bool _telling;
    private bool _sticky;
    private int _chambersCleared;

    private TMP_Text _line;
    private CanvasGroup _lineGroup;
    private GauntletMenu _menu;
    private bool _menuFound;
    private CanvasGroup _page;
    private TMP_Text _pageText;
    private RawImage _picture;
    private RectTransform _frame;
    private Image _veil;
    private GameObject _pageArrow;
    private RectTransform _skipBar;
    private TMP_Text _skipLabel;
    private LocalizedTmpFont _font;

    private static FMOD.Sound _blip;
    private static FMOD.ChannelGroup _blipGroup;
    private static bool _blipReady;

    #region Public calls
    /// <summary>Shows a line at the bottom of the screen, after any already waiting.</summary>
    public static void Say(string key, Speaker who = Speaker.Narrator, float hold = -1f)
    {
        if (_instance == null) return;
        _instance._queue.Enqueue(new Line { Key = key, Who = who, Hold = hold });
    }

    /// <summary>Like <see cref="Say"/>, but only the first time in a run. False if it was already told.</summary>
    public static bool SayOnce(string key, Speaker who = Speaker.Narrator)
    {
        if (!ToldThisRun.Add(key)) return false;
        Say(key, who);
        return true;
    }

    /// <summary>Removes the line on screen and everything waiting, e.g. when a cutscene is skipped.</summary>
    public static void ClearLines()
    {
        if (_instance != null) _instance.RestartLines();
    }

    /// <summary>A fresh run: every once-per-run line can be told again.</summary>
    public static void NewRun()
    {
        ToldThisRun.Clear();
        _diedInThisLevel = false;
    }

    /// <summary>The line said over the death screen. It stays until the next scene loads.</summary>
    public static void SayDeathLine(GameObject killer)
    {
        _diedInThisLevel = true;

        string key;
        if (killer != null && killer.GetComponentInParent<DemonBoss>() != null) key = "Story_Death_Brutus";
        else if (killer != null && killer.GetComponentInParent<HellLord>() != null) key = "Story_Death_Pergius";
        else key = DrawDeathCard();

        ClearLines();
        Say(key, Speaker.Narrator, float.PositiveInfinity);
    }

    /// <summary>Plays black pages of narration, then calls onDone. Time is stopped while they show.</summary>
    /// <param name="shatterAfter">The key of a line whose page shatters like the death screen when the player turns it.</param>
    /// <param name="fallInto">A point in the last picture (0-1, from its bottom-left) the view falls into at the end,
    /// before onDone. Null for a plain fade.</param>
    public static void Tell(Action onDone, string shatterAfter, Vector2? fallInto, params (string key, Speaker who)[] lines)
    {
        if (_instance == null || _instance._telling)
        {
            if (_instance == null) onDone?.Invoke();
            return;
        }
        _instance.StartCoroutine(_instance.TellRoutine(lines, shatterAfter, fallInto, onDone));
    }

    /// <summary>The end of the game (for now: leaving Hell). The ending pages, then the menu.</summary>
    public static void TellEnding(Action thenLoadMenu)
    {
        PlayerPrefs.SetInt("Story_ClearedOnce", 1); // unlocks Layer 2 when the update ships
        PlayerPrefs.Save();
        _finishedRun = true;

        Tell(thenLoadMenu, null, null,
            ("Story_End_01", Speaker.Narrator),
            ("Story_End_02", Speaker.Narrator),
            ("Story_End_03", Speaker.Knight),
            ("Story_End_04", Speaker.Narrator));
    }

    /// <summary>The localized text of a line, for dialogue boxes that type it out themselves.</summary>
    public static string Text(string key) => LocalizationSettings.StringDatabase.GetLocalizedString(Table, key);

    /// <summary>One "syllable" of a speaker's voice. Called per typed letter; skips spaces and every other letter.</summary>
    public static void Blip(Speaker who, string text, int index)
    {
        if (!_blipReady || index % 2 != 0 || !char.IsLetterOrDigit(text[index])) return;

        if (RuntimeManager.CoreSystem.playSound(_blip, _blipGroup, true, out FMOD.Channel channel) != FMOD.RESULT.OK) return;
        channel.setPitch(Pitch(who) * UnityEngine.Random.Range(0.93f, 1.07f));
        channel.setVolume(who == Speaker.Echo ? BlipVolume * 0.45f : BlipVolume); // the family only whispers
        channel.setPaused(false);
    }

    /// <summary>Counts cleared chambers: the family's voices whisper after the second one in each level.</summary>
    public static void ChamberCleared()
    {
        if (_instance == null || (SaveManager.Instance != null && SaveManager.Instance.IsRestoring)) return;
        if (++_instance._chambersCleared != 2) return;

        switch (SceneManager.GetActiveScene().name)
        {
            case "Dungeon Level":
                if (SayOnce("Story_A1_Echo_01", Speaker.Echo)) SayOnce("Story_A1_Echo_02");
                break;
            case "Cave Level": SayOnce("Story_A2_Echo_01", Speaker.Echo); break;
            case "Hell Level": SayOnce("Story_A3_Echo_01", Speaker.Echo); break;
        }
    }

    /// <summary>A loading screen's line of flavour.</summary>
    public static void SayLoadingLine()
    {
        string[] lines = { "Story_Load_01", "Story_Load_02", "Story_Load_03" };
        Say(lines[UnityEngine.Random.Range(0, lines.Length)]);
    }
    #endregion

    #region Setup
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        GameObject host = new GameObject("[StoryDialogue]");
        DontDestroyOnLoad(host);
        _instance = host.AddComponent<StoryDialogue>();
    }

    private void Awake()
    {
        BuildUi();
        CreateBlip();
        SceneManager.sceneLoaded += OnSceneLoaded;
        _lines = StartCoroutine(LinesRoutine());
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (_font != null) _font.AssetChanged -= SetFont;
        if (_blipReady) _blip.release();
        _blipReady = false;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (_sticky) RestartLines(); // the death line; other lines (the loading screen's) finish in the new scene
        _chambersCleared = 0;
        _menuFound = false; // each level has its own pause menu
        StartCoroutine(SceneLines(scene.name));
    }

    private IEnumerator SceneLines(string scene)
    {
        if (scene == "MainMenu")
        {
            _diedInThisLevel = false;
            if (!_finishedRun) yield break;
            _finishedRun = false;
            yield return new WaitForSecondsRealtime(1.2f);
            Say("Story_End_05");
            yield break;
        }

        string[] arrival = scene switch
        {
            "Dungeon Level" => new[] { "Story_A1_Arrive_01", "Story_A1_Arrive_02" },
            "Cave Level" => new[] { "Story_A2_Arrive_01", "Story_A2_Arrive_02" },
            "Hell Level" => new[] { "Story_A3_Arrive_01", "Story_A3_Arrive_02" },
            _ => null
        };
        if (arrival == null) yield break;

        // After the loading screen and the teleport-in.
        yield return new WaitForSecondsRealtime(1f);
        PlayerSpawnTeleportEffect spawn = FindFirstObjectByType<PlayerSpawnTeleportEffect>();
        while (spawn != null && spawn.IsPlaying) yield return null;

        if (_diedInThisLevel)
        {
            _diedInThisLevel = false;
            Say(UnityEngine.Random.value < 0.5f ? "Story_Restart_01" : "Story_Restart_02");
            yield break;
        }

        foreach (string key in arrival) SayOnce(key);
    }

    private static string DrawDeathCard()
    {
        if (_nextDeathCard >= DeathDeck.Length)
        {
            string last = DeathDeck[DeathDeck.Length - 1];
            for (int i = DeathDeck.Length - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (DeathDeck[i], DeathDeck[j]) = (DeathDeck[j], DeathDeck[i]);
            }
            // The last card of one deck mustn't be the first of the next.
            if (DeathDeck[0] == last) (DeathDeck[0], DeathDeck[DeathDeck.Length - 1]) = (DeathDeck[DeathDeck.Length - 1], DeathDeck[0]);
            _nextDeathCard = 0;
        }
        return DeathDeck[_nextDeathCard++];
    }

    private static float Pitch(Speaker who) => who switch
    {
        Speaker.Knight => 0.8f,
        Speaker.Echo => 1.3f,
        Speaker.Brutus => 0.55f,
        Speaker.Pergius => 0.68f,
        Speaker.Shrine => 1.2f,
        _ => 1f
    };

    private static Color Tint(Speaker who) => who switch
    {
        Speaker.Knight => new Color(0.72f, 0.84f, 1f),
        Speaker.Echo => new Color(0.74f, 0.84f, 0.95f),
        Speaker.Brutus => new Color(1f, 0.5f, 0.35f),
        Speaker.Pergius => new Color(0.78f, 0.58f, 1f),
        Speaker.Shrine => new Color(0.5f, 1f, 0.88f),
        _ => new Color(1f, 0.93f, 0.78f) // parchment
    };

    // A 50 ms tone with a quick decay: sine with a little square for a retro edge. Played through the SFX bus so
    // the volume sliders apply. Generated here because Unity's own audio is off (FMOD plays everything).
    private static void CreateBlip()
    {
        const int rate = 44100;
        const float frequency = 330f;
        int count = rate / 20;
        float[] samples = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / rate;
            float wave = Mathf.Sin(2f * Mathf.PI * frequency * t);
            float square = wave >= 0f ? 1f : -1f;
            float envelope = Mathf.Min(1f, t / 0.003f) * Mathf.Exp(-t * 55f);
            samples[i] = (wave * 0.75f + square * 0.25f) * envelope * 0.8f;
        }

        byte[] data = new byte[count * sizeof(float)];
        Buffer.BlockCopy(samples, 0, data, 0, data.Length);
        FMOD.CREATESOUNDEXINFO info = new FMOD.CREATESOUNDEXINFO
        {
            cbsize = Marshal.SizeOf(typeof(FMOD.CREATESOUNDEXINFO)),
            length = (uint)data.Length,
            format = FMOD.SOUND_FORMAT.PCMFLOAT,
            defaultfrequency = rate,
            numchannels = 1
        };

        try
        {
            FMOD.MODE mode = FMOD.MODE.OPENMEMORY | FMOD.MODE.OPENRAW | FMOD.MODE.CREATESAMPLE | FMOD.MODE.LOOP_OFF;
            if (RuntimeManager.CoreSystem.createSound(data, mode, ref info, out _blip) != FMOD.RESULT.OK) return;

            FMOD.Studio.Bus sfx = RuntimeManager.GetBus("bus:/SFX");
            sfx.lockChannelGroup();
            RuntimeManager.StudioSystem.flushCommands();
            sfx.getChannelGroup(out _blipGroup); // left as the master group if this fails
            _blipReady = true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Story] No dialogue blips: {e.Message}");
        }
    }
    #endregion

    #region Bottom line
    private void RestartLines()
    {
        _queue.Clear();
        _sticky = false;
        if (_lines != null) StopCoroutine(_lines);
        _line.alpha = 0f;
        _lines = StartCoroutine(LinesRoutine());
    }

    private IEnumerator LinesRoutine()
    {
        while (true)
        {
            while (_queue.Count == 0 || _telling) yield return null;

            Line line = _queue.Dequeue();

            string text = Text(line.Key);
            _line.text = $"<mark=#000000A0 padding=\"18,18,8,8\">{text}</mark>";
            _line.color = Tint(line.Who);
            _line.fontStyle = line.Who == Speaker.Echo ? FontStyles.Italic : FontStyles.Normal;
            _line.maxVisibleCharacters = 0;
            _line.alpha = 1f;
            bool bossBar = BossHealthBar.Instance != null && BossHealthBar.Instance.IsShowing;
            _line.rectTransform.anchoredPosition = new Vector2(0f, bossBar ? 230f : 120f);

            for (int i = 0; i < text.Length; i++)
            {
                _line.maxVisibleCharacters = i + 1;
                Blip(line.Who, text, i);
                yield return Wait(LetterSeconds);
            }

            float hold = line.Hold >= 0f ? line.Hold : ReadSeconds + text.Length * ReadSecondsPerLetter;
            if (float.IsPositiveInfinity(hold))
            {
                _sticky = true; // stays until the scene changes
                yield break;
            }
            yield return Wait(hold);

            for (float faded = 0f; faded < 0.3f; )
            {
                if (!MenuOpen) faded += Time.unscaledDeltaTime;
                _line.alpha = 1f - faded / 0.3f;
                yield return null;
            }
            _line.alpha = 0f;
        }
    }

    // Like WaitForSecondsRealtime, but the clock stops while the player has the game paused, so a line picks up
    // where it left off instead of having finished (or vanished) behind the menu.
    private IEnumerator Wait(float seconds)
    {
        for (float waited = 0f; waited < seconds; )
        {
            if (!MenuOpen) waited += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    // The pause menu or the inventory: the line hides under them and waits.
    private bool MenuOpen
    {
        get
        {
            if (!_menuFound)
            {
                _menu = FindFirstObjectByType<GauntletMenu>();
                _menuFound = true;
            }
            return _menu != null && _menu.IsMenuOpen;
        }
    }

    private void Update()
    {
        _lineGroup.alpha = MenuOpen ? 0f : 1f;
    }
    #endregion

    #region Pages
    private IEnumerator TellRoutine((string key, Speaker who)[] lines, string shatterAfter, Vector2? fallInto, Action onDone)
    {
        _telling = true;
        _line.alpha = 0f;

        GameplayInputGate gate = GameplayInputGate.Instance;
        if (gate != null) gate.Suspend(this);
        float timeScale = Time.timeScale;
        Time.timeScale = 0f;

        _skipLabel.text = new LocalizedString("UIStrings", "HoldToSkip").GetLocalizedString();
        _skipBar.localScale = new Vector3(0f, 1f, 1f);
        _pageText.text = "";
        _pageArrow.SetActive(false);

        // A line with a picture in Resources/Story, named after its key, shows it above the words; the picture stays
        // until the next line that has one. With no pictures at all, the words sit in the middle of the page.
        Texture2D[] pictures = new Texture2D[lines.Length];
        bool anyPicture = false;
        for (int i = 0; i < lines.Length; i++) anyPicture |= (pictures[i] = Resources.Load<Texture2D>("Story/" + lines[i].key)) != null;
        Stretch(_pageText.rectTransform, anyPicture ? new Vector2(0.15f, 0.08f) : new Vector2(0.2f, 0.3f),
            anyPicture ? new Vector2(0.85f, 0.27f) : new Vector2(0.8f, 0.7f));
        Vector2 arrowAt = new Vector2(0.5f, anyPicture ? 0.05f : 0.24f);
        Stretch(_pageArrow.GetComponent<RectTransform>(), arrowAt, arrowAt);
        _pageArrow.GetComponent<RectTransform>().sizeDelta = new Vector2(100f, 60f);
        _picture.texture = null;
        _picture.color = Color.clear;
        _frame.localScale = Vector3.one;
        _frame.anchoredPosition = Vector2.zero;
        _veil.color = Color.clear;
        _skipLabel.gameObject.SetActive(true);

        _page.gameObject.SetActive(true);
        yield return FadePage(0f, 1f, 0.6f);

        float held = 0f;
        bool skipped = false;

        // Tracks the skip button through every wait below. True once it has been held long enough.
        bool Skipping()
        {
            bool down = (Keyboard.current != null && Keyboard.current.escapeKey.isPressed) ||
                        (Gamepad.current != null && Gamepad.current.startButton.isPressed);
            held = down ? held + Time.unscaledDeltaTime : 0f;
            _skipBar.localScale = new Vector3(Mathf.Clamp01(held / SkipHoldSeconds), 1f, 1f);
            skipped |= held >= SkipHoldSeconds;
            return skipped;
        }

        for (int i = 0; i < lines.Length; i++)
        {
            (string key, Speaker who) = lines[i];
            if (pictures[i] != null)
            {
                _pageText.text = "";
                yield return ShowPicture(pictures[i]);
            }

            string text = Text(key);
            _pageText.text = text;
            _pageText.color = Tint(who);
            _pageText.maxVisibleCharacters = 0;
            _pageArrow.SetActive(false);

            float letterClock = 0f;
            int shown = 0;
            while (shown < text.Length)
            {
                if (Skipping()) break;
                if (AdvancePressed()) shown = text.Length;
                for (letterClock += Time.unscaledDeltaTime; letterClock >= LetterSeconds && shown < text.Length; letterClock -= LetterSeconds)
                {
                    Blip(who, text, shown++);
                }
                _pageText.maxVisibleCharacters = shown;
                yield return null;
            }
            _pageText.maxVisibleCharacters = text.Length;
            if (skipped) break;

            yield return null; // the press that finished the typing mustn't also turn the page
            _pageArrow.SetActive(true);
            while (!AdvancePressed())
            {
                if (Skipping()) break;
                yield return null;
            }
            if (skipped) break;

            if (key == shatterAfter)
            {
                _pageArrow.SetActive(false);
                yield return Shatter();
            }
        }

        if (!skipped && fallInto.HasValue && _picture.texture != null) yield return FallInto(fallInto.Value);

        // Everything after the pages loads a scene or carries on under the black, so the page fades on its own time.
        Time.timeScale = timeScale;
        if (gate != null) gate.RestoreWhenReleased(this);
        _telling = false;
        onDone?.Invoke();

        yield return null;
        yield return FadePage(1f, 0f, 0.6f);
        _page.gameObject.SetActive(false);
    }

    // The view plunges into a point of the picture (the pit), glowing hot and then going black, so the next scene
    // arrives as if the knight had dropped into it.
    private IEnumerator FallInto(Vector2 point)
    {
        _pageText.text = "";
        _pageArrow.SetActive(false);
        _skipLabel.gameObject.SetActive(false);
        _picture.rectTransform.DOKill();
        RuntimeManager.PlayOneShot("event:/Environment/Teleport");

        // The point, measured from the frame's centre. Moving the frame by (1 - scale) of it keeps the point still.
        Vector2 offset = new Vector2((point.x - 0.5f) * _frame.rect.width, (point.y - 0.5f) * _frame.rect.height);
        Color heat = new Color(1f, 0.36f, 0.12f, 0.45f);
        const float seconds = 1.5f;

        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            float e = t / seconds;
            float scale = 1f + 11f * e * e * e; // slow at first, then falling
            _frame.localScale = new Vector3(scale, scale, 1f);
            _frame.anchoredPosition = (1f - scale) * offset;
            _veil.color = e < 0.75f ? Color.Lerp(Color.clear, heat, Mathf.InverseLerp(0.35f, 0.75f, e))
                                    : Color.Lerp(heat, Color.black, (e - 0.75f) / 0.25f);
            yield return null;
        }

        _veil.color = Color.black;
        yield return new WaitForSecondsRealtime(0.3f);
    }

    // Fades the old picture out and the new one in, then lets it drift slowly closer while the words are read.
    private IEnumerator ShowPicture(Texture2D picture)
    {
        if (_picture.texture != null) yield return FadePicture(1f, 0f, 0.35f);

        _picture.texture = picture;
        _picture.rectTransform.DOKill();
        _picture.rectTransform.localScale = Vector3.one;
        _picture.rectTransform.DOScale(1.08f, 14f).SetEase(Ease.Linear).SetUpdate(true);
        yield return FadePicture(0f, 1f, 0.6f);
    }

    private IEnumerator FadePicture(float from, float to, float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            _picture.color = new Color(1f, 1f, 1f, Mathf.Lerp(from, to, t / seconds));
            yield return null;
        }
        _picture.color = new Color(1f, 1f, 1f, to);
    }

    private static bool AdvancePressed() =>
        (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) ||
        (Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame)) ||
        (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);

    private IEnumerator FadePage(float from, float to, float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            _page.alpha = Mathf.Lerp(from, to, t / seconds);
            yield return null;
        }
        _page.alpha = to;
    }

    // The page breaks in two and falls away, the same way the screen does when the knight dies.
    private IEnumerator Shatter()
    {
        _skipLabel.gameObject.SetActive(false); // it stays put while the page falls
        yield return new WaitForEndOfFrame();
        // The capture holds sRGB colours but comes back marked linear, which washes the picture out in a linear-space
        // project. Copy it into a texture marked sRGB.
        Texture2D capture = ScreenCapture.CaptureScreenshotAsTexture();
        Texture2D shot = new Texture2D(capture.width, capture.height, capture.format, false, false);
        shot.LoadRawTextureData(capture.GetRawTextureData<byte>());
        shot.Apply();
        Destroy(capture);
        RuntimeManager.PlayOneShot("event:/Combat/Impact_Heavy");

        RawImage left = Half(shot, 0f);
        RawImage right = Half(shot, 0.5f);
        _pageText.text = "";
        _picture.texture = null; // it's in the halves now: behind them, the page is empty
        _picture.color = Color.clear;

        yield return new WaitForSecondsRealtime(0.35f);
        RuntimeManager.PlayOneShot("event:/Player/Player_Death");
        foreach ((RawImage half, float spin) in new[] { (left, 30f), (right, -30f) })
        {
            half.rectTransform.DOAnchorPosY(-1500f, 1.3f).SetEase(Ease.InBack).SetUpdate(true);
            half.rectTransform.DORotate(new Vector3(0f, 0f, spin), 1.3f).SetUpdate(true);
        }
        yield return new WaitForSecondsRealtime(1.5f);

        Destroy(left.gameObject);
        Destroy(right.gameObject);
        Destroy(shot);
        _skipLabel.gameObject.SetActive(true);
    }

    private RawImage Half(Texture2D shot, float x)
    {
        RawImage half = new GameObject("Half", typeof(RectTransform)).AddComponent<RawImage>();
        half.transform.SetParent(_page.transform, false);
        half.texture = shot;
        half.uvRect = new Rect(x, 0f, 0.5f, 1f);
        Stretch(half.rectTransform, new Vector2(x, 0f), new Vector2(x + 0.5f, 1f));
        return half;
    }
    #endregion

    #region UI
    private void BuildUi()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32100; // over the loading screen and the cutscene bars
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>(); // the black page swallows clicks meant for whatever is under it

        _line = CreateText("Line", transform, 34f);
        Stretch(_line.rectTransform, new Vector2(0.15f, 0f), new Vector2(0.85f, 0f));
        _line.rectTransform.sizeDelta = new Vector2(_line.rectTransform.sizeDelta.x, 120f);
        _line.rectTransform.anchoredPosition = new Vector2(0f, 120f);
        _line.alignment = TextAlignmentOptions.Bottom;
        _line.alpha = 0f;
        _lineGroup = _line.gameObject.AddComponent<CanvasGroup>(); // hides the line under the pause menu

        Image black = new GameObject("Page", typeof(RectTransform)).AddComponent<Image>();
        black.transform.SetParent(transform, false);
        black.color = Color.black;
        Stretch(black.rectTransform, Vector2.zero, Vector2.one);
        _page = black.gameObject.AddComponent<CanvasGroup>();

        // The picture above the words. The frame clips the slow zoom.
        _frame = new GameObject("Frame", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
        _frame.SetParent(black.transform, false);
        Stretch(_frame, new Vector2(0.5f, 0.6f), new Vector2(0.5f, 0.6f));
        _frame.sizeDelta = new Vector2(1152f, 648f);
        _picture = new GameObject("Picture", typeof(RectTransform)).AddComponent<RawImage>();
        _picture.transform.SetParent(_frame, false);
        _picture.raycastTarget = false;
        Stretch(_picture.rectTransform, Vector2.zero, Vector2.one);

        // Tints the whole page during the fall into the last picture.
        _veil = new GameObject("Veil", typeof(RectTransform)).AddComponent<Image>();
        _veil.transform.SetParent(black.transform, false);
        _veil.raycastTarget = false;
        _veil.color = Color.clear;
        Stretch(_veil.rectTransform, Vector2.zero, Vector2.one);

        _pageText = CreateText("Text", black.transform, 44f);
        _pageText.alignment = TextAlignmentOptions.Center;
        _pageText.lineSpacing = 20f;

        TMP_Text arrow = CreateText("Arrow", black.transform, 40f);
        arrow.rectTransform.sizeDelta = new Vector2(100f, 60f);
        arrow.alignment = TextAlignmentOptions.Center;
        arrow.text = "v";
        arrow.color = new Color(1f, 0.93f, 0.78f, 0.7f);
        arrow.rectTransform.DOAnchorPosY(-10f, 0.4f).SetRelative().SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true);
        _pageArrow = arrow.gameObject;

        _skipLabel = CreateText("Skip", black.transform, 24f);
        Stretch(_skipLabel.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f));
        _skipLabel.rectTransform.pivot = new Vector2(1f, 0f);
        _skipLabel.rectTransform.sizeDelta = new Vector2(500f, 40f);
        _skipLabel.rectTransform.anchoredPosition = new Vector2(-40f, 40f);
        _skipLabel.alignment = TextAlignmentOptions.BottomRight;
        _skipLabel.color = new Color(1f, 1f, 1f, 0.5f);

        Image bar = new GameObject("SkipBar", typeof(RectTransform)).AddComponent<Image>();
        bar.transform.SetParent(_skipLabel.transform, false);
        bar.color = new Color(1f, 1f, 1f, 0.6f);
        _skipBar = bar.rectTransform;
        Stretch(_skipBar, new Vector2(0f, 0f), new Vector2(1f, 0f));
        _skipBar.pivot = new Vector2(1f, 1f);
        _skipBar.sizeDelta = new Vector2(0f, 3f);
        _skipBar.anchoredPosition = new Vector2(0f, -6f);

        _page.gameObject.SetActive(false);

        // The game's pixel font, swapped for the right one in Japanese, Chinese and German.
        _font = new LocalizedTmpFont { TableReference = "UIFonts", TableEntryReference = "MainFont" };
        _font.AssetChanged += SetFont;
    }

    private void SetFont(TMP_FontAsset font)
    {
        if (font == null) return;
        foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true)) text.font = font;
    }

    private static TMP_Text CreateText(string name, Transform parent, float size)
    {
        TextMeshProUGUI text = new GameObject(name, typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        text.transform.SetParent(parent, false);
        text.fontSize = size;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
    #endregion
}
