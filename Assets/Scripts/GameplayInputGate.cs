using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Single owner of "is the player allowed to act right now", used by every menu that takes over
/// the screen (pause, upgrade/character screen, reward screen).
///
/// The problem it solves: enabling an action map while one of its controls is already held makes
/// the Input System run its initial-state check and fire that action immediately. PlayerCombat
/// binds attacks to 'performed', so the very click or button press that closed a menu became an
/// attack the instant gameplay resumed. Restoring input only once every button has been released
/// means a menu press can never leak into gameplay.
///
/// It bootstraps itself onto its own DontDestroyOnLoad object so the restore coroutine cannot be
/// killed by the menu that started it being deactivated - that would strand the player unable to
/// act at all.
/// </summary>
public class GameplayInputGate : MonoBehaviour
{
    public static GameplayInputGate Instance { get; private set; }

    [Tooltip("Safety cap in unscaled seconds. Input is restored even if a control somehow reads " +
             "as held forever, so it can never be lost permanently.")]
    [SerializeField] private float restoreTimeout = 1f;

    [Tooltip("Frames the watchdog waits before deciding a menu forgot to restore input.")]
    [SerializeField] private int watchdogGraceFrames = 10;

    private InputActionMap _playerMap;
    private Coroutine _restoreRoutine;
    private int _unguardedFrames;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;

        GameObject host = new GameObject("[GameplayInputGate]");
        host.AddComponent<GameplayInputGate>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Menus never survive a scene change, so any suspend still outstanding belongs to a menu that
    /// no longer exists. Clearing it here means a menu that failed to close cleanly costs a frame
    /// of input rather than leaving the player permanently unable to act.
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _unguardedFrames = 0;

        if (_restoreRoutine != null)
        {
            StopCoroutine(_restoreRoutine);
            _restoreRoutine = null;
        }

        InputActionMap map = PlayerMap;
        if (map != null) map.Enable();
    }

    /// <summary>
    /// Lets a component that already holds the Player map hand it over, so the gate does not have
    /// to guess. Falls back to the project-wide asset when nobody registers one.
    /// </summary>
    public void RegisterPlayerMap(InputActionMap map)
    {
        if (map != null) _playerMap = map;
    }

    private InputActionMap PlayerMap
    {
        get
        {
            if (_playerMap == null && InputSystem.actions != null)
            {
                _playerMap = InputSystem.actions.FindActionMap("Player");
            }

            return _playerMap;
        }
    }

    /// <summary>Blocks gameplay input. Call when a menu opens.</summary>
    public void Suspend()
    {
        _unguardedFrames = 0;

        if (_restoreRoutine != null)
        {
            StopCoroutine(_restoreRoutine);
            _restoreRoutine = null;
        }

        InputActionMap map = PlayerMap;
        if (map != null) map.Disable();
    }

    /// <summary>
    /// Gives gameplay input back once the closing press has been released. Call when a menu closes.
    ///
    /// Deliberately NOT reference counted. These menus are modal and share their close paths, so a
    /// suspend raised by one of them is routinely released through another one's ConfirmClose, and
    /// a counter left the player permanently unable to act whenever the pairing did not line up.
    /// Restoring slightly early is a far better failure mode than dead controls.
    /// </summary>
    public void RestoreWhenReleased()
    {
        _unguardedFrames = 0;

        if (_restoreRoutine != null) StopCoroutine(_restoreRoutine);
        _restoreRoutine = StartCoroutine(RestoreRoutine());
    }

    private IEnumerator RestoreRoutine()
    {
        // Realtime: menus run at timeScale 0.
        float deadline = Time.realtimeSinceStartup + restoreTimeout;

        while (IsAnyButtonHeld() && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        // One more frame so the release itself is consumed by the UI, not by gameplay.
        yield return null;

        _restoreRoutine = null;

        InputActionMap map = PlayerMap;
        if (map != null) map.Enable();
    }

    /// <summary>
    /// Last line of defence. Every menu in this game freezes time while it is open, so gameplay
    /// input being disabled while time is running normally means a menu closed without restoring
    /// it. Rather than trust every close path to be perfectly paired, recover here.
    /// </summary>
    private void Update()
    {
        if (_restoreRoutine != null) return;

        InputActionMap map = PlayerMap;

        bool stuck = map != null && !map.enabled && Time.timeScale > 0f;
        if (!stuck)
        {
            _unguardedFrames = 0;
            return;
        }

        _unguardedFrames++;
        if (_unguardedFrames <= watchdogGraceFrames) return;

        _unguardedFrames = 0;
        Debug.LogWarning("[GameplayInputGate] Gameplay input was still suspended while the game was " +
                         "running. A menu closed without restoring it, so it is being restored now.");

        RestoreWhenReleased();
    }

    /// <summary>
    /// True while any button that could have dismissed a menu is still down. Checked broadly
    /// rather than per-binding, so dodge, heal and skill casts are covered as well as attacks.
    /// </summary>
    public static bool IsAnyButtonHeld()
    {
        Mouse mouse = Mouse.current;
        if (mouse != null && (mouse.leftButton.isPressed || mouse.rightButton.isPressed ||
                              mouse.middleButton.isPressed))
        {
            return true;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && (keyboard.enterKey.isPressed || keyboard.numpadEnterKey.isPressed ||
                                 keyboard.spaceKey.isPressed || keyboard.escapeKey.isPressed ||
                                 keyboard.tabKey.isPressed))
        {
            return true;
        }

        Gamepad gamepad = Gamepad.current;
        if (gamepad != null)
        {
            foreach (InputControl control in gamepad.allControls)
            {
                if (control is UnityEngine.InputSystem.Controls.ButtonControl button &&
                    button.isPressed)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
