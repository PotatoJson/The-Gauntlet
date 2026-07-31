using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

// The struct perfectly matches the new Google Script expectations
[System.Serializable]
public struct ChamberTelemetry
{
    public string runID;
    public string chamberName;
    public float timeSpent;
    public int potionsUsed;
    public int staminaExhaustions;
    public int deltaHealth; // Exit Health - Entry Health
    public bool diedInChamber;
    public string killerEnemy;
    
    // --- NEW COMBAT METRICS ---
    public int lightAttacksLanded;
    public int heavyAttacksLanded;
    public int lightDamageDealt;
    public int heavyDamageDealt;
    public int rangedDamage;
}

public class MetricsTracker : MonoBehaviour
{
    public static MetricsTracker Instance { get; private set; }

    [Header("Webhook Configuration")]
    [Tooltip("Paste your Google Apps Script Web App URL here")]
    public string googleSheetURL = "https://script.google.com/macros/s/AKfycbwQ90ZZ5-fVOvUwV_6AaK8s4tlQw_M95HDqET7NctHKxBzhuYOyIrQWVPohX_Ljfdc/exec";

    // Used your exact variable name here to prevent the CS0103 error!
    private ChamberTelemetry currentChamberTelemetry;
    
    private float chamberStartTime;
    private int entryHealth;

    // A unique ID to track all chambers cleared in a single session/run
    private string currentRunID; 

    private void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); }

        // Generate a unique ID for this specific run
        currentRunID = System.Guid.NewGuid().ToString();
    }

    // Call this the exact moment the player steps into a new chamber
    public void StartChamber(string chamberName, int startingHealth)
    {
        chamberStartTime = Time.time;
        entryHealth = startingHealth;

        // Reset the struct for the new chamber
        currentChamberTelemetry = new ChamberTelemetry
        {
            runID = currentRunID,
            chamberName = chamberName,
            killerEnemy = "None"
        };
    }

    public void StartNewRun()
    {
        currentRunID = System.Guid.NewGuid().ToString();
        Debug.Log("New Run Started! ID: " + currentRunID);
    }

    /// <summary>The ID of the run in progress, so a save file can resume it instead of splitting it in two.</summary>
    public string CurrentRunID => currentRunID;

    /// <summary>Re-adopts a run ID from a save file so telemetry stays stitched to one run across a quit.</summary>
    public void ResumeRun(string runID)
    {
        if (string.IsNullOrEmpty(runID)) return;

        currentRunID = runID;
        Debug.Log("Resumed Run ID: " + currentRunID);
    }
    // --- Tracking Methods ---

    public void RecordPotionUsed() => currentChamberTelemetry.potionsUsed++;
    public void RecordStaminaExhaustion() => currentChamberTelemetry.staminaExhaustions++;
    
    // Kept this just in case you ever give the player a bow or magic
    public void RecordDamage(string type, int amount)
    {
        if (type == "Ranged") currentChamberTelemetry.rangedDamage += amount;
    }

    // New Melee Tracking for Light vs Heavy
    public void RecordMeleeHit(CombatInput attackType, int damage)
    {
        if (attackType == CombatInput.Light) 
        {
            currentChamberTelemetry.lightAttacksLanded++;
            currentChamberTelemetry.lightDamageDealt += damage;
        }
        else if (attackType == CombatInput.Heavy)
        {
            currentChamberTelemetry.heavyAttacksLanded++;
            currentChamberTelemetry.heavyDamageDealt += damage;
        }
    }

    /// Call this when leaving the chamber alive OR when dying
    public void EndChamber(int exitHealth, bool died, string enemyName = "None")
    {
        currentChamberTelemetry.timeSpent = Time.time - chamberStartTime;
        currentChamberTelemetry.deltaHealth = exitHealth - entryHealth;
        currentChamberTelemetry.diedInChamber = died;
        currentChamberTelemetry.killerEnemy = enemyName;

        // Fire and forget the web request
        StartCoroutine(SendToGoogleSheet(currentChamberTelemetry));

        if (died) 
        {
            // If they died, the run is over. Generate a new Run ID for the next attempt.
            // currentRunID = System.Guid.NewGuid().ToString();
        }
    }

    private IEnumerator SendToGoogleSheet(ChamberTelemetry dataPayload)
    {
        string jsonData = JsonUtility.ToJson(dataPayload);
        
        using (UnityWebRequest request = new UnityWebRequest(googleSheetURL, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Metrics Send Error: {request.error}");
            }
            else
            {
                Debug.Log($"Metrics logged for chamber: {dataPayload.chamberName}");
            }
        }
    }
}