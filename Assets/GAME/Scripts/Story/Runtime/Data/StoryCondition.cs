// Assets/GAME/Scripts/Story/Runtime/Data/StoryCondition.cs
using Game.Story;
using Game.Story.Core;
using Game.Mission;
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
        EventNotCompleted,
        MissionActive,
        MissionCompleted,
        MissionObjectiveCompleted
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
        [SerializeField] private string missionId;
        [SerializeField] private string objectiveId;

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
                case StoryConditionType.MissionActive:
                    if (!CanUseMissionCondition()) return false;
                    return MissionManager.Instance.IsMissionActive(missionId);
                case StoryConditionType.MissionCompleted:
                    if (!CanUseMissionCondition()) return false;
                    return MissionManager.Instance.IsMissionCompleted(missionId);
                case StoryConditionType.MissionObjectiveCompleted:
                    if (!CanUseMissionCondition()) return false;
                    return MissionManager.Instance.IsObjectiveCompleted(missionId, objectiveId);
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

        private bool CanUseMissionCondition()
        {
            if (MissionManager.Instance != null) return true;

            Debug.LogWarning($"[StoryCondition] MissionManager missing for type='{type}' missionId='{missionId}' objectiveId='{objectiveId}'.");
            return false;
        }
    }
}
