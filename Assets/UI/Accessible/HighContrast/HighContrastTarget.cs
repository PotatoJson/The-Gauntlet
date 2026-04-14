using UnityEngine;

public class HighContrastTarget : MonoBehaviour
{
    public enum TargetType { Player, Enemy, Structure }
    public TargetType type;

    private Material[] _originalMaterials;
    private Renderer _renderer;

    private void Awake()
    {
        // 1. Try to find the renderer on this object
        _renderer = GetComponent<Renderer>();

        // 2. If it's a character, the SkinnedMeshRenderer is likely in a child object
        if (_renderer == null)
        {
            _renderer = GetComponentInChildren<SkinnedMeshRenderer>();
        }

        // 3. Last resort: check for any generic renderer in children
        if (_renderer == null)
        {
            _renderer = GetComponentInChildren<Renderer>();
        }

        if (_renderer != null)
        {
            _originalMaterials = _renderer.sharedMaterials;
        }
        else
        {
            // This will tell you exactly which object is causing the crash
            Debug.LogError($"[HighContrast] No Renderer found on {gameObject.name} or its children!");
        }
    }

    public void ApplyColor(Material highContrastMat, Color color)
    {
        if (_renderer == null || highContrastMat == null) return;

        // Use 'materials' (plural) to ensure all sub-meshes change color
        Material[] newMats = new Material[_originalMaterials.Length];
        for (int i = 0; i < newMats.Length; i++)
        {
            newMats[i] = new Material(highContrastMat);
            newMats[i].SetColor("_Color", color);
        }
        _renderer.materials = newMats;
    }

    public void ResetMaterials()
    {
        if (_renderer != null && _originalMaterials != null)
        {
            _renderer.materials = _originalMaterials;
        }
    }
}