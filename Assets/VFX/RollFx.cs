using UnityEngine;

/// <summary>
/// One roll's effect: wind streaks that follow the player for as long as the roll lasts.
/// </summary>
public class RollFx : MonoBehaviour
{
    [SerializeField] private ParticleSystem streaks;
    [SerializeField] private float lifetime = 1.8f;

    private PlayerMovement _movement;

    public void Play(PlayerMovement movement, Vector3 direction)
    {
        _movement = movement;
        transform.SetPositionAndRotation(movement.transform.position, Quaternion.LookRotation(direction, Vector3.up));

        streaks.Play();

        Destroy(gameObject, lifetime);
    }

    private void LateUpdate()
    {
        if (_movement == null || !_movement.IsRolling)
        {
            if (streaks.isEmitting) streaks.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            return;
        }

        transform.position = _movement.transform.position;
    }
}
