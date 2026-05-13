using UnityEngine;

public class ExperienceManager : MonoBehaviour
{
    [Header("Experience Variables")]
    public int CurrentLevel;
    public float CurrentExp;
    public float ExpToNextLevel;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void AddExperience(float amount)
    {
        if(CurrentExp >= ExpToNextLevel)
        {
            //Call level up
        }
    }
}
