using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The fire wards round Brutus's arena: iron posts along the edge of his platform with panes of glowing glass between
/// them, so that nobody goes off the edge into the spikes. A pane takes two blows (his charge, or the player thrown
/// into it): it cracks at the first and shatters at the second, leaving a gap. His roar into phase 2 breaks them all.
/// The ring measures the platform itself when it rises, so it fits whatever floor it's raised on.
/// </summary>
public class WardRing : MonoBehaviour
{
    public const int HitsToBreak = 2;
    private const int Sides = 16;
    private const float Inset = 0.9f;  // in from the edge of the floor
    private const float Height = 3.6f;
    private const float RiseSeconds = 1.2f;

    public class Pane
    {
        public Transform Glass;
        public Material Material;
        public Collider Body;
        public Vector3 From, To;
        public int Hits;
        public bool Broken => Hits >= HitsToBreak;
    }

    private readonly List<Pane> _panes = new List<Pane>();
    private readonly List<Transform> _posts = new List<Transform>();
    private Vector3 _centre;
    private Material _shardMaterial;

    /// <summary>Raises a ring of wards round the floor that <paramref name="centre"/> stands on.</summary>
    public static WardRing Raise(Vector3 centre, GameObject postPrefab, Material postMaterial, Material glassMaterial, Material shardMaterial)
    {
        WardRing ring = new GameObject("WardRing").AddComponent<WardRing>();
        ring._centre = EnemyFx.FloorUnder(centre + Vector3.up * 2f);
        ring._shardMaterial = shardMaterial;
        ring.Build(postPrefab, postMaterial, glassMaterial);
        ring.StartCoroutine(ring.Rise());
        return ring;
    }

    private void Build(GameObject postPrefab, Material postMaterial, Material glassMaterial)
    {
        var corners = new Vector3[Sides];
        for (int i = 0; i < Sides; i++)
        {
            Vector3 along = Quaternion.Euler(0f, i * 360f / Sides, 0f) * Vector3.forward;
            corners[i] = EnemyFx.FloorUnder(_centre + along * (EdgeDistance(_centre, along) - Inset) + Vector3.up * 2f);
        }

        for (int i = 0; i < Sides; i++)
        {
            Transform post = Instantiate(postPrefab, corners[i], Quaternion.LookRotation(corners[i] - _centre), transform).transform;
            foreach (Renderer part in post.GetComponentsInChildren<Renderer>()) part.sharedMaterial = postMaterial;
            _posts.Add(post);

            Vector3 from = corners[i], to = corners[(i + 1) % Sides];
            _panes.Add(BuildPane(from, to, glassMaterial));
        }
    }

    private Pane BuildPane(Vector3 from, Vector3 to, Material glassMaterial)
    {
        Vector3 across = to - from;
        across.y = 0f;
        float width = across.magnitude;
        Vector3 inward = Vector3.Cross(across.normalized, Vector3.up);
        if (Vector3.Dot(inward, _centre - from) < 0f) inward = -inward;

        GameObject glass = new GameObject("WardPane", typeof(MeshFilter), typeof(MeshRenderer));
        glass.transform.SetParent(transform, false);
        glass.transform.SetPositionAndRotation((from + to) * 0.5f, Quaternion.LookRotation(inward));
        glass.GetComponent<MeshFilter>().sharedMesh = PaneMesh(width);
        Material material = new Material(glassMaterial);
        material.SetTexture("_CrackTex", GlassFx.DrawCracks(new Vector2(0.5f, 0.5f), 96, 64));
        material.SetFloat("_Rise", 0f);
        MeshRenderer view = glass.GetComponent<MeshRenderer>();
        view.sharedMaterial = material;
        view.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        BoxCollider body = glass.AddComponent<BoxCollider>();
        body.center = new Vector3(0f, Height * 0.5f, 0f);
        body.size = new Vector3(width, Height, 0.4f);

        return new Pane { Glass = glass.transform, Material = material, Body = body, From = from, To = to };
    }

