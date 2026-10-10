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

    /// <summary>The enemy's StatusManager, added on the spot when it doesn't have one yet.</summary>
    public static StatusManager For(BaseEnemy enemy)
    {
        StatusManager manager = enemy.GetComponent<StatusManager>();
        return manager != null ? manager : enemy.gameObject.AddComponent<StatusManager>();
    }

    /// <summary>The active status of this type, or null.</summary>
    public T Get<T>() where T : BaseStatusEffect
    {
        foreach (BaseStatusEffect status in _activeStatuses)
        {
            if (status is T match) return match;
        }
        return null;
    }

    public void ApplyStatus(BaseStatusEffect newStatus)
    {
        if (CheckForSynergies(newStatus))
        {
            return;
        }

        // The same status again refreshes the old one instead of stacking a second copy of it.
        BaseStatusEffect existing = GetStatusByID(newStatus.StatusID);
        if (existing != null)
        {
            existing.OnRemove(_myEnemy);
            _activeStatuses.Remove(existing);
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
