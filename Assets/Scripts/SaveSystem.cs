using System;
using System.IO;
using UnityEngine;

namespace Save
{
    [Serializable]
    public class SaveData
    {
        public string sceneName;
        public float playerHealth;
        public int playerStamina;
        public string leftGauntlet;
        public string rightGauntlet;
        public bool[] chambersCleared;
        public Vector3 playerPosition;
        public bool hasPlayerPosition = false;
    }

    public static class SaveSystem
    {
        private static readonly string SaveFileName = Path.Combine(Application.persistentDataPath, "savegame.json");

        public static void Save(SaveData data)
        {
            try
            {
                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(SaveFileName, json);
#if UNITY_EDITOR
                Debug.Log($"SaveSystem: Saved game to {SaveFileName}");
#endif
            }
            catch (Exception e)
            {
                Debug.LogError("SaveSystem: Failed to save game: " + e);
            }
        }

        public static SaveData Load()
        {
            try
            {
                if (!File.Exists(SaveFileName)) return null;
                string json = File.ReadAllText(SaveFileName);
                SaveData data = JsonUtility.FromJson<SaveData>(json);
                return data;
            }
            catch (Exception e)
            {
                Debug.LogError("SaveSystem: Failed to load save: " + e);
                return null;
            }
        }

        public static void DeleteSave()
        {
            try
            {
                if (File.Exists(SaveFileName)) File.Delete(SaveFileName);
            }
            catch (Exception e)
            {
                Debug.LogError("SaveSystem: Failed to delete save: " + e);
            }
        }

        // Convenience: create a save using commonly available components
        public static void AutoSave(GameObject player, EnemySpawner spawner)
        {
            if (player == null) return;

            var ph = player.GetComponent<PlayerHealth>();
            var pc = player.GetComponent<PlayerCombat>();

            SaveData data = new SaveData();
            data.sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

            data.playerPosition = player.transform.position;
            data.hasPlayerPosition = true;

            if (ph != null) data.playerHealth = ph.CurrentHealth;
            if (pc != null) data.playerStamina = pc.GetCurrentStamina();

            data.leftGauntlet = (pc != null && pc.LeftGauntletData != null) ? pc.LeftGauntletData.name : string.Empty;
            data.rightGauntlet = (pc != null && pc.RightGauntletData != null) ? pc.RightGauntletData.name : string.Empty;

            if (spawner != null && spawner.chambers != null)
            {
                data.chambersCleared = new bool[spawner.chambers.Count];
                for (int i = 0; i < spawner.chambers.Count; i++)
                {
                    data.chambersCleared[i] = spawner.chambers[i].isCleared;
                }
            }

            Save(data);
        }
    }
}
