using UnityEngine;
using TMPro;
using DG.Tweening;
using System.Collections;
using UnityEngine.Localization;
using UnityEngine.InputSystem;

public class TutorialNotificationManager : MonoBehaviour
{
    public static TutorialNotificationManager Instance;

    [Header("UI References")]
    [Tooltip("The background panel that will slide up.")]
    public RectTransform notificationPanel; 
    [Tooltip("The text component inside the panel.")]
    public TMP_Text notificationText;

// --- 2. ADD LOCALIZED STRINGS ---
    [Header("Localized Strings")]
    public LocalizedString primaryString;
    public LocalizedString secondaryString;
    [Tooltip("Requires 3 Smart Arguments: {0} = Hand, {1} = Item Name, {2} = Keybind")]
    public LocalizedString notificationFormatString;

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
        string actionName = isPrimary ? "RightSkill" : "LeftSkill";
        string keybind = "[" + InputHelper.GetBindingString(actionName) + "]";
        
        // --- 3. FETCH THE TRANSLATED HAND WORD ---
        string hand = isPrimary ? primaryString.GetLocalizedString() : secondaryString.GetLocalizedString();
        
        // --- 4. FETCH THE SMART STRING WITH VARIABLES ---
        notificationText.text = notificationFormatString.GetLocalizedString(hand, itemName, keybind);
        
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

            yield return null; 
            yield return new WaitForSeconds(_animator.GetCurrentAnimatorStateInfo(0).length);
        }
        
        notificationPanel.gameObject.SetActive(false);
    }
}
