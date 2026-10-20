using DG.Tweening;
using UnityEngine;

/// <summary>
/// A small black hole: a dark core swells open, a purple halo spins round it while sparks spiral in, then it
/// collapses with a pop. Played where an Eyeball dies.
/// </summary>
public class BlackHoleFx : MonoBehaviour
{
    [SerializeField] private SpriteRenderer core;    // dark, alpha-blended
    [SerializeField] private SpriteRenderer halo;    // glowing ring, additive
    [SerializeField] private SpriteRenderer rays;    // streaks turning the other way round the halo (optional)
    [SerializeField] private ParticleSystem swirl;   // sparks spiralling inwards
    [SerializeField] private ParticleSystem pop;     // the burst when it collapses

    [SerializeField] private float radius = 1.1f;
    [SerializeField] private float openSeconds = 0.25f;
    [SerializeField] private float holdSeconds = 0.85f;
    [SerializeField] private float collapseSeconds = 0.3f;
    [SerializeField] private float haloSpinDegrees = 160f;

    private Transform _camera;
    private float _spin;

    public float TotalSeconds => openSeconds + holdSeconds + collapseSeconds;

    public void Play()
    {
        float coreSize = radius * 2f / Mathf.Max(0.001f, core.sprite.bounds.size.x);
        float haloSize = radius * 3.2f / Mathf.Max(0.001f, halo.sprite.bounds.size.x);

        float raysSize = rays != null ? radius * 3.6f / Mathf.Max(0.001f, rays.sprite.bounds.size.x) : 0f;

        core.transform.localScale = Vector3.zero;
        halo.transform.localScale = Vector3.zero;
        if (rays != null) rays.transform.localScale = Vector3.zero;

        core.transform.DOScale(Vector3.one * coreSize, openSeconds).SetEase(Ease.OutBack).SetLink(gameObject);
        halo.transform.DOScale(Vector3.one * haloSize, openSeconds).SetEase(Ease.OutBack).SetLink(gameObject);
        if (rays != null) rays.transform.DOScale(Vector3.one * raysSize, openSeconds).SetEase(Ease.OutBack).SetLink(gameObject);

        if (swirl != null) swirl.Play();

        var collapse = DOTween.Sequence().SetLink(gameObject);
        collapse.AppendInterval(openSeconds + holdSeconds);
        collapse.Append(core.transform.DOScale(Vector3.zero, collapseSeconds).SetEase(Ease.InBack));
        collapse.Join(halo.transform.DOScale(Vector3.one * haloSize * 1.4f, collapseSeconds).SetEase(Ease.InQuad));
        collapse.Join(halo.DOFade(0f, collapseSeconds));
        if (rays != null)
        {
            collapse.Join(rays.transform.DOScale(Vector3.zero, collapseSeconds).SetEase(Ease.InBack));
            collapse.Join(rays.DOFade(0f, collapseSeconds));
        }
        collapse.AppendCallback(() =>
        {
            if (swirl != null) swirl.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (pop != null) pop.Play();
        });

        Destroy(gameObject, TotalSeconds + 1.2f);
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
        if (core != null) core.transform.rotation = _camera.rotation;
        if (halo != null) halo.transform.rotation = _camera.rotation * Quaternion.Euler(0f, 0f, _spin);
        if (rays != null) rays.transform.rotation = _camera.rotation * Quaternion.Euler(0f, 0f, -_spin * 0.6f);
    }
}
