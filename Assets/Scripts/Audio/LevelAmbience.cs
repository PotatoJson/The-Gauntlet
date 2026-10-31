using System.Collections.Generic;
using FMOD.Studio;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Plays each level's background ambience loop. It creates itself once at startup and survives scene loads, so no
/// scene needs wiring: add a level to the table below to give it ambience. Reloading the same level (after a death)
/// leaves the loop running instead of restarting it. The loop holds its place while the pause menu is open.
/// </summary>
public class LevelAmbience : MonoBehaviour
{
    private static readonly Dictionary<string, string> EventByScene = new Dictionary<string, string>
    {
        { "Dungeon Level", "event:/Ambience/Amb_Dungeon" },
        { "Cave Level", "event:/Ambience/Amb_Cave" },
        { "Hell Level", "event:/Ambience/Amb_Hell" },
    };

    private EventInstance _instance;
    private string _playing;
    private GauntletMenu _menu;
    private bool _paused;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        GameObject host = new GameObject("[LevelAmbience]");
        DontDestroyOnLoad(host);
        host.AddComponent<LevelAmbience>();
    }

    private void Awake()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        Play(SceneManager.GetActiveScene().name); // the first scene loaded before this object existed
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Stop();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Play(scene.name);

    // The inventory and reward screens also stop time, so this asks the pause menu rather than Time.timeScale.
    private void Update()
    {
        if (_playing == null) return;

        bool paused = _menu != null && _menu.IsPaused;
        if (paused == _paused) return;

        _paused = paused;
        _instance.setPaused(paused);
    }

    private void Play(string sceneName)
    {
        _menu = FindFirstObjectByType<GauntletMenu>(FindObjectsInactive.Include);

        EventByScene.TryGetValue(sceneName, out string path);
        if (path == _playing) return;

        Stop();
        if (path == null) return;

        _instance = FMODUnity.RuntimeManager.CreateInstance(path);
        _instance.start();
        _playing = path;
        _paused = false;
    }

    private void Stop()
    {
        if (_playing == null) return;

        _instance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        _instance.release();
        _playing = null;
    }
}
