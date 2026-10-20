using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Doom
{
    [Serializable]
    public class SaveData
    {
        public int version;
        public string levelId, levelName, mapHash, savedAt;
        public int skill;
        public int kills, items, secrets;
        public float time;
        public PlayerSave player;
        public List<EnemySave> enemies = new();
        public List<PickupSave> pickups = new();
        public List<BarrelSave> barrels = new();
        public List<DoorSave> doors = new();
        public List<LiftSave> lifts = new();
        public List<Vector2Int> secretsFound = new();
        public bool[] seen;
    }

    [Serializable]
    public class PlayerSave
    {
        public Vector3 position;
        public float yaw;
        public PlayerSnapshot status;
        public bool[] keys;
    }

    [Serializable]
    public class EnemySave
    {
        public string id;
        public Vector3 position, spawnPosition, facing;
        public float spawnYaw, respawnTimer;
        public int health;
        public bool dead, awake;
    }

    [Serializable]
    public class PickupSave
    {
        public PickupType type;
        public Vector3 position;
        public bool dropped;
    }

    [Serializable]
    public class BarrelSave
    {
        public Vector3 position;
        public int health;
    }

    [Serializable]
    public class DoorSave
    {
        public int x, y, state;
        public float offset, wait;
    }

    [Serializable]
    public class LiftSave
    {
        public int index, state;
        public float height, wait;
    }

    /// <summary>JSON save files in &lt;persistentDataPath&gt;/saves: slot0..slot5 plus a quicksave slot.</summary>
    public static class SaveSystem
    {
        public const int Version = 1;
        public const int SlotCount = 6;
        public const string QuickSlot = "quick";

        static readonly Dictionary<string, SaveData> cache = new();

        public static string Folder => Path.Combine(Application.persistentDataPath, "saves");
        public static string SlotName(int index) => "slot" + index;
        static string PathFor(string slot) => Path.Combine(Folder, slot + ".json");

        public static bool Exists(string slot) => File.Exists(PathFor(slot));

        /// <summary>Cached read for menu listings; null when empty or unreadable.</summary>
        public static SaveData Peek(string slot)
        {
            if (cache.TryGetValue(slot, out var d)) return d;
            d = Read(slot, out _);
            cache[slot] = d;
            return d;
        }

        public static SaveData Read(string slot, out string error)
        {
            error = null;
            var path = PathFor(slot);
            if (!File.Exists(path))
            {
                error = "That slot is empty.";
                return null;
            }
            try
            {
                var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
                if (data == null || data.version != Version || data.player == null)
                {
                    error = "That save is from an incompatible version.";
                    return null;
                }
                return data;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Doom] Failed to read save '{path}': {e.Message}");
                error = "That save file is damaged.";
                return null;
            }
        }

        public static bool Write(string slot, SaveData data, out string error)
        {
            error = null;
            var path = PathFor(slot);
            try
            {
                Directory.CreateDirectory(Folder);
                var tmp = path + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(data));
                if (File.Exists(path)) File.Replace(tmp, path, null);
                else File.Move(tmp, path);
                cache[slot] = data;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Doom] Failed to write save '{path}': {e}");
                error = "Could not write the save file.";
                return false;
            }
        }

        public static void Delete(string slot)
        {
            var path = PathFor(slot);
            if (File.Exists(path)) File.Delete(path);
            cache.Remove(slot);
        }

        public static string Describe(SaveData d)
        {
            int t = Mathf.FloorToInt(d.time);
            return $"{d.levelId}  {t / 60}:{t % 60:00}  {d.savedAt}";
        }

        /// <summary>Detects saves made before a level's layout was edited.</summary>
        public static string MapHash(LevelDef def)
        {
            unchecked
            {
                int h = 17;
                foreach (var row in def.Map)
                    foreach (char c in row) h = h * 31 + c;
                if (def.Heights != null)
                    foreach (var row in def.Heights)
                        foreach (char c in row) h = h * 31 + c;
                return h.ToString("x8");
            }
        }
    }
}
