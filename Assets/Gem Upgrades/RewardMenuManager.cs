using DG.Tweening;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Localization;

public class RewardMenuManager : MonoBehaviour
{
    private static RewardMenuManager _instance;
    public static RewardMenuManager Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindFirstObjectByType<RewardMenuManager>(FindObjectsInactive.Include);
            return _instance;
        }
    }

    [Header("Localized Strings")]
    public LocalizedString warningDiscardOverflowString;
    public LocalizedString warningConfirmSelectionString;
    public LocalizedString warningLeaveNoRewardString;

    [Header("Screen Configuration")]
    [SerializeField] private GameObject characterScreenRoot;
    [SerializeField] public RectTransform mainPaperPlate;     // Drag the brown center background plate here
    [SerializeField] private RectTransform titleBannerHolder;  // Drag the top Title/Banner container here
    [SerializeField] private RectTransform switchGauntletBtn;  // Drag the upper right swap button here

    [Header("Reward Processing Pool")]
    [SerializeField] private Transform gemSpawnAreaRoot;      // Drag the parent Canvas container where gems can float freely
    [SerializeField] public List<GameObject> allGemPrefabs;

    [Header("Description Box Anchor Panel")]
    [SerializeField] private CanvasGroup descriptionPanelGroup; // Drag your new bottom-right anchor group here
    [SerializeField] private TMP_Text rewardNameText;
    [SerializeField] private TMP_Text rewardDescText;
    [SerializeField] private Image rewardIcon;

    [Header("Warning Panels")]
    [SerializeField] private GameObject warningPanel;
    [SerializeField] private GameObject warningCancelButton;
    [SerializeField] private TMP_Text warningBodyText;

    [Header("Cleanup References")]
    [SerializeField] private GameObject expBarRoot;
    [SerializeField] private InputActionReference lookAction;

    private GameObject _lastSelectedReward;
    private bool _isWarningActive = false;
    private DraggableGem _currentlySlottedGem;
    private bool _isOverflowMode = false;
    private bool _isCurrentlyRewardPhase = false;

    private List<DraggableGem> _activeRewardGems = new List<DraggableGem>();

    private void Awake()
    {
        if (_instance == null) _instance = this;
        if (descriptionPanelGroup != null) descriptionPanelGroup.alpha = 0f; // Keep hidden until hover
    }

    public bool IsRewardModeActive()
    {
        return characterScreenRoot != null && characterScreenRoot.activeSelf && _isCurrentlyRewardPhase;
    }

    public bool CanDragGem(DraggableGem gem)
    {
        if (_isOverflowMode) return true;
        if (!_activeRewardGems.Contains(gem)) return true;
        return _currentlySlottedGem == null || _currentlySlottedGem == gem;
    }

    public void OnGemSlotted(DraggableGem gem)
    {
        if (!_activeRewardGems.Contains(gem)) return;
        _currentlySlottedGem = gem;
        UpdateGemInteractability(false);
    }

    public void OnGemReturned(DraggableGem gem)
    {
        if (!_activeRewardGems.Contains(gem)) return;

        if (_currentlySlottedGem == gem)
        {
            _currentlySlottedGem = null;
            UpdateGemInteractability(true);
        }
    }

    private void UpdateGemInteractability(bool isInteractable)
    {
        if (_isOverflowMode) return;

        foreach (DraggableGem gem in _activeRewardGems)
        {
            if (gem == null) continue;

            // Don't dim the gem we just chose!
            if (gem == _currentlySlottedGem) continue;

            Button btn = gem.GetComponent<Button>();
            if (btn != null) btn.interactable = isInteractable;

            CanvasGroup cg = gem.GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = isInteractable ? 1f : 0.4f;
        }
    }

    public void OpenRewardMenu()
    {
        _isCurrentlyRewardPhase = true; // Set flag
        _isOverflowMode = false;
        SetupMenuExecution();
        GenerateScatterRewards(3);
    }

    public void OpenOverflowMenu(List<GameObject> overflowGemPrefabs)
    {
        _isCurrentlyRewardPhase = true;
        _isOverflowMode = true;
        SetupMenuExecution();

        foreach (Transform child in gemSpawnAreaRoot) { child.DOKill(); Destroy(child.gameObject); }

        List<Button> spawnedButtons = new List<Button>();
        GameObject firstSpawnedGem = null;

        foreach (GameObject gemPrefab in overflowGemPrefabs)
        {
            GameObject spawnedGem = SpawnAndScatterGem(gemPrefab);
            if (firstSpawnedGem == null) firstSpawnedGem = spawnedGem;

            Button btn = spawnedGem.GetComponent<Button>();
            if (btn != null) spawnedButtons.Add(btn);
        }

        TrapControllerFocus(spawnedButtons);
        InitializeFocusState(firstSpawnedGem);
    }

    private void SetupMenuExecution()
    {
        Time.timeScale = 0f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (lookAction != null) lookAction.action.Disable();

        characterScreenRoot.SetActive(true);
        if (expBarRoot != null) expBarRoot.SetActive(false);
        if (warningPanel != null) warningPanel.SetActive(false);
        if (descriptionPanelGroup != null) descriptionPanelGroup.alpha = 0f;

        if (gemSpawnAreaRoot != null && gemSpawnAreaRoot.TryGetComponent<Image>(out var bgImage))
        {
            bgImage.raycastTarget = true;
        }

        _isWarningActive = false;
        _currentlySlottedGem = null;
        _activeRewardGems.Clear();

        if (warningBodyText != null)
        {
            warningBodyText.text = _isOverflowMode 
                ? warningDiscardOverflowString.GetLocalizedString() 
                : warningLeaveNoRewardString.GetLocalizedString();
        }
    }

    private void GenerateScatterRewards(int count)
    {
        foreach (Transform child in gemSpawnAreaRoot) { child.DOKill(); Destroy(child.gameObject); }

        List<GameObject> chosenGems = new List<GameObject>();
        int maxRewards = Mathf.Min(count, allGemPrefabs.Count);

        while (chosenGems.Count < maxRewards)
        {
            GameObject randomGem = allGemPrefabs[Random.Range(0, allGemPrefabs.Count)];
            if (!chosenGems.Contains(randomGem)) chosenGems.Add(randomGem);
        }

        List<Button> spawnedButtons = new List<Button>();
        GameObject firstSpawnedGem = null;
        List<GameObject> gemsForAnimation = new List<GameObject>(); // --- HOOK FOR DROPS ---

        foreach (GameObject gemPrefab in chosenGems)
        {
            GameObject spawnedGem = SpawnAndScatterGem(gemPrefab);
            if (firstSpawnedGem == null) firstSpawnedGem = spawnedGem;

            gemsForAnimation.Add(spawnedGem); // Add to sequence trackers

            Button btn = spawnedGem.GetComponent<Button>();
            if (btn != null) spawnedButtons.Add(btn);
        }

        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.AnimateGemArrivalDrops(gemsForAnimation);
        }


        TrapControllerFocus(spawnedButtons);
        InitializeFocusState(firstSpawnedGem);
    }

    private GameObject SpawnAndScatterGem(GameObject prefab)
    {
        GameObject spawnedGem = Instantiate(prefab, gemSpawnAreaRoot);
        DraggableGem dragScript = spawnedGem.GetComponent<DraggableGem>();
        if (dragScript != null)
        {
            dragScript.enabled = true;
            _activeRewardGems.Add(dragScript); 
        }

        // Configure standard event functions dynamically
        Button btn = spawnedGem.GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                if (Gamepad.current != null && Gamepad.current.wasUpdatedThisFrame && CanDragGem(dragScript) && GemPopupMenu.Instance != null)
                {
                    GemPopupMenu.Instance.OpenMenu(dragScript, spawnedGem.GetComponent<RectTransform>());
                }
            });
        }

        EventTrigger trigger = spawnedGem.GetComponent<EventTrigger>();
        if (trigger == null) trigger = spawnedGem.AddComponent<EventTrigger>();
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        entry.callback.AddListener((data) => { UpdateSmallDescription(spawnedGem); });
        trigger.triggers.Add(entry);

        // --- EXCLUSIVE DROP SHIELD MATH ---
        RectTransform gemRect = spawnedGem.GetComponent<RectTransform>();
        Vector2 safePosition = CalculateSafeScatterPoint(gemRect);
        gemRect.anchoredPosition = safePosition;

        return spawnedGem;
    }

    public Vector2 CalculateSafeScatterPoint(RectTransform gemRect)
    {
        RectTransform spawnAreaCanvas = gemSpawnAreaRoot.GetComponent<RectTransform>();

        int maxAttempts = 50; // --- NEW: Safety limit ---
        int currentAttempt = 0;

        float halfWidth = (mainPaperPlate.rect.width / 2f) - 60f;
        float halfHeight = (mainPaperPlate.rect.height / 2f) - 60f;

        while (currentAttempt < maxAttempts)
        {
            currentAttempt++;
            // 1. Pick a random point relative to the Paper Plate
            float randX = Random.Range(-halfWidth, halfWidth);
            float randY = Random.Range(-halfHeight, halfHeight);

            // 2. Convert to screen space, then back to the gem canvas space
            Vector3 worldPoint = mainPaperPlate.TransformPoint(new Vector3(randX, randY, 0));
            Vector2 testPoint = spawnAreaCanvas.InverseTransformPoint(worldPoint);

            // 3. Avoid the Top Banner and Swap Button
            if (IsPointInsideBlocker(testPoint, titleBannerHolder, spawnAreaCanvas)) continue;
            if (IsPointInsideBlocker(testPoint, switchGauntletBtn, spawnAreaCanvas)) continue;

            // 4.Avoid the Gauntlets! 
            if (InventoryManager.Instance != null)
            {
                if (IsPointInsideBlocker(testPoint, InventoryManager.Instance.primaryGauntlet.GetComponent<RectTransform>(), spawnAreaCanvas)) continue;
                if (IsPointInsideBlocker(testPoint, InventoryManager.Instance.secondaryGauntlet.GetComponent<RectTransform>(), spawnAreaCanvas)) continue;
            }
            if (descriptionPanelGroup != null)
            {
                if (IsPointInsideBlocker(testPoint, descriptionPanelGroup.GetComponent<RectTransform>(), spawnAreaCanvas)) continue;
            }

            return testPoint;
        }

        // Fallback: slightly offset so they don't perfectly stack
        float spreadX = mainPaperPlate.rect.width * 0.4f;
        float spreadY = mainPaperPlate.rect.height * 0.4f;
        return new Vector2(Random.Range(-150, -50), Random.Range(-100, 100));
    }

    private bool IsPointInsideBlocker(Vector2 targetPoint, RectTransform blocker, RectTransform canvas)
    {
        if (blocker == null) return false;

        // Translate the blocker's world boundaries down into local canvas space matching the gems
        Vector3[] blockerCorners = new Vector3[4];
        blocker.GetWorldCorners(blockerCorners);

        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;

        for (int i = 0; i < 4; i++)
        {
            Vector2 localPos = canvas.InverseTransformPoint(blockerCorners[i]);
            if (localPos.x < minX) minX = localPos.x;
            if (localPos.x > maxX) maxX = localPos.x;
            if (localPos.y < minY) minY = localPos.y;
            if (localPos.y > maxY) maxY = localPos.y;
        }

        // Add a 30 pixel safety margin buffer around the edges of the central gauntlet board
        return (targetPoint.x >= minX - 30f && targetPoint.x <= maxX + 30f &&
                targetPoint.y >= minY - 30f && targetPoint.y <= maxY + 30f);
    }

    private void TrapControllerFocus(List<Button> spawnedButtons)
    {
        if (spawnedButtons.Count <= 0) return;
        for (int i = 0; i < spawnedButtons.Count; i++)
        {
            Navigation nav = new Navigation { mode = Navigation.Mode.Explicit };
            nav.selectOnLeft = spawnedButtons[(i == 0) ? spawnedButtons.Count - 1 : i - 1];
            nav.selectOnRight = spawnedButtons[(i == spawnedButtons.Count - 1) ? 0 : i + 1];
            spawnedButtons[i].navigation = nav;
        }
    }

    private void InitializeFocusState(GameObject target)
    {
        if (Gamepad.current != null && target != null && EventSystem.current != null)
        {
            StartCoroutine(SetFocusDelayed(target));
        }
    }

    private System.Collections.IEnumerator SetFocusDelayed(GameObject target)
    {
        yield return null;
        if (EventSystem.current != null && target != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(target);
        }
    }

    private void Update()
    {
        if (!characterScreenRoot.activeSelf) return;

        if (!_isCurrentlyRewardPhase) return;

        if (GemPopupMenu.Instance != null && GemPopupMenu.Instance.gameObject.activeInHierarchy) return;

        if (!_isWarningActive && EventSystem.current != null)
        {
            GameObject currentSel = EventSystem.current.currentSelectedGameObject;
            if (currentSel != null && currentSel != _lastSelectedReward && currentSel.transform.parent == gemSpawnAreaRoot)
            {
                _lastSelectedReward = currentSel;
                UpdateSmallDescription(_lastSelectedReward);
            }
        }

        bool cancelPressed = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
                             (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);

        if (cancelPressed)
        {
            if (_isWarningActive) CancelClose();
            else ShowWarning();
        }
    }

    private void UpdateSmallDescription(GameObject highlightedGem)
    {
        DraggableGem gemData = highlightedGem.GetComponent<DraggableGem>();
        if (gemData != null)
        {
            rewardNameText.text = gemData.gemName;
            rewardDescText.text = gemData.gemDescription;
            if (rewardIcon != null)
            {
                rewardIcon.sprite = gemData.gemIcon;
                rewardIcon.color = Color.white;
            }
            if (descriptionPanelGroup != null) descriptionPanelGroup.alpha = 1f; // Fade in visibility
        }
    }

    public void ShowWarning()
    {
        _isWarningActive = true;
        warningPanel.SetActive(true);

        if (warningBodyText != null)
        {
            if (_isOverflowMode)
                warningBodyText.text = warningDiscardOverflowString.GetLocalizedString();
            else if (_currentlySlottedGem != null)
                warningBodyText.text = warningConfirmSelectionString.GetLocalizedString();
            else
                warningBodyText.text = warningLeaveNoRewardString.GetLocalizedString();
        }

        if (Gamepad.current != null && warningCancelButton != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(warningCancelButton);
        }
    }

    public void CancelClose()
    {
        _isWarningActive = false;
        warningPanel.SetActive(false);

        if (Gamepad.current != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            if (_currentlySlottedGem != null) EventSystem.current.SetSelectedGameObject(_currentlySlottedGem.gameObject);
            else if (_lastSelectedReward != null) EventSystem.current.SetSelectedGameObject(_lastSelectedReward);
        }
    }

    public void ConfirmClose()
    {
        _isWarningActive = false;
        warningPanel.SetActive(false);
        PlayerStatsManager statsManager = FindFirstObjectByType<PlayerStatsManager>();
        if (statsManager != null && InventoryManager.Instance != null)
        {
            statsManager.SyncWithUI(InventoryManager.Instance.primaryGauntlet, InventoryManager.Instance.secondaryGauntlet);
        }
        CloseRewardMenu();
    }

    private void CloseRewardMenu()
    {
        _isCurrentlyRewardPhase = false;
        characterScreenRoot.SetActive(false);
        if (expBarRoot != null) expBarRoot.SetActive(true);
        if (descriptionPanelGroup != null) descriptionPanelGroup.alpha = 0f;

        if (gemSpawnAreaRoot != null && gemSpawnAreaRoot.TryGetComponent<Image>(out var bgImage))
        {
            bgImage.raycastTarget = false;

        }

        if (gemSpawnAreaRoot != null)
        {
            foreach (Transform child in gemSpawnAreaRoot)
            {
                child.DOKill(); // Stop animations before destroying
                Destroy(child.gameObject);
            }
        }

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        if (lookAction != null) lookAction.action.Enable();

        Time.timeScale = 1f;
    }
}