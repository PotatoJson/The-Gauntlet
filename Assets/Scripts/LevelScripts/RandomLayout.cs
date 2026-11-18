using UnityEngine;

/// <summary>
/// Picks one of its children at random when the level loads and switches the rest off. Put each version of a room's
/// props, cover or hazards under one of these (as "Layout A", "Layout B", ...) and every run gets one of them.
/// Keep spawn points, triggers and doors outside it: those must always be there.
///
/// The room's navmesh is baked once for every layout, so give anything in a layout that enemies must walk around a
/// NavMeshObstacle with Carve ticked, and leave layout objects out of the bake; otherwise enemies route around props
/// that aren't there, or through ones that are.
/// </summary>
public class RandomLayout : MonoBehaviour
{
    private void Awake()
    {
        if (transform.childCount == 0) return;

        int keep = Random.Range(0, transform.childCount);
        for (int i = 0; i < transform.childCount; i++) transform.GetChild(i).gameObject.SetActive(i == keep);
    }
}
