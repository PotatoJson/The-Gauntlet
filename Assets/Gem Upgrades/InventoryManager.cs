using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using DG.Tweening; // --- REQUIRED FOR SLIDES AND DROPS ---

public class InventoryManager : MonoBehaviour
{
    private static InventoryManager _instance;
    public static InventoryManager Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindFirstObjectByType<InventoryManager>(FindObjectsInactive.Include);
            return _instance;
        }
    }

    [Header("Input Action")]
    [SerializeField] private InputActionReference inventoryToggleAction;

    [Header("Central Layout Configurations")]
    public Transform primaryGauntlet;    // Drag "Gauntlet Primary" container here
    public Transform secondaryGauntlet;  // Drag "Gauntlet Secondary" container here
    [SerializeField] private RectTransform descriptionBoxAnchor; // Drag "small description" here
    [SerializeField] private TMP_Text gauntletTitleText;

    [Header("Warning Dialog UI")]
    [SerializeField] private GameObject warningPanel;
    [SerializeField] private Button warningConfirmBtn;
    [SerializeField] private Button warningCancelBtn;
    [SerializeField] private TMP_Text warningBodyText;

    [Header("Details Panel Content bindings")]
    [SerializeField] private TMP_Text detailNameText;
    [SerializeField] private TMP_Text detailDescriptionText;
    [SerializeField] private Image detailIcon;

    [Header("Starter Equipment Asset Maps")]
    public GameObject DefaultPrimaryPrefab;
    public GameObject DefaultSecondaryPrefab;

    [Header("Tween Settings")]
    [SerializeField] private float slideDuration = 0.5f;
    [SerializeField] private Ease slideEase = Ease.OutCubic;
    [SerializeField] private float offscreenXOffset = 900f; 

    [HideInInspector] public GameObject activePrimaryPrefab;
    [HideInInspector] public GauntletRarity activePrimaryRarity;
    [HideInInspector] public GameObject activeSecondaryPrefab;
    [HideInInspector] public GauntletRarity activeSecondaryRarity;

    private GameObject _lastSelectedSlot;
    private bool _isWarningActive = false;
    private bool _isDisplayingPrimary = true;
    private bool _isTransitioning = false;

    private void Awake()
    {
        if (_instance == null) _instance = this;
        if (warningPanel != null) warningPanel.SetActive(false);
    }

    private void Start()
    {
        // 1. Recover equipment setups from persistent memory data layers if active
        if (PersistentEquipment.Instance != null && PersistentEquipment.Instance.hasSavedData)
        {
            if (PersistentEquipment.Instance.primaryGauntletPrefab != null)
                EquipBaseGauntletObject(PersistentEquipment.Instance.primaryGauntletPrefab, PersistentEquipment.Instance.primaryRarity, true);

            if (PersistentEquipment.Instance.secondaryGauntletPrefab != null)
                EquipBaseGauntletObject(PersistentEquipment.Instance.secondaryGauntletPrefab, PersistentEquipment.Instance.secondaryRarity, false);

            LoadSavedGems(primaryGauntlet, PersistentEquipment.Instance.primaryGems);
            LoadSavedGems(secondaryGauntlet, PersistentEquipment.Instance.secondaryGems);

            PlayerStatsManager stats = FindFirstObjectByType<PlayerStatsManager>();
            if (stats != null) stats.SyncWithUI(primaryGauntlet, secondaryGauntlet);
        }
        else
        {
            // 2. Fallback execution pipeline delivering common default gauntlet equipment nodes
            if (primaryGauntlet.GetComponentInChildren<GauntletManager>() == null && DefaultPrimaryPrefab != null)
                EquipBaseGauntletObject(DefaultPrimaryPrefab, GauntletRarity.Common, true);

            if (secondaryGauntlet.GetComponentInChildren<GauntletManager>() == null && DefaultSecondaryPrefab != null)
                EquipBaseGauntletObject(DefaultSecondaryPrefab, GauntletRarity.Common, false);
        }

        // Initialize spatial transforms before presentation layer calculations activate
        ResetGauntletPositionsInstant();
    }

    private void EquipBaseGauntletObject(GameObject prefab, GauntletRarity rarity, bool isPrimary)
    {
        Transform targetContainer = isPrimary ? primaryGauntlet : secondaryGauntlet;
        GameObject newGauntlet = Instantiate(prefab, targetContainer);
        newGauntlet.GetComponent<GauntletManager>().InitializeGauntlet(isPrimary, rarity);

        if (isPrimary)
        {
            activePrimaryPrefab = prefab;
            activePrimaryRarity = rarity;
        }
        else
        {
            activeSecondaryPrefab = prefab;
            activeSecondaryRarity = rarity;
        }

        SetupHoverEvents(targetContainer);
    }

    private void LoadSavedGems(Transform gauntletParent, List<GameObject> savedGemPrefabs)
    {
        GauntletManager gm = gauntletParent.GetComponentInChildren<GauntletManager>();
        if (gm != null)
        {
            for (int i = 0; i < savedGemPrefabs.Count; i++)
            {
                if (i < gm.currentActiveSlots && savedGemPrefabs[i] != null)
                {
                    GameObject newlySpawnedGem = Instantiate(savedGemPrefabs[i], gm.fingerSlots[i].transform);
                    RectTransform gemRect = newlySpawnedGem.GetComponent<RectTransform>();

                    gemRect.localPosition = Vector3.zero;
                    gemRect.localScale = Vector3.one;
                    gemRect.sizeDelta = gm.fingerSlots[i].GetComponent<RectTransform>().rect.size;

                    newlySpawnedGem.GetComponent<DraggableGem>().originalPrefab = savedGemPrefabs[i];

                    // Match structural layout images matching backend initialization colors
                    Image slotImage = gm.fingerSlots[i].GetComponent<Image>();
                    Image gemImage = newlySpawnedGem.GetComponent<Image>();
                    if (slotImage != null && gemImage != null) slotImage.color = gemImage.color;
                }
            }
        }
    }

    private void ResetGauntletPositionsInstant()
    {
        RectTransform primaryRect = primaryGauntlet.GetComponent<RectTransform>();
        RectTransform secondaryRect = secondaryGauntlet.GetComponent<RectTransform>();

        primaryRect.DOKill();
        secondaryRect.DOKill();

        primaryRect.anchoredPosition = Vector2.zero;
        primaryGauntlet.gameObject.SetActive(true);

        secondaryRect.anchoredPosition = new Vector2(-offscreenXOffset, 0f);
        secondaryGauntlet.gameObject.SetActive(false);

        _isDisplayingPrimary = true;
        _isTransitioning = false;

        if (gauntletTitleText != null) gauntletTitleText.text = "Gauntlet Primary";
    }

    // --- NEW JUICY DOTWEEN GAUNTLET TRANSITION SCROLL ---
    public void ToggleEquippedGauntletDisplay()
    {
        if (_isTransitioning) return;
        _isTransitioning = true;

        if (gauntletTitleText != null)
        {
            gauntletTitleText.text = _isDisplayingPrimary ? "Gauntlet Secondary" : "Gauntlet Primary";
        }

        RectTransform primaryRect = primaryGauntlet.GetComponent<RectTransform>();
        RectTransform secondaryRect = secondaryGauntlet.GetComponent<RectTransform>();

        primaryRect.DOKill();
        secondaryRect.DOKill();

        if (_isDisplayingPrimary)
        {
            // Transition from Primary -> Secondary
            // Move Primary out to the right side of the screen frame limits
            primaryRect.DOAnchorPos(new Vector2(offscreenXOffset, 0f), slideDuration)
                .SetEase(slideEase)
                .SetUpdate(true)
                .OnComplete(() => primaryGauntlet.gameObject.SetActive(false));

            // Ensure Secondary wakes up tracking into position smoothly coming from the left hand side
            secondaryGauntlet.gameObject.SetActive(true);
            secondaryRect.anchoredPosition = new Vector2(-offscreenXOffset, 0f);
            secondaryRect.DOAnchorPos(Vector2.zero, slideDuration)
                .SetEase(slideEase)
                .SetUpdate(true)
                .OnComplete(() => {
                    _isDisplayingPrimary = false;
                    _isTransitioning = false;
                    FocusActiveGauntletFirstSlot();
                });
        }
        else
        {
            // Transition from Secondary -> Primary
            // Slide secondary out toward the right side boundary frames
            secondaryRect.DOAnchorPos(new Vector2(offscreenXOffset, 0f), slideDuration)
                .SetEase(slideEase)
                .SetUpdate(true)
                .OnComplete(() => secondaryGauntlet.gameObject.SetActive(false));

            // Snap primary from left entry markers moving towards dead center
            primaryGauntlet.gameObject.SetActive(true);
            primaryRect.anchoredPosition = new Vector2(-offscreenXOffset, 0f);
            primaryRect.DOAnchorPos(Vector2.zero, slideDuration)
                .SetEase(slideEase)
                .SetUpdate(true)
                .OnComplete(() => {
                    _isDisplayingPrimary = true;
                    _isTransitioning = false;
                    FocusActiveGauntletFirstSlot();
                });
        }
    }

    // --- NEW JUICY DOTWEEN REWARD SCATTER DROPS ---
    public void AnimateGemArrivalDrops(List<GameObject> spawnedGems)
    {
        foreach (GameObject gem in spawnedGems)
        {
            RectTransform rect = gem.GetComponent<RectTransform>();
            Vector2 targetPos = rect.anchoredPosition;

            // Start far above the viewing canvas mimicking a physical board dropping mechanism
            rect.anchoredPosition = new Vector2(targetPos.x, targetPos.y + 600f);
            rect.localScale = Vector3.zero;

            // Chain arrival drop scales alongside slight bounce impact mechanics
            Sequence dropSeq = DOTween.Sequence();
            dropSeq.Join(rect.DOAnchorPos(targetPos, 0.45f).SetEase(Ease.OutBounce));
            dropSeq.Join(rect.DOScale(Vector3.one, 0.35f).SetEase(Ease.OutBack));
            dropSeq.SetUpdate(true); // Ignore Frozen TimeScale constraints safely
        }
    }

    public void SetupHoverEvents(Transform gauntletParent)
    {
        if (gauntletParent == null) return;

        foreach (Transform child in gauntletParent.GetComponentsInChildren<Transform>(true))
        {
            if (child.name.Contains("Slot") && child.GetComponent<EventTrigger>() == null)
            {
                EventTrigger trigger = child.gameObject.AddComponent<EventTrigger>();
                EventTrigger.Entry entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                entry.callback.AddListener((data) => {
                    if (!_isWarningActive)
                    {
                        _lastSelectedSlot = child.gameObject;
                        UpdateDetailsPanel(child.gameObject);
                    }
                });
                trigger.triggers.Add(entry);

                Button slotBtn = child.GetComponent<Button>();
                if (slotBtn != null)
                {
                    slotBtn.onClick.RemoveAllListeners();
                    slotBtn.onClick.AddListener(() =>
                    {
                        bool isUsingMouseOrKeyboard = Gamepad.current == null || (Mouse.current != null && Mouse.current.wasUpdatedThisFrame);
                        if (isUsingMouseOrKeyboard) return;

                        DraggableGem equippedGem = child.GetComponentInChildren<DraggableGem>();
                        if (equippedGem != null && GemPopupMenu.Instance != null && !GemPopupMenu.Instance.IsPlacingMode)
                        {
                            GemPopupMenu.Instance.OpenMenu(equippedGem, equippedGem.GetComponent<RectTransform>());
                        }
                    });
                }
            }
        }
    }

    private void OnEnable()
    {
        ResetGauntletPositionsInstant();
        if (Gamepad.current != null && EventSystem.current != null)
        {
            FocusActiveGauntletFirstSlot();
        }
        if (inventoryToggleAction != null) inventoryToggleAction.action.Enable();
    }

    private void OnDisable()
    {
        if (inventoryToggleAction != null) inventoryToggleAction.action.Disable();
    }

    private void Update()
    {
        if (GemPopupMenu.Instance != null && GemPopupMenu.Instance.gameObject.activeInHierarchy) return;

        bool togglePressed = (inventoryToggleAction != null && inventoryToggleAction.action.WasPressedThisFrame());

        bool cancelPressed = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
                             (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame) ||
                             togglePressed;

        if (cancelPressed)
        {
            if (_isWarningActive) CancelClose();
            else TryCloseCharacterScreen();
        }

        if (!_isWarningActive && EventSystem.current != null && EventSystem.current.currentSelectedGameObject != _lastSelectedSlot)
        {
            GameObject currentSel = EventSystem.current.currentSelectedGameObject;
            if (currentSel != null && currentSel.name.Contains("Slot"))
            {
                _lastSelectedSlot = currentSel;
                UpdateDetailsPanel(_lastSelectedSlot);
            }
        }
    }

    private void UpdateDetailsPanel(GameObject slot)
    {
        if (slot == null) return;
        DraggableGem equippedGem = slot.GetComponentInChildren<DraggableGem>();

        if (equippedGem != null)
        {
            detailNameText.text = equippedGem.gemName;
            detailDescriptionText.text = equippedGem.gemDescription;
            if (detailIcon != null) { detailIcon.sprite = equippedGem.gemIcon; detailIcon.color = Color.white; }
        }
        else
        {
            detailNameText.text = "Empty Slot";
            detailDescriptionText.text = "No gem equipped here.";
            if (detailIcon != null) detailIcon.color = Color.clear;
        }
    }

    public void TryCloseCharacterScreen()
    {
        _isWarningActive = true;
        warningPanel.SetActive(true);

        bool hasUnequippedGems = false;

        DraggableGem[] allActiveGems = GetComponentsInChildren<DraggableGem>(false);

        foreach (DraggableGem gem in allActiveGems)
        {
            bool isRewardGem = (RewardMenuManager.Instance != null && !RewardMenuManager.Instance.CanDragGem(gem));

            if (!gem.IsEquipped() && !isRewardGem)
            {
                hasUnequippedGems = true;
                break;
            }
        }

        if (warningBodyText != null)
        {
            warningBodyText.text = hasUnequippedGems
                ? "You have unequipped gems left on the board!\nAre you sure you want to leave?"
                : "Are you sure you want to leave the inventory?";
        }

        warningConfirmBtn.onClick.RemoveAllListeners();
        warningConfirmBtn.onClick.AddListener(ConfirmClose);

        warningCancelBtn.onClick.RemoveAllListeners();
        warningCancelBtn.onClick.AddListener(CancelClose);

        if (Gamepad.current != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(warningCancelBtn.gameObject);
        }
    }

    public void FocusActiveGauntletFirstSlot()
    {
        Transform activeContainer = _isDisplayingPrimary ? primaryGauntlet : secondaryGauntlet;
        GauntletManager gauntlet = activeContainer.GetComponentInChildren<GauntletManager>();

        if (gauntlet != null && gauntlet.fingerSlots != null)
        {
            foreach (GameObject slot in gauntlet.fingerSlots)
            {
                if (slot.activeInHierarchy)
                {
                    if (EventSystem.current != null)
                    {
                        EventSystem.current.SetSelectedGameObject(null);
                        EventSystem.current.SetSelectedGameObject(slot);
                    }
                    _lastSelectedSlot = slot;
                    UpdateDetailsPanel(slot);
                    return;
                }
            }
        }
    }

    public void CancelClose()
    {
        _isWarningActive = false;
        warningPanel.SetActive(false);

        if (Gamepad.current != null && _lastSelectedSlot != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(_lastSelectedSlot);
        }
    }

    public void ConfirmClose()
    {
        _isWarningActive = false;
        warningPanel.SetActive(false);

        PlayerStatsManager statsManager = FindFirstObjectByType<PlayerStatsManager>();
        if (statsManager != null)
        {
            statsManager.SyncWithUI(primaryGauntlet, secondaryGauntlet);
        }

        GauntletMenu gauntletMenu = FindFirstObjectByType<GauntletMenu>();
        if (gauntletMenu != null && gauntletMenu.gameObject.activeInHierarchy)
        {
            gauntletMenu.CloseUpgradeMenu();
            gauntletMenu.ResumeGame();
        }
        else
        {
            gameObject.SetActive(false);
            Time.timeScale = 1f;
        }
    }

    public void UpdateDetailsPanelFromGem(DraggableGem equippedGem)
    {
        if (equippedGem != null)
        {
            detailNameText.text = equippedGem.gemName;
            detailDescriptionText.text = equippedGem.gemDescription;
            if (detailIcon != null)
            {
                detailIcon.sprite = equippedGem.gemIcon;
                detailIcon.color = Color.white;
            }
        }
        else
        {
            detailNameText.text = "Empty Slot";
            detailDescriptionText.text = "No gem equipped here.";
            if (detailIcon != null) detailIcon.color = Color.clear;
        }
    }
}