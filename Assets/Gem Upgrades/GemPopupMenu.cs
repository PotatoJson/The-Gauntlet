using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;

public class GemPopupMenu : MonoBehaviour
{
    public static GemPopupMenu Instance;

    [Header("Dynamic UI Panels")]
    [SerializeField] private GameObject unslottedPanel;
    [SerializeField] private GameObject slottedPanel;
    [SerializeField] private GameObject subPanel;

    [Header("Controller Focus Anchors")]
    [SerializeField] private GameObject equipButton;
    [SerializeField] private GameObject swapButton;
    [SerializeField] private GameObject primaryButton;
    [SerializeField] private Button unequipButton;

    [Header("Gauntlet Slot Containers")]
    [SerializeField] private Transform primaryGauntlet;
    [SerializeField] private Transform secondaryGauntlet;

    private DraggableGem _targetGem;
    private RectTransform _rectTransform;
    private bool _isPlacingMode = false;
    private bool _placementConfirmArmed;
    private Transform _originalGemParent;

    public bool IsPlacingMode => _isPlacingMode;

    private void Awake()
    {
        Instance = this;
        _rectTransform = GetComponent<RectTransform>();
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (_isPlacingMode)
        {
            HandlePlacementMode();
            return;
        }

        if (gameObject.activeInHierarchy)
        {
            bool cancelPressed = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
                                 (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);

            if (cancelPressed)
            {
                if (subPanel != null && subPanel.activeSelf)
                {
                    subPanel.SetActive(false);
                    bool isEquipped = _targetGem.IsEquipped();
                    unslottedPanel.SetActive(!isEquipped);
                    slottedPanel.SetActive(isEquipped);

                    // Coming back from the sub-panel re-shows these buttons, so the disabled ones
                    // have to be skipped again.
                    WirePanelNavigation(isEquipped ? slottedPanel : unslottedPanel);

                    if (EventSystem.current != null)
                    {
                        EventSystem.current.SetSelectedGameObject(null);
                        EventSystem.current.SetSelectedGameObject(isEquipped ? swapButton : equipButton);
                    }
                }
                else
                {
                    CloseMenu();
                    if (EventSystem.current != null && _targetGem != null)
                    {
                        EventSystem.current.SetSelectedGameObject(null);
                        EventSystem.current.SetSelectedGameObject(_targetGem.gameObject);
                    }
                }
            }
        }
    }

    public void OpenMenu(DraggableGem gem, RectTransform gemRect)
    {
        _targetGem = gem;
        _isPlacingMode = false;

        transform.position = gemRect.position;
        _rectTransform.anchoredPosition += new Vector2(gemRect.rect.width / 2f, -gemRect.rect.height / 2f);

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            Vector3[] corners = new Vector3[4];
            _rectTransform.GetWorldCorners(corners);

            if (corners[2].x > Screen.width) _rectTransform.anchoredPosition -= new Vector2(corners[2].x - Screen.width + 20f, 0);
            if (corners[0].y < 0) _rectTransform.anchoredPosition += new Vector2(0, Mathf.Abs(corners[0].y) + 20f);
        }

        bool isEquipped = gem.IsEquipped();
        unslottedPanel.SetActive(!isEquipped);
        slottedPanel.SetActive(isEquipped);
        subPanel.SetActive(false);

        if (unequipButton != null)
        {
            bool isRewardMode = RewardMenuManager.Instance != null && RewardMenuManager.Instance.IsRewardModeActive();
            unequipButton.interactable = isRewardMode;
        }

        gameObject.SetActive(true);

        // Must run after the interactable states above, since it reads them.
        WirePanelNavigation(isEquipped ? slottedPanel : unslottedPanel);

