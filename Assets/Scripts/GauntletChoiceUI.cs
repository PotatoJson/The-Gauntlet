using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Localization;

public class GauntletChoiceUI : MonoBehaviour
{
    public static GauntletChoiceUI Instance;

    [Header("Localization")]
    public LocalizedString PrimarySlotsString;
    public LocalizedString SecondarySlotsString;
    public LocalizedString TitleString;

    [Header("Chest")]
    public Image ChestImage;
    public TMP_Text TitleText;

    [Header("Option A UI")]
    public Button ButtonA;
    public TMP_Text NameTextA;
    public TMP_Text SlotsTextA;
    public TMP_Text SecondaryTextA;
    public Image HaloA;
    public Image DimA;
    public Transform GauntletAnchorA;

    [Header("Option B UI")]
    public Button ButtonB;
    public TMP_Text NameTextB;
    public TMP_Text SlotsTextB;
    public TMP_Text SecondaryTextB;
    public Image HaloB;
    public Image DimB;
    public Transform GauntletAnchorB;

    [Header("Warning Dialog UI")]
    public GameObject WarningPanel;
    public Button WarningConfirmBtn;
    public Button WarningCancelBtn;
    private bool _isWarningActive = false;

    [Header("Visual Tuning")]
    [Range(0.1f, 1f)] public float GauntletVisualScale = 0.4f;

    private GauntletInteractable _activeInteractable;
    private GameObject _spawnedPrefabA;
    private GameObject _spawnedPrefabB;

    private const string ChestOpenEvent = "event:/Environment/Chest_Open";
    private const float PipStartX = 128f;
    private const float PipSpacing = 28f;
    private const int MaxPrimarySlots = 5;
    private const int MaxSecondarySlots = 3;
    private const float HaloAlpha = 0.13f;
    private const float UnfocusedDim = 0.4f;
    private const float FocusedScale = 1.05f;
    private const float FocusSpeed = 8f;
    // Matches the frame timing of Chest_Open.aseprite.
    private static readonly float[] ChestFrameSeconds = { 0.18f, 0.15f, 0.06f, 0.06f, 0.06f, 0.07f, 0.2f };

    private Sprite[] _chestFrames;
    private Sequence _opening;
    private bool _isOpening;
    private Button _focused;

    private void Awake()
    {
        Instance = this;
        // Awake runs on first open when the panel starts off. Left on in the scene, it hides itself at play.
        if (!_isOpening) gameObject.SetActive(false);
        if (WarningPanel != null) WarningPanel.SetActive(false);
        FocusOnHover(ButtonA);
        FocusOnHover(ButtonB);
    }

    // --- ESCAPE / CANCEL INPUT LOOP ---
    private void Update()
    {
        if (!gameObject.activeInHierarchy || _isOpening) return;

        bool cancelPressed = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
                             (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);

        if (cancelPressed)
        {
            if (_isWarningActive) CancelClose();
            else ShowWarning();
        }

        TrackFocus();
    }

    // The selected button is the focused side, so mouse and gamepad share one highlight.
    private void TrackFocus()
    {
        EventSystem es = EventSystem.current;
        if (es != null)
        {
            GameObject selected = es.currentSelectedGameObject;
            if (selected == ButtonA.gameObject) _focused = ButtonA;
            else if (selected == ButtonB.gameObject) _focused = ButtonB;
            else if (selected == null && !_isWarningActive) es.SetSelectedGameObject(_focused.gameObject);
        }

        ShowFocus(ButtonA, HaloA, DimA, GauntletAnchorA);
        ShowFocus(ButtonB, HaloB, DimB, GauntletAnchorB);
    }

    private void ShowFocus(Button btn, Image halo, Image dim, Transform anchor)
    {
        bool focused = btn == _focused;
        float step = GauntletScreenFx.ReduceMotion ? 1f : Time.unscaledDeltaTime * FocusSpeed;

        SetAlpha(halo, Mathf.MoveTowards(halo.color.a, focused ? HaloAlpha : 0f, step));
        SetAlpha(dim, Mathf.MoveTowards(dim.color.a, focused ? 0f : UnfocusedDim, step));

        anchor.localScale = Vector3.MoveTowards(anchor.localScale, Vector3.one * (focused ? FocusedScale : 1f), step);
    }

    private static void SetAlpha(Image image, float alpha)
    {
        Color color = image.color;
        color.a = alpha;
        image.color = color;
    }

    private void FocusOnHover(Button btn)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        entry.callback.AddListener(_ =>
        {
            if (btn.interactable && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(btn.gameObject);
        });
        btn.gameObject.AddComponent<EventTrigger>().triggers.Add(entry);
    }

    private void SetChoicesInteractable(bool interactable)
    {
        ButtonA.interactable = interactable;
        ButtonB.interactable = interactable;
    }

