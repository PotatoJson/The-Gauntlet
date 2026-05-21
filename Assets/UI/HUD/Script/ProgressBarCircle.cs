using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem;

[ExecuteInEditMode]
public class ProgressBarCircle : MonoBehaviour, IPointerClickHandler, ISubmitHandler
{

    public static ProgressBarCircle Instance {get; private set;}

    [Header("UI References (Drag & Drop)")]
    [SerializeField] private Image fillBar;
    [SerializeField] private TMPro.TMP_Text txtLevel;

    [Header("Level Up Notification")]
    [Tooltip("Drag your Red Arrow UI object here")]
    [SerializeField] private GameObject redArrow;

    [Header("Level & EXP Settings")]
    public int currentLevel = 1;
    public float currentExp = 0f;
    public float expToNextLevel = 100f;

    [Header("UI Colors")]
    public Color levelTextColor = new Color(0.1f, 0.2f, 0.8f, 1f);
    public Color barColor = Color.yellow;

    private Sequence _expSequence;

    [Header("Reward Menu Link")]
    public UnityEvent onRewardReadyClicked;
    [Tooltip("Drag Reward Menu Input Here")]
    public InputActionReference rewardMenuInput;

    private int _pendingLevelUps = 0;
    public bool HasPendingLevelUps => _pendingLevelUps > 0;

    // Animation Tracking
    private Tween _pulseTween;
    private Tween _arrowTween;
    private Vector3 _originalTextScale = Vector3.one;
    private Vector3 _originalBarScale = Vector3.one;
    private Vector2 _originalArrowPos;

    private void OnEnable()
    {
        if(rewardMenuInput != null) rewardMenuInput.action.Enable();
    }

    private void OnDisable()
    {
        if(rewardMenuInput != null) rewardMenuInput.action.Disable();
    }

    private void Awake()
    {
        if(Instance == null) Instance = this;
        else if(Instance != this && Application.isPlaying) Destroy(gameObject);

        if (txtLevel != null) _originalTextScale = txtLevel.transform.localScale;
        _originalBarScale = transform.localScale;

        // Remember exactly where the arrow starts, then hide it!
        if (redArrow != null)
        {
            _originalArrowPos = redArrow.GetComponent<RectTransform>().anchoredPosition;
            redArrow.SetActive(false);
        }
    }

    private void Start()
    {
        UpdateVisualsInstantly();
    }

    public void AddExperience(float amount)
    {
        float newExp = currentExp + amount;
        int levelsGained = 0;

        while (newExp >= expToNextLevel)
        {
            newExp -= expToNextLevel;
            levelsGained++;

            expToNextLevel = Mathf.Round(expToNextLevel * 1.5f);
        }

        currentExp = newExp;
        int previousLevel = currentLevel;
        currentLevel += levelsGained;

        _pendingLevelUps += levelsGained;

        if (_pendingLevelUps > 0)
        {
            // 1. Start the bar breathing
            if (_pulseTween == null || !_pulseTween.IsActive())
            {
                _pulseTween = transform.DOScale(_originalBarScale * 1.05f, 0.6f)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetUpdate(true);
            }

            // 2. Show the arrow and start the pointing bounce
            if (redArrow != null)
            {
                redArrow.SetActive(true);
                RectTransform arrowRect = redArrow.GetComponent<RectTransform>();

                _arrowTween?.Kill();

                // Start from the original position, move DOWN by only 4px
                arrowRect.anchoredPosition = _originalArrowPos;
                _arrowTween = arrowRect.DOAnchorPos(_originalArrowPos + new Vector2(0f, -0.1f), 0.8f)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetUpdate(true);
            }
        }

        // Keep the normal EXP fill animation running regardless of level up
        _expSequence?.Kill();
        _expSequence = DOTween.Sequence().SetUpdate(true);

        if (levelsGained == 0)
        {
            _expSequence.Append(fillBar.DOFillAmount(currentExp / expToNextLevel, 0.4f).SetEase(Ease.OutCubic));
        }
        else
        {
            _expSequence.Append(fillBar.DOFillAmount(1f, 0.25f).SetEase(Ease.InQuad));

            for (int i = 0; i < levelsGained; i++)
            {
                int levelToShow = previousLevel + i + 1;

                _expSequence.AppendCallback(() => {
                    if (txtLevel != null)
                    {
                        txtLevel.text = levelToShow.ToString();
                        txtLevel.transform.DOKill();
                        txtLevel.transform.localScale = _originalTextScale;
                        txtLevel.transform.DOPunchScale(_originalTextScale * 0.6f, 0.35f, 8, 1f);
                    }
                    if (fillBar != null) fillBar.fillAmount = 0f;
                });

                if (i < levelsGained - 1)
                {
                    _expSequence.Append(fillBar.DOFillAmount(1f, 0.15f).SetEase(Ease.Linear));
                }
            }

            _expSequence.Append(fillBar.DOFillAmount(currentExp / expToNextLevel, 0.5f).SetEase(Ease.OutCubic));
        }
    }

