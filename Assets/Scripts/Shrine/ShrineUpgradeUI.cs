using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Localization;
using UnityEngine.UI;

/// <summary>
/// The shrine's upgrade screen: lists every gem the player has equipped, lets them pick one and raises it a
/// tier. Built from code the first time a shrine is used, copying the look and fonts of the gem description
/// panel, so there is no prefab to keep in sync. Using it spends the shrine.
/// </summary>
public class ShrineUpgradeUI : MonoBehaviour
{
    private static ShrineUpgradeUI _instance;

    public static bool IsOpen => _instance != null && _instance._open;

    public static void Open(ShrineInteractable shrine)
    {
        if (_instance == null) _instance = Build();
        if (_instance != null) _instance.Show(shrine);
    }

    private static LocalizedString L(string key) => new LocalizedString("Gem", key);

    private class Card
    {
        public DraggableGem Gem;
        public Button Button;
        public Image Background;
    }

    private static readonly Color CardNormal = new Color(0.93f, 0.88f, 0.76f, 1f);
    private static readonly Color CardSelected = new Color(1f, 0.85f, 0.4f, 1f);

    private ShrineInteractable _shrine;
    private readonly List<Card> _cards = new List<Card>();
    private Card _selected;
    private bool _open;
    private bool _finishing;

    private RectTransform _cardRow;
    private TMP_Text _hint;
    private Button _upgradeButton;
    private TMP_Text _upgradeLabel;
    private TMP_Text _closeLabel;
    private TMP_Text _title;
    private TMP_Text _cardTemplate;

    // ------------------------------------------------------------------ building

    private static ShrineUpgradeUI Build()
    {
        InventoryManager inv = InventoryManager.Instance;
        if (inv == null || inv.DetailNameText == null)
        {
            Debug.LogWarning("[Shrine] The inventory isn't available, so the shrine screen can't be built.");
            return null;
        }

        Canvas canvas = inv.GetComponentInParent<Canvas>(true);
        if (canvas == null) return null;
        canvas = canvas.rootCanvas;

        var rootGo = new GameObject("ShrineUpgradeUI", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var root = (RectTransform)rootGo.transform;
        root.SetParent(canvas.transform, false);
        Stretch(root);
        root.SetAsLastSibling();
        rootGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f); // dims the game and blocks clicks

        var ui = rootGo.AddComponent<ShrineUpgradeUI>();
        ui._cardTemplate = inv.DetailNameText;

        // Panel: same paper frame as the gem description box.
        RectTransform panel = NewRect("Panel", root, new Vector2(1180f, 680f));
        Image panelImage = panel.gameObject.AddComponent<Image>();
        Image src = inv.DetailPanelImage;
        if (src != null)
        {
            panelImage.sprite = src.sprite;
            panelImage.type = src.type;
            panelImage.pixelsPerUnitMultiplier = src.pixelsPerUnitMultiplier;
            panelImage.color = src.color;
        }
        else
        {
            panelImage.color = new Color(0.97f, 0.94f, 0.86f, 1f);
        }

        ui._title = ui.NewText("Title", panel, new Vector2(0f, 270f), new Vector2(1000f, 70f), 46, TextAlignmentOptions.Center);
        ui._hint = ui.NewText("Hint", panel, new Vector2(0f, 205f), new Vector2(1040f, 60f), 26, TextAlignmentOptions.Center);

        ui._cardRow = NewRect("Cards", panel, new Vector2(1100f, 330f));
        ui._cardRow.anchoredPosition = new Vector2(0f, 10f);

        ui._upgradeButton = ui.NewButton("Upgrade", panel, new Vector2(-170f, -265f), new Vector2(300f, 80f), out ui._upgradeLabel);
        Button close = ui.NewButton("Close", panel, new Vector2(170f, -265f), new Vector2(300f, 80f), out ui._closeLabel);
        ui._upgradeButton.onClick.AddListener(ui.ConfirmUpgrade);
        close.onClick.AddListener(ui.Close);

        rootGo.SetActive(false);
        return ui;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static RectTransform NewRect(string name, Transform parent, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        return rt;
    }

    // Text objects are clones of the gem title, so they keep its font and its per-language font swapping.
    private TMP_Text NewText(string name, Transform parent, Vector2 pos, Vector2 size, float fontSize, TextAlignmentOptions align)
    {
        GameObject go = Instantiate(_cardTemplate.gameObject, parent);
        go.name = name;
        go.SetActive(true);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        TMP_Text t = go.GetComponent<TMP_Text>();
        t.fontSize = fontSize;
        t.enableAutoSizing = false;
        t.enableWordWrapping = true;
        t.overflowMode = TextOverflowModes.Ellipsis;
        t.alignment = align;
        t.color = Color.black;
        t.raycastTarget = false;
        t.text = "";
        return t;
    }

    private Button NewButton(string name, Transform parent, Vector2 pos, Vector2 size, out TMP_Text label)
    {
        RectTransform rt = NewRect(name, parent, size);
        rt.anchoredPosition = pos;

        Image img = rt.gameObject.AddComponent<Image>();
        img.color = CardNormal;

        Button btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.93f, 0.7f);
        colors.selectedColor = new Color(1f, 0.85f, 0.4f);
        colors.pressedColor = new Color(0.9f, 0.75f, 0.3f);
        colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.7f);
        btn.colors = colors;

