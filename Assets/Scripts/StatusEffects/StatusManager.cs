using UnityEngine;
using System.Collections.Generic;

public class StatusManager : MonoBehaviour
{
    private BaseEnemy _myEnemy;
    private List<BaseStatusEffect> _activeStatuses = new List<BaseStatusEffect>();

    private void Awake()
    {
        _myEnemy = GetComponent<BaseEnemy>();
    }

    private void Update()
    {
        // Loop backwards so we can safely remove items from the list as they expire
        for (int i = _activeStatuses.Count - 1; i >= 0; i--)
        {
            BaseStatusEffect status = _activeStatuses[i];
            
            status.OnTick(_myEnemy);

            if (status.UpdateTimer(Time.deltaTime))
            {
                status.OnRemove(_myEnemy);
                _activeStatuses.RemoveAt(i);
            }
        }
    }

    public void ApplyStatus(BaseStatusEffect newStatus)
    {
        if (CheckForSynergies(newStatus))
        {
            return; 
        }
        
        newStatus.OnApply(_myEnemy);
        _activeStatuses.Add(newStatus);
    }

//synergies need more ideation but some implemention for later here (not working currently)
    private bool CheckForSynergies(BaseStatusEffect incomingStatus)
    {
        // Example: If hitting a "Wet" enemy with "Lightning"
        if (incomingStatus.StatusID == "Lightning")
        {
            BaseStatusEffect wetStatus = GetStatusByID("Wet");
            if (wetStatus != null)
            {
                wetStatus.OnRemove(_myEnemy);
                _activeStatuses.Remove(wetStatus);

                Debug.Log("SYNERGY TRIGGERED: Electrocuted!");
                //ApplyStatus(new StunStatus(3f)); 
                
                return true;
            }
        }
        return false;
    }

    private BaseStatusEffect GetStatusByID(string id)
    {
        foreach (var status in _activeStatuses)
        {
            if (status.StatusID == id) return status;
        }
        return null;
    }

}
