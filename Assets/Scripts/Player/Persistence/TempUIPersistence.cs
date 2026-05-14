using UnityEngine;

public class TempUIPersistence : MonoBehaviour
{//This is for carrying data over to the next scene for the player
    public static TempUIPersistence Instance;

    private void Awake()
    {
        // 1. The Highlander Rule: There can be only one UI Canvas!
        if (Instance == null)
        {
            Instance = this;
            
            // Tell Unity to completely ignore this Canvas when unloading the scene
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            // If your teammates left a "dummy" Canvas in Level 2 for their own testing,
            // destroy it so your fully loaded Level 1 Canvas takes over!
            Destroy(gameObject);
        }
    }
}