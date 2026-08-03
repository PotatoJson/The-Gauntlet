using System.Collections;
using System.Collections.Generic;
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

    // Who currently wants gameplay input off. Keyed by the menu itself rather than counted, so a
    // menu that suspends twice cannot inflate a counter and strand the player.
    private readonly List<Object> _holders = new List<Object>();

    /// <summary>True while any menu is holding gameplay input off.</summary>
    public bool IsSuspended => _holders.Count > 0;

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
        _holders.Clear();

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

    /// <summary>Blocks gameplay input. Call when a menu opens, passing the menu itself.</summary>
    public void Suspend(Object owner)
    {
        _unguardedFrames = 0;

        if (owner != null && !_holders.Contains(owner)) _holders.Add(owner);

        if (_restoreRoutine != null)
        {
            StopCoroutine(_restoreRoutine);
            _restoreRoutine = null;
        }

        InputActionMap map = PlayerMap;
        if (map != null) map.Disable();
    }

    /// <summary>
    /// Gives gameplay input back once the closing press has been released, provided no other menu
    /// is still holding it. Call when a menu closes, passing the same owner it suspended with.
    ///
    /// Keyed by owner rather than counted: these menus share their close paths, so one menu's
    /// suspend is routinely released through another's ConfirmClose. An unmatched release is a
    /// harmless no-op instead of a counter drifting out of step.
    /// </summary>
    public void RestoreWhenReleased(Object owner)
    {
        _unguardedFrames = 0;

        if (owner != null) _holders.Remove(owner);

        // Something else still wants input off - a reward screen opened over the pause menu, say.
        if (_holders.Count > 0) return;

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

        // A menu destroyed without releasing would otherwise hold input off forever.
        for (int i = _holders.Count - 1; i >= 0; i--)
        {
            if (_holders[i] == null) _holders.RemoveAt(i);
        }

        InputActionMap map = PlayerMap;

        // Every menu here freezes time while it is open, so input being off while time runs means
        // something failed to release. Menus that are genuinely open sit at timeScale 0 and are
        // therefore never caught by this.
        bool stuck = map != null && !map.enabled && Time.timeScale > 0f;
        if (!stuck)
        {
            _unguardedFrames = 0;
            return;
        }

        _unguardedFrames++;
        if (_unguardedFrames <= watchdogGraceFrames) return;

        _unguardedFrames = 0;

        if (_holders.Count > 0)
        {
            // Naming the holders matters: this means a menu suspended under one owner and was
            // closed through another one's path, which is a bug worth chasing rather than
            // silently papering over.
            Debug.LogWarning($"[GameplayInputGate] Gameplay input is still held by " +
                             $"[{DescribeHolders()}] while the game is running. Forcing release so " +
                             "the player is not locked out - check that owner's close path.");

            _holders.Clear();
        }
        else
        {
            Debug.LogWarning("[GameplayInputGate] Gameplay input was still suspended while the game " +
                             "was running. A menu closed without restoring it, so it is being " +
                             "restored now.");
        }

        RestoreWhenReleased(null);
    }

    private string DescribeHolders()
    {
        List<string> names = new List<string>();
        foreach (Object holder in _holders) names.Add(holder != null ? holder.name : "<destroyed>");

        return string.Join(", ", names);
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
