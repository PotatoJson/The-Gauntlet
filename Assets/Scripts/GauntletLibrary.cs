using UnityEngine;

// Place one GauntletLibrary in the scene (or a persistent GameObject) and assign all GauntletData assets
public class GauntletLibrary : MonoBehaviour
{
    public GauntletData[] allGauntlets = new GauntletData[0];

    private static GauntletLibrary _instance;
    public static GauntletLibrary Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<GauntletLibrary>();
            }
            return _instance;
        }
    }

    public GauntletData FindByName(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        foreach (var g in allGauntlets)
        {
            if (g != null && g.name == name) return g;
        }
        return null;
    }
}
