using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

[ExecuteInEditMode]
public class ProgressBarCircle : MonoBehaviour
{
    [Header("Level & EXP Settings")]
    public int currentLevel = 1;
    public float currentExp = 0f;
    public float expToNextLevel = 100f;

    [Header("UI Colors")]
    public Color levelTextColor = new Color(0.1f, 0.2f, 0.8f, 1f);
    public Color barColor = Color.green;
    public Color barBackgroundColor = Color.white;
    public Color maskColor = new Color(0.9f, 0.9f, 0.9f, 1f);

    private Image bar, mask;
    private TMPro.TMP_Text txtLevel;

    private Sequence _expSequence;

    // NEW: Variable to remember your text's exact starting size!
    private Vector3 _originalTextScale;

    private void Awake()
    {
        txtLevel = transform.Find("Text").GetComponent<TMPro.TMP_Text>();
        bar = transform.Find("BarCircle").GetComponent<Image>();
        mask = transform.Find("Mask").GetComponent<Image>();

        // NEW: Capture the scale (e.g., 16, 16, 16) the moment the game wakes up
        if (txtLevel != null)
        {
            _originalTextScale = txtLevel.transform.localScale;
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
        }

        currentExp = newExp;
        int previousLevel = currentLevel;
        currentLevel += levelsGained;

        _expSequence?.Kill();
        _expSequence = DOTween.Sequence();

        if (levelsGained == 0)
        {
            _expSequence.Append(bar.DOFillAmount(currentExp / expToNextLevel, 0.4f).SetEase(Ease.OutCubic));
        }
        else
        {
            _expSequence.Append(bar.DOFillAmount(1f, 0.25f).SetEase(Ease.InQuad));

            for (int i = 0; i < levelsGained; i++)
            {
                int levelToShow = previousLevel + i + 1;

                _expSequence.AppendCallback(() => {
                    txtLevel.text = levelToShow.ToString();
                    bar.fillAmount = 0f;

                    txtLevel.transform.DOKill();
                    // NEW: Reset back to 16 (or whatever you set it to) instead of 1
                    txtLevel.transform.localScale = _originalTextScale;

                    // NEW: Punch relative to the original scale!
                    txtLevel.transform.DOPunchScale(_originalTextScale * 0.6f, 0.35f, 8, 1f);
                });

                if (i < levelsGained - 1)
                {
                    _expSequence.Append(bar.DOFillAmount(1f, 0.15f).SetEase(Ease.Linear));
                }
            }

            _expSequence.Append(bar.DOFillAmount(currentExp / expToNextLevel, 0.5f).SetEase(Ease.OutCubic));
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

        txtLevel.transform.DOKill();
        // NEW: Reset back to your custom scale
        txtLevel.transform.localScale = _originalTextScale;

        txtLevel.text = currentLevel.ToString();

        if (levelsLost > 0)
        {
            txtLevel.color = Color.red;
            txtLevel.DOColor(levelTextColor, 0.5f);

            // NEW: Punch relative to the original scale!
            txtLevel.transform.DOPunchScale(_originalTextScale * 0.3f, 0.3f, 10, 1f);
        }

        bar.DOFillAmount(currentExp / expToNextLevel, 0.4f).SetEase(Ease.OutCubic);
    }

    private void UpdateVisualsInstantly()
    {
        if (txtLevel != null)
        {
            txtLevel.text = currentLevel.ToString();
            txtLevel.color = levelTextColor;
        }

        if (bar != null && expToNextLevel > 0)
        {
            bar.fillAmount = currentExp / expToNextLevel;
            bar.color = barColor;
        }

        if (mask != null)
        {
            mask.color = maskColor;
        }
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            UpdateVisualsInstantly();
            // Continuously update the baseline scale while you are tweaking in the editor
            if (txtLevel != null) _originalTextScale = txtLevel.transform.localScale;
        }
    }

    private void OnDestroy()
    {
        _expSequence?.Kill();
        if (txtLevel != null) txtLevel.transform.DOKill();
    }
}