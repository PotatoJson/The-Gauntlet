using UnityEngine;

public class HUD : MonoBehaviour
{
    [SerializeField] private GameObject HUD_Object;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        HUD_Object.SetActive(true);
    }

    void Start()
    {
        HUD_Object.SetActive(true);

    }
    // Update is called once per frame
    void Update()
    {
        
    }
}
