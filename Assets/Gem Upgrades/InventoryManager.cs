using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine.Localization;

public class InventoryManager : MonoBehaviour
{
    [Header("Localized Strings")]
    public LocalizedString gauntletPrimaryString;
    public LocalizedString gauntletSecondaryString;
    public LocalizedString warningUnequippedGemsString;
    public LocalizedString warningLeaveInventoryString;
    public LocalizedString warningDiscardGauntletString;
    
    [Header("Starting Equipment")]
    [SerializeField] private GameObject defaultGauntletPrefab;
    [HideInInspector] public GameObject activePrimaryPrefab;
    [HideInInspector] public GameObject activeSecondaryPrefab;

    private static InventoryManager _instance;
    public static InventoryManager Instance
    {
        get
        {
            if (_instance == null)
            {
                // This forces Unity to find the script even if the GameObject is disabled!
                _instance = FindFirstObjectByType<InventoryManager>(FindObjectsInactive.Include);
            }
            return _instance;
        }
    }

    [Header("Header Navigation")]
    [SerializeField] private Button headerTitleButton;
    [SerializeField] private Button returnButton;
    [SerializeField] private Button switchGauntletButton;

    [Header("Central Layout Configurations")]
public Transform primaryGauntlet;
    public Transform secondaryGauntlet;
    [SerializeField] private TMP_Text gauntletTitleText;

    [Header("Warning Dialog UI")]
    [SerializeField] private GameObject warningPanel;
    [SerializeField] private Button warningCancelBtn;
    [SerializeField] private Button warningConfirmBtn;
    [SerializeField] private TMP_Text warningBodyText;

    [Header("Details Panel Content bindings")]
    [SerializeField] private GameObject descriptionBoxAnchor;
    [SerializeField] private CanvasGroup detailCanvasGroup;
    [SerializeField] private TMP_Text detailNameText;
[SerializeField] private TMP_Text detailDescriptionText;
    [SerializeField] private Image detailIcon;

    [Header("Tween Settings")]
    [SerializeField] private float slideDuration = 0.5f;
    [SerializeField] private Ease slideEase = Ease.OutCubic;
    [SerializeField] private float offscreenXOffset = 900f;

    [Header("Replace Gauntlet UI")]
    [SerializeField] private GameObject replaceGauntletPanel;
    [SerializeField] private Button replaceGauntletBtn;
    [SerializeField] private Button replaceDiscardBtn;

    [Header("Localization Keys")]
    [SerializeField] private LocalizedString primaryKey;
    [SerializeField] private LocalizedString secondaryKey;

    private bool _isInitialized = false;
    private GameObject _pendingGauntletPrefab;
    private GauntletRarity _pendingGauntletRarity;
    private bool _isReplaceUIPending = false;

    public bool IsReplaceUIPending => _isReplaceUIPending;

    /// <summary>
    /// True while the primary/secondary pages are sliding. The page tween grabs controller focus
    /// for the header when it lands, so anything that wants focus afterwards has to wait this out.
    /// </summary>
    public bool IsTransitioning => _isTransitioning;

    /// <summary>True when the primary gauntlet is the page currently on screen.</summary>
    public bool IsDisplayingPrimary => _isDisplayingPrimary;

    private DraggableGem _lastSelectedGem;
    private bool _isWarningActive = false;
    private bool _isDisplayingPrimary = true;
    private bool _isTransitioning = false;
    private DraggableGem _hoveredGem;
    private DraggableGem _focusedGem;
    private bool _gamepadHadControl;

    private void Awake()
    {
        if (warningPanel != null) warningPanel.SetActive(false);
        if (replaceGauntletPanel != null) replaceGauntletPanel.SetActive(false);
        if (detailCanvasGroup != null) detailCanvasGroup.alpha = 0f;

        if (headerTitleButton != null)
        {
            headerTitleButton.onClick.AddListener(OnHeaderTitleClicked);
        }
    }

    private void OnHeaderTitleClicked()
    {
        if (Gamepad.current == null) return;
        FocusFirstGemOrSlot();
    }

    private void Start()
    {
        // --- THE FIX: Check the Backpack first! ---
        if (PersistentEquipment.Instance != null && PersistentEquipment.Instance.hasSavedData)
        {
            LoadEquipmentFromBackpack();
        }
        else
        {
            InitializeDefaultEquipment();
        }

        // NEW: Force a stats sync immediately so the player gets their bonuses without needing to open the menu
        PlayerStatsManager stats = FindFirstObjectByType<PlayerStatsManager>();
        if (stats != null)
        {
            stats.SyncWithUI(primaryGauntlet, secondaryGauntlet);
        }
        ForceLoadOnSpawn();
    }

    public void ForceLoadOnSpawn()
    {
        // Prevent double-loading if the player immediately opens the menu
        if (_isInitialized) return;
        _isInitialized = true;

        if (PersistentEquipment.Instance != null && PersistentEquipment.Instance.hasSavedData)
        {
            LoadEquipmentFromBackpack();
        }
        else
        {
            InitializeDefaultEquipment();
        }

        // Force a stats sync immediately so the player gets their bonuses
        PlayerStatsManager stats = FindFirstObjectByType<PlayerStatsManager>();
        if (stats != null)
        {
            stats.SyncWithUI(primaryGauntlet, secondaryGauntlet);
        }
    }

    /// <summary>
    /// Forces the displayed gear to be rebuilt from PersistentEquipment even if this
    /// InventoryManager already initialized itself. Used when loading a save file, where the
    /// backpack contents change underneath a UI that has survived from the previous scene.
    /// </summary>
    public void ReloadFromBackpack()
    {
        if (PersistentEquipment.Instance == null || !PersistentEquipment.Instance.hasSavedData)
        {
            Debug.LogWarning("[InventoryManager] ReloadFromBackpack called but the backpack is empty.");
            return;
        }

        _isInitialized = true;
        LoadEquipmentFromBackpack();

        PlayerStatsManager stats = FindFirstObjectByType<PlayerStatsManager>();
        if (stats != null) stats.SyncWithUI(primaryGauntlet, secondaryGauntlet);
    }