    public void OpenChoiceMenu(GauntletInteractable interactable)
    {
        _activeInteractable = interactable;
        _isOpening = true;
        StoryDialogue.SayOnce("Story_Chest_01");
        _isWarningActive = false; // Reset warning state
        if (WarningPanel != null) WarningPanel.SetActive(false);

        gameObject.SetActive(true);
        Time.timeScale = 0f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (_spawnedPrefabA != null) Destroy(_spawnedPrefabA);
        if (_spawnedPrefabB != null) Destroy(_spawnedPrefabB);

        _spawnedPrefabA = SetupChoice(ButtonA, NameTextA, SlotsTextA, SecondaryTextA, GauntletAnchorA, interactable.GauntletPrefabA, interactable.RarityA);
        _spawnedPrefabB = SetupChoice(ButtonB, NameTextB, SlotsTextB, SecondaryTextB, GauntletAnchorB, interactable.GauntletPrefabB, interactable.RarityB);

        /*Navigation navA = new Navigation { mode = Navigation.Mode.Explicit };
        navA.selectOnLeft = ButtonB;
        navA.selectOnRight = ButtonB;
        ButtonA.navigation = navA;

        Navigation navB = new Navigation { mode = Navigation.Mode.Explicit };
        navB.selectOnLeft = ButtonA;
        navB.selectOnRight = ButtonA;
        ButtonB.navigation = navB;*/

        PlayOpening();
    }

    // --- CHEST OPENING ---
    private void PlayOpening()
    {
        _opening?.Kill();
        if (_chestFrames == null)
        {
            _chestFrames = new Sprite[ChestFrameSeconds.Length];
            for (int i = 0; i < _chestFrames.Length; i++) _chestFrames[i] = Resources.Load<Sprite>($"GauntletChest/Chest_{i + 1}");
        }

        _focused = ButtonA;
        _isOpening = true;
        SetChoicesInteractable(false);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);

        TitleText.text = TitleString.GetLocalizedString();
        SetAlpha(HaloA, 0f);
        SetAlpha(HaloB, 0f);
        SetAlpha(DimA, 0f);
        SetAlpha(DimB, 0f);

        if (GauntletScreenFx.ReduceMotion)
        {
            ChestImage.sprite = _chestFrames[_chestFrames.Length - 1];
            ChestImage.rectTransform.localScale = Vector3.one;
            TitleText.alpha = 1f;
            ButtonA.GetComponent<CanvasGroup>().alpha = 1f;
            ButtonB.GetComponent<CanvasGroup>().alpha = 1f;
            GauntletAnchorA.localScale = Vector3.one;
            GauntletAnchorB.localScale = Vector3.one;
            UiSfxListener.PlayMenuSound(ChestOpenEvent);
            FinishOpening();
            return;
        }

        ChestImage.sprite = _chestFrames[0];
        ChestImage.rectTransform.localScale = Vector3.one * 0.94f;
        TitleText.alpha = 0f;
        ButtonA.GetComponent<CanvasGroup>().alpha = 0f;
        ButtonB.GetComponent<CanvasGroup>().alpha = 0f;
        GauntletAnchorA.localScale = Vector3.zero;
        GauntletAnchorB.localScale = Vector3.zero;

        _opening = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
        _opening.Insert(0f, ChestImage.rectTransform.DOScale(1f, ChestFrameSeconds[0]).SetEase(Ease.OutBack));
        _opening.InsertCallback(ChestFrameSeconds[0], () => UiSfxListener.PlayMenuSound(ChestOpenEvent));

        float time = 0f;
        for (int i = 0; i < _chestFrames.Length; i++)
        {
            Sprite frame = _chestFrames[i];
            _opening.InsertCallback(time, () => ChestImage.sprite = frame);
            time += ChestFrameSeconds[i];
        }

