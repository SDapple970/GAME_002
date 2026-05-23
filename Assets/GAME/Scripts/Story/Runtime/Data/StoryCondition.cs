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
        PersonaLevelAtLeast,
        ChapterAtLeast,
        MainProgressAtLeast,
        EventCompleted,
        EventNotCompleted
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
                case StoryConditionType.ChapterAtLeast:
                    if (!CanUseProgressCondition()) return false;
                    return StoryProgressManager.Instance.CurrentChapter >= intValue;
                case StoryConditionType.MainProgressAtLeast:
                    if (!CanUseProgressCondition()) return false;
                    return StoryProgressManager.Instance.MainProgress >= intValue;
                case StoryConditionType.EventCompleted:
                    if (!CanUseProgressCondition()) return false;
                    return StoryProgressManager.Instance.IsEventCompleted(key);
                case StoryConditionType.EventNotCompleted:
                    if (!CanUseProgressCondition()) return false;
                    return !StoryProgressManager.Instance.IsEventCompleted(key);
                default:
                    return false;
            }
        }

        private bool CanUseProgressCondition()
        {
            if (StoryProgressManager.Instance != null) return true;

            Debug.LogWarning($"[StoryCondition] StoryProgressManager missing for type='{type}' key='{key}'.");
            return false;
        }
    }
}