        label = NewText("Label", rt, Vector2.zero, size, 34, TextAlignmentOptions.Center);
        return btn;
    }

    // ------------------------------------------------------------------ showing

    private void Show(ShrineInteractable shrine)
    {
        _shrine = shrine;
        _finishing = false;

        _title.text = L("Shrine_Title").GetLocalizedString();
        _upgradeLabel.text = L("Shrine_Upgrade").GetLocalizedString();
        _closeLabel.text = L("Shrine_Close").GetLocalizedString();

        BuildCards();

        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        _open = true;

        Time.timeScale = 0f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        if (GameplayInputGate.Instance != null) GameplayInputGate.Instance.Suspend(this);

        if (Gamepad.current != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            GameObject first = _cards.Count > 0 ? _cards[0].Button.gameObject : _upgradeButton.gameObject;
            EventSystem.current.SetSelectedGameObject(first);
        }
    }

    private void BuildCards()
    {
        foreach (Card c in _cards) if (c.Button != null) Destroy(c.Button.gameObject);
        _cards.Clear();
        _selected = null;

        InventoryManager inv = InventoryManager.Instance;
        var gems = new List<DraggableGem>();
        CollectGems(inv.primaryGauntlet, gems);
        CollectGems(inv.secondaryGauntlet, gems);
        gems.RemoveAll(g => g == null || g.Tier >= DraggableGem.MaxTier);

        const float cardW = 170f, cardH = 300f, gap = 18f;
        float total = gems.Count * cardW + Mathf.Max(0, gems.Count - 1) * gap;
        float x = -total / 2f + cardW / 2f;

        foreach (DraggableGem gem in gems)
        {
            RectTransform rt = NewRect("Card", _cardRow, new Vector2(cardW, cardH));
            rt.anchoredPosition = new Vector2(x, 0f);
            x += cardW + gap;

            Image bg = rt.gameObject.AddComponent<Image>();
            bg.color = CardNormal;
            Button btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.95f, 0.8f);
            colors.selectedColor = new Color(1f, 0.9f, 0.55f);
            colors.pressedColor = new Color(0.9f, 0.8f, 0.5f);
            btn.colors = colors;

            RectTransform icon = NewRect("Icon", rt, new Vector2(90f, 110f));
            icon.anchoredPosition = new Vector2(0f, 90f);
            Image iconImg = icon.gameObject.AddComponent<Image>();
            iconImg.sprite = gem.gemIcon;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            TMP_Text name = NewText("Name", rt, new Vector2(0f, -10f), new Vector2(cardW - 14f, 64f), 22, TextAlignmentOptions.Center);
            name.text = gem.gemName.GetLocalizedString();

            int tier = gem.Tier;
            TMP_Text tiers = NewText("Tiers", rt, new Vector2(0f, -88f), new Vector2(cardW - 14f, 100f), 18, TextAlignmentOptions.Top);
            tiers.text = $"<color=#{ColorUtility.ToHtmlStringRGB(GemTierInfo.TierColor(tier))}>{GemTierInfo.Label(tier)}</color>\n" +
                         $"> <color=#{ColorUtility.ToHtmlStringRGB(GemTierInfo.TierColor(tier + 1))}>{GemTierInfo.Label(tier + 1)}</color>";

            var card = new Card { Gem = gem, Button = btn, Background = bg };
            btn.onClick.AddListener(() => Select(card));
            _cards.Add(card);
        }

        bool any = _cards.Count > 0;
        _hint.text = L(any ? "Shrine_Hint" : "Shrine_NoGems").GetLocalizedString();
        _upgradeButton.interactable = false;
    }

    private static void CollectGems(Transform gauntletContainer, List<DraggableGem> into)
    {
        if (gauntletContainer == null) return;
        GauntletManager gm = gauntletContainer.GetComponentInChildren<GauntletManager>(true);
        if (gm == null) return;

        for (int i = 0; i < gm.fingerSlots.Count && i < gm.currentActiveSlots; i++)
        {
            if (gm.fingerSlots[i] == null) continue;
            DraggableGem g = gm.fingerSlots[i].GetComponentInChildren<DraggableGem>(true);
            if (g != null) into.Add(g);
        }

        if (gm.SkillSlot != null)
        {
            SkillSlotManager skill = gm.SkillSlot.GetComponent<SkillSlotManager>();
            DraggableGem g = skill != null ? skill.GetEquippedSkillGem() : gm.SkillSlot.GetComponentInChildren<DraggableGem>(true);
            if (g != null) into.Add(g);
        }
    }

    private void Select(Card card)
    {
        _selected = card;
        foreach (Card c in _cards) c.Background.color = c == card ? CardSelected : CardNormal;
        _upgradeButton.interactable = true;

        if (Gamepad.current != null && InputHelper.IsGamepadLastUsed() && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(_upgradeButton.gameObject);
        }
    }

    // ------------------------------------------------------------------ acting

    private void ConfirmUpgrade()
    {
        if (_selected == null || _selected.Gem == null || _finishing) return;

        DraggableGem gem = _selected.Gem;
        gem.Tier++;
        gem.PlayMergeFeedback();

        InventoryManager inv = InventoryManager.Instance;
        PlayerStatsManager stats = FindFirstObjectByType<PlayerStatsManager>();
        if (stats != null && inv != null) stats.SyncWithUI(inv.primaryGauntlet, inv.secondaryGauntlet);

        // Write it to the backpack now, so the new tier is still there after the next level loads.
        if (PersistentEquipment.Instance != null && inv != null) PersistentEquipment.Instance.SaveEquipment(inv);

        if (_shrine != null) _shrine.CompleteUse();

        StartCoroutine(FinishRoutine());
    }

    private IEnumerator FinishRoutine()
    {
        _finishing = true;
        _upgradeButton.interactable = false;
        _hint.text = L("Shrine_Done").GetLocalizedString();
        yield return new WaitForSecondsRealtime(0.9f);
        Close();
    }

    private void Close()
    {
        if (!_open) return;
        _open = false;
        gameObject.SetActive(false);

        Time.timeScale = 1f;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        if (GameplayInputGate.Instance != null) GameplayInputGate.Instance.RestoreWhenReleased(this);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    private void Update()
    {
        if (!_open || _finishing) return;

        bool cancel = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
                      (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);
        if (cancel) Close();
    }

    private void OnDisable()
    {
        // Safety: never leave the game frozen if this is switched off some other way.
        if (_open)
        {
            _open = false;
            Time.timeScale = 1f;
            if (GameplayInputGate.Instance != null) GameplayInputGate.Instance.RestoreWhenReleased(this);
        }
    }
}
