using UnityEngine;

/// <summary>
/// The player's movement effects. PlayerMovement adds this at runtime, so no scene or prefab needs wiring up.
///
///   Sprint  short speed lines around the edge of the screen, faded in and out with the Running state
///   Roll    one RollFx per roll (wind streaks)
///
/// The prefabs live in Resources/PlayerFx. A missing one quietly does nothing, so movement never breaks over a visual.
/// </summary>
public class PlayerMotionFx : MonoBehaviour
{
    private const float SprintLinesPerSecond = 70f;
    private const float SprintFadeSpeed = 7f;
    private static readonly Vector3 SprintLinesOffset = new Vector3(0f, 0f, 3f); // in front of the camera, nearer than the player

    private PlayerManager _state;
    private PlayerMovement _movement;
    private ParticleSystem _sprintLines;
    private Transform _sprintCamera;
    private float _sprintAmount;

    private void Awake()
    {
        _state = GetComponent<PlayerManager>();
        _movement = GetComponent<PlayerMovement>();
    }

    private void LateUpdate()
    {
        bool sprinting = _state.GetCurrentState() == PlayerState.Running;
        _sprintAmount = Mathf.MoveTowards(_sprintAmount, sprinting ? 1f : 0f, SprintFadeSpeed * Time.deltaTime);

        if (_sprintAmount <= 0f && _sprintLines == null) return; // not created until the first sprint
        if (!EnsureSprintLines()) return;

        var emission = _sprintLines.emission;
        emission.rateOverTimeMultiplier = SprintLinesPerSecond * _sprintAmount;
    }

    /// <summary>Plays the roll's wind streaks, heading in this world direction.</summary>
    public void PlayRoll(Vector3 direction)
    {
        GameObject prefab = Resources.Load<GameObject>("PlayerFx/RollFx");
        if (prefab == null) return;

        Instantiate(prefab).GetComponent<RollFx>().Play(_movement, direction);
    }

    // The lines live on the camera so they never smear as it moves. It can change when a level loads.
    private bool EnsureSprintLines()
    {
        Camera main = Camera.main;
        if (main == null) return false;

        if (_sprintLines == null)
        {
            GameObject prefab = Resources.Load<GameObject>("PlayerFx/SprintLinesFx");
            if (prefab == null) return false;

            _sprintLines = Instantiate(prefab).GetComponent<ParticleSystem>();
            _sprintCamera = null;
        }

        if (_sprintCamera != main.transform)
        {
            _sprintCamera = main.transform;
            _sprintLines.transform.SetParent(_sprintCamera, false);
            _sprintLines.transform.localPosition = SprintLinesOffset;
            _sprintLines.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // emits back towards the camera
        }

        return true;
    }
}
