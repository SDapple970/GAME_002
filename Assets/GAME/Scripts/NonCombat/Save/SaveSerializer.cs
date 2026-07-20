using UnityEngine;

namespace Game.NonCombat.Save
{
    public static class SaveSerializer
    {
        public static string ToJson(SaveData data)
        {
            return JsonUtility.ToJson(data, true);
        }

        public static string ToJson(GameSaveData data)
        {
            return JsonUtility.ToJson(data, true);
        }

        public static SaveData FromJson(string json)
        {
            if (string.IsNullOrEmpty(json))
                return new SaveData();

            return JsonUtility.FromJson<SaveData>(json) ?? new SaveData();
        }

        public static GameSaveData FromGameSaveJson(string json)
        {
            if (string.IsNullOrEmpty(json))
                return new GameSaveData();

            return JsonUtility.FromJson<GameSaveData>(json) ?? new GameSaveData();
        }

        public static bool TryFromGameSaveJson(string json, out GameSaveData data)
        {
            data = null;
            if (string.IsNullOrWhiteSpace(json))
                return false;

            try
            {
                data = JsonUtility.FromJson<GameSaveData>(json);
                return data != null;
            }
            catch
            {
                return false;
            }
        }
    }
}
