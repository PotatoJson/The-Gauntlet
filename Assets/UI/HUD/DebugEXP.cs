using UnityEngine;

/// <summary>Retired: the +/- shortcuts now live in the debug menu (press 0). Kept so scenes don't lose the component.</summary>
public class DebugEXP : MonoBehaviour
{
    [SerializeField] private ProgressBarCircle expBar;
    [SerializeField] private float expChunk = 25f;
}
