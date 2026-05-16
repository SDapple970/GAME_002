using UnityEngine;

namespace Game.NonCombat.Save
{
    public static class SaveSerializer
    {
        public static string ToJson(SaveData data)
        {
            return JsonUtility.ToJson(data, true);
        }

        public static SaveData FromJson(string json)
        {
            if (string.IsNullOrEmpty(json))
                return new SaveData();

            return JsonUtility.FromJson<SaveData>(json) ?? new SaveData();
        }
    }
}
