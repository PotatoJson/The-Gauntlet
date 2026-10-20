using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine.InputSystem;

/// <summary>
/// Walks the player through the game: moving, fighting, potions, levelling up and equipping a gem,
/// merging two matching gems, and upgrading a gem at a shrine. Every key in the text is read from the
/// player's current bindings, so it stays right after a remap.
/// </summary>
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
    [Tooltip("Found automatically when left empty.")]
    public ProgressBarCircle expBar;
    [Tooltip("Found automatically when left empty.")]
    public Transform primaryGauntletContainer;
    [Tooltip("Found automatically when left empty.")]
    public Transform secondaryGauntletContainer;

    [Header("Portal Spawning")]
    public GameObject portalPrefab;
    public Transform portalSpawnPoint;

    [Header("Dialogue Settings")]
    public float textSpeed = 0.03f;

    public enum TutorialStep
    {
        Intro, Movement, PunchingIntro, Punching, PotionIntro, Potions,
        RewardIntro, EquippingGem,
        MergeIntro, MergingGems,
        ShrineIntro, UsingShrine,
        Completed
    }
    private TutorialStep _currentStep = TutorialStep.Intro;

    [Header("Movement Goal Settings")]
    public Transform movementTarget;
    public float targetRadius = 2.0f;

    [Header("Enemy Spawning")]
    public GameObject enemyPrefab;
    public Transform enemySpawnPoint;

    [Header("Scripted Rewards")]
    [Tooltip("The first Level Up offers exactly these gems (three different ones) so the lesson is the same every time.")]
    public List<GameObject> firstRewardGems = new List<GameObject>();
    [Tooltip("The second Level Up offers these: put the SAME gem twice (to merge) plus one other.")]
    public List<GameObject> mergeRewardGems = new List<GameObject>();

    [Header("Shrine")]
    [Tooltip("Starts hidden; appears when it is time to learn about upgrading gems.")]
    public ShrineInteractable shrine;

    private GameObject _spawnedEnemy;
    private bool _isTransitioning = false;
    private bool _rewardMenuWasOpened = false;

    // Exact number of equipped gems before a quest starts, so we can tell when one was added.
    private int _startingGemCount = 0;

    private string[] _currentDialogueSequence;
    private int _currentSequenceIndex = 0;

    private bool _isTyping = false;
    private string _currentLine = "";
    private Vector3 _indicatorStartPos;
    private Coroutine _typingCoroutine;

    private void Awake()
    {
        // Start from nothing. The Backpack survives a trip back to the main menu, so without this the
        // gauntlets and gems from the last play session would load into the tutorial and break its
        // steps. (Awake runs before the equipment is loaded for the scene.)
        if (PersistentEquipment.Instance != null) PersistentEquipment.Instance.ResetForNewRun();
    }

    private void OnDestroy()
    {
        // ...and leave nothing behind: using the shrine here saves the tutorial's gems into the Backpack.
        if (PersistentEquipment.Instance != null) PersistentEquipment.Instance.ResetForNewRun();

        if (GameplayInputGate.Instance != null) GameplayInputGate.Instance.RestoreWhenReleased(this);
    }

    private void Start()
    {
        HideObjectiveInstantly();
        ResolveReferences();

        if (shrine != null) shrine.gameObject.SetActive(false);

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

    // These live inside the HUD prefab, so they are looked up instead of being wired by hand in the scene.
    private void ResolveReferences()
    {
        if (expBar == null) expBar = ProgressBarCircle.Instance != null ? ProgressBarCircle.Instance : FindFirstObjectByType<ProgressBarCircle>(FindObjectsInactive.Include);

        InventoryManager inventory = InventoryManager.Instance;
        if (inventory != null)
        {
            if (primaryGauntletContainer == null) primaryGauntletContainer = inventory.primaryGauntlet;
            if (secondaryGauntletContainer == null) secondaryGauntletContainer = inventory.secondaryGauntlet;
        }
    }

    private static string Key(string action) => InputHelper.GetBindingString(action);

    private IEnumerator StartIntroRoutine()
    {
        yield return null;

        // Let the arrival effect (teleport-in and the camera sweep) finish before anyone talks.
        PlayerSpawnTeleportEffect arrival = playerMovement != null ? playerMovement.GetComponent<PlayerSpawnTeleportEffect>() : null;
        while (arrival != null && arrival.IsPlaying) yield return null;

        _currentStep = TutorialStep.Intro;

        string[] introLines = new string[]
        {
            "Welcome to the dungeon! Let's begin with the basics.",
            $"Use {Key("Move")} to move.\n{Key("Roll")} to roll, Hold {Key("Roll")} to sprint.",
            $"And the important part: {Key("Jump")} to Jump!",
            "Those controls are all remappable in the settings menu."
        };

        StartDialogueSequence(introLines);
    }

    #region Fighting
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
            $"Use {Key("LightAttack")} for Light Attack combos!",
            $"Hold {Key("HeavyAttack")} to charge a Heavy Attack, and let go to swing.",
            $"Press {Key("LockOn")} to lock onto an enemy, and press it again to let go.",
            "Attacks use Stamina (the green bar), so don't swing wildly!"
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
            $"Press {Key("Heal")} to drink a Health Potion and recover your HP!"
        };

        StartDialogueSequence(potionLines);
    }
    #endregion

    #region Gems
    private IEnumerator WaitAndStartRewardIntro()
    {
        yield return new WaitForSeconds(0.5f);

        // The first Level Up offers a fixed set of different gems.
        OfferScriptedRewards(firstRewardGems);

        yield return new WaitForSeconds(2.5f);
        StartRewardIntro();
    }

    // Queue the next reward menu's gems and hand out the level that unlocks it.
    private void OfferScriptedRewards(List<GameObject> gems)
    {
        if (RewardMenuManager.Instance != null && gems != null && gems.Count > 0)
        {
            RewardMenuManager.Instance.SetNextRewards(gems);
        }

        if (expBar != null)
        {
            expBar.AddExperience(expBar.expToNextLevel);
        }
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
            "Pick a gem and drag it onto one of the finger sockets on your Gauntlet to equip it.",
            $"Future reference: You can press {Key("RewardsMenu")} to open the reward menu, or hold Alt to show the cursor and the level up button.\n\nPress {Key("OpenInventory")} any time to open your inventory and rearrange your gems."
        };

        StartDialogueSequence(rewardLines);
    }

    private void StartMergeIntro()
    {
        _currentStep = TutorialStep.MergeIntro;
        _isTransitioning = false;
        _rewardMenuWasOpened = false;

        string[] mergeLines = new string[]
        {
            "Your first gem is equipped. Your Gauntlet just got stronger!",
            "Here's a trick: two identical gems of the same tier can be merged into one stronger gem.",
            "I've given you another level. This time the reward has two matching gems.",
            "Drag one gem on top of the other to merge them into a Tier II gem, then equip the result.",
            $"You can merge later too: open your inventory with {Key("OpenInventory")} and drag one gem onto an identical gem in another socket.\n\nGems of different kinds or tiers won't merge, so nothing gets swallowed by mistake."
        };

        StartDialogueSequence(mergeLines);
    }

    private void StartShrineIntro()
    {
        _currentStep = TutorialStep.ShrineIntro;
        _isTransitioning = false;

        if (shrine != null)
        {
            shrine.gameObject.SetActive(true);
        }

        string[] shrineLines = new string[]
        {
            "Well done! A Tier II gem is much stronger than a plain one.",
            "Gems can be upgraded even further at shrines. One has appeared ahead of you.",
            $"Walk up to the shrine and press {Key("Interact")}, then choose one of your gems to raise its tier.",
            "Each shrine can only be used once, so choose wisely!"
        };

        StartDialogueSequence(shrineLines);
    }

    // Highest tier among the gems sitting in the gauntlet sockets.
    private int GetHighestEquippedTier()
    {
        int best = 0;
        best = Mathf.Max(best, HighestTierIn(primaryGauntletContainer));
        best = Mathf.Max(best, HighestTierIn(secondaryGauntletContainer));
        return best;
    }

    private static int HighestTierIn(Transform container)
    {
        if (container == null) return 0;

        int best = 0;
        foreach (DraggableGem gem in container.GetComponentsInChildren<DraggableGem>(true))
        {
            best = Mathf.Max(best, gem.Tier);
        }
        return best;
    }

    // Count all the gems attached to the gauntlet UI.
    private int GetTotalEquippedGems()
    {
        int count = 0;
        if (primaryGauntletContainer != null)
            count += primaryGauntletContainer.GetComponentsInChildren<DraggableGem>(true).Length;
        if (secondaryGauntletContainer != null)
            count += secondaryGauntletContainer.GetComponentsInChildren<DraggableGem>(true).Length;
        return count;
    }
    #endregion

    #region Dialogue
    private void StartDialogueSequence(string[] lines)
    {
        _currentDialogueSequence = lines;
        _currentSequenceIndex = 0;

        // Switch the player's input off (no punching, no skills, no walking) but leave PlayerMovement running:
        // disabling it left the walk/run animation looping on the spot. With no input it settles to idle.
        if (GameplayInputGate.Instance != null) GameplayInputGate.Instance.SuspendWhileRunning(this);
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
        foreach (char letter in textToType)
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

    private void EndDialogue()
    {
        dialoguePanelButton.interactable = false;

        dialoguePanel.DOFade(0f, 0.3f).SetUpdate(true).OnComplete(() =>
        {
            dialoguePanel.gameObject.SetActive(false);
            dialoguePanelButton.interactable = true;
        });

        // Input comes back once the button that closed the dialogue has been released, so that click or
        // space press can't turn into a punch or a jump.
        if (GameplayInputGate.Instance != null) GameplayInputGate.Instance.RestoreWhenReleased(this);
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
            ShowObjective($"Quest: Press {Key("Heal")} to drink a Health Potion.");
        }
        else if (_currentStep == TutorialStep.RewardIntro)
        {
            _currentStep = TutorialStep.EquippingGem;
            _startingGemCount = GetTotalEquippedGems(); // remember how many gems there were before the quest starts

            ShowObjective("Quest: Open the Level Up menu and equip a Gem.");
        }
        else if (_currentStep == TutorialStep.MergeIntro)
        {
            _currentStep = TutorialStep.MergingGems;
            _startingGemCount = GetTotalEquippedGems();

            OfferScriptedRewards(mergeRewardGems);
            ShowObjective("Quest: Level up, merge the two matching gems, then equip the Tier II gem.");
        }
        else if (_currentStep == TutorialStep.ShrineIntro)
        {
            _currentStep = TutorialStep.UsingShrine;
            ShowObjective($"Quest: Walk up to the shrine and press {Key("Interact")} to upgrade a gem.");
        }
    }
    #endregion

    private void Update()
    {
        switch (_currentStep)
        {
            case TutorialStep.Movement: CheckMovementProgress(); break;
            case TutorialStep.Punching: CheckPunchingProgress(); break;
            case TutorialStep.Potions: CheckPotionProgress(); break;
            case TutorialStep.EquippingGem: CheckEquipGemProgress(); break;
            case TutorialStep.MergingGems: CheckMergeProgress(); break;
            case TutorialStep.UsingShrine: CheckShrineProgress(); break;
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

    #region Progress checks
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

        // The reward menu freezes the game: while time is stopped the menu is open.
        if (Time.timeScale == 0f)
        {
            _rewardMenuWasOpened = true;
            return; // Wait for them to close it
        }

        if (!_rewardMenuWasOpened) return;
        _rewardMenuWasOpened = false;

        // They closed the menu. Did the number of equipped gems go up?
        if (GetTotalEquippedGems() > _startingGemCount)
        {
            _isTransitioning = true;
            HideObjectiveInstantly();
            StartCoroutine(WaitAndStartMergeIntro());
        }
        else
        {
            ShowObjective("Quest: Click Level Up and drag a gem onto your Gauntlet.");
            OfferScriptedRewards(firstRewardGems); // the level comes back so they can try again
        }
    }

    private void CheckMergeProgress()
    {
        if (_isTransitioning) return;

        if (Time.timeScale == 0f)
        {
            _rewardMenuWasOpened = true;
            return;
        }

        if (!_rewardMenuWasOpened) return;
        _rewardMenuWasOpened = false;

        if (GetHighestEquippedTier() >= 2)
        {
            _isTransitioning = true;
            HideObjectiveInstantly();
            StartCoroutine(WaitAndStartShrineIntro());
        }
        else
        {
            ShowObjective("Quest: Drag one matching gem onto the other, then equip the Tier II gem.");
            OfferScriptedRewards(mergeRewardGems);
        }
    }

    private void CheckShrineProgress()
    {
        if (_isTransitioning) return;

        // No shrine in the scene: don't strand the player, just finish.
        if (shrine == null || shrine.IsSpent)
        {
            _isTransitioning = true;
            HideObjectiveInstantly();
            CompleteTutorial();
        }
    }

    private IEnumerator WaitAndStartMergeIntro()
    {
        yield return new WaitForSeconds(0.8f);
        StartMergeIntro();
    }

    private IEnumerator WaitAndStartShrineIntro()
    {
        yield return new WaitForSeconds(1f);
        StartShrineIntro();
    }
    #endregion

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

    private void CompleteTutorial()
    {
        _currentStep = TutorialStep.Completed;

        tutorialPanel.DOFade(0f, 0.3f).OnComplete(() =>
        {
            if (portalPrefab != null && portalSpawnPoint != null)
            {
                Instantiate(portalPrefab, portalSpawnPoint.position, portalSpawnPoint.rotation);
            }

            ShowObjective("Tutorial Complete! Enter the portal.");
            tutorialPanel.DOFade(0f, 0.5f).SetDelay(5f);
        });
    }
}
