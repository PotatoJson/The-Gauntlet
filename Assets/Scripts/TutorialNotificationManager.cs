using UnityEngine;
using TMPro;
using DG.Tweening;
using System.Collections;

public class TutorialNotificationManager : MonoBehaviour
{
    public static TutorialNotificationManager Instance;

    [Header("UI References")]
    [Tooltip("The background panel that will slide up.")]
    public RectTransform notificationPanel; 
    [Tooltip("The text component inside the panel.")]
    public TMP_Text notificationText;

    private void Awake()
    {
        Instance = this;
        
        if (notificationPanel != null)
        {
            notificationPanel.anchoredPosition = new Vector2(0, -200f); 
        }
    }

    public void ShowTutorial(bool isPrimary, string itemName)
    {
        StopAllCoroutines(); 
        StartCoroutine(TutorialSequence(isPrimary, itemName));
    }

    private IEnumerator TutorialSequence(bool isPrimary, string itemName)
    {
        string keybind = isPrimary ? "[E]" : "[Q]";
        string hand = isPrimary ? "Primary" : "Secondary";
        
        notificationText.text = $"New {hand} {itemName} Acquired!\nPress <color=#FFD700>{keybind}</color> to cast.";
        notificationPanel.DOAnchorPos(new Vector2(0, 150f), 0.5f).SetEase(Ease.OutBack);

        yield return new WaitForSeconds(4f);

        notificationPanel.DOAnchorPos(new Vector2(0, -200f), 0.5f).SetEase(Ease.InBack);
    }
}
