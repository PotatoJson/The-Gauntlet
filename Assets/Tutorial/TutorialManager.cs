using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using DG.Tweening;
using UnityEngine.InputSystem;

public class TutorialManager : MonoBehaviour
{
    [Header("Dialogue UI References")]
    public CanvasGroup dialoguePanel;
    public Button dialoguePanelButton;
    public TMP_Text dialogueText;
    public GameObject continueIndicator;

    [Header("Objective UI References")]
    public CanvasGroup tutorialPanel;
    public TMP_Text objectiveText;

    [Header("Player References")]
    public CharacterController playerController;
    public PlayerMovement playerMovement;
    public PlayerHealth playerHealth;

    [Header("System References")]
    public ProgressBarCircle expBar;
    public Transform primaryGauntletContainer;
    public Transform secondaryGauntletContainer;

    [Header("Portal Spawning")]
    public GameObject portalPrefab;
    public Transform portalSpawnPoint;

    [Header("Dialogue Settings")]
    public float textSpeed = 0.03f;

    public enum TutorialStep { Intro, Movement, PunchingIntro, Punching, PotionIntro, Potions, RewardIntro, EquippingGem, Completed }
    private TutorialStep _currentStep = TutorialStep.Intro;

    [Header("Movement Goal Settings")]
    public Transform movementTarget;
    public float targetRadius = 2.0f;

    [Header("Enemy Spawning")]
    public GameObject enemyPrefab;
    public Transform enemySpawnPoint;

    private GameObject _spawnedEnemy;
    private bool _isTransitioning = false;
    private bool _rewardMenuWasOpened = false;

    // --- NEW: Track the exact number of gems! ---
    private int _startingGemCount = 0;

    private string[] _currentDialogueSequence;
    private int _currentSequenceIndex = 0;

    private bool _isTyping = false;
    private string _currentLine = "";
    private Vector3 _indicatorStartPos;
    private Coroutine _typingCoroutine;

    private void Start()
    {
        HideObjectiveInstantly();

        if (continueIndicator != null)
        {
            _indicatorStartPos = continueIndicator.transform.localPosition;
            continueIndicator.SetActive(false);
        }

        if (dialoguePanelButton != null)
        {
            dialoguePanelButton.onClick.AddListener(OnDialogueClicked);
        }

        StartCoroutine(StartIntroRoutine());
    }

    private IEnumerator StartIntroRoutine()
    {
        yield return null;

        _currentStep = TutorialStep.Intro;

        string[] introLines = new string[]
        {
            "Welcome to the dungeon! Let's begin with the basics.",
            "Use W, A, S, D to move.\nShift to roll, Hold Shift to sprint.",
            "And the important part: Space to Jump!",
            "Those controls are all remappable in the settings menu."
        };

        StartDialogueSequence(introLines);
    }

    private IEnumerator WaitAndStartPunchingIntro()
    {
        yield return new WaitForSeconds(1f);
        StartPunchingIntro();
    }

    private void StartPunchingIntro()
    {
        _currentStep = TutorialStep.PunchingIntro;
        _isTransitioning = false;

        if (enemyPrefab != null && enemySpawnPoint != null)
        {
            _spawnedEnemy = Instantiate(enemyPrefab, enemySpawnPoint.position, enemySpawnPoint.rotation);

            if (_spawnedEnemy.TryGetComponent<EliteEnemy>(out var eliteAI))
            {
                eliteAI.isTutorialDummy = true;
            }
        }

        string[] punchingLines = new string[]
        {
            "Nice job!",
            "Now let's practice combat. I have spawned an enemy.",
            "Use Left Click for Light Attack combos!",
            "Use Right Click for Heavy Attack combos!"
        };

        StartDialogueSequence(punchingLines);
    }

    private IEnumerator WaitAndStartPotionIntro()
    {
        yield return new WaitForSeconds(1.5f);
        StartPotionIntro();
    }

    private void StartPotionIntro()
    {
        _currentStep = TutorialStep.PotionIntro;
        _isTransitioning = false;

        if (playerHealth != null)
        {
            float damageNeeded = playerHealth.CurrentHealth - 20f;
            if (damageNeeded > 0)
            {
                playerHealth.TakeDamage(damageNeeded);
            }
        }

        string[] potionLines = new string[]
        {
            "Great work defeating that enemy!",
            "It looks like you took some damage in that fight, though.",
            "Press R to drink a Health Potion and recover your HP!"
        };

        StartDialogueSequence(potionLines);
    }

