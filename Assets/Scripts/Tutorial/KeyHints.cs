using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Localization;
using UnityEngine.UI;

/// <summary>
/// The tutorial's control hints: a panel low on the screen with a row per control, each a keycap and what it does,
/// e.g. [Shift] Hold to sprint. Keys are read from the player's current bindings, keyboard or gamepad, so a remap
/// shows up here too. A row is ticked off once the player has done it; the panel fades when told to.
/// Built in code, so a scene only needs to call <see cref="Show"/>.
/// </summary>
public class KeyHints : MonoBehaviour
{
    /// <summary>One line of the panel. Move = the four movement keys as a cross.</summary>
    public struct Row
    {
        public string Action; // an action in the Player map, or "Move"
        public string Label;  // already translated
    }

    private static readonly Color Panel = new Color(0.06f, 0.04f, 0.05f, 0.78f);
    private static readonly Color Cap = new Color(0.16f, 0.13f, 0.13f, 1f);
    private static readonly Color CapEdge = new Color(0.86f, 0.74f, 0.52f, 1f);
    private static readonly Color Text = new Color(0.95f, 0.9f, 0.82f, 1f);
    private static readonly Color Done = new Color(0.55f, 0.85f, 0.45f, 1f);

    private static KeyHints _instance;

    private CanvasGroup _group;
    private RectTransform _panel;
    private TMP_Text[] _labels = new TMP_Text[0];
    private Image[][] _caps = new Image[0][];

    /// <summary>Replaces whatever hint is up with these rows.</summary>
    public static void Show(params Row[] rows)
    {
        if (_instance == null) _instance = new GameObject("KeyHints").AddComponent<KeyHints>();
        _instance.Build(rows);
    }

    /// <summary>Ticks a row off: its keys and words turn green.</summary>
    public static void Tick(int row)
    {
        if (_instance == null || row < 0 || row >= _instance._labels.Length) return;
        _instance._labels[row].color = Done;
        foreach (Image cap in _instance._caps[row]) cap.color = Done;
        _instance._labels[row].transform.DOPunchScale(Vector3.one * 0.12f, 0.25f, 4).SetUpdate(true);
    }

    public static void Hide()
    {
        if (_instance == null) return;
        _instance._group.DOKill();
        _instance._group.DOFade(0f, 0.35f).SetUpdate(true);
    }

