using System.Collections.Generic;
using TMPro;
using UnityEditor.Localization.Plugins.XLIFF.V20;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class RewardMenuManager : MonoBehaviour
{
    public static RewardMenuManager Instance { get; private set; }

    [Header("Screen References")]
    [SerializeField] private GameObject characterScreenRoot;
    [SerializeField] private GameObject leftSideGauntlets; //also for reward menu gauntlets weren't showing up
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
    [SerializeField] private TMP_Text menuTitleText; // Drag your "Rewards" title text here!
    [SerializeField] private TMP_Text warningBodyText; // Drag the text inside your Warning Panel here

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

    // Checks if a gem is allowed to be picked up
    public bool CanDragGem(DraggableGem gem)
    {
        if (_isOverflowMode) return true;

        // Otherwise, apply the strict Level-Up Reward lock
        return _currentlySlottedGem == null || _currentlySlottedGem == gem;
    }

    // Called by GemDropSlot.cs
    public void OnGemSlotted(DraggableGem gem)
    {
        _currentlySlottedGem = gem;
    }

    // Called by GemReturnZone.cs
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

        // Clean out the holder first
        foreach (Transform child in gemHolder) { Destroy(child.gameObject); }

        GameObject firstSpawnedGem = null;

        // Spawn the specific overflow gems!
        foreach (GameObject gemPrefab in overflowGemPrefabs)
        {
            GameObject spawnedGem = Instantiate(gemPrefab, gemHolder);
            if (firstSpawnedGem == null) firstSpawnedGem = spawnedGem;

            DraggableGem dragScript = spawnedGem.GetComponent<DraggableGem>();
            if (dragScript != null) dragScript.enabled = true;

            Button btn = spawnedGem.GetComponent<Button>();
            if (btn != null) btn.onClick.RemoveAllListeners();

            EventTrigger trigger = spawnedGem.GetComponent<EventTrigger>();
            if (trigger == null) trigger = spawnedGem.AddComponent<EventTrigger>();

            EventTrigger.Entry entry = new EventTrigger.Entry();
            entry.eventID = EventTriggerType.PointerEnter;
            entry.callback.AddListener((data) => { UpdateSmallDescription(spawnedGem); });
            trigger.triggers.Add(entry);
        }

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

        // Reset text for standard level ups
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

        foreach (GameObject gemPrefab in chosenGems)
        {
            GameObject spawnedGem = Instantiate(gemPrefab, gemHolder);
            if (firstSpawnedGem == null) firstSpawnedGem = spawnedGem;

            DraggableGem dragScript = spawnedGem.GetComponent<DraggableGem>();
            if (dragScript != null) dragScript.enabled = true;

            Button btn = spawnedGem.GetComponent<Button>();
            if (btn != null) btn.onClick.RemoveAllListeners();

            EventTrigger trigger = spawnedGem.GetComponent<EventTrigger>();
            if (trigger == null) trigger = spawnedGem.AddComponent<EventTrigger>();

            EventTrigger.Entry entry = new EventTrigger.Entry();
            entry.eventID = EventTriggerType.PointerEnter;
            entry.callback.AddListener((data) => { UpdateSmallDescription(spawnedGem); });
            trigger.triggers.Add(entry);
        }

        if (Gamepad.current != null && firstSpawnedGem != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(firstSpawnedGem);
        }
    }

    private void Update()
    {
        if (rightSideRewards.activeSelf)
        {
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
                    CancelClose(); // Hide warning, stay in menu
                }
                else
                {
                    ShowWarning(); // Show warning to confirm leaving
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
        Time.timeScale = 1f;
    }
}