        InsertReveal(ButtonA, GauntletAnchorA, time);
        InsertReveal(ButtonB, GauntletAnchorB, time + 0.08f);
        _opening.Insert(time, TitleText.DOFade(1f, 0.2f));
        _opening.OnComplete(FinishOpening);
    }

    private void InsertReveal(Button btn, Transform anchor, float time)
    {
        _opening.Insert(time, btn.GetComponent<CanvasGroup>().DOFade(1f, 0.2f));
        _opening.Insert(time, anchor.DOScale(1f, 0.3f).SetEase(Ease.OutBack));
    }

    private void FinishOpening()
    {
        _isOpening = false;
        SetChoicesInteractable(true);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(ButtonA.gameObject);
    }

    // --- WARNING MENU LOGIC ---
    private void ShowWarning()
    {
        Debug.Log("Warning Test");
        _isWarningActive = true;
        SetChoicesInteractable(false);
        if (WarningPanel != null) WarningPanel.SetActive(true);

        if (WarningConfirmBtn != null)
        {
            WarningConfirmBtn.onClick.RemoveAllListeners();
            WarningConfirmBtn.onClick.AddListener(ConfirmClose);
        }

        if (WarningCancelBtn != null)
        {
            WarningCancelBtn.onClick.RemoveAllListeners();
            WarningCancelBtn.onClick.AddListener(CancelClose);
        }

        // Snap controller focus to cancel button for safety
        if (Gamepad.current != null && EventSystem.current != null && WarningCancelBtn != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(WarningCancelBtn.gameObject);
        }
    }

    private void CancelClose()
    {
        _isWarningActive = false;
        SetChoicesInteractable(true);
        if (WarningPanel != null) WarningPanel.SetActive(false);

        // Snap focus back to the side the player was on
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(_focused.gameObject);
        }
    }

    private void ConfirmClose()
    {
        _isWarningActive = false;
        if (WarningPanel != null) WarningPanel.SetActive(false);

        gameObject.SetActive(false);
        Time.timeScale = 1f;

        // Hide cursor and unlock game since we backed out
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    private GameObject SetupChoice(Button btn, TMP_Text nameTxt, TMP_Text slotsTxt, TMP_Text secondaryTxt, Transform anchor, GameObject prefab, GauntletRarity rarity)
    {
        if (prefab == null) return null;

        GauntletManager gmInfo = prefab.GetComponent<GauntletManager>();
        string localizedName = (gmInfo != null && gmInfo.LinkedGauntletData != null) ? gmInfo.LinkedGauntletData.Name : prefab.name;

        nameTxt.text = $"{localizedName}\n<size=70%>{rarity}</size>";

        int rarityBonus = (int)rarity;
        int pSlots = 3 + rarityBonus;
        int sSlots = 1 + rarityBonus;

        slotsTxt.text = PrimarySlotsString.GetLocalizedString();
        secondaryTxt.text = SecondarySlotsString.GetLocalizedString();
        BuildPips(slotsTxt.transform, pSlots, MaxPrimarySlots, new Color32(0xF5, 0xD7, 0x78, 0xFF));
        BuildPips(secondaryTxt.transform, sSlots, MaxSecondarySlots, new Color32(0xC9, 0xA3, 0x6B, 0xFF));

        GameObject visualGauntlet = Instantiate(prefab, anchor);

        GauntletManager gmInstance = visualGauntlet.GetComponent<GauntletManager>();
        if (gmInstance != null) gmInstance.InitializeGauntlet(true, rarity);
        ShowGauntletOnly(visualGauntlet);

        // After InitializeGauntlet, which resets the transform.
        RectTransform rt = visualGauntlet.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            visualGauntlet.transform.localScale = new Vector3(GauntletVisualScale, GauntletVisualScale, 1f);
        }

        CanvasGroup cg = visualGauntlet.GetComponent<CanvasGroup>();
        if (cg == null) cg = visualGauntlet.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;

        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => OnGauntletChosen(prefab, rarity));
        return visualGauntlet;
    }

    // It came out of a chest, so no arm and no skin showing: the bare fingers swap for gloved ones of the same name.
    private void ShowGauntletOnly(GameObject gauntlet)
    {
        GauntletHandRig rig = gauntlet.GetComponent<GauntletHandRig>();
        if (rig != null) rig.enabled = false;

        Transform arm = gauntlet.transform.Find("Gauntlet_Image");
        if (arm != null) arm.gameObject.SetActive(false);

        foreach (Image image in gauntlet.GetComponentsInChildren<Image>(true))
        {
            if (image.sprite == null) continue;
            Sprite gloved = Resources.Load<Sprite>($"GauntletEmpty/{image.sprite.name}");
            if (gloved != null) image.sprite = gloved;
        }
    }

    private void BuildPips(Transform label, int filled, int total, Color fill)
    {
        for (int i = label.childCount - 1; i >= 0; i--)
        {
            if (label.GetChild(i).name == "Pip") Destroy(label.GetChild(i).gameObject);
        }

        for (int i = 0; i < total; i++)
        {
            Image back = MakePipImage(label, 22f, new Color32(0x2B, 0x18, 0x10, 0xFF));
            RectTransform backRect = back.rectTransform;
            backRect.anchorMin = backRect.anchorMax = new Vector2(0f, 0.5f);
            backRect.anchoredPosition = new Vector2(PipStartX + i * PipSpacing, 0f);
            MakePipImage(back.transform, 14f, i < filled ? fill : new Color32(0x4A, 0x2E, 0x22, 0xFF));
        }
    }

    private static Image MakePipImage(Transform parent, float size, Color color)
    {
        GameObject go = new GameObject("Pip", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.rectTransform.sizeDelta = new Vector2(size, size);
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private void OnGauntletChosen(GameObject chosenPrefab, GauntletRarity chosenRarity)
    {
        gameObject.SetActive(false);
        // Time.timeScale = 1f; // REMOVED: Keep paused because InventoryManager Swap menu opens next!

        // Note: Cursor remains visible because InventoryManager Swap menu opens next!
        if (InventoryManager.Instance != null && chosenPrefab != null)
        {
            InventoryManager.Instance.TryEquipNewGauntlet(chosenPrefab, chosenRarity);
            if (_activeInteractable != null) _activeInteractable.CompleteInteraction();
        }
    }

    private Color GetRarityColor(GauntletRarity rarity)
    {
        switch (rarity)
        {
            case GauntletRarity.Rare:
                return new Color(0.2f, 0.6f, 1f); // Sky Blue
            case GauntletRarity.UltraRare:
                return new Color(1f, 0.2f, 1f); // Magenta/Purple
            default:
                return Color.white;
        }
    }
}
