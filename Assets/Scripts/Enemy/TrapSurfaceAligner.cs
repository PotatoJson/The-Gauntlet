using UnityEngine;

[ExecuteAlways]
public class TrapSurfaceAligner : MonoBehaviour
{
    [Header("Surface Alignment")]
    [SerializeField] private LayerMask surfaceMask = ~0;
    [SerializeField] private float probeDistance = 1f;
    [SerializeField] private float probeRadius = 0.1f;
    [SerializeField] private float surfaceOffset = 0f;
    [SerializeField] private bool alignOnAwake = true;
    [SerializeField] private bool alignInEditor = true;

    private static readonly Vector3[] ProbeDirections =
    {
        Vector3.up,
        Vector3.down,
        Vector3.left,
        Vector3.right,
        Vector3.forward,
        Vector3.back
    };

    private void Awake()
    {
        if (alignOnAwake)
        {
            AlignToSurface();
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying && alignInEditor)
        {
            AlignToSurface();
        }
    }
#endif

    public void AlignToSurface()
    {
        if (!TryGetBestSurfaceHit(out RaycastHit hit))
        {
            return;
        }

        Quaternion rotation = Quaternion.FromToRotation(transform.up, hit.normal) * transform.rotation;
        transform.rotation = rotation;
        transform.position = hit.point + hit.normal * surfaceOffset;
    }

    private bool TryGetBestSurfaceHit(out RaycastHit bestHit)
    {
        Vector3 origin = transform.position;
        bestHit = default;

        bool found = false;
        float bestDistance = float.MaxValue;

        foreach (Vector3 direction in ProbeDirections)
        {
            if (Physics.SphereCast(origin, probeRadius, direction, out RaycastHit hit, probeDistance, surfaceMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.distance < bestDistance)
                {
                    bestDistance = hit.distance;
                    bestHit = hit;
                    found = true;
                }
            }
        }

        return found;
    }
}