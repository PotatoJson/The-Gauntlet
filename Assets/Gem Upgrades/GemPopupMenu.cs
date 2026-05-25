using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
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
    private Transform _originalGemParent;
    private DraggableGem _displacedGem;

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
        if (!gauntletParent.gameObject.activeInHierarchy)
        {
            if (InventoryManager.Instance != null) InventoryManager.Instance.ToggleEquippedGauntletDisplay();
        }

        GauntletManager gauntlet = gauntletParent.GetComponentInChildren<GauntletManager>();
        if (gauntlet == null) { CloseMenu(); return; }

        StartPlacementMode(gauntlet);
    }

    // --- THE GEM-CENTRIC PLACEMENT FIX ---
    private void StartPlacementMode(GauntletManager gauntlet)
    {
        _isPlacingMode = true;
        _originalGemParent = _targetGem.transform.parent;

        unslottedPanel.SetActive(false);
        slottedPanel.SetActive(false);
        subPanel.SetActive(false);

        _targetGem.transform.SetParent(transform.parent);
        _targetGem.transform.SetAsLastSibling();

        CanvasGroup cg = _targetGem.GetComponent<CanvasGroup>();
        if (cg != null) { cg.alpha = 0.6f; cg.blocksRaycasts = false; }

        GameObject firstValidSlot = null;

        // --- WAKE UP SLOTS BASED ON GEM TYPE ---
        if (_targetGem.LinkedGemData.gemType == GemType.Stat)
        {
            // Only wake up the finger slots
            foreach (GameObject slot in gauntlet.fingerSlots)
            {
                if (slot.activeInHierarchy)
                {
                    Button slotBtn = slot.GetComponent<Button>();
                    if (slotBtn != null) slotBtn.interactable = true; // Make controller see it!
                    if (firstValidSlot == null) firstValidSlot = slot;
                }
            }
        }
        else if (_targetGem.LinkedGemData.gemType == GemType.Skill)
        {
            // Only wake up the Ultimate slot
            if (gauntlet.SkillSlot != null && gauntlet.SkillSlot.activeInHierarchy)
            {
                Button slotBtn = gauntlet.SkillSlot.GetComponent<Button>();
                if (slotBtn != null) slotBtn.interactable = true;
                firstValidSlot = gauntlet.SkillSlot;
            }
        }

        // Snap controller focus to the first valid slot we found
        if (Gamepad.current != null && firstValidSlot != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(firstValidSlot);
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

    private void HandlePlacementMode()
    {
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

    private void ConfirmPlacement(Transform targetSlot)
    {
        DraggableGem swappedGem = null;
        if (targetSlot.childCount > 0) swappedGem = targetSlot.GetChild(0).GetComponent<DraggableGem>();

        EndPlacementMode(); // Visually restore gem AND disable slots!

        _targetGem.parentAfterDrag = targetSlot;
        _targetGem.transform.SetParent(targetSlot);

        RectTransform gemRect = _targetGem.GetComponent<RectTransform>();
        RectTransform slotRect = targetSlot.GetComponent<RectTransform>();
        if (gemRect != null && slotRect != null) { gemRect.sizeDelta = slotRect.rect.size; }

        _targetGem.AnimateToNewHome();

        if (RewardMenuManager.Instance != null && RewardMenuManager.Instance.IsRewardModeActive())
            RewardMenuManager.Instance.OnGemSlotted(_targetGem);

        if (swappedGem != null)
        {
            _displacedGem = swappedGem;
            _targetGem = swappedGem;
            _originalGemParent = null;

            // Restart placement mode for the displaced gem (wakes slots back up!)
            GauntletManager currentGauntlet = targetSlot.parent.GetComponent<GauntletManager>();
            StartPlacementMode(currentGauntlet);
        }
        else
        {
            _displacedGem = null;
            CloseMenu();
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
                EventSystem.current.SetSelectedGameObject(_targetGem.gameObject); // Focus the newly slotted gem
            }
        }
    }

    private void CancelPlacement()
    {
        EndPlacementMode();

        if (_originalGemParent != null)
        {
            _targetGem.transform.SetParent(_originalGemParent);
            _targetGem.transform.localPosition = Vector3.zero;
            OpenMenu(_targetGem, _targetGem.GetComponent<RectTransform>());
        }
        else if (_displacedGem != null)
        {
            // --- THE FIX: Revert the Swap! ---
            // If we canceled while holding the displaced gem, drop it to the board,
            // or return it to its rightful slot. (For safety, ARPG standard is to drop it to the board).
            Transform rewardContainer = GameObject.Find("Reward_Gem_Container").transform;
            _targetGem.parentAfterDrag = rewardContainer;
            _targetGem.transform.SetParent(rewardContainer);
            _targetGem.ReturnToInventory();

            CloseMenu();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(_targetGem.gameObject);
        }
        else
        {
            _targetGem.ReturnToInventory();
            CloseMenu();

            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
                EventSystem.current.SetSelectedGameObject(_targetGem.gameObject);
            }
        }
    }
}