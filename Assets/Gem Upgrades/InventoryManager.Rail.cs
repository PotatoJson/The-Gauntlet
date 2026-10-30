using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

/// <summary>
/// The gauntlet rail and the swap bar, plus the little animations that go with the character screen.
///
/// The rail replaces the old blank bookmark in the corner: two cards, one per gauntlet, each showing its name, its
/// element and how many sockets are filled, so it is obvious from the first glance that there are two gauntlets and
/// which one is on screen. While a new gauntlet is waiting to be placed, the cards turn into "Will be replaced" and
/// "Keep", and a bar along the bottom shows what is coming in and how it compares. Everything is built from code (the
/// scene's own art is reused where it exists), so there is nothing to wire up in the inspector.
///
/// All motion runs on unscaled time (the screen pauses the game) and is skipped when the "ReduceMotion" setting is on.
/// </summary>
public partial class InventoryManager
{
    #region Look
    private static readonly Color Cream = new Color32(0xEF, 0xE2, 0xC2, 0xFF);
    private static readonly Color Bark = new Color32(0x4A, 0x2E, 0x22, 0xFF);
    private static readonly Color BarkDeep = new Color32(0x2B, 0x18, 0x10, 0xFF);
    private static readonly Color Ink = new Color32(0x3B, 0x24, 0x18, 0xFF);
    private static readonly Color InkSoft = new Color32(0x7A, 0x5A, 0x44, 0xFF);
    private static readonly Color CardOff = new Color32(0x7E, 0x4A, 0x38, 0xFF);
    private static readonly Color CardOffText = new Color32(0xE6, 0xCF, 0xA8, 0xFF);
    private static readonly Color CardOffSoft = new Color32(0xC9, 0xB5, 0x8C, 0xFF);
    private static readonly Color Moss = new Color32(0x2A, 0x94, 0x60, 0xFF);
    private static readonly Color Alarm = new Color32(0xC0, 0x3A, 0x2E, 0xFF);
    private static readonly Color Gold = new Color32(0xF2, 0xB6, 0x3D, 0xFF);
    private static readonly Color SkillPink = new Color32(0xD8, 0x50, 0x6A, 0xFF);
    private static readonly Color Positive = new Color32(0x97, 0xC4, 0x59, 0xFF);
    private static readonly Color Negative = new Color32(0xF0, 0x95, 0x95, 0xFF);

    private static Color ElementColor(ElementType element)
    {
        switch (element)
        {
            case ElementType.Fire: return new Color32(0xE8, 0x74, 0x2A, 0xFF);
            case ElementType.Ice: return new Color32(0x4F, 0xA3, 0xE0, 0xFF);
            case ElementType.Earth: return new Color32(0x6B, 0x8E, 0x3A, 0xFF);
            case ElementType.Wind: return new Color32(0x7A, 0xD3, 0xC0, 0xFF);
            case ElementType.Lightning: return new Color32(0xF2, 0xD2, 0x3D, 0xFF);
            default: return new Color32(0x8F, 0x93, 0x9C, 0xFF);
        }
    }

    // One string from the UI table, with a plain fallback so a missing entry never shows "No translation found".
    private static string Loc(string key, string fallback)
    {
        try
        {
            var s = new LocalizedString("UIStrings", key);
            if (s.IsEmpty) return fallback;
            string value = s.GetLocalizedString();
            return string.IsNullOrEmpty(value) || value.StartsWith("No translation") ? fallback : value;
        }
        catch (System.Exception)
        {
            return fallback;
        }
    }

    private static string SwitcherWord(LocalizedString key, string fallback)
    {
        if (key == null || key.IsEmpty) return fallback;
        string word = key.GetLocalizedString();
        return string.IsNullOrEmpty(word) ? fallback : word;
    }

    // The gauntlet's display name, from the existing translated names where there are some.
    private static string GauntletLabel(GauntletData data)
    {
        if (data == null) return Loc("Gauntlet Basic Name", "Gauntlet");

        string key = null;
        switch (data.Element)
        {
            case ElementType.Fire: key = "firegauntlet"; break;
            case ElementType.Earth: key = "earthgauntlet"; break;
            case ElementType.Lightning: key = "lightninggauntlet"; break;
            case ElementType.Wind: key = "windgauntlet"; break;
        }

        if (key != null) return Loc(key, data.Name);
        return string.IsNullOrEmpty(data.Name) ? Loc("Gauntlet Basic Name", "Gauntlet") : data.Name;
    }

