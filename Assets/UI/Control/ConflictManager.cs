using UnityEngine;
using UnityEngine.InputSystem;
using DG.Tweening; // For the canvas shake
using TMPro;
using System.Linq;

public class ConflictManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private RectTransform settingsPanel; // The whole menu to shake
    [SerializeField] private GameObject conflictPopup; // The tab/popup that appears
    [SerializeField] private TextMeshProUGUI conflictText; // "Conflict: [Move Forward] and [Move Backward] use [S]"

    public bool CheckForConflicts(InputAction action, int bindingIndex, string newPath)
    {
        // Search the action map for any other action using the same key
        var duplicate = action.actionMap.bindings.FirstOrDefault(b =>
            b.effectivePath == newPath &&
            b.action != action.name);

        if (!string.IsNullOrEmpty(duplicate.action))
        {
            TriggerConflictUI(action.name, duplicate.action, newPath);
            return true; // Conflict found!
        }

        return false;
    }

    private void TriggerConflictUI(string actionA, string actionB, string keyPath)
    {
        // 1. Shake the whole settings menu
        settingsPanel.DOShakeAnchorPos(0.4f, 15, 20).SetUpdate(true);

        // 2. Format the human-readable key name (e.g. "S")
        string keyName = InputControlPath.ToHumanReadableString(keyPath,
            InputControlPath.HumanReadableStringOptions.OmitDevice);

        // 3. Update text and show the popup tab
        conflictText.text = $"<b>{keyName}</b> is already used by <b>{actionB}</b>.\n" +
                           $"Binding it to <b>{actionA}</b> will create a conflict!";

        conflictPopup.SetActive(true);
        conflictPopup.transform.localScale = Vector3.zero;
        conflictPopup.transform.DOScale(Vector3.one, 0.2f).SetEase(Ease.OutBack).SetUpdate(true);
    }

    public void CloseConflictPopup()
    {
        conflictPopup.transform.DOScale(Vector3.zero, 0.15f).SetUpdate(true)
            .OnComplete(() => conflictPopup.SetActive(false));
    }
}