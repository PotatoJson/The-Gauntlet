using UnityEngine;
using UnityEngine.InputSystem;
using DG.Tweening;
using TMPro;
using System.Linq;
using UnityEngine.EventSystems; // Required for focus control

public class ConflictManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private RectTransform settingsPanel;
    [SerializeField] private GameObject conflictPopup;
    [SerializeField] private TextMeshProUGUI conflictText;

    [Header("Focus Management")]
    [SerializeField] private GameObject okButton; // NEW: The button to highlight
    private GameObject _previousSelection; // Remembers where you were before the popup

    // CHANGED: Instead of showing the warning instantly, just return the message so RebindHandler can control the flow.
    public string GetConflictMessage(InputAction action, int bindingIndex, string newPath)
    {
        var duplicate = action.actionMap.bindings.FirstOrDefault(b =>
            b.effectivePath == newPath &&
            b.action != action.name);

        if (!string.IsNullOrEmpty(duplicate.action))
        {
            string keyName = InputControlPath.ToHumanReadableString(newPath,
                InputControlPath.HumanReadableStringOptions.OmitDevice);

            return $"<b>{keyName}</b> is already used by <b>{duplicate.action}</b>.\n" +
                   $"Binding it to <b>{action.name}</b> will create a conflict!";
        }

        return null; // No conflict!
    }

    // CHANGED: Now accepts the button that triggered it, so it knows where to return focus!
    public void ShowWarning(string message, GameObject returnButton = null)
    {
        _previousSelection = returnButton;

        settingsPanel.DOComplete();
        settingsPanel.DOShakeAnchorPos(0.4f, 15, 20).SetUpdate(true);

        conflictText.text = message;

        conflictPopup.SetActive(true);
        conflictPopup.transform.DOKill();
        conflictPopup.transform.localScale = Vector3.zero;
        conflictPopup.transform.DOScale(Vector3.one, 0.2f).SetEase(Ease.OutBack).SetUpdate(true);

        // THE FIX: Steal focus and highlight the OK button!
        if (okButton != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(okButton);
        }
    }

    public void CloseConflictPopup()
    {
        conflictPopup.transform.DOScale(Vector3.zero, 0.15f).SetUpdate(true)
            .OnComplete(() => {
                conflictPopup.SetActive(false);

                // THE FIX: Restore focus back to the Rebind button!
                if (_previousSelection != null && EventSystem.current != null)
                {
                    EventSystem.current.SetSelectedGameObject(null);
                    EventSystem.current.SetSelectedGameObject(_previousSelection);
                }
            });
    }

    public bool IsPopupActive()
    {
        return conflictPopup != null && conflictPopup.activeInHierarchy;
    }
}