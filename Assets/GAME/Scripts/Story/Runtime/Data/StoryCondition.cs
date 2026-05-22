// Assets/GAME/Scripts/Story/Runtime/Data/StoryCondition.cs
using Game.Story;
using Game.Systems.Persona;
using UnityEngine;

namespace Game.Story.Data
{
    public enum StoryConditionType
    {
        BoolFlagEquals,
        IntFlagAtLeast,
        PersonaLevelAtLeast
    }

    [System.Serializable]
    public sealed class StoryCondition
    {
        [SerializeField] private StoryConditionType type;
        [SerializeField] private string key;
        [SerializeField] private bool boolValue;
        [SerializeField] private int intValue;
        [SerializeField] private PersonaStat personaStat;
        [SerializeField] private int requiredLevel = 1;

        public bool IsMet()
        {
            switch (type)
            {
                case StoryConditionType.BoolFlagEquals:
                    if (StoryFlagManager.Instance == null)
                    {
                        Debug.LogWarning($"[StoryCondition] StoryFlagManager missing for bool key='{key}'.");
                        return false;
                    }
                    return StoryFlagManager.Instance.GetBool(key) == boolValue;
                case StoryConditionType.IntFlagAtLeast:
                    if (StoryFlagManager.Instance == null)
                    {
                        Debug.LogWarning($"[StoryCondition] StoryFlagManager missing for int key='{key}'.");
                        return false;
                    }
                    return StoryFlagManager.Instance.GetInt(key) >= intValue;
                case StoryConditionType.PersonaLevelAtLeast:
                    if (PersonaStatusManager.Instance == null)
                    {
                        Debug.LogWarning($"[StoryCondition] PersonaStatusManager missing for stat='{personaStat}'.");
                        return false;
                    }
                    return PersonaStatusManager.Instance.GetLevel(personaStat) >= requiredLevel;
                default:
                    return false;
            }
        }
    }
}
