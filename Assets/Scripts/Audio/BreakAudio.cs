using UnityEngine;
using System.Collections.Generic;

public class BreakAudio : MonoBehaviour
{
    [Header("FMOD Audio")]
    public string breakSoundPath;

    private static Dictionary<string, float> _lastPlayedTimes = new Dictionary<string, float>();
    private static float _cooldownWindow = 0.1f; 

    private void Start()
    {
        Fracture fractureScript = GetComponent<Fracture>();

        if (fractureScript != null)
        {
            fractureScript.callbackOptions.onFracture.AddListener(PlaySound);
        }
    }

    private void PlaySound(Collider hitByCollider, GameObject fracturedObject, Vector3 contactPoint)
    {
        if (string.IsNullOrEmpty(breakSoundPath)) return;

        if (!_lastPlayedTimes.ContainsKey(breakSoundPath))
        {
            _lastPlayedTimes[breakSoundPath] = 0f;
        }

        if (Time.time - _lastPlayedTimes[breakSoundPath] > _cooldownWindow)
        {
            FMODUnity.RuntimeManager.PlayOneShot(breakSoundPath, contactPoint);
            
            _lastPlayedTimes[breakSoundPath] = Time.time;
        }
    }
}