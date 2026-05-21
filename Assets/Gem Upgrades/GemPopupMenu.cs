using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using UnityEngine.UI; // --- NEW: Needed for the Button component! ---

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

    // --- NEW: Reference to the actual Unequip button! ---
    [SerializeField] private Button unequipButton;

    [Header("Gauntlet Slot Containers")]
    [SerializeField] private Transform primaryGauntlet;
    [SerializeField] private Transform secondaryGauntlet;

    private DraggableGem _targetGem;
    private RectTransform _rectTransform;

    private bool _isPlacingMode = false;
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
        Vector3[] corners = new Vector3[4];
        _rectTransform.GetWorldCorners(corners);

        float screenWidth = Screen.width;
        float screenHeight = Screen.height;

        // Check right edge
        if (corners[2].x > screenWidth)
            _rectTransform.anchoredPosition -= new Vector2(corners[2].x - screenWidth + 20, 0);

        // Check bottom edge
        if (corners[0].y < 0)
            _rectTransform.anchoredPosition += new Vector2(0, Mathf.Abs(corners[0].y) + 20);

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

    public void OnEquipClicked()
    {
        subPanel.SetActive(true);
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

    public void OnSwapClicked()
    {
        subPanel.SetActive(true);
        if (Gamepad.current != null && primaryButton != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(primaryButton);
        }
    }

    public void OnUnequipClicked()
    {
        if (_targetGem != null)
        {
            RewardMenuManager manager = RewardMenuManager.Instance;
            if (manager != null && manager.IsRewardModeActive())
            {
                Transform rewardContainer = GameObject.Find("Reward_Gem_Container").transform;
                _targetGem.transform.SetParent(rewardContainer);

                manager.OnGemReturned(_targetGem);

                /*if (InventoryManager.Instance != null)
                {
                    InventoryManager.Instance.AnimateSingleGemDrop(_targetGem);
                }*/
            }
            _targetGem.ReturnToInventory();
        }
        CloseMenu();

        // 3. Instead of focusing the slot (which is empty), focus the gem itself!
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
        // Add a check to ensure the gauntletParent is actually visible
        if (gauntletParent == null || !gauntletParent.gameObject.activeInHierarchy)
        {
            Debug.Log("Gauntlet not visible!");
            return;
        }

        GauntletManager gauntlet = gauntletParent.GetComponentInChildren<GauntletManager>();
        if (gauntlet == null) return;

        StartPlacementMode(gauntlet);
    }

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
        if (cg != null)
        {
            cg.alpha = 0.6f;
            cg.blocksRaycasts = false;
        }

        if (Gamepad.current != null)
        {
            foreach (GameObject slot in gauntlet.fingerSlots)
            {
                if (slot.activeInHierarchy && slot.transform.childCount == 0)
                {
                    if (EventSystem.current != null)
                    {
                        EventSystem.current.SetSelectedGameObject(null);
                        EventSystem.current.SetSelectedGameObject(slot);
                    }
                    break;
                }
            }
        }
    }

    private void HandlePlacementMode()
    {
        if (Mouse.current != null && Mouse.current.delta.ReadValue().sqrMagnitude > 0.1f)
        {
            Vector3 mousePos = Mouse.current.position.ReadValue();
            _targetGem.transform.position = Vector3.Lerp(_targetGem.transform.position, mousePos + new Vector3(40f, -40f, 0f), Time.unscaledDeltaTime * 25f);
        }
        else if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
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
            if (currentSel != null && currentSel.name.Contains("Slot") && currentSel.transform.childCount == 0)
            {
                ConfirmPlacement(currentSel.transform);
            }
        }

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            PointerEventData pointerData = new PointerEventData(EventSystem.current) { position = Mouse.current.position.ReadValue() };
            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerData, results);

            bool clickedValidSlot = false;
            foreach (RaycastResult result in results)
            {
                if (result.gameObject.name.Contains("Slot") && result.gameObject.transform.childCount == 0)
                {
                    ConfirmPlacement(result.gameObject.transform);
                    clickedValidSlot = true;
                    break;
                }
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
        RestoreGemVisuals();

        _targetGem.parentAfterDrag = targetSlot;
        _targetGem.transform.SetParent(targetSlot);

        RectTransform gemRect = _targetGem.GetComponent<RectTransform>();
        RectTransform slotRect = targetSlot.GetComponent<RectTransform>();
        if (gemRect != null && slotRect != null)
        {
            gemRect.sizeDelta = slotRect.rect.size;
        }

        _targetGem.AnimateToNewHome();

        if (RewardMenuManager.Instance != null && RewardMenuManager.Instance.IsRewardModeActive())
        {
            RewardMenuManager.Instance.OnGemSlotted(_targetGem);
        }

        CloseMenu();
    }

    private void CancelPlacement()
    {
        RestoreGemVisuals();

        _targetGem.transform.SetParent(_originalGemParent);
        _targetGem.transform.localPosition = Vector3.zero;

        OpenMenu(_targetGem, _targetGem.GetComponent<RectTransform>());
    }

    private void RestoreGemVisuals()
    {
        _isPlacingMode = false;

        CanvasGroup cg = _targetGem.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.alpha = 1f;
            cg.blocksRaycasts = true;
        }
    }
}