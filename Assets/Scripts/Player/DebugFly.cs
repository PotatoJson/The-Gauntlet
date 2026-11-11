using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Debug menu: fly anywhere, through walls and over gaps. WASD (or the left stick) moves along where the camera looks,
/// Space / right trigger rises, Ctrl / left trigger sinks, Shift goes fast. Gravity, collisions and the player's own
/// movement are off while it's on; turning it off drops the player where they are.
///
/// Walls aren't solid while flying, so triggers don't fire either: a room only spawns once you land in it.
/// </summary>
public class DebugFly : MonoBehaviour
{
    private const float Speed = 10f;
    private const float FastSpeed = 30f;

    private CharacterController _controller;
    private PlayerMovement _movement;

    // A death or a restart reloads the level with a fresh player, so the setting is kept here and put back on it.
    private static bool _wanted;

    [RuntimeInitializeOnLoadMethod]
    private static void KeepAcrossLoads() => UnityEngine.SceneManagement.SceneManager.sceneLoaded += (_, _) =>
    {
        PlayerMovement player = FindFirstObjectByType<PlayerMovement>();
        if (_wanted && player != null && player.GetComponent<DebugFly>() == null) player.gameObject.AddComponent<DebugFly>();
    };

    public static bool IsOn { get { PlayerMovement p = FindFirstObjectByType<PlayerMovement>(); return p != null && p.GetComponent<DebugFly>() != null; } }

    /// <summary>Turns flying on or off for the player. Returns whether it's now on.</summary>
    public static bool Toggle()
    {
        PlayerMovement player = FindFirstObjectByType<PlayerMovement>();
        if (player == null) return false;

        DebugFly fly = player.GetComponent<DebugFly>();
        if (fly != null)
        {
            Destroy(fly);
            _wanted = false;
            return false;
        }

        player.gameObject.AddComponent<DebugFly>();
        _wanted = true;
        return true;
    }

    private void OnEnable()
    {
        _controller = GetComponent<CharacterController>();
        _movement = GetComponent<PlayerMovement>();
        _movement.enabled = false;
        _controller.enabled = false;
    }

    private void OnDisable()
    {
        _controller.enabled = true;
        _movement.enabled = true;
        _movement.ResetMotion();
    }

    private void Update()
    {
        // Respawning and level loads switch these back on under us; flying keeps them off.
        _movement.enabled = false;
        _controller.enabled = false;

        // The debug menu (or any other menu) has the input while it's open.
        if (GameplayInputGate.Instance != null && GameplayInputGate.Instance.IsSuspended) return;

        Vector2 move = Vector2.zero;
        float rise = 0f;
        bool fast = false;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            move += new Vector2((keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
                                (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
            rise += (keyboard.spaceKey.isPressed ? 1 : 0) - (keyboard.leftCtrlKey.isPressed ? 1 : 0);
            fast |= keyboard.leftShiftKey.isPressed;
        }

        Gamepad pad = Gamepad.current;
        if (pad != null)
        {
            move += pad.leftStick.ReadValue();
            rise += pad.rightTrigger.ReadValue() - pad.leftTrigger.ReadValue();
            fast |= pad.leftStickButton.isPressed;
        }

        Transform view = Camera.main != null ? Camera.main.transform : transform;
        Vector3 forward = view.forward;
        Vector3 right = Vector3.ProjectOnPlane(view.right, Vector3.up).normalized;
        Vector3 direction = Vector3.ClampMagnitude(forward * move.y + right * move.x, 1f) + Vector3.up * Mathf.Clamp(rise, -1f, 1f);

        transform.position += direction * ((fast ? FastSpeed : Speed) * Time.unscaledDeltaTime);

        Vector3 facing = Vector3.ProjectOnPlane(direction, Vector3.up);
        if (facing.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(facing);
    }
}