    // A flat pane, its pivot at the middle of its bottom edge: across along x, up along y.
    private static Mesh PaneMesh(float width)
    {
        float half = width * 0.5f;
        Mesh mesh = new Mesh { name = "WardPane" };
        mesh.vertices = new[] { new Vector3(-half, 0f, 0f), new Vector3(half, 0f, 0f), new Vector3(half, Height, 0f), new Vector3(-half, Height, 0f) };
        mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
        mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private IEnumerator Rise()
    {
        FMODUnity.RuntimeManager.PlayOneShot("event:/Environment/Gate_Open", _centre);
        foreach (Transform post in _posts) EnemyFx.Impact(post.position, 1.4f, EnemyFx.Fire);

        for (float elapsed = 0f; elapsed < RiseSeconds; elapsed += Time.deltaTime)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / RiseSeconds);
            foreach (Transform post in _posts) post.localScale = new Vector3(1f, Mathf.Max(0.01f, t), 1f);
            foreach (Pane pane in _panes) if (!pane.Broken) pane.Material.SetFloat("_Rise", t);
            yield return null;
        }
        foreach (Transform post in _posts) post.localScale = Vector3.one;
        foreach (Pane pane in _panes) if (!pane.Broken) pane.Material.SetFloat("_Rise", 1f);
    }

    /// <summary>How far from a point, along a direction, the floor runs out.</summary>
    public static float EdgeDistance(Vector3 from, Vector3 along)
    {
        along.y = 0f;
        along.Normalize();
        float floor = from.y;
        for (float r = 1f; r < 60f; r += 0.25f)
        {
            Vector3 probe = from + along * r + Vector3.up * 3f;
            bool solid = Physics.Raycast(probe, Vector3.down, out RaycastHit hit, 8f, ~0, QueryTriggerInteraction.Ignore);
            if (!solid || hit.point.y < floor - 1f) return r - 0.25f;
        }
        return 60f;
    }

    /// <summary>The first pane (whole or broken) a line across the floor meets, and where.</summary>
    public bool Cast(Vector3 from, Vector3 along, out Pane pane, out Vector3 point)
    {
        pane = null;
        point = Vector3.zero;
        along.y = 0f;
        along.Normalize();
        float nearest = float.MaxValue;
        foreach (Pane candidate in _panes)
        {
            Vector2 p = new Vector2(from.x, from.z), r = new Vector2(along.x, along.z);
            Vector2 q = new Vector2(candidate.From.x, candidate.From.z), s = new Vector2(candidate.To.x - candidate.From.x, candidate.To.z - candidate.From.z);
            float cross = r.x * s.y - r.y * s.x;
            if (Mathf.Abs(cross) < 1e-5f) continue;
            Vector2 qp = q - p;
            float t = (qp.x * s.y - qp.y * s.x) / cross;
            float u = (qp.x * r.y - qp.y * r.x) / cross;
            if (t <= 0f || u < 0f || u > 1f || t >= nearest) continue;
            nearest = t;
            pane = candidate;
            point = new Vector3(from.x + along.x * t, Mathf.Lerp(candidate.From.y, candidate.To.y, u), from.z + along.z * t);
        }
        return pane != null;
    }

    /// <summary>A blow against a pane: cracks spreading from where it landed, and at the second, the pane gone.</summary>
    public void Hit(Pane pane, Vector3 at)
    {
        if (pane == null || pane.Broken) return;
        pane.Hits++;

        Vector3 local = pane.Glass.InverseTransformPoint(at);
        float width = Vector3.Distance(new Vector3(pane.From.x, 0f, pane.From.z), new Vector3(pane.To.x, 0f, pane.To.z));
        pane.Material.SetVector("_CrackCentre", new Vector4(Mathf.Clamp01(local.x / width + 0.5f), Mathf.Clamp01(local.y / Height), 0f, 0f));
        StartCoroutine(Flash(pane));

        if (pane.Broken)
        {
            Shatter(pane);
            return;
        }

        pane.Material.SetFloat("_Crack", 0.55f);
        FMODUnity.RuntimeManager.PlayOneShot("event:/Boss/Ward_Crack", at);
        EnemyFx.Strike(at, 1.8f, EnemyFx.Fire);
    }

    private IEnumerator Flash(Pane pane)
    {
        for (float elapsed = 0f; elapsed < 0.3f && pane.Material != null; elapsed += Time.deltaTime)
        {
            pane.Material.SetFloat("_Flash", 1f - elapsed / 0.3f);
            yield return null;
        }
        if (pane.Material != null) pane.Material.SetFloat("_Flash", 0f);
    }

    private void Shatter(Pane pane, bool loud = true)
    {
        pane.Hits = HitsToBreak;
        pane.Body.enabled = false;
        pane.Glass.GetComponent<Renderer>().enabled = false;
        float width = Vector3.Distance(new Vector3(pane.From.x, 0f, pane.From.z), new Vector3(pane.To.x, 0f, pane.To.z));
        GlassFx.Shatter(pane.Glass, 0f, Height, width, _shardMaterial, loud ? 30 : 16, 0.8f);
        Vector3 middle = pane.Glass.position + Vector3.up * Height * 0.5f;
        if (loud) FMODUnity.RuntimeManager.PlayOneShot("event:/Boss/Glass_Shatter", middle);
        EnemyFx.Impact(pane.Glass.position, 2f, EnemyFx.Fire);
    }

    /// <summary>Every pane left, breaking in a wave out from a point: his roar.</summary>
    public IEnumerator ShatterAll(Vector3 from, float seconds)
    {
        var order = new List<Pane>(_panes);
        order.RemoveAll(pane => pane.Broken);
        order.Sort((a, b) => Vector3.Distance(from, a.Glass.position).CompareTo(Vector3.Distance(from, b.Glass.position)));

        float near = order.Count > 0 ? Vector3.Distance(from, order[0].Glass.position) : 0f;
        float far = order.Count > 0 ? Vector3.Distance(from, order[order.Count - 1].Glass.position) : 1f;
        float started = Time.time;
        for (int i = 0; i < order.Count; i++)
        {
            float when = Mathf.InverseLerp(near, far, Vector3.Distance(from, order[i].Glass.position)) * seconds;
            while (Time.time - started < when) yield return null;
            Shatter(order[i], i % 3 == 0); // not every pane's sound at once
        }
    }

    public bool AllBroken => _panes.TrueForAll(pane => pane.Broken);
}
