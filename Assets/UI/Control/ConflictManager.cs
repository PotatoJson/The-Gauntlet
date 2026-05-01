using UnityEngine;
using UnityEngine.InputSystem;
using DG.Tweening;
using TMPro;
using System.Linq;
using System.Collections; // Required for Coroutine

public class ConflictManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private RectTransform settingsPanel;
    [SerializeField] private GameObject conflictPopup;
    [SerializeField] private TextMeshProUGUI conflictText;

    public bool CheckForConflicts(InputAction action, int bindingIndex, string newPath)
    {
        var duplicate = action.actionMap.bindings.FirstOrDefault(b =>
            b.effectivePath == newPath &&
            b.action != action.name);

        if (!string.IsNullOrEmpty(duplicate.action))
        {
            string keyName = InputControlPath.ToHumanReadableString(newPath,
                InputControlPath.HumanReadableStringOptions.OmitDevice);

            // Format the specific conflict message
            string message = $"<b>{keyName}</b> is already used by <b>{duplicate.action}</b>.\n" +
                             $"Binding it to <b>{action.name}</b> will create a conflict!";

            ShowWarning(message); // Pass to the generic warning method
            return true;
        }

        return false;
    }

    // NEW: A universal method any script can call to show a warning
    public void ShowWarning(string message)
    {
        settingsPanel.DOComplete(); // Stop existing shakes before starting a new one
        settingsPanel.DOShakeAnchorPos(0.4f, 15, 20).SetUpdate(true);

        conflictText.text = message;

        conflictPopup.SetActive(true);
        conflictPopup.transform.DOKill();
        conflictPopup.transform.localScale = Vector3.zero;
        conflictPopup.transform.DOScale(Vector3.one, 0.2f).SetEase(Ease.OutBack).SetUpdate(true);

        // Auto-hide the warning after 3 seconds
        StopAllCoroutines();
        StartCoroutine(AutoCloseWarning(3f));
    }

    public void CloseConflictPopup()
    {
        conflictPopup.transform.DOScale(Vector3.zero, 0.15f).SetUpdate(true)
            .OnComplete(() => conflictPopup.SetActive(false));
    }

    private IEnumerator AutoCloseWarning(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        if (conflictPopup.activeSelf)
        {
            CloseConflictPopup();
        }
    }
}