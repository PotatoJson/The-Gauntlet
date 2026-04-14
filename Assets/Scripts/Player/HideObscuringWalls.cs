using System.Collections.Generic;
using UnityEngine;

public class HideObscuringWalls : MonoBehaviour
{
    [Tooltip("The player's body to shoot the raycast towards.")]
    public Transform playerTarget;
    
    [Tooltip("The layers that are allowed to fade (e.g., FadeObjects).")]
    public LayerMask wallLayerMask;

    [Tooltip("Drag the GhostMaterial you created into this slot.")]
    public Material ghostMaterial;

    // A dictionary to remember the original materials of the objects we hide
    private Dictionary<Renderer, Material[]> _hiddenObjects = new Dictionary<Renderer, Material[]>();
    // A temporary list to track what we hit this exact frame
    private List<Renderer> _hitsThisFrame = new List<Renderer>();

    void LateUpdate()
    {
        if (playerTarget == null || ghostMaterial == null) return;

        Vector3 directionToPlayer = playerTarget.position - transform.position;
        float distanceToPlayer = directionToPlayer.magnitude;

        // 1. Find everything currently blocking the camera
        RaycastHit[] hits = Physics.RaycastAll(transform.position, directionToPlayer.normalized, distanceToPlayer, wallLayerMask);
        _hitsThisFrame.Clear();

        // 2. Hide whatever we hit by swapping its material
        foreach (RaycastHit hit in hits)
        {
            Renderer objRenderer = hit.collider.GetComponent<Renderer>();
            if (objRenderer != null)
            {
                _hitsThisFrame.Add(objRenderer);
                
                // If this is the first frame we hit it, save its original materials and swap them
                if (!_hiddenObjects.ContainsKey(objRenderer))
                {
                    // Save the original materials
                    _hiddenObjects.Add(objRenderer, objRenderer.sharedMaterials);
                    
                    // Create an array of our Ghost material to replace the old ones
                    Material[] ghostMats = new Material[objRenderer.sharedMaterials.Length];
                    for (int i = 0; i < ghostMats.Length; i++) 
                    {
                        ghostMats[i] = ghostMaterial;
                    }
                    
                    // Apply the swap!
                    objRenderer.sharedMaterials = ghostMats;
                }
            }
        }

        // 3. Un-hide objects that are NO LONGER being hit
        List<Renderer> objectsToRestore = new List<Renderer>();
        foreach (var kvp in _hiddenObjects)
        {
            if (!_hitsThisFrame.Contains(kvp.Key))
            {
                // Put the original materials back
                if (kvp.Key != null) 
                {
                    kvp.Key.sharedMaterials = kvp.Value;
                }
                objectsToRestore.Add(kvp.Key);
            }
        }

        // Remove the restored objects from our tracking dictionary
        foreach (Renderer rend in objectsToRestore)
        {
            _hiddenObjects.Remove(rend);
        }
    }
}