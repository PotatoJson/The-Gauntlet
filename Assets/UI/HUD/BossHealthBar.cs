using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening; // Required for DOTween
using TMPro;

/// <summary>
/// Shows one health bar per living boss, stacked vertically and each labelled with its own name.
///
/// This used to be a single shared bar, which broke down as soon as a chamber held two bosses:
/// engaging the second one overwrote the first one's bar, and killing either boss hid the bar for
/// both. Bars are now owned by the boss that registered them, so they are independent.
///
/// The bar assigned in the Inspector acts as the template. Extra bars are cloned from it at
/// runtime, so no additional UI has to be authored to support more bosses.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class BossHealthBar : MonoBehaviour
{
    public static BossHealthBar Instance;

    [Header("UI References")]
    [Tooltip("A single boss bar. Used directly for the first boss and cloned for any others.")]
    [SerializeField] private GameObject healthBarContainer;
    [SerializeField] private Image fillImage;
    [SerializeField] private TMP_Text bossNameText;

    [Header("Animation Settings")]
    [SerializeField] private float fadeDuration = 0.5f;
    [SerializeField] private float introFillDuration = 1.0f; // How fast it shoots left-to-right
    [SerializeField] private float damageDropDuration = 0.25f; // Smooth health drain when hit

    [Header("Layout")]
    [Tooltip("How many bars sit side by side before wrapping onto another row. 2 puts a second " +
             "boss beside the first instead of underneath, so the name labels cannot overlap.")]
    [SerializeField] private int barsPerRow = 2;

    [Tooltip("Horizontal gap between bars in a row, on top of the bar's own width.")]
    [SerializeField] private float columnGap = 40f;

    [Tooltip("Vertical gap between rows. Only used when there are more bosses than fit in a row.")]
    [SerializeField] private float rowSpacing = 64f;

    private class Bar
    {
        public GameObject Root;
        public CanvasGroup Group;
        public Image Fill;
        public TMP_Text Label;
        public RectTransform Rect;
        public Tween FillTween;
        public bool InUse;

        // Guards against restarting the drain tween when nothing actually changed, so callers
        // are free to refresh their bar every frame.
        public float LastTarget = -1f;
    }

    private class Entry
    {
        public Component Owner;
        public Bar Bar;
    }

    private readonly List<Bar> _pool = new List<Bar>();
    private readonly List<Entry> _entries = new List<Entry>();

    // Child paths inside the template, used to find the same parts inside a clone.
    private string _fillPath;
    private string _labelPath;
    private Vector2 _basePosition;

    private PlayerHealth _player;
    private float _playerLookupTimer;

    private void Awake()
    {
        Instance = this;

        // Per-bar CanvasGroups do the fading now, so the shared one stays fully visible.
        CanvasGroup rootGroup = GetComponent<CanvasGroup>();
        if (rootGroup != null) rootGroup.alpha = 1f;

        if (healthBarContainer == null) return;

        _fillPath = GetRelativePath(healthBarContainer.transform, fillImage != null ? fillImage.transform : null);
        _labelPath = GetRelativePath(healthBarContainer.transform, bossNameText != null ? bossNameText.transform : null);

        RectTransform templateRect = healthBarContainer.GetComponent<RectTransform>();
        if (templateRect != null) _basePosition = templateRect.anchoredPosition;

        healthBarContainer.SetActive(false);
    }

    #region Public API

    /// <summary>
    /// Shows (or refreshes) the bar belonging to one boss. Safe to call repeatedly - a boss that
    /// already has a bar just gets it updated, so callers do not need to track whether they have
    /// registered yet.
    /// </summary>
    public void ShowBossHealthBar(Component boss, float currentHealth, float maxHealth, string bossName)
    {
        if (boss == null || healthBarContainer == null) return;

        Entry entry = FindEntry(boss);
        if (entry == null)
        {
            Bar bar = TakeBar();
            if (bar == null) return;

            entry = new Entry { Owner = boss, Bar = bar };
            _entries.Add(entry);
            LayoutBars();

            bar.Root.SetActive(true);
            bar.Group.DOKill();
            bar.Group.alpha = 0f;
            bar.Group.DOFade(1f, fadeDuration).SetUpdate(true);

            if (bar.Fill != null)
            {
                bar.Fill.fillAmount = 0f;
                bar.LastTarget = Ratio(currentHealth, maxHealth);
                bar.FillTween?.Kill();
                bar.FillTween = bar.Fill.DOFillAmount(Ratio(currentHealth, maxHealth), introFillDuration)
                    .SetEase(Ease.OutCubic)
                    .SetUpdate(true);
            }
        }
        else
        {
            SetFill(entry.Bar, currentHealth, maxHealth);
        }

        if (entry.Bar.Label != null) entry.Bar.Label.text = bossName;
    }

    /// <summary>
    /// Drains one boss's bar. If that boss has no bar yet it is shown, so a boss hit from range
    /// can never end up damaged but invisible.
    /// </summary>
    public void UpdateHealth(Component boss, float currentHealth, float maxHealth, string bossName = null)
    {
        if (boss == null) return;

        Entry entry = FindEntry(boss);
        if (entry == null)
        {
            ShowBossHealthBar(boss, currentHealth, maxHealth, bossName ?? boss.name);
            return;
        }

        SetFill(entry.Bar, currentHealth, maxHealth);
    }

    /// <summary>Hides and releases one boss's bar, closing the gap it leaves behind.</summary>
    public void HideBossHealthBar(Component boss)
    {
        Entry entry = FindEntry(boss);
        if (entry == null) return;

        _entries.Remove(entry);
        ReleaseBar(entry.Bar);
        LayoutBars();
    }

    /// <summary>Clears every bar, for the player dying or leaving the fight.</summary>
    public void HideAllBossHealthBars()
    {
        for (int i = _entries.Count - 1; i >= 0; i--) ReleaseBar(_entries[i].Bar);

        _entries.Clear();
    }

    #endregion

    private void Update()
    {
        // A boss destroyed without calling Hide would otherwise strand its bar on screen.
        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            if (_entries[i].Owner == null)
            {
                ReleaseBar(_entries[i].Bar);
                _entries.RemoveAt(i);
                LayoutBars();
            }
        }

        if (_entries.Count == 0) return;

        // The bars belong to a fight the player is no longer in.
        if (ResolvePlayer() != null && _player.IsDead) HideAllBossHealthBars();
    }

    private PlayerHealth ResolvePlayer()
    {
        if (_player != null) return _player;

        _playerLookupTimer -= Time.unscaledDeltaTime;
        if (_playerLookupTimer > 0f) return null;

        _playerLookupTimer = 0.5f;
        _player = FindFirstObjectByType<PlayerHealth>();
        return _player;
    }

    #region Bar Plumbing

    private Entry FindEntry(Component boss)
    {
        foreach (Entry entry in _entries)
        {
            if (entry.Owner == boss) return entry;
        }

        return null;
    }

    private void SetFill(Bar bar, float currentHealth, float maxHealth)
    {
        if (bar.Fill == null) return;

        float target = Ratio(currentHealth, maxHealth);
        if (Mathf.Approximately(bar.LastTarget, target)) return;

        bar.LastTarget = target;

        bar.FillTween?.Kill();
        bar.FillTween = bar.Fill.DOFillAmount(target, damageDropDuration)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true);
    }

    private static float Ratio(float current, float max)
    {
        return max > 0f ? Mathf.Clamp01(current / max) : 0f;
    }

    private Bar TakeBar()
    {
        foreach (Bar bar in _pool)
        {
            if (!bar.InUse)
            {
                bar.InUse = true;
                return bar;
            }
        }

        Bar created = CreateBar(_pool.Count == 0);
        if (created == null) return null;

        created.InUse = true;
        _pool.Add(created);
        return created;
    }

    /// <summary>The first bar reuses the authored template; the rest are clones of it.</summary>
    private Bar CreateBar(bool useTemplate)
    {
        GameObject root;
        if (useTemplate)
        {
            root = healthBarContainer;
        }
        else
        {
            root = Instantiate(healthBarContainer, healthBarContainer.transform.parent);
            root.name = $"{healthBarContainer.name}_{_pool.Count}";
        }

        if (root == null) return null;

        CanvasGroup group = root.GetComponent<CanvasGroup>();
        if (group == null) group = root.AddComponent<CanvasGroup>();

        // Bars are pure feedback and must never eat clicks.
        group.interactable = false;
        group.blocksRaycasts = false;

        Bar bar = new Bar
        {
            Root = root,
            Group = group,
            Rect = root.GetComponent<RectTransform>(),
            Fill = ResolveChild<Image>(root.transform, _fillPath, useTemplate ? fillImage : null),
            Label = ResolveChild<TMP_Text>(root.transform, _labelPath, useTemplate ? bossNameText : null),
        };

        root.SetActive(false);
        return bar;
    }

    private void ReleaseBar(Bar bar)
    {
        if (bar == null) return;

        bar.FillTween?.Kill();
        bar.FillTween = null;
        bar.LastTarget = -1f;

        bar.Group.DOKill();
        bar.Group.DOFade(0f, fadeDuration).SetUpdate(true).OnComplete(() =>
        {
            if (bar.Root != null) bar.Root.SetActive(false);
            bar.InUse = false;
        });
    }

    /// <summary>
    /// Spreads the bars sideways around the anchor, in the order the bosses were engaged.
    ///
    /// Each row is centred on the anchor, so a single boss stays exactly where the bar was
    /// authored and only a second boss pushes them apart.
    /// </summary>
    private void LayoutBars()
    {
        int count = _entries.Count;
        if (count == 0) return;

        int perRow = Mathf.Max(1, barsPerRow);
        float columnSpacing = GetBarWidth() + columnGap;

        for (int i = 0; i < count; i++)
        {
            RectTransform rect = _entries[i].Bar.Rect;
            if (rect == null) continue;

            int row = i / perRow;
            int column = i % perRow;

            // Centre on however many bars this particular row ended up with.
            int barsInRow = Mathf.Min(perRow, count - (row * perRow));
            float offset = column - ((barsInRow - 1) * 0.5f);

            rect.anchoredPosition = _basePosition
                                    + Vector2.right * (columnSpacing * offset)
                                    + Vector2.down * (rowSpacing * row);
        }
    }

    /// <summary>Measured from the template so the spacing follows the bar's real size.</summary>
    private float GetBarWidth()
    {
        RectTransform templateRect = healthBarContainer != null
            ? healthBarContainer.GetComponent<RectTransform>()
            : null;

        if (templateRect == null) return 0f;

        // rect.width is 0 until a layout pass has run, so fall back to the authored size.
        return templateRect.rect.width > 1f ? templateRect.rect.width : templateRect.sizeDelta.x;
    }

    #endregion

    #region Clone Part Lookup

    /// <summary>
    /// Slash-separated path from an ancestor down to a descendant, used to find the same part
    /// inside a clone. A clone is an exact copy, so the same path always resolves.
    /// </summary>
    private static string GetRelativePath(Transform root, Transform child)
    {
        if (root == null || child == null || child == root) return string.Empty;

        string path = child.name;
        Transform cursor = child.parent;

        while (cursor != null && cursor != root)
        {
            path = cursor.name + "/" + path;
            cursor = cursor.parent;
        }

        return cursor == root ? path : string.Empty;
    }

    private static T ResolveChild<T>(Transform root, string path, T templateValue) where T : Component
    {
        if (templateValue != null) return templateValue;

        if (!string.IsNullOrEmpty(path))
        {
            Transform found = root.Find(path);
            if (found != null)
            {
                T component = found.GetComponent<T>();
                if (component != null) return component;
            }
        }

        // Last resort so a renamed child does not leave the clone blank.
        return root.GetComponentInChildren<T>(true);
    }

    #endregion

    private void OnDestroy()
    {
        foreach (Bar bar in _pool)
        {
            bar.FillTween?.Kill();
            if (bar.Group != null) bar.Group.DOKill();
            if (bar.Fill != null) bar.Fill.DOKill();
        }

        if (Instance == this) Instance = null;
    }
}