    private static void Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    private TMP_Text MakeText(Transform parent, string name, float minSize, float maxSize, TextAlignmentOptions align, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        var text = go.GetComponent<TextMeshProUGUI>();
        if (detailNameText != null) text.font = detailNameText.font;
        text.enableAutoSizing = true; // longer words in other languages shrink instead of spilling out
        text.fontSizeMin = minSize;
        text.fontSizeMax = maxSize;
        text.alignment = align;
        text.color = color;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;

        // Chinese and Japanese need their own font, like every other piece of UI text.
        var font = go.AddComponent<LocalizedFontAsset>();
        font.AssetReference = new LocalizedTmpFont { TableReference = "UIFonts", TableEntryReference = "MainFont" };
        return text;
    }

    private static Image MakeImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }
    #endregion

    #region Rail
    private class RailCard
    {
        public bool primary;
        public RectTransform root;
        public CanvasGroup group;
        public Graphic bg;
        public SlantedPanel edge;
        public Outline frame;
        public TMP_Text title, sub, tag;
        public GameObject pipRow;
        public Image[] pipFill = new Image[5];
        public Image[] pipBorder = new Image[5];
        public Image skillFill, skillBorder;
        public bool active;
    }

    private const float CardWidth = 330f, CardHeight = 112f, ActiveShift = -12f, Slant = 36f;

    private RailCard _railPrimary, _railSecondary;
    private TMP_Text _railHeader;
    private bool _switcherBuilt;
    private bool _animateSwitch; // set while a page change is in progress, so the cards animate instead of jumping
    private float _pipTimer;
    private RectTransform _titleHolder;
    private Vector2 _titleHolderBase;
    private Sequence _openSeq;

    private void BuildGauntletSwitcher()
    {
        if (_switcherBuilt || switchGauntletButton == null) return;
        _switcherBuilt = true;

        // The old switch button stays as the rail's container: it is the controller's stop in the header loop.
        var rail = (RectTransform)switchGauntletButton.transform;
        var hover = rail.GetComponent<UIHoverSpriteToggle>();
        if (hover != null) hover.enabled = false;

        switchGauntletButton.transition = Selectable.Transition.None;
        var art = rail.GetComponent<Image>();
        art.sprite = null;
        art.color = new Color(0f, 0f, 0f, 0f); // invisible, but still a target for the controller's selection

        rail.sizeDelta = new Vector2(CardWidth + 20f, 250f);
        rail.anchoredPosition = new Vector2(432f, 84f); // clear of the gauntlet's cuff

        Transform brackets = rail.Find("Selection_Brackets");
        if (brackets != null) ((RectTransform)brackets).sizeDelta = rail.sizeDelta + new Vector2(16f, 16f);

        if (gauntletTitleText != null && gauntletTitleText.transform.parent is RectTransform holder)
        {
            _titleHolder = holder;
            _titleHolderBase = holder.anchoredPosition;
        }

        _railHeader = MakeText(rail, "Rail Header", 12f, 20f, TextAlignmentOptions.Center, Ink);
        Place(_railHeader.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -140f), new Vector2(CardWidth, 44f));
        _railHeader.textWrappingMode = TextWrappingModes.Normal;
        _railHeader.gameObject.SetActive(false);

        _railPrimary = BuildRailCard(rail, "Card Primary", CardHeight * 0.5f, true);
        _railSecondary = BuildRailCard(rail, "Card Secondary", -CardHeight * 0.5f, false);
    }

    private RailCard BuildRailCard(RectTransform rail, string name, float y, bool primary)
    {
        var card = new RailCard { primary = primary };

        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(SlantedPanel), typeof(Button), typeof(Outline), typeof(CanvasGroup));
        card.root = (RectTransform)go.transform;
        card.root.SetParent(rail, false);
        Place(card.root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(CardWidth, CardHeight));

        card.group = go.GetComponent<CanvasGroup>();
        card.bg = go.GetComponent<SlantedPanel>();
        card.bg.color = CardOff;

        var button = go.GetComponent<Button>();
        button.targetGraphic = card.bg;
        button.transition = Selectable.Transition.None;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(() => ShowGauntlet(primary));

        card.frame = go.GetComponent<Outline>();
        card.frame.effectColor = Alarm;
        card.frame.effectDistance = new Vector2(5f, 5f);
        card.frame.enabled = false;

        ((SlantedPanel)card.bg).slant = Slant;

        // The coloured spine along the slanted left edge: its own panel, so it can follow the slant.
        var edgeGo = new GameObject("Edge", typeof(RectTransform), typeof(CanvasRenderer), typeof(SlantedPanel));
        edgeGo.transform.SetParent(card.root, false);
        var edgeRect = (RectTransform)edgeGo.transform;
        edgeRect.anchorMin = Vector2.zero;
        edgeRect.anchorMax = Vector2.one;
        edgeRect.offsetMin = edgeRect.offsetMax = Vector2.zero;
        card.edge = edgeGo.GetComponent<SlantedPanel>();
        card.edge.raycastTarget = false;
        card.edge.slant = Slant;
        card.edge.stripWidth = 4f;
        card.edge.color = Bark;

        card.title = MakeText(card.root, "Title", 16f, 28f, TextAlignmentOptions.MidlineLeft, Ink);
        Place(card.title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(34f, -6f), new Vector2(-48f, 34f));

        card.sub = MakeText(card.root, "Sub", 12f, 18f, TextAlignmentOptions.MidlineLeft, InkSoft);
        Place(card.sub.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(46f, -39f), new Vector2(-60f, 24f));

        card.tag = MakeText(card.root, "Tag", 12f, 20f, TextAlignmentOptions.MidlineLeft, Alarm);
        Place(card.tag.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(60f, 8f), new Vector2(-74f, 32f));
        card.tag.gameObject.SetActive(false);

        var rowGo = new GameObject("Sockets", typeof(RectTransform));
        rowGo.transform.SetParent(card.root, false);
        card.pipRow = rowGo;
        Place((RectTransform)rowGo.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        for (int i = 0; i < 5; i++)
        {
            card.pipBorder[i] = MakePip(rowGo.transform, $"Pip {i + 1}", new Vector2(60f + i * 28f, 10f), 22f, 0f, out card.pipFill[i]);
        }

        // The skill socket is a diamond, set a little apart from the five finger sockets.
        card.skillBorder = MakePip(rowGo.transform, "Skill Pip", new Vector2(60f + 5 * 28f + 14f, 13f), 17f, 45f, out card.skillFill);

        return card;
    }

    private Image MakePip(Transform parent, string name, Vector2 position, float size, float rotation, out Image fill)
    {
        Image border = MakeImage(parent, name, Bark);
        Place(border.rectTransform, Vector2.zero, Vector2.zero, new Vector2(0f, 0f), position, new Vector2(size, size));
        border.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rotation);

        fill = MakeImage(border.transform, "Fill", Cream);
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = Vector2.one;
        fill.rectTransform.offsetMin = new Vector2(4f, 4f);
        fill.rectTransform.offsetMax = new Vector2(-4f, -4f);
        return border;
    }

    // Newest first: a replaced gauntlet stays in the hierarchy until the end of the frame, and the rail must not read it.
    private GauntletManager ManagerOf(Transform page)
    {
        if (page == null) return null;

        for (int i = page.childCount - 1; i >= 0; i--)
        {
            GauntletManager manager = page.GetChild(i).GetComponent<GauntletManager>();
            if (manager != null) return manager;
        }

        return null;
    }

    private RectTransform ActivePage() => (RectTransform)(_isDisplayingPrimary ? primaryGauntlet : secondaryGauntlet);

    /// <summary>Brings both cards in line with the gauntlets: names, elements, which one is on screen, and the swap state.</summary>
    private void RefreshGauntletSwitcher()
    {
        if (_railPrimary == null) return;

        FillCard(_railPrimary, primaryGauntlet, primaryKey, "Primary");
        FillCard(_railSecondary, secondaryGauntlet, secondaryKey, "Secondary");
        StyleCards(_animateSwitch);

        if (_railHeader != null)
        {
            _railHeader.gameObject.SetActive(_isReplaceUIPending);
            if (_isReplaceUIPending) _railHeader.text = _swapNotice;
        }

        RefreshRailPips();
        RefreshSwapBar();
    }

    private void FillCard(RailCard card, Transform page, LocalizedString key, string fallback)
    {
        GauntletManager gm = ManagerOf(page);
        card.title.text = SwitcherWord(key, fallback);
        card.sub.text = GauntletLabel(gm != null ? gm.LinkedGauntletData : null);
    }

    // Active card: cream, green edge, nudged toward the gauntlet. The other recedes. In swap mode the sockets give
    // way to a verdict: "Will be replaced" on the one on screen, "Keep" on the other.
    private void StyleCards(bool animate)
    {
        StyleCard(_railPrimary, _isDisplayingPrimary, animate);
        StyleCard(_railSecondary, !_isDisplayingPrimary, animate);
    }

    private void StyleCard(RailCard card, bool active, bool animate)
    {
        bool wasActive = card.active;
        card.active = active;

        Color bg = active ? Cream : CardOff;
        Color edge = active ? Moss : Bark;
        float edgeWidth = active ? 12f : 4f;
        float x = active ? ActiveShift : 0f;
        float scale = active ? 1f : 0.96f;

        card.title.color = active ? Ink : CardOffText;
        card.sub.color = active ? InkSoft : CardOffSoft;

        bool pending = _isReplaceUIPending;
        card.pipRow.SetActive(!pending);
        card.tag.gameObject.SetActive(pending);
        if (pending)
        {
            card.tag.text = Loc(active ? "Gauntlet Will Replace" : "Gauntlet Keep", active ? "Will be replaced" : "Keep");
            card.tag.color = active ? Alarm : Positive;
        }

        SetFramePulse(card, pending && active);

        card.bg.DOKill();
        card.edge.DOKill();
        DOTween.Kill(card.edge);
        card.root.DOKill();

        if (!animate || GauntletScreenFx.ReduceMotion || !isActiveAndEnabled)
        {
            card.bg.color = bg;
            card.edge.color = edge;
            card.edge.stripWidth = edgeWidth;
            card.root.anchoredPosition = new Vector2(x, card.root.anchoredPosition.y);
            card.root.localScale = Vector3.one * scale;
            return;
        }

        card.bg.DOColor(bg, 0.15f).SetUpdate(true).SetLink(card.bg.gameObject);
        card.edge.DOColor(edge, 0.15f).SetUpdate(true).SetLink(card.edge.gameObject);
        DOTween.To(() => card.edge.stripWidth, v => card.edge.stripWidth = v, edgeWidth, 0.2f).SetEase(Ease.OutBack).SetUpdate(true).SetTarget(card.edge).SetLink(card.edge.gameObject);
        card.root.DOAnchorPosX(x, 0.24f).SetEase(Ease.OutBack, 1.8f).SetUpdate(true).SetLink(card.root.gameObject);

        // The card that just took over pops up a little; the other settles back.
        if (active && !wasActive) card.root.localScale = Vector3.one * 1.1f;
        card.root.DOScale(scale, 0.26f).SetEase(Ease.OutBack, 2f).SetUpdate(true).SetLink(card.root.gameObject);
    }

    // A slow red pulse on the outline of the gauntlet that is about to be replaced.
    private void SetFramePulse(RailCard card, bool on)
    {

        card.frame.enabled = on;
        DOTween.Kill(card.frame);
        if (!on) return;

        Color bright = Alarm;
        Color dim = new Color(Alarm.r, Alarm.g, Alarm.b, 0.25f);
        card.frame.effectColor = bright;

        if (GauntletScreenFx.ReduceMotion) return;
        DOTween.Kill(card.frame);
        DOVirtual.Float(0f, 1f, 0.7f, t => { if (card != null && card.frame != null) card.frame.effectColor = Color.Lerp(bright, dim, t); })
            .SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true).SetTarget(card.frame).SetLink(card.root.gameObject);
    }

    private void RefreshRailPipsThrottled()
    {
        _pipTimer -= Time.unscaledDeltaTime;
        if (_pipTimer > 0f) return;
        _pipTimer = 0.25f;
        RefreshRailPips();
    }

    private void RefreshRailPips()
    {
        if (_railPrimary == null) return;
        RefreshPips(_railPrimary, primaryGauntlet);
        RefreshPips(_railSecondary, secondaryGauntlet);
    }

    // Five finger sockets and the skill socket: locked ones are dark, filled ones gold (pink for the skill gem), empty ones pale.
    private void RefreshPips(RailCard card, Transform page)
    {
        GauntletManager gm = ManagerOf(page);
        Color empty = card.active ? CardOffSoft : new Color32(0x6A, 0x40, 0x30, 0xFF);

        for (int i = 0; i < 5; i++)
        {
            bool exists = gm != null && i < gm.fingerSlots.Count && gm.fingerSlots[i] != null;
            bool locked = !exists || i >= gm.currentActiveSlots;
            bool filled = false;

            if (exists && !locked)
            {
                Transform slot = gm.fingerSlots[i].transform;
                DraggableGem gem = slot.GetComponentInChildren<DraggableGem>(true);
                filled = gem != null && gem.transform.parent == slot;
            }

            card.pipBorder[i].color = locked ? new Color(Bark.r, Bark.g, Bark.b, 0.5f) : Bark;
            card.pipFill[i].color = locked ? BarkDeep : filled ? Gold : empty;
        }

        bool skillFilled = false;
        if (gm != null && gm.SkillSlot != null)
        {
            SkillSlotManager skill = gm.SkillSlot.GetComponent<SkillSlotManager>();
            skillFilled = skill != null && skill.GetEquippedSkillGem() != null;
        }

        card.skillFill.color = skillFilled ? SkillPink : empty;
    }
    #endregion

    #region Swap bar
    private const float BarRestY = -516f; // relative to the swap overlay; puts the bar on the bottom edge of the screen
    private const float BarHeight = 140f;

    private RectTransform _bar;
    private Image _barIcon, _barRarityBg;
    private TMP_Text _barRarity, _barName, _barStats;
    private LocalizeStringEvent _noticeEvent;
    private string _swapNotice = "";
    private Tween _barTween;

    private void BuildSwapBar()
    {
        if (_bar != null || replaceGauntletPanel == null) return;

        var panel = (RectTransform)replaceGauntletPanel.transform;

        // The old paper note says "Gauntlet slots full! Choose one to replace.": keep its words for the rail, hide the paper.
        Transform note = panel.Find("background");
        if (note != null)
        {
            _noticeEvent = note.GetComponentInChildren<LocalizeStringEvent>(true);
            note.gameObject.SetActive(false);
        }

        var barGo = new GameObject("Swap Bar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        _bar = (RectTransform)barGo.transform;
        _bar.SetParent(panel, false);
        Place(_bar, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-371f, BarRestY), new Vector2(((RectTransform)transform).rect.width, BarHeight));
        var barImage = barGo.GetComponent<Image>();
        barImage.color = new Color(Bark.r, Bark.g, Bark.b, 0.98f);

        Image top = MakeImage(_bar, "Top Line", BarkDeep);
        Place(top.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 5f));

        // Incoming gauntlet: a framed swatch in its element colour, then rarity, name and the comparison.
        Image frame = MakeImage(_bar, "Icon Frame", Cream);
        Place(frame.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(34f, 0f), new Vector2(100f, 100f));
        _barIcon = MakeImage(frame.transform, "Icon", Gold);
        _barIcon.rectTransform.anchorMin = Vector2.zero;
        _barIcon.rectTransform.anchorMax = Vector2.one;
        _barIcon.rectTransform.offsetMin = new Vector2(8f, 8f);
        _barIcon.rectTransform.offsetMax = new Vector2(-8f, -8f);

        _barRarityBg = MakeImage(_bar, "Rarity Tag", Cream);
        Place(_barRarityBg.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(160f, 42f), new Vector2(150f, 28f));
        _barRarity = MakeText(_barRarityBg.transform, "Rarity", 12f, 18f, TextAlignmentOptions.Center, Ink);
        _barRarity.rectTransform.anchorMin = Vector2.zero;
        _barRarity.rectTransform.anchorMax = Vector2.one;
        _barRarity.rectTransform.offsetMin = new Vector2(6f, 0f);
        _barRarity.rectTransform.offsetMax = new Vector2(-6f, 0f);

        _barName = MakeText(_bar, "Name", 18f, 34f, TextAlignmentOptions.MidlineLeft, Cream);
        Place(_barName.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(160f, 4f), new Vector2(420f, 40f));

        _barStats = MakeText(_bar, "Stats", 14f, 22f, TextAlignmentOptions.MidlineLeft, Cream);
        Place(_barStats.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(160f, -38f), new Vector2(430f, 32f));
        _barStats.richText = true;

        // The "slots full" notice lives beside the rarity tag: the space under the rail now belongs to the gauntlet's cuff.
        if (_railHeader != null)
        {
            _railHeader.rectTransform.SetParent(_bar, false);
            Place(_railHeader.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(320f, 42f), new Vector2(330f, 28f));
            _railHeader.color = Cream;
            _railHeader.alignment = TextAlignmentOptions.MidlineLeft;
            _railHeader.textWrappingMode = TextWrappingModes.NoWrap;
        }

        // The two existing buttons move into the bar, so their navigation and click wiring stay as they are.
        if (replaceGauntletBtn != null)
        {
            replaceGauntletBtn.transform.SetParent(_bar, false);
            var rect = (RectTransform)replaceGauntletBtn.transform;
            Place(rect, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-226f, 0f), new Vector2(330f, 70f));

            var image = replaceGauntletBtn.GetComponent<Image>();
            if (image != null) image.color = new Color(0.6f, 0.95f, 0.72f, 1f); // the one clear green action

            TMP_Text label = replaceGauntletBtn.GetComponentInChildren<TMP_Text>(true);
            if (label != null) { label.enableAutoSizing = true; label.fontSizeMin = 12f; label.fontSizeMax = 30f; label.color = Ink; }
        }

        if (replaceDiscardBtn != null)
        {
            replaceDiscardBtn.transform.SetParent(_bar, false);
            var rect = (RectTransform)replaceDiscardBtn.transform;
            Place(rect, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-30f, 0f), new Vector2(180f, 70f));

            TMP_Text label = replaceDiscardBtn.GetComponentInChildren<TMP_Text>(true);
            if (label != null) { label.enableAutoSizing = true; label.fontSizeMin = 12f; label.fontSizeMax = 30f; label.color = Ink; }
        }
    }

    private void ShowSwapBar()
    {
        BuildSwapBar();
        if (_bar == null) return;

        _swapNotice = _noticeEvent != null ? _noticeEvent.StringReference.GetLocalizedString() : "";

        if (replaceGauntletBtn != null) replaceGauntletBtn.interactable = true;
        if (replaceDiscardBtn != null) replaceDiscardBtn.interactable = true;

        RefreshSwapBar();

        _barTween?.Kill();
        if (GauntletScreenFx.ReduceMotion)
        {
            _bar.anchoredPosition = new Vector2(-371f, BarRestY);
            return;
        }

        // Rises from below the edge of the screen with a little overshoot.
        _bar.anchoredPosition = new Vector2(-371f, BarRestY - 180f);
        _barTween = _bar.DOAnchorPosY(BarRestY, 0.4f).SetEase(Ease.OutBack, 1.2f).SetUpdate(true).SetLink(_bar.gameObject);
    }

    /// <summary>Slides the bar away, then switches the overlay off. The buttons stop answering at once so nothing fires twice.</summary>
    private void HideSwapBar()
    {
        _barTween?.Kill();
        if (replaceGauntletPanel == null || !replaceGauntletPanel.activeSelf) return;

        if (replaceGauntletBtn != null) replaceGauntletBtn.interactable = false;
        if (replaceDiscardBtn != null) replaceDiscardBtn.interactable = false;

        if (_bar == null || GauntletScreenFx.ReduceMotion || !isActiveAndEnabled)
        {
            replaceGauntletPanel.SetActive(false);
            return;
        }

        _barTween = _bar.DOAnchorPosY(BarRestY - 180f, 0.24f).SetEase(Ease.InQuad).SetUpdate(true).SetLink(_bar.gameObject)
            .OnComplete(() =>
            {
                if (!_isReplaceUIPending && replaceGauntletPanel != null) replaceGauntletPanel.SetActive(false);
            });
    }

    private void HideSwapBarInstant()
    {
        _barTween?.Kill();
        if (replaceGauntletPanel != null) replaceGauntletPanel.SetActive(false);
        if (replaceGauntletBtn != null) replaceGauntletBtn.interactable = true;
        if (replaceDiscardBtn != null) replaceDiscardBtn.interactable = true;
    }

    private static int SocketCount(bool primary, GauntletRarity rarity, GauntletManager gm)
    {
        // Same rule GauntletManager.InitializeGauntlet applies when the gauntlet is actually placed.
        int bonus = (int)rarity;
        int fingers = gm != null ? gm.fingerSlots.Count : 5;
        return primary ? Mathf.Clamp(3 + bonus, 0, fingers) : Mathf.Clamp(1 + bonus, 0, 3);
    }

    private static string Delta(int change)
    {
        if (change > 0) return $" <color=#{ColorUtility.ToHtmlStringRGB(Positive)}>(+{change})</color>";
        if (change < 0) return $" <color=#{ColorUtility.ToHtmlStringRGB(Negative)}>({change})</color>";
        return "";
    }

    /// <summary>What is coming in, and how it compares with the gauntlet currently on screen (the one it would replace).</summary>
    private void RefreshSwapBar()
    {
        if (!_isReplaceUIPending || _bar == null || _pendingGauntletPrefab == null) return;

        GauntletManager incoming = _pendingGauntletPrefab.GetComponent<GauntletManager>();
        GauntletData data = incoming != null ? incoming.LinkedGauntletData : null;
        GauntletManager current = ManagerOf(_isDisplayingPrimary ? primaryGauntlet : secondaryGauntlet);

        int newSockets = SocketCount(_isDisplayingPrimary, _pendingGauntletRarity, incoming);
        int oldSockets = current != null ? current.currentActiveSlots : 0;
        int newDamage = data != null ? data.Damage : 0;
        int oldDamage = current != null && current.LinkedGauntletData != null ? current.LinkedGauntletData.Damage : 0;

        _barName.text = GauntletLabel(data);
        _barIcon.color = ElementColor(data != null ? data.Element : ElementType.Physical);

        switch (_pendingGauntletRarity)
        {
            case GauntletRarity.Rare:
                _barRarity.text = Loc("Gauntlet Rarity Rare", "Rare");
                _barRarityBg.color = new Color32(0x4F, 0xA3, 0xE0, 0xFF);
                break;
            case GauntletRarity.UltraRare:
                _barRarity.text = Loc("Gauntlet Rarity Ultra", "Ultra rare");
                _barRarityBg.color = Gold;
                break;
            default:
                _barRarity.text = Loc("Gauntlet Rarity Common", "Common");
                _barRarityBg.color = new Color32(0xBF, 0xB8, 0xA0, 0xFF);
                break;
        }

        string stats = $"{Loc("Gauntlet Sockets", "Sockets")} {newSockets}{Delta(newSockets - oldSockets)}";
        if (newDamage > 0 || oldDamage > 0) stats += $"     {Loc("Gauntlet Damage", "Damage")} {newDamage}{Delta(newDamage - oldDamage)}";
        _barStats.text = stats;
    }
    #endregion

    #region Motion
    private void KillOpenFx()
    {
        _openSeq?.Kill();
        _openSeq = null;
        RestoreOpenState();
    }

    // Puts back everything the opening animation moves, in case it was cut short.
    private void RestoreOpenState()
    {
        foreach (RailCard card in new[] { _railPrimary, _railSecondary })
        {
            if (card == null) continue;
            card.group.alpha = 1f;
            card.root.anchoredPosition = new Vector2(card.active ? ActiveShift : 0f, card.root.anchoredPosition.y);
        }

        if (primaryGauntlet != null) primaryGauntlet.localScale = Vector3.one;
        if (secondaryGauntlet != null) secondaryGauntlet.localScale = Vector3.one;
        if (_titleHolder != null) _titleHolder.anchoredPosition = _titleHolderBase;
    }

    /// <summary>The screen opens: the banner drops in, the gauntlet pops, the rail cards slide in one after the other.</summary>
    private void PlayOpenFx()
    {
        KillOpenFx();
        if (GauntletScreenFx.ReduceMotion || _railPrimary == null) return;

        _openSeq = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);

        RailCard[] cards = { _railPrimary, _railSecondary };
        for (int i = 0; i < cards.Length; i++)
        {
            RailCard card = cards[i];
            float rest = card.active ? ActiveShift : 0f;
            float at = 0.1f + i * 0.08f;

            card.group.alpha = 0f;
            card.root.anchoredPosition = new Vector2(rest + 90f, card.root.anchoredPosition.y);
            _openSeq.Insert(at, card.root.DOAnchorPosX(rest, 0.34f).SetEase(Ease.OutBack, 1.3f));
            _openSeq.Insert(at, card.group.DOFade(1f, 0.2f));
        }

        RectTransform page = ActivePage();
        page.localScale = Vector3.one * 0.86f;
        _openSeq.Insert(0.04f, page.DOScale(1f, 0.38f).SetEase(Ease.OutBack, 1.6f));

        if (_titleHolder != null)
        {
            _titleHolder.anchoredPosition = _titleHolderBase + new Vector2(0f, 60f);
            _openSeq.Insert(0f, _titleHolder.DOAnchorPos(_titleHolderBase, 0.38f).SetEase(Ease.OutBack, 1.2f));
        }
    }

    /// <summary>The gauntlet on screen changed: it squashes in, rings out gold, and the cards trade places. No sliding.</summary>
    private void PlaySwitchFx()
    {
        if (GauntletScreenFx.ReduceMotion) return;

        RectTransform page = ActivePage();
        page.DOKill();
        page.localScale = new Vector3(0.9f, 1.08f, 1f);
        page.DOScale(1f, 0.3f).SetEase(Ease.OutBack, 1.8f).SetUpdate(true).SetLink(page.gameObject);

        if (_titleHolder != null)
        {
            _titleHolder.DOComplete();
            _titleHolder.DOPunchScale(Vector3.one * 0.06f, 0.25f, 6, 0.6f).SetUpdate(true).SetLink(_titleHolder.gameObject);
        }

        GemSwapFx.PulseAt(page, new Color(1f, 0.85f, 0.35f, 1f));
        GemSwapFx.PlaySound();
    }

    /// <summary>A new gauntlet has just taken its place: it lands with a pop and two rings.</summary>
    private void PlayReplacedFx()
    {
        RefreshGauntletSwitcher();
        if (GauntletScreenFx.ReduceMotion) return;

        RectTransform page = ActivePage();
        page.DOKill();
        page.localScale = Vector3.one * 0.6f;
        page.DOScale(1f, 0.45f).SetEase(Ease.OutBack, 2.2f).SetUpdate(true).SetLink(page.gameObject);

        GemSwapFx.PulseAt(page, new Color(1f, 0.85f, 0.35f, 1f));
        DOVirtual.DelayedCall(0.12f, () => { if (page != null) GemSwapFx.PulseAt(page, Color.white); }).SetUpdate(true).SetLink(page.gameObject);
        GemSwapFx.PlaySound();

        RailCard card = _isDisplayingPrimary ? _railPrimary : _railSecondary;
        if (card != null)
        {
            card.root.DOKill(true);
            card.root.DOPunchScale(Vector3.one * 0.1f, 0.4f, 8, 0.7f).SetUpdate(true).SetLink(card.root.gameObject);
        }
    }
    #endregion
}
