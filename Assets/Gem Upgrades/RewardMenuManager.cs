using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class RewardMenuManager : MonoBehaviour
{
    public static RewardMenuManager Instance { get; private set; }

    [Header("Screen References")]
    [SerializeField] private GameObject characterScreenRoot;
    [SerializeField] private GameObject leftSideGauntlets;
    [SerializeField] private GameObject rightSideDetails;
    [SerializeField] private GameObject rightSideRewards;

    [Header("Reward Generation")]
    [SerializeField] private Transform gemHolder;
    [SerializeField] private List<GameObject> allGemPrefabs;

    [Header("Small Description UI")]
    [SerializeField] private TMP_Text rewardNameText;
    [SerializeField] private TMP_Text rewardDescText;
    [SerializeField] private Image rewardIcon;

    [Header("Warning Dialog UI")]
    [SerializeField] private GameObject warningPanel;
    [SerializeField] private GameObject warningCancelButton;

    [Header("UI Text Overrides")]
    [SerializeField] private TMP_Text menuTitleText;
    [SerializeField] private TMP_Text warningBodyText;

    [Header("Navigation Anchors")]
    [SerializeField] private GameObject primaryTitleButton;
    [SerializeField] private GameObject secondaryTitleButton; // NEW: Added this so it knows both gauntlets!

    [Header("Cleanup References")]
    [SerializeField] private GameObject expBarRoot;
    [SerializeField] private InputActionReference lookAction;

    private GameObject _lastSelectedReward;
    private bool _isWarningActive = false;
    private DraggableGem _currentlySlottedGem;

    private bool _isOverflowMode = false;

    private void Awake()
    {
        Instance = this;
    }

    public bool IsRewardModeActive()
    {
        return rightSideRewards != null && rightSideRewards.activeSelf;
    }

    public bool CanDragGem(DraggableGem gem)
    {
        if (_isOverflowMode) return true;
        return _currentlySlottedGem == null || _currentlySlottedGem == gem;
    }

    public void OnGemSlotted(DraggableGem gem)
    {
        _currentlySlottedGem = gem;
    }

    public void OnGemReturned(DraggableGem gem)
    {
        if (_currentlySlottedGem == gem)
        {
            _currentlySlottedGem = null;
        }
    }

    public void OpenOverflowMenu(List<GameObject> overflowGemPrefabs)
    {
        Time.timeScale = 0f;

        characterScreenRoot.SetActive(true);
        leftSideGauntlets.SetActive(true);
        rightSideDetails.SetActive(false);
        rightSideRewards.SetActive(true);

        if (warningPanel != null) warningPanel.SetActive(false);
        _isWarningActive = false;
        _currentlySlottedGem = null;

        _isOverflowMode = true;

        if (menuTitleText != null) menuTitleText.text = "Gem Holder";
        if (warningBodyText != null) warningBodyText.text = "Discard unequipped gems permanently?";

        foreach (Transform child in gemHolder) { Destroy(child.gameObject); }

        GameObject firstSpawnedGem = null;
        List<Button> spawnedButtons = new List<Button>();

        foreach (GameObject gemPrefab in overflowGemPrefabs)
        {
            GameObject spawnedGem = Instantiate(gemPrefab, gemHolder);
            if (firstSpawnedGem == null) firstSpawnedGem = spawnedGem;

            DraggableGem dragScript = spawnedGem.GetComponent<DraggableGem>();
            if (dragScript != null) dragScript.enabled = true;

            Button btn = spawnedGem.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();

                btn.onClick.AddListener(() =>
                {
                    if (!CanDragGem(dragScript)) return;

                    if (GemPopupMenu.Instance != null)
                    {
                        GemPopupMenu.Instance.OpenMenu(dragScript, spawnedGem.GetComponent<RectTransform>());
                    }
                });

                spawnedButtons.Add(btn);
            }

            EventTrigger trigger = spawnedGem.GetComponent<EventTrigger>();
            if (trigger == null) trigger = spawnedGem.AddComponent<EventTrigger>();

            EventTrigger.Entry entry = new EventTrigger.Entry();
            entry.eventID = EventTriggerType.PointerEnter;
            entry.callback.AddListener((data) => { UpdateSmallDescription(spawnedGem); });
            trigger.triggers.Add(entry);
        }

        TrapControllerFocus(spawnedButtons);

        if (Gamepad.current != null && firstSpawnedGem != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(firstSpawnedGem);
        }
    }

    public void OpenRewardMenu()
    {
        Time.timeScale = 0f;

        characterScreenRoot.SetActive(true);
        leftSideGauntlets.SetActive(true);
        rightSideDetails.SetActive(false);
        rightSideRewards.SetActive(true);

        if (warningPanel != null) warningPanel.SetActive(false);
        _isWarningActive = false;
        _currentlySlottedGem = null;
        _isOverflowMode = false;

        if (menuTitleText != null) menuTitleText.text = "Choose a Reward";
        if (warningBodyText != null) warningBodyText.text = "Leave without taking a reward?";

        GenerateRewards();
    }

    private void GenerateRewards()
    {
        foreach (Transform child in gemHolder) { Destroy(child.gameObject); }

        List<GameObject> chosenGems = new List<GameObject>();
        int maxRewards = Mathf.Min(3, allGemPrefabs.Count);

        while (chosenGems.Count < maxRewards)
        {
            GameObject randomGem = allGemPrefabs[Random.Range(0, allGemPrefabs.Count)];
            if (!chosenGems.Contains(randomGem)) { chosenGems.Add(randomGem); }
        }

        GameObject firstSpawnedGem = null;
        List<Button> spawnedButtons = new List<Button>();

        foreach (GameObject gemPrefab in chosenGems)
        {
            GameObject spawnedGem = Instantiate(gemPrefab, gemHolder);
            if (firstSpawnedGem == null) firstSpawnedGem = spawnedGem;

            DraggableGem dragScript = spawnedGem.GetComponent<DraggableGem>();
            if (dragScript != null) dragScript.enabled = true;

            Button btn = spawnedGem.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();

                btn.onClick.AddListener(() =>
                {
                    if (!CanDragGem(dragScript)) return;

                    if (GemPopupMenu.Instance != null)
                    {
                        GemPopupMenu.Instance.OpenMenu(dragScript, spawnedGem.GetComponent<RectTransform>());
                    }
                });

                spawnedButtons.Add(btn);
            }

            EventTrigger trigger = spawnedGem.GetComponent<EventTrigger>();
            if (trigger == null) trigger = spawnedGem.AddComponent<EventTrigger>();

            EventTrigger.Entry entry = new EventTrigger.Entry();
            entry.eventID = EventTriggerType.PointerEnter;
            entry.callback.AddListener((data) => { UpdateSmallDescription(spawnedGem); });
            trigger.triggers.Add(entry);
        }

        TrapControllerFocus(spawnedButtons);

        if (Gamepad.current != null && firstSpawnedGem != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(firstSpawnedGem);
        }
    }

    private void TrapControllerFocus(List<Button> spawnedButtons)
    {
        if (spawnedButtons.Count > 0)
        {
            for (int i = 0; i < spawnedButtons.Count; i++)
            {
                Navigation nav = new Navigation();
                nav.mode = Navigation.Mode.Explicit;

                int leftIndex = (i == 0) ? spawnedButtons.Count - 1 : i - 1;
                int rightIndex = (i == spawnedButtons.Count - 1) ? 0 : i + 1;

                nav.selectOnLeft = spawnedButtons[leftIndex];
                nav.selectOnRight = spawnedButtons[rightIndex];

                spawnedButtons[i].navigation = nav;
            }
        }
    }

    private void Update()
    {
        if (rightSideRewards.activeSelf)
        {
            if (GemPopupMenu.Instance != null && GemPopupMenu.Instance.gameObject.activeInHierarchy) return;

            if (!_isWarningActive && EventSystem.current != null)
            {
                GameObject currentSel = EventSystem.current.currentSelectedGameObject;
                if (currentSel != null && currentSel != _lastSelectedReward && currentSel.transform.parent == gemHolder)
                {
                    _lastSelectedReward = currentSel;
                    UpdateSmallDescription(_lastSelectedReward);
                }
            }

            bool cancelPressed = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
                                 (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);

            if (cancelPressed)
            {
                if (_isWarningActive)
                {
                    CancelClose();
                }
                else
                {
                    GameObject currentSel = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;

                    if (currentSel != null)
                    {
                        // 1. If focused on a gem -> Step back to Primary Title
                        if (currentSel.transform.IsChildOf(gemHolder))
                        {
                            if (primaryTitleButton != null)
                            {
                                EventSystem.current.SetSelectedGameObject(null);
                                EventSystem.current.SetSelectedGameObject(primaryTitleButton);
                            }
                        }
                        // 2. THE FIX 1: If focused on a SLOT (after equipping!) -> Step back to the matching Gauntlet Title
                        else if (currentSel.name.Contains("Slot") && InventoryManager.Instance != null)
                        {
                            if (currentSel.transform.IsChildOf(InventoryManager.Instance.primaryGauntlet) && primaryTitleButton != null)
                            {
                                EventSystem.current.SetSelectedGameObject(null);
                                EventSystem.current.SetSelectedGameObject(primaryTitleButton);
                            }
                            else if (currentSel.transform.IsChildOf(InventoryManager.Instance.secondaryGauntlet) && secondaryTitleButton != null)
                            {
                                EventSystem.current.SetSelectedGameObject(null);
                                EventSystem.current.SetSelectedGameObject(secondaryTitleButton);
                            }
                            else
                            {
                                ShowWarning();
                            }
                        }
                        else
                        {
                            ShowWarning();
                        }
                    }
                    else
                    {
                        ShowWarning();
                    }
                }
            }
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
        }
    }

    public void ShowWarning()
    {
        _isWarningActive = true;
        warningPanel.SetActive(true);

        // THE FIX: Dynamically check the player's status right before the panel appears!
        if (warningBodyText != null)
        {
            if (_isOverflowMode)
            {
                warningBodyText.text = "Discard unequipped gems permanently?";
            }
            else if (_currentlySlottedGem != null)
            {
                // If they have successfully slotted a gem!
                warningBodyText.text = "Reward equipped! Ready to leave?";
            }
            else
            {
                // If they haven't picked anything yet!
                warningBodyText.text = "Leave without taking a reward?";
            }
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

        if (Gamepad.current != null && _lastSelectedReward != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(_lastSelectedReward);
        }
    }

    public void ConfirmClose()
    {
        _isWarningActive = false;
        warningPanel.SetActive(false);
        CloseRewardMenu();
    }

    private void CloseRewardMenu()
    {
        characterScreenRoot.SetActive(false);

        if (expBarRoot != null)
        {
            expBarRoot.SetActive(false);
        }

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        if (lookAction != null)
        {
            lookAction.action.Enable();
        }

        Time.timeScale = 1f;
    }
}