using UnityEngine;

/// <summary>
/// Little stars circling above a stunned boss's head, each turned to face the camera, for as long as the stun lasts.
/// </summary>
public class DizzyStars : MonoBehaviour
{
    private const int Count = 5;
    private const float TurnsPerSecond = 0.7f;

    private static Mesh _star;
    private static Material _material;

    private Transform _head;
    private float _above, _radius, _until;
    private Transform[] _stars;

    /// <summary>Stars over this head for this long; <paramref name="size"/> scales them to the one wearing them.</summary>
    public static void Show(Transform head, float seconds, float size = 1f)
    {
        if (head == null) return;
        DizzyStars stars = new GameObject("DizzyStars").AddComponent<DizzyStars>();
        stars._head = head;
        stars._above = 0.9f * size;
        stars._radius = 0.8f * size;
        stars._until = Time.time + seconds;
        stars.Build(0.32f * size);
    }

    private void Build(float starSize)
    {
        if (_star == null) _star = StarMesh();
        if (_material == null)
        {
            _material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            _material.SetColor("_BaseColor", new Color(1f, 0.92f, 0.35f, 1f));
            _material.SetFloat("_Cull", 0f);
        }

        _stars = new Transform[Count];
        for (int i = 0; i < Count; i++)
        {
            GameObject star = new GameObject("Star", typeof(MeshFilter), typeof(MeshRenderer));
            star.transform.SetParent(transform, false);
            star.transform.localScale = Vector3.one * starSize;
            star.GetComponent<MeshFilter>().sharedMesh = _star;
            MeshRenderer view = star.GetComponent<MeshRenderer>();
            view.sharedMaterial = _material;
            view.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _stars[i] = star.transform;
        }
    }

    private void LateUpdate()
    {
        if (_head == null || Time.time >= _until)
        {
            Destroy(gameObject);
            return;
        }

        transform.position = _head.position + Vector3.up * _above;
        Camera view = Camera.main;
        float turn = Time.time * TurnsPerSecond * Mathf.PI * 2f;
        for (int i = 0; i < Count; i++)
        {
            float angle = turn + i * Mathf.PI * 2f / Count;
            Vector3 round = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle * 2f) * 0.12f, Mathf.Sin(angle)) * _radius;
            _stars[i].position = transform.position + round;
            if (view != null) _stars[i].rotation = Quaternion.LookRotation(_stars[i].position - view.transform.position) * Quaternion.Euler(0f, 0f, Time.time * 200f);
        }
    }

    // A flat five-pointed star, one unit across.
    private static Mesh StarMesh()
    {
        var vertices = new Vector3[11];
        vertices[0] = Vector3.zero;
        for (int i = 0; i < 10; i++)
        {
            float angle = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
            float reach = i % 2 == 0 ? 0.5f : 0.2f;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle) * reach, Mathf.Sin(angle) * reach, 0f);
        }
        var triangles = new int[30];
        for (int i = 0; i < 10; i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = (i + 1) % 10 + 1;
        }
        Mesh mesh = new Mesh { name = "Star", vertices = vertices, triangles = triangles };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