    private void LoadEquipmentFromBackpack()
    {
        PersistentEquipment pack = PersistentEquipment.Instance;

        // 1. Restore Primary Gauntlet
        if (pack.primaryGauntletPrefab != null && primaryGauntlet != null)
        {
            // Clear existing primary gauntlet clones
            ClearGauntletContainer(primaryGauntlet);

            activePrimaryPrefab = pack.primaryGauntletPrefab;
            GameObject newPrimary = Instantiate(pack.primaryGauntletPrefab, primaryGauntlet);
            newPrimary.transform.localPosition = Vector3.zero;
            newPrimary.transform.localScale = Vector3.one;

            GauntletManager pm = newPrimary.GetComponent<GauntletManager>();
            if (pm != null)
            {
                pm.InitializeGauntlet(true, pack.primaryRarity);
                RestoreGemsToGauntlet(pm, pack.primaryGems);

                if (pack.primarySkillGemPrefab != null && pm.SkillSlot != null)
                {
                    GameObject spawnedSkillGem = Instantiate(pack.primarySkillGemPrefab, pm.SkillSlot.transform);
                    DraggableGem skillGemScript = spawnedSkillGem.GetComponent<DraggableGem>();
                    SkillSlotManager skillSlotManager = pm.SkillSlot.GetComponent<SkillSlotManager>();

                    if (skillGemScript != null && skillSlotManager != null)
                    {
                        skillGemScript.parentAfterDrag = pm.SkillSlot.transform;
                        skillSlotManager.SlotSkillGem(skillGemScript);
                    }
                }
            }
        }

        // 2. Restore Secondary Gauntlet
        if (pack.secondaryGauntletPrefab != null && secondaryGauntlet != null)
        {
            // Clear existing secondary gauntlet clones
            ClearGauntletContainer(secondaryGauntlet);

            activeSecondaryPrefab = pack.secondaryGauntletPrefab;
            GameObject newSecondary = Instantiate(pack.secondaryGauntletPrefab, secondaryGauntlet);
            newSecondary.transform.localPosition = Vector3.zero;
            newSecondary.transform.localScale = Vector3.one;

            GauntletManager sm = newSecondary.GetComponent<GauntletManager>();
            if (sm != null)
            {
                sm.InitializeGauntlet(false, pack.secondaryRarity);
                RestoreGemsToGauntlet(sm, pack.secondaryGems); // Put gems back in slots!

                if (pack.secondarySkillGemPrefab != null && sm.SkillSlot != null)
                {
                    GameObject spawnedSkillGem = Instantiate(pack.secondarySkillGemPrefab, sm.SkillSlot.transform);
                    DraggableGem skillGemScript = spawnedSkillGem.GetComponent<DraggableGem>();
                    SkillSlotManager skillSlotManager = sm.SkillSlot.GetComponent<SkillSlotManager>();

                    if (skillGemScript != null && skillSlotManager != null)
                    {
                        skillGemScript.parentAfterDrag = sm.SkillSlot.transform;
                        skillSlotManager.SlotSkillGem(skillGemScript);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Empties a gauntlet container, detaching each child before destroying it.
    ///
    /// Destroy is deferred to the end of the frame, so without the detach the old gauntlet is
    /// still a child while the replacement is being built. Anything that looks the container up
    /// this frame (PlayerStatsManager.SyncWithUI, for one) could then read the dying gauntlet
    /// instead of the new one.
    /// </summary>
    private void ClearGauntletContainer(Transform container)
    {
        if (container == null) return;

        // ToList-style copy: reparenting while iterating the live child list would skip entries.
        List<Transform> children = new List<Transform>();
        foreach (Transform child in container) children.Add(child);

        foreach (Transform child in children)
        {
            child.SetParent(null, false);
            Destroy(child.gameObject);
        }
    }

    private void RestoreGemsToGauntlet(GauntletManager gm, List<GameObject> savedGems)
    {
        // Loop through the saved gem list and put them back exactly where they were
        for (int i = 0; i < gm.fingerSlots.Count && i < savedGems.Count; i++)
        {
            // Use currentActiveSlots instead of activeInHierarchy (which is false if UI is hidden)
            if (savedGems[i] != null && i < gm.currentActiveSlots)
            {
                GameObject spawnedGem = Instantiate(savedGems[i], gm.fingerSlots[i].transform);
                spawnedGem.transform.localPosition = Vector3.zero;
                spawnedGem.transform.localScale = Vector3.one;

                DraggableGem gemScript = spawnedGem.GetComponent<DraggableGem>();
                if (gemScript != null)
                {
                    // Ensure the gem knows it belongs in this slot so it doesn't fly away
                    gemScript.parentAfterDrag = gm.fingerSlots[i].transform;
                }
            }
        }
    }

    public void InitializeDefaultEquipment()
    {
        // Don't do anything if we forgot to assign the prefab in the Inspector
        if (defaultGauntletPrefab == null) return;

        // 1. If Primary is empty, spawn a Basic Primary Gauntlet
        if (primaryGauntlet != null && primaryGauntlet.childCount == 0)
        {
            activePrimaryPrefab = defaultGauntletPrefab;

            GameObject newPrimary = Instantiate(defaultGauntletPrefab, primaryGauntlet);
            newPrimary.transform.localPosition = Vector3.zero;
            newPrimary.transform.localScale = Vector3.one;

            GauntletManager pm = newPrimary.GetComponent<GauntletManager>();
            if (pm != null) pm.InitializeGauntlet(true, GauntletRarity.Common); // true = Primary, Normal rarity
        }

        // 2. If Secondary is empty, spawn a Basic Secondary Gauntlet
        if (secondaryGauntlet != null && secondaryGauntlet.childCount == 0)
        {
            activeSecondaryPrefab = defaultGauntletPrefab;

            GameObject newSecondary = Instantiate(defaultGauntletPrefab, secondaryGauntlet);
            newSecondary.transform.localPosition = Vector3.zero;
            newSecondary.transform.localScale = Vector3.one;

            GauntletManager sm = newSecondary.GetComponent<GauntletManager>();
            if (sm != null) sm.InitializeGauntlet(false, GauntletRarity.Common); // false = Secondary, Normal rarity
        }
    }

    private void OnEnable()
    {
        ResetGauntletPositionsInstant();
        RefreshDescriptionPanel();
        
        // If we are entering for a reward, let the RewardMenuManager handle focus!
        if (RewardMenuManager.Instance != null && RewardMenuManager.Instance.IsRewardModeActive()) return;

        // Use a coroutine to ensure the EventSystem is ready and visuals update correctly
        StopAllCoroutines(); 
        StartCoroutine(FocusTitleDelayed());
    }

    private System.Collections.IEnumerator FocusTitleDelayed()
    {
        // Wait for the end of frame or at least one frame to let UI settle
        yield return null;
        SetupNavigation();
        FocusTitle();
    }

    private void Update()
    {
        if (GemPopupMenu.Instance != null && GemPopupMenu.Instance.gameObject.activeInHierarchy) return;

        // Runs before the reward-mode return below, because gems are hovered in reward mode too.
        HandleInputModeChange();

        // NEW: Prioritize Replace UI inputs even in Reward Mode to prevent overlapping cancel handling
        if (_isReplaceUIPending)
        {
            HandleCancelInput();
            HandleControllerSwitch();
            return;
        }

        if (RewardMenuManager.Instance != null && RewardMenuManager.Instance.IsRewardModeActive()) return;

        HandleCancelInput();
        HandleControllerSwitch();
    }

    /// <summary>
    /// Hands the highlight over when the player swaps between mouse and controller.
    ///
    /// Each device leaves its own highlight behind: a controller-selected gem stays selected
    /// until something else is selected, and a hovered gem stays hovered until the pointer moves
    /// off it. Whichever device is no longer driving therefore leaves a stale bracket sitting
    /// next to the live one. Clearing the outgoing device's highlight on the switch is what keeps
    /// exactly one indicator on screen.
    ///
    /// Deliberately fires only on the transition. Doing this every frame would erase the
    /// controller's own bracket the moment it drew one.
    /// </summary>
    private void HandleInputModeChange()
    {
        bool gamepadNow = InputHelper.IsGamepadLastUsed();
        if (gamepadNow == _gamepadHadControl) return;

        _gamepadHadControl = gamepadNow;

        // The popups drive focus deliberately, so leave their buttons alone.
        if (_isWarningActive || _isReplaceUIPending) return;

        // Placement mode owns the selection: it IS the player's cursor for the gem being carried.
        // Note the popup's GameObject can be inactive while placing, so the guard at the top of
        // Update does not cover this.
        if (GemPopupMenu.Instance != null && GemPopupMenu.Instance.IsPlacingMode) return;

        if (gamepadNow) ClearHoverHighlight();
        else ClearSelectionHighlight();
    }

    /// <summary>Mouse took over: drop the EventSystem selection so OnDeselect clears its bracket.</summary>
    private void ClearSelectionHighlight()
    {
        if (EventSystem.current == null) return;

        GameObject current = EventSystem.current.currentSelectedGameObject;
        if (current == null) return;

        // Only give up focus that belongs to the gem board, never the header buttons.
        if (IsFocusedOnGemOrSlot(current)) EventSystem.current.SetSelectedGameObject(null);
    }

    /// <summary>Controller took over: the pointer is not moving any more, so clear its highlight.</summary>
    private void ClearHoverHighlight()
    {
        if (_hoveredGem != null)
        {
            _hoveredGem.HideSelectionBracket();
            SetHoveredGem(_hoveredGem, false);
        }

        GauntletManager activeManager = GetActiveGauntletManager();
        if (activeManager != null) activeManager.HideBracket();
    }

    private void HandleControllerSwitch()
    {
        if (Gamepad.current == null || EventSystem.current == null) return;

        bool stickMoved = Gamepad.current.leftStick.ReadValue().sqrMagnitude > 0.2f || 
                         Gamepad.current.rightStick.ReadValue().sqrMagnitude > 0.2f;
        bool dpadPressed = Gamepad.current.dpad.ReadValue().sqrMagnitude > 0.1f;
        
        bool buttonPressed = false;
        foreach (var control in Gamepad.current.allControls)
        {
            if (control is UnityEngine.InputSystem.Controls.ButtonControl button && button.wasPressedThisFrame)
            {
                buttonPressed = true;
                break;
            }
        }

        if (stickMoved || dpadPressed || buttonPressed)
        {
            GameObject current = EventSystem.current.currentSelectedGameObject;

            if (_isWarningActive)
            {
                // Force focus to warning buttons if it escaped to background or header
                if (current == null || (current != warningConfirmBtn.gameObject && current != warningCancelBtn.gameObject))
                {
                    EventSystem.current.SetSelectedGameObject(warningCancelBtn.gameObject);
                    TrapWarningFocus();
                }
            }
            else if (current == null)
            {
                if (_isReplaceUIPending)
                {
                    EventSystem.current.SetSelectedGameObject(replaceGauntletBtn.gameObject);
                }
                else
                {
                    SetupNavigation();
                    FocusTitle();
                }
            }
        }
    }

    private void TrapWarningFocus()
    {
        if (warningConfirmBtn == null || warningCancelBtn == null) return;

        Navigation confirmNav = new Navigation { mode = Navigation.Mode.Explicit };
        confirmNav.selectOnLeft = warningCancelBtn;
        confirmNav.selectOnRight = warningCancelBtn;
        confirmNav.selectOnUp = null;
        confirmNav.selectOnDown = null;
        warningConfirmBtn.navigation = confirmNav;

        Navigation cancelNav = new Navigation { mode = Navigation.Mode.Explicit };
        cancelNav.selectOnLeft = warningConfirmBtn;
        cancelNav.selectOnRight = warningConfirmBtn;
        cancelNav.selectOnUp = null;
        cancelNav.selectOnDown = null;
        warningCancelBtn.navigation = cancelNav;
    }

private void HandleCancelInput()
    {
        bool cancelPressed = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
                             (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);

        if (cancelPressed)
        {
            if (_isWarningActive)
            {
                if (_isReplaceUIPending) CancelDiscardWarning();
                else CancelClose();
            }
            else
            {
                // 1. Figure out exactly what the player is currently focused on
                GameObject currentSel = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;

                // 2. HIGHEST PRIORITY: If we are in the gem grid, escape to the title loop first!
                if (IsFocusedOnGemOrSlot(currentSel))
                {
                    FocusTitle();
                }
                // 3. We are already safely in the title navigation loop
                else
                {
                    // Now we check what kind of warning to show!
                    if (_isReplaceUIPending)
                    {
                        ShowDiscardGauntletWarning();
                    }
                    else
                    {
                        TryCloseCharacterScreen();
                    }
                }
            }
        }
    }

private bool IsFocusedOnGemOrSlot(GameObject obj)
    {
        if (obj == null) return false;

        // Check if the highlighted object is a gem (covers loose gems on the board)
        if (obj.GetComponent<DraggableGem>() != null) return true;

        // Check if it belongs to one of the gauntlet containers (covers empty slots)
        return obj.transform.IsChildOf(primaryGauntlet) || obj.transform.IsChildOf(secondaryGauntlet);
    }

    // --- REFINED GAUNTLET ANIMATIONS ---
    private void ResetGauntletPositionsInstant()
    {
        primaryGauntlet.GetComponent<RectTransform>().DOKill();
        secondaryGauntlet.GetComponent<RectTransform>().DOKill();

        primaryGauntlet.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
        primaryGauntlet.gameObject.SetActive(true);

        secondaryGauntlet.GetComponent<RectTransform>().anchoredPosition = new Vector2(-offscreenXOffset, 0f);
        secondaryGauntlet.gameObject.SetActive(false);

        _isDisplayingPrimary = true;
        _isTransitioning = false;
        
        // NEW: Clear any stale swap state when the inventory resets/opens normally
        if (replaceGauntletPanel != null) replaceGauntletPanel.SetActive(false);
        _isReplaceUIPending = false;
        _pendingGauntletPrefab = null;

        if (gauntletTitleText != null) 
            gauntletTitleText.text = gauntletPrimaryString.GetLocalizedString();

        UpdateReplaceButtonText();
    }

    public void ToggleEquippedGauntletDisplay()
    {
        if (_isTransitioning) return;
        _isTransitioning = true;

        // Flip state immediately so UI logic (like the replace button) updates instantly
        _isDisplayingPrimary = !_isDisplayingPrimary;

        if (gauntletTitleText != null)
            gauntletTitleText.text = _isDisplayingPrimary ? gauntletPrimaryString.GetLocalizedString() : gauntletSecondaryString.GetLocalizedString();

        UpdateReplaceButtonText();

        RectTransform primaryRect = primaryGauntlet.GetComponent<RectTransform>();
        RectTransform secondaryRect = secondaryGauntlet.GetComponent<RectTransform>();

        if (!_isDisplayingPrimary) // Switched to Secondary
        {
            primaryRect.DOAnchorPos(new Vector2(offscreenXOffset, 0f), slideDuration).SetEase(slideEase).SetUpdate(true)
                .OnComplete(() => primaryGauntlet.gameObject.SetActive(false));

            secondaryGauntlet.gameObject.SetActive(true);
            secondaryRect.anchoredPosition = new Vector2(-offscreenXOffset, 0f);
            secondaryRect.DOAnchorPos(Vector2.zero, slideDuration).SetEase(slideEase).SetUpdate(true)
                .OnComplete(() => { 
                    _isTransitioning = false; 
                    SetupNavigation();
                    FocusTitle(); 
                });
        }
        else // Switched to Primary
        {
            secondaryRect.DOAnchorPos(new Vector2(offscreenXOffset, 0f), slideDuration).SetEase(slideEase).SetUpdate(true)
                .OnComplete(() => secondaryGauntlet.gameObject.SetActive(false));

            primaryGauntlet.gameObject.SetActive(true);
            primaryRect.anchoredPosition = new Vector2(-offscreenXOffset, 0f);
            primaryRect.DOAnchorPos(Vector2.zero, slideDuration).SetEase(slideEase).SetUpdate(true)
                .OnComplete(() => { 
                    _isTransitioning = false; 
                    SetupNavigation();
                    FocusTitle(); 
                });
        }
    }

    public void AnimateGemArrivalDrops(List<GameObject> spawnedGems)
    {
        foreach (GameObject gem in spawnedGems)
        {
            if (gem == null) continue;

            RectTransform rect = gem.GetComponent<RectTransform>();
            if (rect == null) continue;

            Vector2 targetPos = rect.anchoredPosition;

            rect.anchoredPosition = new Vector2(targetPos.x, targetPos.y + 600f);
            rect.localScale = Vector3.zero;

            Sequence dropSeq = DOTween.Sequence();
            dropSeq.Join(rect.DOAnchorPos(targetPos, 0.45f).SetEase(Ease.OutBounce));
            dropSeq.Join(rect.DOScale(Vector3.one, 0.35f).SetEase(Ease.OutBack));
            dropSeq.SetUpdate(true);
            dropSeq.SetLink(gem); // Link to the gem's lifecycle to avoid Safe Mode warnings
        }
    }

    public void AnimateSingleGemDrop(DraggableGem gem)
    {
        if (gem == null) return;
        gem.transform.DOKill();
        AnimateGemArrivalDrops(new List<GameObject> { gem.gameObject });
    }

    // --- CLOSING & WARNINGS ---
    public void TryCloseCharacterScreen()
    {
        // NEW: If a swap is pending, you can't just 'exit'. You must deal with the gauntlet.
        // Redirecting to the Discard Warning ensures the player knows they are giving up the item.
        if (_isReplaceUIPending)
        {
            ShowDiscardGauntletWarning();
            return;
        }

        _isWarningActive = true;
        warningPanel.SetActive(true);

        bool hasUnequippedGems = false;
        foreach (DraggableGem gem in GetComponentsInChildren<DraggableGem>(false))
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
                ? warningUnequippedGemsString.GetLocalizedString()
                : warningLeaveInventoryString.GetLocalizedString();
        }

        warningConfirmBtn.onClick.RemoveAllListeners();
        warningConfirmBtn.onClick.AddListener(ConfirmClose);
        warningCancelBtn.onClick.RemoveAllListeners();
        warningCancelBtn.onClick.AddListener(CancelClose);

        TrapWarningFocus();

        if (InputHelper.IsGamepadLastUsed() && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(warningCancelBtn.gameObject);
        }
    }

    public void CancelClose()
    {
        _isWarningActive = false;
        warningPanel.SetActive(false);

        if (InputHelper.IsGamepadLastUsed() && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            if (_lastSelectedGem != null) EventSystem.current.SetSelectedGameObject(_lastSelectedGem.gameObject);
            else FocusTitle();
        }
    }

    public void ConfirmClose()
    {
        _isWarningActive = false;
        warningPanel.SetActive(false);

        PlayerStatsManager statsManager = FindFirstObjectByType<PlayerStatsManager>();
        if (statsManager != null) statsManager.SyncWithUI(primaryGauntlet, secondaryGauntlet);

        // Release OUR hold in both branches. TryEquipNewGauntlet suspends input under this object,
        // but GauntletMenu lives on the always-active UIManager, so the branch below effectively
        // always takes ResumeGame - which only releases GauntletMenu's own hold. Leaving this one
        // behind kept input suspended forever after swapping a gauntlet.
        if (GameplayInputGate.Instance != null) GameplayInputGate.Instance.RestoreWhenReleased(this);

        GauntletMenu gauntletMenu = FindFirstObjectByType<GauntletMenu>();
        if (gauntletMenu != null && gauntletMenu.gameObject.activeInHierarchy)
        {
            gauntletMenu.ResumeGame();
        }
        else
        {
            if (gauntletMenu != null) gauntletMenu.CloseUpgradeMenu();
            gameObject.SetActive(false);

            Time.timeScale = 1f;
        }
    }

    // --- UX HELPERS ---
    public void FocusTitle()
    {
        if (Gamepad.current == null || EventSystem.current == null) return;
        if (!InputHelper.IsGamepadLastUsed()) return;

        if (headerTitleButton != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(headerTitleButton.gameObject);
        }
    }

    public void FocusFirstAvailableGem()
    {
        if (Gamepad.current == null || EventSystem.current == null) return;
        if (!InputHelper.IsGamepadLastUsed()) return;

        DraggableGem[] allGems = GetComponentsInChildren<DraggableGem>();
        GameObject target = null;

        foreach (var gem in allGems)
        {
            if (gem != null && gem.gameObject.activeInHierarchy)
            {
                target = gem.gameObject;
                break;
            }
        }

        if (target != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(target);
        }
        else
        {
            // If no gems are found, focus a slot or the title so focus isn't lost!
            FocusFirstGemOrSlot();
        }
    }

    public void FocusFirstGemOrSlot()
    {
        if (!InputHelper.IsGamepadLastUsed()) return;

        GauntletManager activeManager = GetActiveGauntletManager();
        if (activeManager == null) return;
        
        Selectable first = null;

        // Strictly check slots in index order (Index 0 = Slot 1)
        for (int i = 0; i < activeManager.fingerSlots.Count; i++)
        {
            GameObject slotObj = activeManager.fingerSlots[i];
            if (slotObj == null || !slotObj.activeInHierarchy) continue;

            Selectable sel = GetSelectableFromSlot(slotObj);
            // Check interactability on the actual target (Gem or Slot)
            if (sel != null && sel.interactable)
            {
                first = sel;
                break;
            }
        }
        
        // If no finger slots found, try skill slot
        if (first == null && activeManager.SkillSlot != null && activeManager.SkillSlot.activeInHierarchy)
        {
            Selectable sel = GetSelectableFromSlot(activeManager.SkillSlot);
            if (sel != null && sel.interactable) first = sel;
        }
        
        if (first != null && EventSystem.current != null)
        {
            Debug.Log($"[InventoryManager] South Button Confirm -> Focusing: {first.gameObject.name} (Parent: {first.transform.parent.name})");
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(first.gameObject);
        }
    }

    private GauntletManager GetActiveGauntletManager()
    {
        Transform target = _isDisplayingPrimary ? primaryGauntlet : secondaryGauntlet;
        if (target == null) return null;
        return target.GetComponentInChildren<GauntletManager>(true); // Include inactive to be safe
    }

    private Selectable GetSelectableFromSlot(GameObject slotObj)
    {
        if (slotObj == null) return null;
        
        // Check for gem first
        DraggableGem gem = slotObj.GetComponentInChildren<DraggableGem>(true);
        if (gem != null)
        {
            Selectable gemSel = gem.GetComponent<Selectable>();
            if (gemSel != null) 
            {
                return gemSel;
            }
        }
        
        // Fallback to slot itself
        return slotObj.GetComponent<Selectable>();
    }

    public void SetupNavigation()
    {
        GauntletManager activeManager = GetActiveGauntletManager();
        if (activeManager == null || headerTitleButton == null) return;

        // 1. HARD RESET: Clear navigation for EVERYTHING in the gauntlet hierarchy
        Selectable[] allHierarchySelectables = activeManager.GetComponentsInChildren<Selectable>(true);
        foreach(var s in allHierarchySelectables)
        {
            Navigation n = s.navigation;
            n.mode = Navigation.Mode.None;
            s.navigation = n;
        }

        List<Selectable> selectables = new List<Selectable>();
        List<Selectable> allPotentialSlots = new List<Selectable>();
        
        // 2. Collect the 'best' selectable for each finger slot in strict order
        if (activeManager.fingerSlots != null)
        {
            foreach (GameObject slotObj in activeManager.fingerSlots)
            {
                if (slotObj == null) continue;
                Selectable sel = GetSelectableFromSlot(slotObj);
                if (sel != null) allPotentialSlots.Add(sel);
            }
        }
        
        if (activeManager.SkillSlot != null)
        {
            Selectable sel = GetSelectableFromSlot(activeManager.SkillSlot);
            if (sel != null) allPotentialSlots.Add(sel);
        }

        // 3. Filter for interactable selectables
        foreach (Selectable sel in allPotentialSlots)
        {
            if (sel.gameObject.activeInHierarchy && sel.interactable)
            {
                selectables.Add(sel);
            }
        }
        
        // Add reward gems to the loop if they exist
        Transform rewardContainer = GameObject.Find("Reward_Gem_Container")?.transform;
        if (rewardContainer != null)
        {
            foreach (Transform child in rewardContainer)
            {
                if (child.gameObject.activeInHierarchy)
                {
                    Selectable sel = child.GetComponent<Selectable>();
                    if (sel != null && sel.interactable) selectables.Add(sel);
                }
            }
        }

        if (selectables.Count == 0)
        {
            Debug.LogWarning("[InventoryManager] No interactable selectables found for navigation!");
            return;
        }
        
        // 3. HEADER NAVIGATION: Return <-> Title <-> SwitchGauntlet (Closed Horizontal Loop)
        // Vertical navigation into the gem grid is DISABLED per user request.
        Navigation titleNav = headerTitleButton.navigation;
        titleNav.mode = Navigation.Mode.Explicit;
        titleNav.selectOnUp = null;
        titleNav.selectOnDown = null; 

        if (returnButton != null)
        {
            Navigation returnNav = returnButton.navigation;
            returnNav.mode = Navigation.Mode.Explicit;
            returnNav.selectOnUp = null;
            returnNav.selectOnDown = null;

            titleNav.selectOnLeft = returnButton;
            returnNav.selectOnRight = headerTitleButton;

            if (switchGauntletButton != null)
            {
                returnNav.selectOnLeft = switchGauntletButton;
            }
            else
            {
                returnNav.selectOnLeft = headerTitleButton;
            }
            returnButton.navigation = returnNav;
        }

        if (switchGauntletButton != null)
        {
            Navigation switchNav = switchGauntletButton.navigation;
            switchNav.mode = Navigation.Mode.Explicit;
            switchNav.selectOnUp = null;
            // Only allow down if the Replace UI is active (popup interaction)
            switchNav.selectOnDown = _isReplaceUIPending ? replaceGauntletBtn : null;

            titleNav.selectOnRight = switchGauntletButton;
            switchNav.selectOnLeft = headerTitleButton;

            if (returnButton != null)
            {
                switchNav.selectOnRight = returnButton;
            }
            else
            {
                switchNav.selectOnRight = headerTitleButton;
            }
            switchGauntletButton.navigation = switchNav;
        }

        headerTitleButton.navigation = titleNav;
        
        // 5. SLOT LOOP: Circular linear navigation
        int count = selectables.Count;
        for (int i = 0; i < count; i++)
        {
            Navigation nav = selectables[i].navigation;
            nav.mode = Navigation.Mode.Explicit;
            
            nav.selectOnLeft = selectables[(i == 0) ? count - 1 : i - 1];
            nav.selectOnRight = selectables[(i == count - 1) ? 0 : i + 1];
            nav.selectOnUp = selectables[(i == 0) ? count - 1 : i - 1];
            nav.selectOnDown = selectables[(i == count - 1) ? 0 : i + 1];

            selectables[i].navigation = nav;
        }
    }

    public bool TryEquipNewGauntlet(GameObject gauntletPrefab, GauntletRarity rarity)
    {
        if (gauntletPrefab == null) return false;

        // --- THE FIX: Force the inventory open so the player sees what is happening! ---
        if (!gameObject.activeInHierarchy)
        {
            gameObject.SetActive(true);
            Time.timeScale = 0f;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            // This path opens the screen without going through GauntletMenu.PauseGame, so it has
            // to suspend gameplay input itself or clicks in here reach PlayerCombat.
            if (GameplayInputGate.Instance != null) GameplayInputGate.Instance.Suspend(this);
            // Note: If you have a player input script to disable (like camera look), disable it here!
        }

        // Check Primary First
        if (primaryGauntlet.childCount == 0)
        {
            ForceReplaceGauntlet(true, gauntletPrefab, rarity);
            return true;
        }
        // Check Secondary Next
        else if (secondaryGauntlet.childCount == 0)
        {
            ForceReplaceGauntlet(false, gauntletPrefab, rarity);
            return true;
        }

        // Both are full! Trigger the Replace UI
        ShowReplaceGauntletPanel(gauntletPrefab, rarity);
        return false;
    }

    // --- NEW: Gauntlet Swap UI Methods ---
    private void ShowReplaceGauntletPanel(GameObject prefab, GauntletRarity rarity)
    {
        _pendingGauntletPrefab = prefab;
        _pendingGauntletRarity = rarity;
        _isReplaceUIPending = true;

        if (replaceGauntletPanel != null) replaceGauntletPanel.SetActive(true);

        // --- THE FIX: Trap Controller Navigation to the Popup! ---
        if (replaceGauntletBtn != null && replaceDiscardBtn != null)
        {
            // NEW: Link header to the popup
            if (switchGauntletButton != null)
            {
                Navigation headerNav = switchGauntletButton.navigation;
                headerNav.selectOnDown = replaceGauntletBtn;
                switchGauntletButton.navigation = headerNav;
            }

            Navigation replaceNav = new Navigation { mode = Navigation.Mode.Explicit };
            replaceNav.selectOnDown = replaceDiscardBtn;
            replaceNav.selectOnUp = switchGauntletButton; // Link back to header
            replaceNav.selectOnLeft = null;
            replaceNav.selectOnRight = null;
            replaceGauntletBtn.navigation = replaceNav;

            Navigation discardNav = new Navigation { mode = Navigation.Mode.Explicit };
            discardNav.selectOnDown = null;
            discardNav.selectOnUp = replaceGauntletBtn;
            discardNav.selectOnLeft = null;
            discardNav.selectOnRight = null;
            replaceDiscardBtn.navigation = discardNav;
        }

        if (replaceGauntletBtn != null)
        {
            replaceGauntletBtn.onClick.RemoveAllListeners();
            replaceGauntletBtn.onClick.AddListener(() => ConfirmReplace(_isDisplayingPrimary));
            UpdateReplaceButtonText();
        }

        if (replaceDiscardBtn != null)
        {
            replaceDiscardBtn.onClick.RemoveAllListeners();
            replaceDiscardBtn.onClick.AddListener(ShowDiscardGauntletWarning);
        }

        // Lock controller focus to the popup!
        if (InputHelper.IsGamepadLastUsed() && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(replaceGauntletBtn.gameObject);
        }
    }

    private void ShowDiscardGauntletWarning()
    {
        _isWarningActive = true;
        warningPanel.SetActive(true);

        if (warningBodyText != null)
        {
            warningBodyText.text = (warningDiscardGauntletString != null) 
                ? warningDiscardGauntletString.GetLocalizedString() 
                : "Are you sure you want to discard this gauntlet? It will be lost forever.";
        }

        warningConfirmBtn.onClick.RemoveAllListeners();
        warningConfirmBtn.onClick.AddListener(ConfirmDiscardPending);
        
        warningCancelBtn.onClick.RemoveAllListeners();
        warningCancelBtn.onClick.AddListener(CancelDiscardWarning);

        TrapWarningFocus();

        if (InputHelper.IsGamepadLastUsed() && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(warningCancelBtn.gameObject);
        }
    }

    private void ConfirmDiscardPending()
    {
        _isWarningActive = false;
        warningPanel.SetActive(false);
        DiscardPendingGauntlet();
    }

    private void CancelDiscardWarning()
    {
        _isWarningActive = false;
        warningPanel.SetActive(false);
        if (InputHelper.IsGamepadLastUsed() && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(replaceGauntletBtn.gameObject);
        }
    }

    private void UpdateReplaceButtonText()
    {
        if (replaceGauntletBtn == null) return;
        
        var localizeEvent = replaceGauntletBtn.GetComponentInChildren<UnityEngine.Localization.Components.LocalizeStringEvent>();
        if (localizeEvent != null)
        {
            localizeEvent.StringReference = _isDisplayingPrimary ? primaryKey : secondaryKey;
            localizeEvent.RefreshString();
        }
    }

    private void ConfirmReplace(bool isPrimary)
    {
        if (replaceGauntletPanel != null) replaceGauntletPanel.SetActive(false);
        _isReplaceUIPending = false;

        ForceReplaceGauntlet(isPrimary, _pendingGauntletPrefab, _pendingGauntletRarity);
        _pendingGauntletPrefab = null;

        SetupNavigation(); // Restore header navigation
    }

    private void DiscardPendingGauntlet()
    {
        _isWarningActive = false;
        if (warningPanel != null) warningPanel.SetActive(false);
        
        if (replaceGauntletPanel != null) replaceGauntletPanel.SetActive(false);
        _isReplaceUIPending = false;
        _pendingGauntletPrefab = null;

        SetupNavigation(); // Restore header navigation
        FocusFirstAvailableGem();
    }

    // 2. FORCE REPLACE: Overwrites a specific hand and safely evicts its gems.
    public void ForceReplaceGauntlet(bool isPrimary, GameObject gauntletPrefab, GauntletRarity rarity)
    {
        if (isPrimary) activePrimaryPrefab = gauntletPrefab;
        else activeSecondaryPrefab = gauntletPrefab;

        Transform targetSlot = isPrimary ? primaryGauntlet : secondaryGauntlet;
        DraggableGem savedSkillGem = null;
        List<DraggableGem> savedStatGems = new List<DraggableGem>();

        // Step 1: Lift the old gems out so they survive the swap.
        //
        // These used to be dumped straight onto the reward board, which quietly destroyed them:
        // Reward_Gem_Container is the same object RewardMenuManager uses as its gem spawn area, and
        // that wipes all of its children whenever the reward menu opens or closes. The skill gem
        // survived only because it was re-slotted instead. Stat gems are now carried across too,
        // and only genuine overflow reaches the board.
        GauntletManager oldManager = targetSlot.GetComponentInChildren<GauntletManager>(true);
        if (oldManager != null)
        {
            //saves skill gem as well now
            if (oldManager.SkillSlot != null)
            {
                savedSkillGem = oldManager.SkillSlot.GetComponentInChildren<DraggableGem>(true);
                if (savedSkillGem != null)
                {
                    savedSkillGem.transform.SetParent(transform); // Temporarily hide it in the inventory root
                }
            }

            foreach (GameObject slot in oldManager.fingerSlots)
            {
                if (slot == null) continue;

                DraggableGem gem = slot.GetComponentInChildren<DraggableGem>(true);
                if (gem != null)
                {
                    savedStatGems.Add(gem);
                    gem.transform.SetParent(transform); // Park it in the inventory root for now
                }
            }
        }

        // Step 2: Destroy the old gauntlet. The rescued gems are parented elsewhere by now, so
        // there is nothing left here to preserve.
        foreach (Transform child in targetSlot)
        {
            Destroy(child.gameObject);
        }

        // Step 3: Spawn the new gauntlet
        GameObject newGauntlet = Instantiate(gauntletPrefab, targetSlot);
        newGauntlet.transform.localPosition = Vector3.zero;
        newGauntlet.transform.localScale = Vector3.one;

        GauntletManager gm = newGauntlet.GetComponent<GauntletManager>();
        if (gm != null)
        {
            gm.InitializeGauntlet(isPrimary, rarity);

            if (savedSkillGem != null && gm.SkillSlot != null)
            {
                SkillSlotManager skillSlotManager = gm.SkillSlot.GetComponent<SkillSlotManager>();
                if (skillSlotManager != null)
                {
                    savedSkillGem.parentAfterDrag = gm.SkillSlot.transform;
                    skillSlotManager.SlotSkillGem(savedSkillGem);
                }
            }

            RestoreStatGemsAfterSwap(gm, savedStatGems);
        }
        else
        {
            // No manager on the new gauntlet, so there is nowhere to put them back.
            foreach (DraggableGem gem in savedStatGems) EvictGemToBoard(gem);
        }

        // Step 4: Reset UI layout so the player sees their new gear
        if (isPrimary && !_isDisplayingPrimary) ToggleEquippedGauntletDisplay();
        else if (!isPrimary && _isDisplayingPrimary) ToggleEquippedGauntletDisplay();

        FocusFirstAvailableGem();
    }

    /// <summary>
    /// Puts the rescued stat gems back into the new gauntlet, in their old order, filling only the
    /// slots it has actually unlocked. Anything that no longer fits goes to the board.
    /// </summary>
    private void RestoreStatGemsAfterSwap(GauntletManager gm, List<DraggableGem> savedGems)
    {
        int nextSlot = 0;

        foreach (DraggableGem gem in savedGems)
        {
            if (gem == null) continue;

            // Find the next free unlocked slot.
            while (nextSlot < gm.fingerSlots.Count && nextSlot < gm.currentActiveSlots &&
                   gm.fingerSlots[nextSlot] != null &&
                   gm.fingerSlots[nextSlot].GetComponentInChildren<DraggableGem>(true) != null)
            {
                nextSlot++;
            }

            bool hasRoom = nextSlot < gm.fingerSlots.Count && nextSlot < gm.currentActiveSlots &&
                           gm.fingerSlots[nextSlot] != null;

            if (!hasRoom)
            {
                // The new gauntlet has fewer sockets than the old one, so this gem cannot come with.
                EvictGemToBoard(gem);
                continue;
            }

            Transform slot = gm.fingerSlots[nextSlot].transform;
            gem.parentAfterDrag = slot;
            gem.transform.SetParent(slot);
            gem.transform.localPosition = Vector3.zero;
            gem.transform.localScale = Vector3.one;

            RectTransform gemRect = gem.GetComponent<RectTransform>();
            RectTransform slotRect = slot.GetComponent<RectTransform>();
            if (gemRect != null && slotRect != null) gemRect.sizeDelta = slotRect.rect.size;

            nextSlot++;
        }
    }

    /// <summary>
    /// Drops a gem onto the reward board. Note this is a lossy destination: the reward menu clears
    /// that container when it opens or closes, so only use it when there is genuinely no socket.
    /// </summary>
    private void EvictGemToBoard(DraggableGem gem)
    {
        if (gem == null) return;

        GameObject container = GameObject.Find("Reward_Gem_Container");
        if (container == null)
        {
            Debug.LogWarning($"[InventoryManager] Nowhere to put '{gem.name}' - no Reward_Gem_Container.");
            return;
        }

        gem.parentAfterDrag = container.transform;
        gem.transform.SetParent(container.transform);

        RectTransform gemRect = gem.GetComponent<RectTransform>();
        if (gemRect != null && RewardMenuManager.Instance != null)
            gemRect.anchoredPosition = RewardMenuManager.Instance.CalculateSafeScatterPoint(gemRect);

        gem.ReturnToInventory();
        AnimateSingleGemDrop(gem);
    }

    public bool AutoSlotSkillGem(GameObject skillGemPrefab)
    {
        GauntletManager primaryManager = primaryGauntlet.GetComponentInChildren<GauntletManager>();
        if (primaryManager != null && primaryManager.SkillSlot != null)
        {
            SkillSlotManager skillSlot = primaryManager.SkillSlot.GetComponent<SkillSlotManager>();
            // GetEquippedSkillGem checks the hierarchy, so a slot the player emptied is correctly
            // seen as free. CurrentSkillGem alone would still report the removed gem.
            if (skillSlot != null && skillSlot.GetEquippedSkillGem() == null)
            {
                GameObject newGem = Instantiate(skillGemPrefab);
                DraggableGem gemScript = newGem.GetComponent<DraggableGem>();
                gemScript.parentAfterDrag = skillSlot.transform;
                skillSlot.SlotSkillGem(gemScript);
                
                PlayerStatsManager stats = FindFirstObjectByType<PlayerStatsManager>();
                if (stats != null) stats.SyncWithUI(primaryGauntlet, secondaryGauntlet);
                
                return true; 
            }
        }

        GauntletManager secondaryManager = secondaryGauntlet.GetComponentInChildren<GauntletManager>();
        if (secondaryManager != null && secondaryManager.SkillSlot != null)
        {
            SkillSlotManager skillSlot = secondaryManager.SkillSlot.GetComponent<SkillSlotManager>();
            // GetEquippedSkillGem checks the hierarchy, so a slot the player emptied is correctly
            // seen as free. CurrentSkillGem alone would still report the removed gem.
            if (skillSlot != null && skillSlot.GetEquippedSkillGem() == null)
            {
                GameObject newGem = Instantiate(skillGemPrefab);
                DraggableGem gemScript = newGem.GetComponent<DraggableGem>();
                gemScript.parentAfterDrag = skillSlot.transform;
                skillSlot.SlotSkillGem(gemScript);
                
                PlayerStatsManager stats = FindFirstObjectByType<PlayerStatsManager>();
                if (stats != null) stats.SyncWithUI(primaryGauntlet, secondaryGauntlet);

                return true;
            }
        }

        return false;
    }

    // --- SMART DESCRIPTION UI ---

    public void SetHoveredGem(DraggableGem gem, bool isHovering)
    {
        if (isHovering) _hoveredGem = gem;
        else if (_hoveredGem == gem) _hoveredGem = null;
        RefreshDescriptionPanel();
    }

    public void SetFocusedGem(DraggableGem gem, bool isFocused)
    {
        if (isFocused) 
        {
            _focusedGem = gem;
            _lastSelectedGem = gem;
        }
        else if (_focusedGem == gem) 
        {
            _focusedGem = null;
        }
        RefreshDescriptionPanel();
    }

    public void RefreshDescriptionPanel()
    {
        // If the Gem Menu is open, freeze the panel so the description doesn't vanish!
        if (GemPopupMenu.Instance != null && GemPopupMenu.Instance.gameObject.activeInHierarchy) return;

        // Prioritize mouse hover. If no mouse hover, fall back to controller focus.
        DraggableGem targetGem = _hoveredGem != null ? _hoveredGem : _focusedGem;

        if (targetGem != null)
        {
            descriptionBoxAnchor.gameObject.SetActive(true);
            if (detailCanvasGroup != null) detailCanvasGroup.alpha = 1f;

            detailNameText.text = targetGem.gemName.GetLocalizedString();
            detailDescriptionText.text = targetGem.gemDescription.GetLocalizedString();
            if (detailIcon != null)
            {
                detailIcon.sprite = targetGem.gemIcon;
                detailIcon.color = Color.white;
            }
        }
        else
        {
            // Nothing is hovered or focused, completely hide the panel!
            descriptionBoxAnchor.gameObject.SetActive(false);
            if (detailCanvasGroup != null) detailCanvasGroup.alpha = 0f;
        }
    }
}