    public void RemoveExperience(float amount)
    {
        float newExp = currentExp - amount;
        int levelsLost = 0;

        while (newExp < 0 && currentLevel > 1)
        {
            newExp += expToNextLevel;
            levelsLost++;
        }

        if (currentLevel - levelsLost <= 1 && newExp < 0)
        {
            newExp = 0;
            levelsLost = currentLevel - 1;
        }

        currentExp = newExp;
        currentLevel -= levelsLost;

        _expSequence?.Kill();

        if (txtLevel != null)
        {
            txtLevel.transform.DOKill();
            txtLevel.transform.localScale = _originalTextScale;
            txtLevel.text = currentLevel.ToString();

            if (levelsLost > 0)
            {
                txtLevel.color = Color.red;
                txtLevel.DOColor(levelTextColor, 0.5f);
                txtLevel.transform.DOPunchScale(_originalTextScale * 0.3f, 0.3f, 10, 1f);
            }
        }

        if (fillBar != null)
        {
            fillBar.DOFillAmount(currentExp / expToNextLevel, 0.4f).SetEase(Ease.OutCubic);
        }
    }

    private void UpdateVisualsInstantly()
    {
        if (txtLevel != null)
        {
            txtLevel.text = currentLevel.ToString();
            txtLevel.color = levelTextColor;
        }

        if (fillBar != null && expToNextLevel > 0)
        {
            fillBar.fillAmount = currentExp / expToNextLevel;
            fillBar.color = barColor;
        }
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            UpdateVisualsInstantly();
            if (txtLevel != null) _originalTextScale = txtLevel.transform.localScale;
            _originalBarScale = transform.localScale;
        }
        else
        {
            if(HasPendingLevelUps && rewardMenuInput != null)
            {
                if (rewardMenuInput.action.WasPressedThisFrame())
                {
                    TryOpenRewardMenu();
                }
            }
        }
    }

    private void OnDestroy()
    {
        _expSequence?.Kill();
        _arrowTween?.Kill();
        if (txtLevel != null) txtLevel.transform.DOKill();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        TryOpenRewardMenu();
    }

    public void OnSubmit(BaseEventData eventData)
    {
        TryOpenRewardMenu();
    }

    private void TryOpenRewardMenu()
    {
        if (_pendingLevelUps > 0)
        {
            _pendingLevelUps--;

            // If no more levels are pending, completely kill all animations!
            if (_pendingLevelUps <= 0)
            {
                // Stop bar breathing
                _pulseTween?.Kill();
                transform.localScale = _originalBarScale;

                // Stop arrow pointing and hide it
                if (redArrow != null)
                {
                    _arrowTween?.Kill();
                    redArrow.GetComponent<RectTransform>().anchoredPosition = _originalArrowPos;
                    redArrow.SetActive(false);
                }
            }

            onRewardReadyClicked?.Invoke();

            if(RewardMenuManager.Instance != null && Application.isPlaying)
            {
                RewardMenuManager.Instance.OpenRewardMenu();
            }
        }
    }
}