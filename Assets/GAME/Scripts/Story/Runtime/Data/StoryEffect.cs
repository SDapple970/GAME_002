// Assets/GAME/Scripts/Story/Runtime/Data/StoryEffect.cs
using Game.Story;
using Game.Story.Core;
using Game.Mission;
using Game.Mission.Data;
using Game.Quest;
using Game.Systems.Persona;
using UnityEngine;

namespace Game.Story.Data
{
    public enum StoryEffectType
    {
        SetBoolFlag,
        SetIntFlag,
        AddIntFlag,
        AddPersonaXp,
        SetChapter,
        SetMainProgress,
        AdvanceMainProgress,
        MarkEventCompleted,
        ClearEventCompleted,
        StartMission,
        CompleteMission,
        CompleteMissionObjective,
        PublishQuestEvent
    }

    [System.Serializable]
    public sealed class StoryEffect
    {
        [SerializeField] private StoryEffectType type;
        [SerializeField] private string key;
        [SerializeField] private bool boolValue;
        [SerializeField] private int intValue;
        [SerializeField] private PersonaStat personaStat;
        [SerializeField] private int xpAmount;
        [SerializeField] private MissionDefinitionSO missionDefinition;
        [SerializeField] private string missionId;
        [SerializeField] private string objectiveId;
        [SerializeField] private QuestEventType questEventType = QuestEventType.Unknown;

        public void Apply()
        {
            Apply(default);
        }

        internal void Apply(StoryEffectContext context)
        {
            switch (type)
            {
                case StoryEffectType.SetBoolFlag:
                    if (!CanUseFlagEffect()) return;
                    StoryFlagManager.Instance.SetBool(key, boolValue);
                    return;
                case StoryEffectType.SetIntFlag:
                    if (!CanUseFlagEffect()) return;
                    StoryFlagManager.Instance.SetInt(key, intValue);
                    return;
                case StoryEffectType.AddIntFlag:
                    if (!CanUseFlagEffect()) return;
                    StoryFlagManager.Instance.AddInt(key, intValue);
                    return;
                case StoryEffectType.AddPersonaXp:
                    if (PersonaStatusManager.Instance == null)
                    {
                        Debug.LogWarning($"[StoryEffect] PersonaStatusManager missing for stat='{personaStat}' xp={xpAmount}.");
                        return;
                    }

                    if (xpAmount <= 0)
                    {
                        Debug.LogWarning($"[StoryEffect] Ignored non-positive Persona XP amount={xpAmount}.");
                        return;
                    }

                    PersonaStatusManager.Instance.AddXp(personaStat, xpAmount);
                    return;
                case StoryEffectType.SetChapter:
                    if (!CanUseProgressEffect()) return;
                    StoryProgressManager.Instance.SetChapter(intValue);
                    return;
                case StoryEffectType.SetMainProgress:
                    if (!CanUseProgressEffect()) return;
                    StoryProgressManager.Instance.SetMainProgress(intValue);
                    return;
                case StoryEffectType.AdvanceMainProgress:
                    if (!CanUseProgressEffect()) return;
                    StoryProgressManager.Instance.AdvanceMainProgress(intValue <= 0 ? 1 : intValue);
                    return;
                case StoryEffectType.MarkEventCompleted:
                    if (!CanUseProgressEffect()) return;
                    StoryProgressManager.Instance.MarkEventCompleted(key);
                    return;
                case StoryEffectType.ClearEventCompleted:
                    if (!CanUseProgressEffect()) return;
                    StoryProgressManager.Instance.ClearEventCompleted(key);
                    return;
                case StoryEffectType.StartMission:
                    if (!CanUseMissionEffect()) return;
                    MissionManager.Instance.StartMission(missionDefinition);
                    return;
                case StoryEffectType.CompleteMission:
                    if (!CanUseMissionEffect()) return;
                    MissionManager.Instance.CompleteMission(missionId);
                    return;
                case StoryEffectType.CompleteMissionObjective:
                    if (!CanUseMissionEffect()) return;
                    MissionManager.Instance.CompleteObjective(missionId, objectiveId);
                    return;
                case StoryEffectType.PublishQuestEvent:
                    PublishQuestEvent(context);
                    return;
                default:
                    return;
            }
        }

        private void PublishQuestEvent(StoryEffectContext context)
        {
            if (questEventType == QuestEventType.Unknown ||
                string.IsNullOrWhiteSpace(missionId) ||
                string.IsNullOrWhiteSpace(objectiveId) ||
                intValue <= 0)
            {
                Debug.LogWarning(
                    $"[StoryEffect] Invalid authored QuestEvent. type={questEventType}, questId='{missionId}', objectiveId='{objectiveId}', amount={intValue}.");
                return;
            }

            QuestEventChannel.Publish(new QuestEvent(
                questEventType,
                missionId,
                objectiveId,
                intValue,
                context.Source,
                context.EventId));
        }

        private bool CanUseFlagEffect()
        {
            if (StoryFlagManager.Instance == null)
            {
                Debug.LogWarning($"[StoryEffect] StoryFlagManager missing for key='{key}'.");
                return false;
            }

            if (!string.IsNullOrEmpty(key)) return true;

            Debug.LogWarning("[StoryEffect] Empty flag key was ignored.");
            return false;
        }

        private bool CanUseProgressEffect()
        {
            if (StoryProgressManager.Instance != null) return true;

            Debug.LogWarning($"[StoryEffect] StoryProgressManager missing for type='{type}' key='{key}'.");
            return false;
        }

        private bool CanUseMissionEffect()
        {
            if (MissionManager.Instance != null) return true;

            Debug.LogWarning($"[StoryEffect] MissionManager missing for type='{type}' missionId='{missionId}' objectiveId='{objectiveId}'.");
            return false;
        }
    }

    internal readonly struct StoryEffectContext
    {
        public readonly GameObject Source;
        public readonly string EventId;

        public StoryEffectContext(GameObject source, string eventId)
        {
            Source = source;
            EventId = eventId;
        }
    }
}
