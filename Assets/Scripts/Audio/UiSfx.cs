using FMODUnity;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Gives every button, slider and toggle in a scene its hover and click sounds. It creates itself once at startup and
/// adds a small listener to each control when a scene loads, so no menu needs wiring.
/// ponytail: controls created after the scene loads (reward gems, rail cards) stay silent; they have their own gem sounds.
/// </summary>
public class UiSfx : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        GameObject host = new GameObject("[UiSfx]");
        DontDestroyOnLoad(host);
        host.AddComponent<UiSfx>();
    }

    private void Awake()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        AddListeners();
    }

    private void OnDestroy() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => AddListeners();

    private static void AddListeners()
    {
        foreach (Selectable control in FindObjectsByType<Selectable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (control.GetComponent<UiSfxListener>() == null) control.gameObject.AddComponent<UiSfxListener>();
        }
    }
}

/// <summary>One control's sounds. Added at runtime by <see cref="UiSfx"/>.</summary>
public class UiSfxListener : MonoBehaviour, IPointerEnterHandler, ISelectHandler, IPointerClickHandler, ISubmitHandler
{
    private const float HoverGap = 0.05f; // sweeping across a list shouldn't machine-gun the hover sound
    private const float ClickHush = 0.2f; // a click often moves the selection; that isn't a hover

    private static float _lastHover;
    private static float _lastClick = -1f;
    private static int _lastMenuSoundFrame = -1;
    private static UiSfxListener _lastHovered;
    private Selectable _control;

    private void Awake() => _control = GetComponent<Selectable>();

    public void OnPointerEnter(PointerEventData eventData) => Hover();

    // A mouse press selects the control it lands on, and the pointer already played the hover when it arrived.
    public void OnSelect(BaseEventData eventData)
    {
        if (!(eventData is PointerEventData)) Hover();
    }

    public void OnPointerClick(PointerEventData eventData) => Click();
    public void OnSubmit(BaseEventData eventData) => Click();

    /// <summary>For a menu opening or closing. When a click caused it, this plays instead of the click sound.</summary>
    public static void PlayMenuSound(string eventPath)
    {
        _lastMenuSoundFrame = Time.frameCount;
        RuntimeManager.PlayOneShot(eventPath);
    }

    private void Hover()
    {
        float now = Time.unscaledTime;
        if (!_control.IsInteractable() || now - _lastHover < HoverGap || now - _lastClick < ClickHush) return;

        bool sameControl = IsSameControl(_lastHovered);
        _lastHovered = this;
        if (sameControl) return;

        _lastHover = now;
        RuntimeManager.PlayOneShot("event:/UI/UI_Hover");
    }

    private void Click()
    {
        if (!_control.IsInteractable() || _control is Slider) return;

        _lastClick = Time.unscaledTime;
        if (_lastMenuSoundFrame != Time.frameCount) RuntimeManager.PlayOneShot("event:/UI/UI_Click");
    }

    // Return is built from two buttons side by side (its icon and its text) that do the same thing.
    private bool IsSameControl(UiSfxListener other)
    {
        if (other == null || other == this || other.transform.parent != transform.parent) return false;
        if (!(_control is Button a) || !(other._control is Button b)) return false;

        return a.onClick.GetPersistentEventCount() > 0 && b.onClick.GetPersistentEventCount() > 0
            && a.onClick.GetPersistentTarget(0) == b.onClick.GetPersistentTarget(0)
            && a.onClick.GetPersistentMethodName(0) == b.onClick.GetPersistentMethodName(0);
    }
}