        if (Gamepad.current != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(isEquipped ? swapButton : equipButton);
        }
    }

    public void CloseMenu()
    {
        _isPlacingMode = false;
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Links a panel's buttons into a vertical loop, skipping any that are not interactable.
    ///
    /// Unity only steps over a disabled Selectable automatically under Navigation.Mode.Automatic.
    /// These panels are wired explicitly, so without this the D-pad happily landed on a greyed-out
    /// Unequip and the player got stuck on a button that does nothing.
    ///
    /// When only one button is usable it links to itself, so pressing up or down simply stays put.
    /// </summary>
    private static void WirePanelNavigation(GameObject panel)
    {
        if (panel == null) return;

        List<Selectable> usable = new List<Selectable>();
        foreach (Selectable selectable in panel.GetComponentsInChildren<Selectable>(false))
        {
            if (selectable.interactable) usable.Add(selectable);
        }

        int count = usable.Count;
        if (count == 0) return;

        for (int i = 0; i < count; i++)
        {
            Selectable previous = usable[(i == 0) ? count - 1 : i - 1];
            Selectable next = usable[(i == count - 1) ? 0 : i + 1];

            // Only the vertical axis is rebuilt; any horizontal links the panel was authored with
            // are left alone.
            Navigation nav = usable[i].navigation;
            nav.mode = Navigation.Mode.Explicit;
            nav.selectOnUp = previous;
            nav.selectOnDown = next;
            usable[i].navigation = nav;
        }
    }

    public void OnEquipClicked() { subPanel.SetActive(true); FocusSubMenu(); }
    public void OnSwapClicked() { subPanel.SetActive(true); FocusSubMenu(); }

    private void FocusSubMenu()
    {
        if (Gamepad.current != null && primaryButton != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(primaryButton);
        }
    }

    public void OnReturnClicked()
    {
        if (_targetGem != null) _targetGem.ReturnToInventory();
        CloseMenu();

        if (EventSystem.current != null && _targetGem != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(_targetGem.gameObject);
        }
    }

    public void OnUnequipClicked()
    {
        if (_targetGem != null)
        {
            Transform rewardContainer = GameObject.Find("Reward_Gem_Container").transform;
            _targetGem.parentAfterDrag = rewardContainer;
            _targetGem.transform.SetParent(rewardContainer);

            if (RewardMenuManager.Instance != null && RewardMenuManager.Instance.IsRewardModeActive())
                RewardMenuManager.Instance.OnGemReturned(_targetGem);

            if (RewardMenuManager.Instance != null)
            {
                RectTransform gemRect = _targetGem.GetComponent<RectTransform>();
                gemRect.anchoredPosition = RewardMenuManager.Instance.CalculateSafeScatterPoint(gemRect);
            }

            _targetGem.ReturnToInventory();

            if (InventoryManager.Instance != null)
                InventoryManager.Instance.AnimateSingleGemDrop(_targetGem);
        }

        CloseMenu();

        if (EventSystem.current != null && _targetGem != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(_targetGem.gameObject);
        }
    }

    public void OnPrimaryClicked() { TryEquipToGauntlet(primaryGauntlet); }
    public void OnSecondaryClicked() { TryEquipToGauntlet(secondaryGauntlet); }

    private void TryEquipToGauntlet(Transform gauntletParent)
    {
        if (gauntletParent == null) { CloseMenu(); return; }

        if (subPanel != null) subPanel.SetActive(false);

        // Only flip the page when the chosen gauntlet is not the one already on screen. Picking
        // Primary while looking at Primary should drop you straight into placement.
        if (gauntletParent.gameObject.activeInHierarchy)
        {
            BeginPlacement(gauntletParent);
            return;
        }

        if (InventoryManager.Instance == null) { CloseMenu(); return; }

        InventoryManager.Instance.ToggleEquippedGauntletDisplay();
        StartCoroutine(BeginPlacementAfterPageSwitch(gauntletParent));
    }

    /// <summary>
    /// Waits for the page slide before entering placement mode.
    ///
    /// ToggleEquippedGauntletDisplay finishes by calling SetupNavigation and FocusTitle, which
    /// yanks controller focus to the header. Starting placement before that lands left the gem
    /// carried but the focus on the title bar, with no way to reach a slot.
    /// </summary>
    private IEnumerator BeginPlacementAfterPageSwitch(Transform gauntletParent)
    {
        while (InventoryManager.Instance != null && InventoryManager.Instance.IsTransitioning)
        {
            yield return null;
        }

        BeginPlacement(gauntletParent);
    }

    private void BeginPlacement(Transform gauntletParent)
    {
        // Include inactive: the page may still be settling when we look it up.
        GauntletManager gauntlet = gauntletParent.GetComponentInChildren<GauntletManager>(true);
        if (gauntlet == null) { CloseMenu(); return; }

        StartPlacementMode(gauntlet);
    }

    // --- THE GEM-CENTRIC PLACEMENT FIX ---
    private void StartPlacementMode(GauntletManager gauntlet)
    {
        _isPlacingMode = true;

        // Placement cannot be confirmed until the button that started it is released.
        _placementConfirmArmed = false;

        _originalGemParent = _targetGem.transform.parent;

        unslottedPanel.SetActive(false);
        slottedPanel.SetActive(false);
        subPanel.SetActive(false);

        _targetGem.transform.SetParent(transform.parent);
        _targetGem.transform.SetAsLastSibling();

        CanvasGroup cg = _targetGem.GetComponent<CanvasGroup>();
        if (cg != null) { cg.alpha = 0.6f; cg.blocksRaycasts = false; }

        GameObject firstValidSlot = null;
        List<Selectable> placementSlots = new List<Selectable>();

        // --- WAKE UP SLOTS BASED ON GEM TYPE ---
        if (_targetGem.LinkedGemData.gemType == GemType.Stat)
        {
            // Only wake up the finger slots this gauntlet has actually unlocked. The sealed ones
            // past currentActiveSlots stay active so they can show their cross, so checking
            // activeInHierarchy alone let the player drop gems into locked slots.
            for (int i = 0; i < gauntlet.fingerSlots.Count && i < gauntlet.currentActiveSlots; i++)
            {
                GameObject slot = gauntlet.fingerSlots[i];
                if (slot == null || !slot.activeInHierarchy) continue;

                Button slotBtn = slot.GetComponent<Button>();
                if (slotBtn != null)
                {
                    slotBtn.interactable = true; // Make controller see it!
                    placementSlots.Add(slotBtn);
                }

                if (firstValidSlot == null) firstValidSlot = slot;
            }
        }
        else if (_targetGem.LinkedGemData.gemType == GemType.Skill)
        {
            // Only wake up the Ultimate slot
            if (gauntlet.SkillSlot != null && gauntlet.SkillSlot.activeInHierarchy)
            {
                Button slotBtn = gauntlet.SkillSlot.GetComponent<Button>();
                if (slotBtn != null)
                {
                    slotBtn.interactable = true;
                    placementSlots.Add(slotBtn);
                }

                firstValidSlot = gauntlet.SkillSlot;
            }
        }

        WirePlacementNavigation(placementSlots);

        // Start the cursor on the slot the gem came from, so the player moves out from its own
        // position rather than being teleported to slot 1 every time.
        GameObject startSlot = firstValidSlot;
        if (_originalGemParent != null)
        {
            foreach (Selectable slot in placementSlots)
            {
                if (slot.gameObject == _originalGemParent.gameObject)
                {
                    startSlot = slot.gameObject;
                    break;
                }
            }
        }

        // Snap controller focus to the slot we chose
        if (Gamepad.current != null && startSlot != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(startSlot);
        }
    }

    /// <summary>
    /// Links the woken slots into a loop so the D-pad can walk along them.
    ///
    /// InventoryManager.SetupNavigation clears every slot's navigation to Mode.None and only
    /// rewires the ones it considers selectable, which skips occupied slots. Without rebuilding it
    /// here the carried gem had nowhere to move to and the placement cursor sat still.
    /// </summary>
    private static void WirePlacementNavigation(List<Selectable> slots)
    {
        int count = slots.Count;
        if (count == 0) return;

        for (int i = 0; i < count; i++)
        {
            Selectable previous = slots[(i == 0) ? count - 1 : i - 1];
            Selectable next = slots[(i == count - 1) ? 0 : i + 1];

            slots[i].navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnLeft = previous,
                selectOnUp = previous,
                selectOnRight = next,
                selectOnDown = next,
            };
        }
    }

    private void EndPlacementMode()
    {
        _isPlacingMode = false;
        CanvasGroup cg = _targetGem.GetComponent<CanvasGroup>();
        if (cg != null) { cg.alpha = 1f; cg.blocksRaycasts = true; }

        // PUT THE SLOTS BACK TO SLEEP!
        if (InventoryManager.Instance != null)
        {
            DisableSlots(InventoryManager.Instance.primaryGauntlet);
            DisableSlots(InventoryManager.Instance.secondaryGauntlet);
        }

        // Navigation is deliberately NOT rebuilt here. This runs before the gems are re-parented,
        // so it would map the board as it was mid-placement and leave a slot unreachable. The
        // callers rebuild it once the gems have actually landed.
    }

    /// <summary>
    /// Rebuilds the inventory's gem-to-gem navigation. Must be called AFTER any re-parenting,
    /// because SetupNavigation reads which slot holds which gem to build the loop.
    /// </summary>
    private static void RefreshInventoryNavigation()
    {
        if (InventoryManager.Instance != null) InventoryManager.Instance.SetupNavigation();
    }

    /// <summary>
    /// The gem sitting directly in a slot, or null.
    ///
    /// Slots can hold other children (highlights and the like), so indexing GetChild(0) and hoping
    /// it was the gem silently found nothing and skipped the swap.
    /// </summary>
    private static DraggableGem FindGemIn(Transform slot)
    {
        if (slot == null) return null;

        DraggableGem gem = slot.GetComponentInChildren<DraggableGem>(true);
        return (gem != null && gem.transform.parent == slot) ? gem : null;
    }

    private void DisableSlots(Transform gauntletParent)
    {
        if (gauntletParent == null) return;
        GauntletManager gm = gauntletParent.GetComponentInChildren<GauntletManager>();
        if (gm != null)
        {
            // Put fingers to sleep
            foreach (GameObject slot in gm.fingerSlots)
            {
                Button btn = slot.GetComponent<Button>();
                if (btn != null) btn.interactable = false;
            }

            // Put ultimate slot to sleep
            if (gm.SkillSlot != null)
            {
                Button btn = gm.SkillSlot.GetComponent<Button>();
                if (btn != null) btn.interactable = false;
            }
        }
    }

    /// <summary>Slides the carried gem to follow the mouse, or the currently focused slot.</summary>
    private void MoveCarriedGem()
    {
        if (_targetGem == null) return;

        if (Mouse.current != null && Mouse.current.delta.ReadValue().sqrMagnitude > 2.0f)
        {
            Vector3 mousePos = Mouse.current.position.ReadValue();
            _targetGem.transform.position = Vector3.Lerp(_targetGem.transform.position, mousePos + new Vector3(40f, -40f, 0f), Time.unscaledDeltaTime * 25f);
        }
        else if (Gamepad.current != null && EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
        {
            GameObject currentSel = EventSystem.current.currentSelectedGameObject;
            if (currentSel.name.Contains("Slot"))
            {
                Vector3 targetPos = currentSel.transform.position + new Vector3(40f, -40f, 0f);
                _targetGem.transform.position = Vector3.Lerp(_targetGem.transform.position, targetPos, Time.unscaledDeltaTime * 15f);
            }
        }
    }

    private void HandlePlacementMode()
    {
        // The press that chose Primary/Secondary is often still held on the frame placement starts,
        // and the confirm checks below would consume it immediately - dropping the gem back on the
        // slot it came from before the player could move. Wait for a clean release first.
        if (!_placementConfirmArmed)
        {
            bool southHeld = Gamepad.current != null && Gamepad.current.buttonSouth.isPressed;
            bool clickHeld = Mouse.current != null && Mouse.current.leftButton.isPressed;
            bool submitHeld = Keyboard.current != null &&
                              (Keyboard.current.enterKey.isPressed || Keyboard.current.spaceKey.isPressed);

            if (!southHeld && !clickHeld && !submitHeld) _placementConfirmArmed = true;

            MoveCarriedGem();
            return;
        }

        MoveCarriedGem();

        if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)
        {
            GameObject currentSel = EventSystem.current.currentSelectedGameObject;
            if (currentSel != null)
            {
                if (currentSel.name.Contains("Slot")) ConfirmPlacement(currentSel.transform);
                else if (currentSel.GetComponent<DraggableGem>() != null && currentSel.GetComponent<DraggableGem>().IsEquipped())
                    ConfirmPlacement(currentSel.transform.parent);
            }
        }

        // ... Keep existing Mouse Raycast confirmation ...
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            PointerEventData pointerData = new PointerEventData(EventSystem.current) { position = Mouse.current.position.ReadValue() };
            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerData, results);

            bool clickedValidSlot = false;
            foreach (RaycastResult result in results)
            {
                if (result.gameObject.name.Contains("Slot") && result.gameObject.GetComponent<Button>().interactable) { ConfirmPlacement(result.gameObject.transform); clickedValidSlot = true; break; }
                DraggableGem clickedGem = result.gameObject.GetComponent<DraggableGem>();
                if (clickedGem != null && clickedGem.IsEquipped()) { ConfirmPlacement(clickedGem.transform.parent); clickedValidSlot = true; break; }
            }
            if (!clickedValidSlot) CancelPlacement();
        }

        if ((Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame) ||
            (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame))
        {
            CancelPlacement();
        }
    }

    /// <summary>
    /// Drops the carried gem into a slot. If that slot is occupied the two gems trade places:
    /// the occupant moves to the slot the carried gem came from.
    ///
    /// This used to hand the displaced gem back to the player to re-place, which turned a swap
    /// into an open-ended chain and never actually swapped anything.
    /// </summary>
    private void ConfirmPlacement(Transform targetSlot)
    {
        DraggableGem displacedGem = FindGemIn(targetSlot);

        // Where the carried gem came from. Null when it was picked up off the reward board.
        Transform sourceSlot = _originalGemParent;

        Debug.Log($"[GemPopupMenu] Placing '{_targetGem.name}' into '{targetSlot.name}' " +
                  $"from '{(sourceSlot != null ? sourceSlot.name : "board")}', " +
                  $"displacing '{(displacedGem != null ? displacedGem.name : "nothing")}'.");

        EndPlacementMode(); // Visually restore gem AND disable slots!

        // Dropping a gem back on the slot it already occupied is a no-op, not a swap with itself.
        if (displacedGem == _targetGem) displacedGem = null;

        PlaceGemInSlot(_targetGem, targetSlot);

        if (RewardMenuManager.Instance != null && RewardMenuManager.Instance.IsRewardModeActive())
            RewardMenuManager.Instance.OnGemSlotted(_targetGem);

        if (displacedGem != null)
        {
            if (IsGauntletSlot(sourceSlot))
            {
                // A real swap: the occupant takes the carried gem's old home.
                PlaceGemInSlot(displacedGem, sourceSlot);
            }
            else
            {
                // The carried gem came off the board, so there is no slot to swap into and the
                // occupant has to go back to the board instead.
                SendGemToBoard(displacedGem);
            }
        }

        // Both gems have moved, so the equipped stat totals may have changed.
        PlayerStatsManager stats = FindFirstObjectByType<PlayerStatsManager>();
        if (stats != null && InventoryManager.Instance != null)
        {
            stats.SyncWithUI(InventoryManager.Instance.primaryGauntlet,
                             InventoryManager.Instance.secondaryGauntlet);
        }

        CloseMenu();

        // Now that both gems have landed, rebuild the loop so every occupied slot is reachable.
        RefreshInventoryNavigation();

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(_targetGem.gameObject); // Focus the newly slotted gem
        }
    }

    /// <summary>Parents a gem into a slot and sizes it to fit.</summary>
    private void PlaceGemInSlot(DraggableGem gem, Transform slot)
    {
        if (gem == null || slot == null) return;

        gem.parentAfterDrag = slot;
        gem.transform.SetParent(slot);

        RectTransform gemRect = gem.GetComponent<RectTransform>();
        RectTransform slotRect = slot.GetComponent<RectTransform>();
        if (gemRect != null && slotRect != null) gemRect.sizeDelta = slotRect.rect.size;

        gem.AnimateToNewHome();
    }

    /// <summary>Evicts a gem back to the reward board, scattered so it does not land on a pile.</summary>
    private void SendGemToBoard(DraggableGem gem)
    {
        if (gem == null) return;

        GameObject container = GameObject.Find("Reward_Gem_Container");
        if (container == null) return;

        gem.parentAfterDrag = container.transform;
        gem.transform.SetParent(container.transform);

        RectTransform gemRect = gem.GetComponent<RectTransform>();
        if (gemRect != null && RewardMenuManager.Instance != null)
        {
            gemRect.anchoredPosition = RewardMenuManager.Instance.CalculateSafeScatterPoint(gemRect);
        }

        gem.ReturnToInventory();

        if (RewardMenuManager.Instance != null && RewardMenuManager.Instance.IsRewardModeActive())
            RewardMenuManager.Instance.OnGemReturned(gem);

        if (InventoryManager.Instance != null) InventoryManager.Instance.AnimateSingleGemDrop(gem);
    }

    /// <summary>True if this transform is an actual gauntlet socket rather than the reward board.</summary>
    private static bool IsGauntletSlot(Transform candidate)
    {
        if (candidate == null) return false;

        return candidate.GetComponent<GemDropSlot>() != null ||
               candidate.GetComponent<SkillSlotManager>() != null;
    }

    private void CancelPlacement()
    {
        EndPlacementMode();

        if (_originalGemParent != null)
        {
            _targetGem.transform.SetParent(_originalGemParent);
            _targetGem.transform.localPosition = Vector3.zero;
            RefreshInventoryNavigation();
            OpenMenu(_targetGem, _targetGem.GetComponent<RectTransform>());
        }
        else
        {
            _targetGem.ReturnToInventory();
            CloseMenu();
            RefreshInventoryNavigation();

            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
                EventSystem.current.SetSelectedGameObject(_targetGem.gameObject);
            }
        }
    }
}