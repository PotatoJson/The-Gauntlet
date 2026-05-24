using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using DG.Tweening;

public class InventoryManager : MonoBehaviour
{
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
    [SerializeField] private TMP_Text detailNameText;
    [SerializeField] private TMP_Text detailDescriptionText;
    [SerializeField] private Image detailIcon;

    [Header("Tween Settings")]
    [SerializeField] private float slideDuration = 0.5f;
    [SerializeField] private Ease slideEase = Ease.OutCubic;
    [SerializeField] private float offscreenXOffset = 900f;

    [Header("Replace Gauntlet UI")]
    [SerializeField] private GameObject replaceGauntletPanel;
    [SerializeField] private Button replacePrimaryBtn;
    [SerializeField] private Button replaceSecondaryBtn;
    [SerializeField] private Button replaceDiscardBtn;


    private GameObject _pendingGauntletPrefab;
    private GauntletRarity _pendingGauntletRarity;
    private bool _isReplaceUIPending = false;

    private DraggableGem _lastSelectedGem;
    private bool _isWarningActive = false;
    private bool _isDisplayingPrimary = true;
    private bool _isTransitioning = false;
    private DraggableGem _hoveredGem;
    private DraggableGem _focusedGem;

    private void Awake()
    {
        if (warningPanel != null) warningPanel.SetActive(false);
        if (replaceGauntletPanel != null) replaceGauntletPanel.SetActive(false);
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
    }

