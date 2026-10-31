using DG.Tweening;
using UnityEngine;

/// <summary>
/// The Eyeball winding up a fireball: an orb swells in front of the eye while embers spiral into it, then it either
/// flashes as the fireball leaves (<see cref="Release"/>) or fizzles when the Eyeball is interrupted (<see cref="Cancel"/>).
/// </summary>
public class EyeChargeFx : MonoBehaviour
{
    [SerializeField] private SpriteRenderer core;    // the growing orb, additive
    [SerializeField] private SpriteRenderer halo;    // ring that closes in round it, additive
    [SerializeField] private ParticleSystem swirl;   // embers spiralling inwards
    [SerializeField] private ParticleSystem pop;     // the burst as the fireball leaves

    [SerializeField] private float radius = 0.6f;
    [SerializeField] private float haloSpinDegrees = 220f;
    [SerializeField] private Color startColor = new Color(0.9f, 0.15f, 0.03f, 0.5f);
    [SerializeField] private Color endColor = new Color(1f, 0.65f, 0.2f, 1f);

    private Transform _camera;
    private float _spin;
    private float _coreSize;
    private float _haloSize;

    public void Begin(float seconds)
    {
        _coreSize = radius * 2f / Mathf.Max(0.001f, core.sprite.bounds.size.x);
        _haloSize = radius * 3f / Mathf.Max(0.001f, halo.sprite.bounds.size.x);

        core.color = startColor;
        core.transform.localScale = Vector3.zero;
        core.transform.DOScale(Vector3.one * _coreSize, seconds).SetEase(Ease.InQuad).SetLink(gameObject);
        core.DOColor(endColor, seconds).SetEase(Ease.InQuad).SetLink(gameObject);

        halo.color = new Color(endColor.r, endColor.g, endColor.b, 0f);
        halo.transform.localScale = Vector3.one * _haloSize * 2f;
        halo.transform.DOScale(Vector3.one * _haloSize, seconds).SetEase(Ease.OutQuad).SetLink(gameObject);
        halo.DOFade(0.7f, seconds).SetEase(Ease.InQuad).SetLink(gameObject);

        swirl.Play();

        Destroy(gameObject, seconds + 3f); // safety net if the shot never comes
    }

    /// <summary>The fireball has left: a last bright flash, then gone.</summary>
    public void Release()
    {
        Finish(1.5f, 0.12f);
        pop.Play();
    }

    /// <summary>Interrupted: the orb shrinks away without a burst.</summary>
    public void Cancel() => Finish(0f, 0.15f);

    private void Finish(float endScale, float seconds)
    {
        core.DOKill();
        core.transform.DOKill();
        halo.DOKill();
        halo.transform.DOKill();

        core.transform.DOScale(Vector3.one * _coreSize * endScale, seconds).SetLink(gameObject);
        core.DOFade(0f, seconds).SetLink(gameObject);
        halo.transform.DOScale(Vector3.one * _haloSize * Mathf.Max(endScale, 1f), seconds).SetLink(gameObject);
        halo.DOFade(0f, seconds).SetLink(gameObject);

        swirl.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        Destroy(gameObject, 1.2f);
    }

    private void LateUpdate()
    {
        if (_camera == null)
        {
            if (PlayerCamera.Instance != null && PlayerCamera.Instance.cameraObject != null)
                _camera = PlayerCamera.Instance.cameraObject.transform;
            else if (Camera.main != null)
                _camera = Camera.main.transform;
        }

        if (_camera == null) return;

        _spin += haloSpinDegrees * Time.deltaTime;
        core.transform.rotation = _camera.rotation;
        halo.transform.rotation = _camera.rotation * Quaternion.Euler(0f, 0f, _spin);
    }
}