    private IEnumerator WaitAndStartRewardIntro()
    {
        yield return new WaitForSeconds(0.5f);

        if (expBar != null)
        {
            expBar.AddExperience(expBar.expToNextLevel);
        }

        yield return new WaitForSeconds(2.5f);
        StartRewardIntro();
    }

    private void StartRewardIntro()
    {
        _currentStep = TutorialStep.RewardIntro;
        _isTransitioning = false;
        _rewardMenuWasOpened = false;

        string[] rewardLines = new string[]
        {
            "Excellent! You have fully recovered your health.",
            "Notice your Experience Bar just filled up? You leveled up!",
            "For every level up, you will earn one Gem to upgrade your Gauntlets.",
            "Click the Level Up button to pick your first reward!",
            "Future reference: You can press Tab or Select on Controller to open reward menu. \n\n or Hold alt to show cursor and level up button"
        };

        StartDialogueSequence(rewardLines);
    }

    private void StartDialogueSequence(string[] lines)
    {
        _currentDialogueSequence = lines;
        _currentSequenceIndex = 0;

        if (playerMovement != null) playerMovement.enabled = false;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (dialogueText != null) dialogueText.text = "";

        dialoguePanel.gameObject.SetActive(true);
        dialoguePanel.alpha = 0f;
        dialoguePanel.DOFade(1f, 0.5f).SetUpdate(true).OnComplete(() =>
        {
            PlayDialogue(_currentDialogueSequence[_currentSequenceIndex]);
        });
    }

    private void PlayDialogue(string textToType)
    {
        _currentLine = textToType;
        dialogueText.text = "";
        _isTyping = true;

        if (continueIndicator != null) continueIndicator.SetActive(false);

        if (_typingCoroutine != null) StopCoroutine(_typingCoroutine);
        _typingCoroutine = StartCoroutine(TypeDialogueCoroutine(textToType));
    }

    private IEnumerator TypeDialogueCoroutine(string textToType)
    {
        WaitForSecondsRealtime waitTime = new WaitForSecondsRealtime(textSpeed);
        foreach (char letter in textToType.ToCharArray())
        {
            dialogueText.text += letter;
            yield return waitTime;
        }
        FinishTyping();
    }

    private void FinishTyping()
    {
        _isTyping = false;
        dialogueText.text = _currentLine;

        if (continueIndicator != null)
        {
            continueIndicator.SetActive(true);
            continueIndicator.transform.DOKill();
            continueIndicator.transform.localPosition = _indicatorStartPos;

            continueIndicator.transform.DOLocalMoveY(_indicatorStartPos.y - 10f, 0.4f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine)
                .SetUpdate(true);
        }
    }

    public void OnDialogueClicked()
    {
        if (_isTyping)
        {
            if (_typingCoroutine != null) StopCoroutine(_typingCoroutine);
            FinishTyping();
        }
        else
        {
            _currentSequenceIndex++;

            if (_currentSequenceIndex < _currentDialogueSequence.Length)
            {
                PlayDialogue(_currentDialogueSequence[_currentSequenceIndex]);
            }
            else
            {
                EndDialogue();
            }
        }
    }

    // --- NEW HELPER: Instantly count all gems attached to the UI ---
    private int GetTotalEquippedGems()
    {
        int count = 0;
        if (primaryGauntletContainer != null)
            count += primaryGauntletContainer.GetComponentsInChildren<DraggableGem>(true).Length;
        if (secondaryGauntletContainer != null)
            count += secondaryGauntletContainer.GetComponentsInChildren<DraggableGem>(true).Length;
        return count;
    }

    private void EndDialogue()
    {
        dialoguePanelButton.interactable = false;

        dialoguePanel.DOFade(0f, 0.3f).SetUpdate(true).OnComplete(() =>
        {
            dialoguePanel.gameObject.SetActive(false);
            dialoguePanelButton.interactable = true;
        });

        if (playerMovement != null) playerMovement.enabled = true;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        if (_currentStep == TutorialStep.Intro)
        {
            _currentStep = TutorialStep.Movement;
            ShowObjective("Quest: Move to the glowing circle.");
        }
        else if (_currentStep == TutorialStep.PunchingIntro)
        {
            _currentStep = TutorialStep.Punching;
            ShowObjective("Quest: Defeat the practice dummy.");
        }
        else if (_currentStep == TutorialStep.PotionIntro)
        {
            _currentStep = TutorialStep.Potions;
            ShowObjective("Quest: Press R to drink a Health Potion.");
        }
        else if (_currentStep == TutorialStep.RewardIntro)
        {
            _currentStep = TutorialStep.EquippingGem;

            // --- UPDATED: Remember exactly how many gems the player had before the quest starts! ---
            _startingGemCount = GetTotalEquippedGems();

            ShowObjective("Quest: Open the Level Up menu and equip a Gem.");
        }
    }