    private void LoadEquipmentFromBackpack()
    {
        PersistentEquipment pack = PersistentEquipment.Instance;

        // 1. Restore Primary Gauntlet
        if (pack.primaryGauntletPrefab != null && primaryGauntlet != null)
        {
            activePrimaryPrefab = pack.primaryGauntletPrefab;
            GameObject newPrimary = Instantiate(pack.primaryGauntletPrefab, primaryGauntlet);
            newPrimary.transform.localPosition = Vector3.zero;
            newPrimary.transform.localScale = Vector3.one;

            GauntletManager pm = newPrimary.GetComponent<GauntletManager>();
            if (pm != null)
            {
                pm.InitializeGauntlet(true, pack.primaryRarity);
                RestoreGemsToGauntlet(pm, pack.primaryGems);

                if (pack.ultimateGemPrefab != null && pm.SkillSlot != null)
                {
                    GameObject spawnedSkillGem = Instantiate(pack.ultimateGemPrefab, pm.SkillSlot.transform);
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
            activeSecondaryPrefab = pack.secondaryGauntletPrefab;
            GameObject newSecondary = Instantiate(pack.secondaryGauntletPrefab, secondaryGauntlet);
            newSecondary.transform.localPosition = Vector3.zero;
            newSecondary.transform.localScale = Vector3.one;

            GauntletManager sm = newSecondary.GetComponent<GauntletManager>();
            if (sm != null)
            {
                sm.InitializeGauntlet(false, pack.secondaryRarity);
                RestoreGemsToGauntlet(sm, pack.secondaryGems); // Put gems back in slots!
            }
        }
    }

    private void RestoreGemsToGauntlet(GauntletManager gm, List<GameObject> savedGems)
    {
        // Loop through the saved gem list and put them back exactly where they were
        for (int i = 0; i < gm.fingerSlots.Count && i < savedGems.Count; i++)
        {
            if (savedGems[i] != null && gm.fingerSlots[i].activeInHierarchy)
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
        FocusFirstAvailableGem();
    }

    private void Update()
    {
        if (GemPopupMenu.Instance != null && GemPopupMenu.Instance.gameObject.activeInHierarchy) return;
        if (RewardMenuManager.Instance != null && RewardMenuManager.Instance.IsRewardModeActive()) return;

        HandleCancelInput();
    }

    private void HandleCancelInput()
    {
        bool cancelPressed = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
                             (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);

        if (cancelPressed)
        {
            if (_isReplaceUIPending)
            {
                DiscardPendingGauntlet();
            }
            else if (_isWarningActive)
            {
                CancelClose();
            }
            else
            {
                GameObject currentSel = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;

                // If highlighting a Gem, jump up to the Return Button
                if (currentSel != null && currentSel.GetComponent<DraggableGem>() != null)
                {
                    if (returnButton != null)
                    {
                        EventSystem.current.SetSelectedGameObject(null);
                        EventSystem.current.SetSelectedGameObject(returnButton.gameObject);
                    }
                }
                else
                {
                    // Already on the header buttons -> Open the exit warning
                    TryCloseCharacterScreen();
                }
            }
        }
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
        if (gauntletTitleText != null) gauntletTitleText.text = "Gauntlet Primary";
    }

    public void ToggleEquippedGauntletDisplay()
    {
        if (_isTransitioning) return;
        _isTransitioning = true;

        if (gauntletTitleText != null)
            gauntletTitleText.text = _isDisplayingPrimary ? "Gauntlet Secondary" : "Gauntlet Primary";

        RectTransform primaryRect = primaryGauntlet.GetComponent<RectTransform>();
        RectTransform secondaryRect = secondaryGauntlet.GetComponent<RectTransform>();

        if (_isDisplayingPrimary)
        {
            primaryRect.DOAnchorPos(new Vector2(offscreenXOffset, 0f), slideDuration).SetEase(slideEase).SetUpdate(true)
                .OnComplete(() => primaryGauntlet.gameObject.SetActive(false));

            secondaryGauntlet.gameObject.SetActive(true);
            secondaryRect.anchoredPosition = new Vector2(-offscreenXOffset, 0f);
            secondaryRect.DOAnchorPos(Vector2.zero, slideDuration).SetEase(slideEase).SetUpdate(true)
                .OnComplete(() => { _isDisplayingPrimary = false; _isTransitioning = false; FocusFirstAvailableGem(); });
        }
        else
        {
            secondaryRect.DOAnchorPos(new Vector2(offscreenXOffset, 0f), slideDuration).SetEase(slideEase).SetUpdate(true)
                .OnComplete(() => secondaryGauntlet.gameObject.SetActive(false));

            primaryGauntlet.gameObject.SetActive(true);
            primaryRect.anchoredPosition = new Vector2(-offscreenXOffset, 0f);
            primaryRect.DOAnchorPos(Vector2.zero, slideDuration).SetEase(slideEase).SetUpdate(true)
                .OnComplete(() => { _isDisplayingPrimary = true; _isTransitioning = false; FocusFirstAvailableGem(); });
        }
    }

    public void AnimateGemArrivalDrops(List<GameObject> spawnedGems)
    {
        foreach (GameObject gem in spawnedGems)
        {
            RectTransform rect = gem.GetComponent<RectTransform>();
            Vector2 targetPos = rect.anchoredPosition;

            rect.anchoredPosition = new Vector2(targetPos.x, targetPos.y + 600f);
            rect.localScale = Vector3.zero;

            Sequence dropSeq = DOTween.Sequence();
            dropSeq.Join(rect.DOAnchorPos(targetPos, 0.45f).SetEase(Ease.OutBounce));
            dropSeq.Join(rect.DOScale(Vector3.one, 0.35f).SetEase(Ease.OutBack));
            dropSeq.SetUpdate(true);
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

    public void CancelClose()
    {
        _isWarningActive = false;
        warningPanel.SetActive(false);

        if (Gamepad.current != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            if (_lastSelectedGem != null) EventSystem.current.SetSelectedGameObject(_lastSelectedGem.gameObject);
            else FocusFirstAvailableGem();
        }
    }

    public void ConfirmClose()
    {
        _isWarningActive = false;
        warningPanel.SetActive(false);

        PlayerStatsManager statsManager = FindFirstObjectByType<PlayerStatsManager>();
        if (statsManager != null) statsManager.SyncWithUI(primaryGauntlet, secondaryGauntlet);

        GauntletMenu gauntletMenu = FindFirstObjectByType<GauntletMenu>();
        if (gauntletMenu != null && gauntletMenu.gameObject.activeInHierarchy)
        {
            gauntletMenu.ResumeGame();
        }
        else
        {
            gameObject.SetActive(false);
            Time.timeScale = 1f;
        }
    }

    // --- UX HELPERS ---
    public void FocusFirstAvailableGem()
    {
        if (Gamepad.current == null || EventSystem.current == null) return;

        DraggableGem[] allGems = GetComponentsInChildren<DraggableGem>();
        if (allGems.Length > 0)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(allGems[0].gameObject);
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

        replacePrimaryBtn.onClick.RemoveAllListeners();
        replacePrimaryBtn.onClick.AddListener(() => ConfirmReplace(true));

        replaceSecondaryBtn.onClick.RemoveAllListeners();
        replaceSecondaryBtn.onClick.AddListener(() => ConfirmReplace(false));

        if (replaceDiscardBtn != null)
        {
            replaceDiscardBtn.onClick.RemoveAllListeners();
            replaceDiscardBtn.onClick.AddListener(DiscardPendingGauntlet);
        }

        // Lock controller focus to the popup!
        if (Gamepad.current != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(replacePrimaryBtn.gameObject);
        }
    }

    private void ConfirmReplace(bool isPrimary)
    {
        if (replaceGauntletPanel != null) replaceGauntletPanel.SetActive(false);
        _isReplaceUIPending = false;

        ForceReplaceGauntlet(isPrimary, _pendingGauntletPrefab, _pendingGauntletRarity);
        _pendingGauntletPrefab = null;
    }

    private void DiscardPendingGauntlet()
    {
        if (replaceGauntletPanel != null) replaceGauntletPanel.SetActive(false);
        _isReplaceUIPending = false;
        _pendingGauntletPrefab = null;

        FocusFirstAvailableGem();
    }

    // 2. FORCE REPLACE: Overwrites a specific hand and safely evicts its gems.
    public void ForceReplaceGauntlet(bool isPrimary, GameObject gauntletPrefab, GauntletRarity rarity)
    {
        if (isPrimary) activePrimaryPrefab = gauntletPrefab;
        else activeSecondaryPrefab = gauntletPrefab;

        Transform targetSlot = isPrimary ? primaryGauntlet : secondaryGauntlet;

        // Step 1: Safely EVICT any slotted gems so the player doesn't lose them!
        GauntletManager oldManager = targetSlot.GetComponentInChildren<GauntletManager>();
        if (oldManager != null)
        {
            foreach (GameObject slot in oldManager.fingerSlots)
            {
                DraggableGem gem = slot.GetComponentInChildren<DraggableGem>();
                if (gem != null)
                {
                    // Toss the gem onto the board safely
                    Transform rewardContainer = GameObject.Find("Reward_Gem_Container").transform;
                    gem.parentAfterDrag = rewardContainer;
                    gem.transform.SetParent(rewardContainer);

                    RectTransform gemRect = gem.GetComponent<RectTransform>();
                    if (RewardMenuManager.Instance != null)
                        gemRect.anchoredPosition = RewardMenuManager.Instance.CalculateSafeScatterPoint(gemRect);

                    gem.ReturnToInventory();
                    AnimateSingleGemDrop(gem);
                }
            }
        }

        // Step 2: Destroy the old gauntlet
        foreach (Transform child in targetSlot)
        {
            Destroy(child.gameObject);
        }

        // Step 3: Spawn the new gauntlet
        GameObject newGauntlet = Instantiate(gauntletPrefab, targetSlot);
        newGauntlet.transform.localPosition = Vector3.zero;
        newGauntlet.transform.localScale = Vector3.one;

        GauntletManager gm = newGauntlet.GetComponent<GauntletManager>();
        if (gm != null) gm.InitializeGauntlet(isPrimary, rarity);

        // Step 4: Reset UI layout so the player sees their new gear
        if (isPrimary && !_isDisplayingPrimary) ToggleEquippedGauntletDisplay();
        else if (!isPrimary && _isDisplayingPrimary) ToggleEquippedGauntletDisplay();

        FocusFirstAvailableGem();
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
        if (isFocused) _focusedGem = gem;
        else if (_focusedGem == gem) _focusedGem = null;
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
            detailNameText.text = targetGem.gemName;
            detailDescriptionText.text = targetGem.gemDescription;
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
        }
    }
}