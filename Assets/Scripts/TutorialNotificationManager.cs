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

    private Animator _animator;

    private void Awake()
    {
        Instance = this;
        
        if (notificationPanel != null)
        {
            _animator = notificationPanel.GetComponent<Animator>();
            notificationPanel.gameObject.SetActive(false); // Ensure it's active for the animator to work
        }

        if (notificationText != null)
        {
            notificationText.alpha = 0f;
        }
    }

    public void ShowTutorial(bool isPrimary, string itemName)
    {
        notificationPanel.gameObject.SetActive(true);
        notificationText.DOKill();
        StopAllCoroutines(); 
        StartCoroutine(TutorialSequence(isPrimary, itemName));
    }

    private IEnumerator TutorialSequence(bool isPrimary, string itemName)
    {
        string keybind = isPrimary ? "[E]" : "[Q]";
        string hand = isPrimary ? "Primary" : "Secondary";
        
        notificationText.text = $"New {hand} {itemName} Acquired!\nPress <color=red>{keybind}</color> to cast.";
        notificationText.alpha = 0f;
        notificationText.DOFade(1f, 0.5f);

        // Show: Play "Notification" reversely (Speed -1, start at 1.0)
        if (_animator != null)
        {
            _animator.SetFloat("Speed", -1f);
            _animator.Play("Notification", 0, 1f);
        }
        yield return new WaitForSeconds(4f);

        // Hide: Play "Notification" normally (Speed 1, start at 0.0)
        if (_animator != null)
        {
            notificationText.DOFade(0f, 0.5f);
            _animator.SetFloat("Speed", 1f);
            _animator.Play("Notification", 0, 0f);

            // Wait for the animation to complete before disabling the GameObject
            yield return null; // Wait one frame for the animator to transition
            yield return new WaitForSeconds(_animator.GetCurrentAnimatorStateInfo(0).length);
        }
        
        notificationPanel.gameObject.SetActive(false);
    }
}