    private void Update()
    {
        switch (_currentStep)
        {
            case TutorialStep.Movement: CheckMovementProgress(); break;
            case TutorialStep.Punching: CheckPunchingProgress(); break;
            case TutorialStep.Potions: CheckPotionProgress(); break;
            case TutorialStep.EquippingGem: CheckEquipGemProgress(); break;
        }

        if (dialoguePanel != null && dialoguePanel.gameObject.activeInHierarchy && dialoguePanelButton.interactable)
        {
            bool advancePressed = false;

            // Check Gamepad (South Button = A / Cross)
            if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)
            {
                advancePressed = true;
            }

            // Check Keyboard (Space or Enter)
            if (Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame))
            {
                advancePressed = true;
            }

            // If any of those were pressed, simulate a click!
            if (advancePressed)
            {
                OnDialogueClicked();
            }
        }
    }

    private void HideObjectiveInstantly()
    {
        if (tutorialPanel != null)
        {
            tutorialPanel.DOKill();
            tutorialPanel.transform.DOKill();
            tutorialPanel.alpha = 0f;
        }
    }

    private void CheckMovementProgress()
    {
        if (playerController == null || movementTarget == null || _isTransitioning) return;

        float distance = Vector3.Distance(playerController.transform.position, movementTarget.position);

        if (distance <= targetRadius)
        {
            _isTransitioning = true;
            if (movementTarget != null) movementTarget.gameObject.SetActive(false);

            HideObjectiveInstantly();
            StartCoroutine(WaitAndStartPunchingIntro());
        }
    }

    private void CheckPunchingProgress()
    {
        if (_isTransitioning) return;

        if (_spawnedEnemy == null || (_spawnedEnemy.TryGetComponent<EliteEnemy>(out var elite) && elite.IsDead()))
        {
            _isTransitioning = true;

            HideObjectiveInstantly();
            StartCoroutine(WaitAndStartPotionIntro());
        }
    }

    private void CheckPotionProgress()
    {
        if (playerHealth == null || _isTransitioning) return;

        if (playerHealth.CurrentHealth >= playerHealth.MaxHealth)
        {
            _isTransitioning = true;

            HideObjectiveInstantly();
            StartCoroutine(WaitAndStartRewardIntro());
        }
    }

    private void CheckEquipGemProgress()
    {
        if (_isTransitioning) return;

        // 1. We know the Reward menu freezes the game. If time is 0, the menu is open!
        if (Time.timeScale == 0f)
        {
            _rewardMenuWasOpened = true;
            return; // Wait for them to close it
        }

        // 2. If time is running normally again, they closed the menu!
        if (_rewardMenuWasOpened && Time.timeScale > 0f)
        {
            // Did the total number of gems go up?
            if (GetTotalEquippedGems() > _startingGemCount)
            {
                _isTransitioning = true;
                HideObjectiveInstantly();
                AdvanceToNextStep();
            }
            else
            {
                // They closed it without equipping!
                _rewardMenuWasOpened = false;
                ShowObjective("Quest: You forgot to equip a Gem! Click Level Up and drag a reward to your Gauntlet.");

                // Give them the EXP back so they can try again
                if (expBar != null)
                {
                    expBar.AddExperience(expBar.expToNextLevel);
                }
            }
        }
    }

    private void ShowObjective(string newText)
    {
        objectiveText.text = newText;
        tutorialPanel.DOKill();
        tutorialPanel.transform.DOKill();

        if (tutorialPanel.alpha < 1f)
        {
            tutorialPanel.alpha = 0f;
            tutorialPanel.DOFade(1f, 0.5f);
        }

        tutorialPanel.transform.localScale = Vector3.one;
        tutorialPanel.transform.DOPunchScale(new Vector3(0.05f, 0.05f, 0.05f), 0.3f, 5, 1f);
    }

    public void AdvanceToNextStep()
    {
        TutorialStep previousStep = _currentStep;
        _currentStep = TutorialStep.Completed;

        tutorialPanel.DOFade(0f, 0.3f).OnComplete(() =>
        {
            if (previousStep == TutorialStep.EquippingGem)
            {
                if (portalPrefab != null && portalSpawnPoint != null)
                {
                    Instantiate(portalPrefab, portalSpawnPoint.position, portalSpawnPoint.rotation);
                }

                ShowObjective("Tutorial Complete! Enter the portal.");
                tutorialPanel.DOFade(0f, 0.5f).SetDelay(5f);
            }
        });
    }
}