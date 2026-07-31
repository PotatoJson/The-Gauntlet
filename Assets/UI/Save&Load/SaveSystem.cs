using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Reads and writes the single save slot as JSON under Application.persistentDataPath.
/// Pure file handling: it knows nothing about the game, so <see cref="SaveManager"/> owns
/// deciding what goes into a <see cref="GameSaveData"/>.
/// </summary>
public static class SaveSystem
{
    private const string FileName = "gauntlet_save.json";
    private const string TempFileName = "gauntlet_save.tmp";
    private const string BackupFileName = "gauntlet_save.bak";

    public static string SavePath => Path.Combine(Application.persistentDataPath, FileName);
    private static string TempPath => Path.Combine(Application.persistentDataPath, TempFileName);
    private static string BackupPath => Path.Combine(Application.persistentDataPath, BackupFileName);

    public static bool HasSave()
    {
        try
        {
            return File.Exists(SavePath);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SaveSystem] Could not check for a save file: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// Writes to a temp file first and then swaps it in, so a crash mid-write can never
    /// leave the player with a truncated save.
    /// </summary>
    public static bool Write(GameSaveData data)
    {
        if (data == null)
        {
            Debug.LogError("[SaveSystem] Refusing to write a null save.");
            return false;
        }

        try
        {
            data.saveVersion = GameSaveData.CurrentVersion;
            data.savedAtUtc = DateTime.UtcNow.ToString("o");

            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(TempPath, json);

            if (File.Exists(SavePath))
            {
                File.Replace(TempPath, SavePath, BackupPath);
            }
            else
            {
                File.Move(TempPath, SavePath);
            }

            Debug.Log($"[SaveSystem] Saved to {SavePath}");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveSystem] Failed to write the save: {e}");
            return false;
        }
    }

    public static bool TryRead(out GameSaveData data)
    {
        data = null;

        if (!HasSave())
        {
            Debug.Log("[SaveSystem] No save file found.");
            return false;
        }

        try
        {
            string json = File.ReadAllText(SavePath);
            data = JsonUtility.FromJson<GameSaveData>(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveSystem] Failed to read the save: {e}");
            return false;
        }

        if (data == null || !data.IsUsable)
        {
            Debug.LogWarning("[SaveSystem] The save file is empty or from an older version, so it is being ignored.");
            data = null;
            return false;
        }

        return true;
    }

    /// <summary>Reads the save purely to show it on a menu, without touching game state.</summary>
    public static GameSaveData Peek()
    {
        TryRead(out GameSaveData data);
        return data;
    }

    public static void Delete()
    {
        try
        {
            if (File.Exists(SavePath)) File.Delete(SavePath);
            if (File.Exists(TempPath)) File.Delete(TempPath);
            if (File.Exists(BackupPath)) File.Delete(BackupPath);
            Debug.Log("[SaveSystem] Save deleted.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveSystem] Failed to delete the save: {e}");
        }
    }
}
