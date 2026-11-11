using UnityEngine;

/// <summary>
/// Glass that cracks and shatters: the mirror at the bottom of Hell, the Unfinished Knight's summoned mirrors, and
/// Brutus's ward panes around his arena.
/// </summary>
public static class GlassFx
{
    /// <summary>White crack lines on a clear texture, branching out from a point (0-1 across and up the glass).</summary>
    public static Texture2D DrawCracks(Vector2 centre, int width = 64, int height = 96)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        Color32[] pixels = new Color32[width * height];

        void Ray(Vector2 at, float angle, int steps)
        {
            for (int s = 0; s < steps; s++)
            {
                angle += Random.Range(-0.45f, 0.45f);
                at += new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 1.6f;
                int x = Mathf.RoundToInt(at.x), y = Mathf.RoundToInt(at.y);
                if (x < 0 || y < 0 || x >= width || y >= height) return;
                pixels[y * width + x] = new Color32(255, 255, 255, 255);
                if (Random.value < 0.035f) Ray(at, angle + Random.Range(-1f, 1f), (steps - s) / 2);
            }
        }

        Vector2 from = new Vector2(centre.x * width, centre.y * height);
        const int rays = 11;
        for (int i = 0; i < rays; i++) Ray(from, (i + Random.value * 0.6f) / rays * Mathf.PI * 2f, Random.Range(30, 70));

        texture.SetPixels32(pixels);
        texture.Apply();
        return texture;
    }

    /// <summary>
    /// A pane of glass bursting into flying shards. The pane lies in the plane of <paramref name="glass"/>: its local
    /// x across, y up from <paramref name="bottom"/> to <paramref name="top"/>; the shards fly out along its forward.
    /// </summary>
    public static void Shatter(Transform glass, float bottom, float top, float width, Material shardMaterial, int shards = 34, float burst = 1f)
    {
        float half = width * 0.5f;
        int debris = LayerMask.NameToLayer("Debris");
        for (int i = 0; i < shards; i++)
        {
            Vector3 local = new Vector3(Random.Range(-half, half), Random.Range(bottom, top), 0.06f);
            GameObject shard = new GameObject("GlassShard", typeof(MeshFilter), typeof(MeshRenderer));
            shard.layer = debris >= 0 ? debris : 0;
            shard.transform.SetPositionAndRotation(glass.TransformPoint(local), glass.rotation * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)));
            shard.GetComponent<MeshFilter>().sharedMesh = ShardMesh(Random.Range(0.12f, 0.32f));
            shard.GetComponent<MeshRenderer>().sharedMaterial = shardMaterial;

            BoxCollider box = shard.AddComponent<BoxCollider>();
            box.size = new Vector3(0.2f, 0.2f, 0.02f);
            Rigidbody body = shard.AddComponent<Rigidbody>();
            body.mass = 0.05f;
            body.linearVelocity = (glass.forward * Random.Range(3f, 7.5f) + Vector3.up * Random.Range(0.5f, 4f) + glass.right * Random.Range(-3f, 3f)) * burst;
            body.angularVelocity = Random.insideUnitSphere * 14f;
            Object.Destroy(shard, Random.Range(2f, 3.2f));
        }
    }

    // A small jagged triangle, seen from both sides.
    private static Mesh ShardMesh(float size)
    {
        Mesh mesh = new Mesh { name = "Shard" };
        mesh.vertices = new[]
        {
            new Vector3(0f, size, 0f),
            new Vector3(size * Random.Range(0.4f, 0.9f), -size * Random.Range(0.3f, 0.8f), 0f),
            new Vector3(-size * Random.Range(0.4f, 0.9f), -size * Random.Range(0.3f, 0.8f), 0f)
        };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 1 };
        mesh.RecalculateNormals();
        return mesh;
    }
}

/// <summary>Something the player's fists can hit that isn't an enemy: a mirror to break, a ward to smash.</summary>
public interface IPunchable
{
    void OnPunched(Vector3 at, bool heavy);
}

/// <summary>An <see cref="IPunchable"/> that just passes each punch on to whoever is listening.</summary>
public class PunchTarget : MonoBehaviour, IPunchable
{
    public event System.Action<Vector3, bool> Punched;

    public void OnPunched(Vector3 at, bool heavy) => Punched?.Invoke(at, heavy);
}