    private void Awake()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30500; // over the character screen (the equip hint sits on it), under cutscene bars
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;
        _group = gameObject.AddComponent<CanvasGroup>();
        _group.blocksRaycasts = false;
        _group.interactable = false;
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    private void Build(Row[] rows)
    {
        if (_panel != null) Destroy(_panel.gameObject);

        _panel = New("Panel", transform, typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        // Bottom left: the middle of the screen is the knight and the floor ahead of him, where the guide arrow is.
        _panel.anchorMin = _panel.anchorMax = _panel.pivot = new Vector2(0f, 0f);
        _panel.anchoredPosition = new Vector2(56f, 96f);
        _panel.GetComponent<Image>().color = Panel;
        var column = _panel.GetComponent<VerticalLayoutGroup>();
        column.padding = new RectOffset(28, 28, 18, 18);
        column.spacing = 12f;
        column.childAlignment = TextAnchor.MiddleLeft;
        column.childControlWidth = column.childControlHeight = true;
        column.childForceExpandWidth = column.childForceExpandHeight = false;
        var fit = _panel.GetComponent<ContentSizeFitter>();
        fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _labels = new TMP_Text[rows.Length];
        _caps = new Image[rows.Length][];
        for (int i = 0; i < rows.Length; i++) BuildRow(i, rows[i]);

        _group.DOKill();
        _group.alpha = 0f;
        _group.DOFade(1f, 0.35f).SetUpdate(true);
        _panel.localScale = Vector3.one * 0.92f;
        _panel.DOScale(1f, 0.35f).SetEase(Ease.OutBack).SetUpdate(true);
    }

    private void BuildRow(int index, Row row)
    {
        RectTransform line = New("Row", _panel, typeof(HorizontalLayoutGroup));
        var layout = line.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 16f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;

        bool cross = row.Action == "Move" && !InputHelper.IsGamepadLastUsed();
        if (string.IsNullOrEmpty(row.Action)) _caps[index] = new Image[0]; // words only
        else _caps[index] = cross ? MoveCross(line) : new[] { KeyCap(line, InputHelper.GetBindingString(row.Action)) };

        TMP_Text label = Label(line, row.Label, 34f);
        _labels[index] = label;
    }

    // W above, A S D below, the way they sit on the keyboard.
    private Image[] MoveCross(Transform parent)
    {
        RectTransform block = New("Keys", parent, typeof(VerticalLayoutGroup));
        var column = block.GetComponent<VerticalLayoutGroup>();
        column.spacing = 6f;
        column.childAlignment = TextAnchor.MiddleCenter;
        column.childControlWidth = column.childControlHeight = true;
        column.childForceExpandWidth = column.childForceExpandHeight = false;

        RectTransform top = New("Top", block, typeof(HorizontalLayoutGroup));
        top.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
        RectTransform bottom = New("Bottom", block, typeof(HorizontalLayoutGroup));
        var bottomRow = bottom.GetComponent<HorizontalLayoutGroup>();
        bottomRow.spacing = 6f;
        bottomRow.childAlignment = TextAnchor.MiddleCenter;
        foreach (HorizontalLayoutGroup group in new[] { top.GetComponent<HorizontalLayoutGroup>(), bottomRow })
        {
            group.childControlWidth = group.childControlHeight = true;
            group.childForceExpandWidth = group.childForceExpandHeight = false;
        }

        return new[]
        {
            KeyCap(top, MovePart("up", "W")),
            KeyCap(bottom, MovePart("left", "A")),
            KeyCap(bottom, MovePart("down", "S")),
            KeyCap(bottom, MovePart("right", "D")),
        };
    }

    // The keyboard key on one side of the Move composite, after any remap.
    private static string MovePart(string part, string fallback)
    {
        InputAction move = InputSystem.actions != null ? InputSystem.actions.FindAction("Move") : null;
        if (move == null) return fallback;
        for (int i = 0; i < move.bindings.Count; i++)
        {
            InputBinding binding = move.bindings[i];
            if (binding.isPartOfComposite && binding.name == part && binding.effectivePath.Contains("<Keyboard>"))
                return move.GetBindingDisplayString(i);
        }
        return fallback;
    }

    private Image KeyCap(Transform parent, string key)
    {
        RectTransform cap = New("Key", parent, typeof(Image), typeof(Outline), typeof(LayoutElement));
        Image image = cap.GetComponent<Image>();
        image.color = Cap;
        Outline edge = cap.GetComponent<Outline>();
        edge.effectColor = CapEdge;
        edge.effectDistance = new Vector2(2f, -2f);

        TMP_Text text = Label(cap, string.IsNullOrEmpty(key) ? "?" : key.ToUpperInvariant(), 28f);
        text.alignment = TextAlignmentOptions.Center;
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = textRect.offsetMax = Vector2.zero;

        LayoutElement size = cap.GetComponent<LayoutElement>();
        size.minHeight = size.preferredHeight = 56f;
        size.minWidth = size.preferredWidth = Mathf.Max(56f, text.GetPreferredValues(text.text).x + 28f);
        return image;
    }

    private static TMP_Text Label(Transform parent, string words, float size)
    {
        RectTransform rect = New("Text", parent, typeof(TextMeshProUGUI));
        TMP_Text text = rect.GetComponent<TMP_Text>();
        if (TMP_Settings.defaultFontAsset != null) text.font = TMP_Settings.defaultFontAsset;
        text.text = words;
        text.fontSize = size;
        text.color = Text;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        // Chinese and Japanese need the CJK-capable font, as everywhere else in the UI.
        var font = text.gameObject.AddComponent<LocalizedFontAsset>();
        font.AssetReference = new LocalizedTmpFont { TableReference = "UIFonts", TableEntryReference = "MainFont" };
        return text;
    }

    private static RectTransform New(string name, Transform parent, params System.Type[] components)
    {
        var go = new GameObject(name, typeof(RectTransform));
        foreach (System.Type component in components) go.AddComponent(component);
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }
}
