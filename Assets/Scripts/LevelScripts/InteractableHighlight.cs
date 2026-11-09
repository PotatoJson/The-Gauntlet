using TMPro;
using UnityEngine;
using UnityEngine.Localization.Settings;

/// <summary>
/// When a room is cleared, the gauntlet chests and shrines near it that can still be used start to glow, and a short
/// notice at the top of the screen says what's there. The glow stays until the thing is used: a chest disappears when
/// it's opened, and a shrine stops glowing once it's spent.
/// </summary>
public class InteractableHighlight : MonoBehaviour
{
    private const float Radius = 40f;          // how far from the player, at the clear, counts as "this room"
    private const float NoticeSeconds = 3.5f;
    private const string GlowMaterialPath = "Highlight/InteractableGlow";
    private static readonly Color ChestColor = new Color(1f, 0.75f, 0.3f);
    private static readonly Color ShrineColor = new Color(0.4f, 1f, 0.88f);

    private static Material _glow;

    private ShrineInteractable _shrine;

    /// <summary>Lights up the usable chests and shrines around this point, and says so.</summary>
    public static void ShowNear(Vector3 point)
    {
        int chests = 0, shrines = 0;

        // An opened chest switches itself off, so every active one can still be used.
        foreach (GauntletInteractable chest in FindObjectsByType<GauntletInteractable>(FindObjectsSortMode.None))
        {
            if (!Near(chest.transform, point)) continue;
            Add(chest.gameObject, ChestColor, null);
            chests++;
        }

        foreach (ShrineInteractable shrine in FindObjectsByType<ShrineInteractable>(FindObjectsSortMode.None))
        {
            if (shrine.IsSpent || !Near(shrine.transform, point)) continue;
            Add(shrine.gameObject, ShrineColor, shrine);
            shrines++;
        }

        string key = chests > 0 && shrines > 0 ? "Notice_ChestAndShrine" : chests > 0 ? "Notice_Chest" : shrines > 0 ? "Notice_Shrine" : null;
        if (key == null) return;

        NoticeBanner.Show(LocalizationSettings.StringDatabase.GetLocalizedString("UIStrings", key), NoticeSeconds);
    }

    private static bool Near(Transform thing, Vector3 point) => (thing.position - point).sqrMagnitude <= Radius * Radius;

    private static void Add(GameObject target, Color color, ShrineInteractable shrine)
    {
        if (target.GetComponent<InteractableHighlight>() != null) return; // already glowing

        if (_glow == null) _glow = Resources.Load<Material>(GlowMaterialPath);
        if (_glow == null) return;

        InteractableHighlight highlight = target.AddComponent<InteractableHighlight>();
        highlight._shrine = shrine;

        Material material = new Material(_glow);
        material.SetColor("_Color", color);

        // A copy of each mesh, drawn with the glow over the real one. The object's own materials stay as they are.
        foreach (MeshRenderer renderer in target.GetComponentsInChildren<MeshRenderer>())
        {
            if (renderer.GetComponent<TMP_Text>() != null) continue; // world-space prompt text
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) continue;

            GameObject copy = new GameObject("Glow", typeof(MeshFilter), typeof(MeshRenderer));
            copy.transform.SetParent(renderer.transform, false);
            copy.GetComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            MeshRenderer glowRenderer = copy.GetComponent<MeshRenderer>();
            Material[] materials = new Material[filter.sharedMesh.subMeshCount];
            for (int i = 0; i < materials.Length; i++) materials[i] = material;
            glowRenderer.sharedMaterials = materials;
            glowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            glowRenderer.receiveShadows = false;
        }
    }

    private void Update()
    {
        if (_shrine != null && _shrine.IsSpent) Remove();
    }

    private void Remove()
    {
        foreach (MeshRenderer renderer in GetComponentsInChildren<MeshRenderer>(true))
        {
            if (renderer.gameObject.name == "Glow") Destroy(renderer.gameObject);
        }
        Destroy(this);
    }

}
