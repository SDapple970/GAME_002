// Assets/GAME/Scripts/Story/Runtime/Data/StoryEffect.cs
using Game.Common.Identity;
using Game.Story;
using Game.Story.Core;
using Game.Mission;
using Game.Mission.Data;
using Game.Quest;
using Game.Reward;
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
        PublishQuestEvent,
        GrantReward
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
        [SerializeField] private string rewardSourceId;
        [SerializeField] private int rewardGold;
        [SerializeField] private int rewardExp;
        [SerializeField] private string rewardItemId;
        [SerializeField] private int rewardItemCount;

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
                case StoryEffectType.GrantReward:
                    GrantReward(context);
                    return;
                default:
                    return;
            }
        }

        private void GrantReward(StoryEffectContext context)
        {
            RewardService service = RewardService.Instance;
            if (service == null)
            {
                Debug.LogWarning("[StoryEffect] RewardService missing. Story reward was not granted.");
                return;
            }

            string sourceId = string.IsNullOrWhiteSpace(rewardSourceId)
                ? context.OutcomeId
                : rewardSourceId.Trim();
            if (string.IsNullOrWhiteSpace(rewardSourceId))
            {
                Debug.LogWarning(
                    $"[StoryEffect] Using story action compatibility identity '{sourceId}'. New production reward effects should author rewardSourceId.");
            }

            RewardSourceType sourceType = context.OutcomeKind == StoryOutcomeKind.Choice
                ? RewardSourceType.Choice
                : RewardSourceType.Story;
            service.GrantReward(new RewardGrantRequest(
                sourceType,
                sourceId,
                Mathf.Max(0, rewardGold),
                Mathf.Max(0, rewardExp),
                rewardItemId,
                Mathf.Max(0, rewardItemCount)));
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
                new GameplayOutcomeIdentity(
                    // Choice Quest events intentionally retain the established Story
                    // source type so Schema 4 processed identities remain compatible.
                    GameplayOutcomeSourceType.Story,
                    context.OutcomeId),
                intValue,
                context.Source));
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

    internal enum StoryOutcomeKind
    {
        Story,
        Choice
    }

    internal readonly struct StoryEffectContext
    {
        public readonly GameObject Source;
        public readonly StoryOutcomeKind OutcomeKind;
        public readonly string StoryEventId;
        public readonly string NodeId;
        public readonly string ChoiceId;
        public readonly int AuthoredChoiceIndex;
        public readonly int EffectIndex;
        public readonly bool IsTimeoutSelection;

        public string OutcomeId
        {
            get
            {
                string prefix = $"story:{StoryEventId}:node:{NodeId}:";
                if (OutcomeKind == StoryOutcomeKind.Story)
                    return $"{prefix}effect:{EffectIndex}";

                if (!string.IsNullOrEmpty(ChoiceId))
                    return $"{prefix}choice-id:{ChoiceId}:effect:{EffectIndex}";

                string compatibilityKind = IsTimeoutSelection ? "timeout" : "choice";
                return $"{prefix}{compatibilityKind}:{AuthoredChoiceIndex}:effect:{EffectIndex}";
            }
        }

        public StoryEffectContext(
            GameObject source,
            StoryOutcomeKind outcomeKind,
            string storyEventId,
            string nodeId,
            string choiceId = null,
            int authoredChoiceIndex = -1,
            int effectIndex = -1,
            bool isTimeoutSelection = false)
        {
            Source = source;
            OutcomeKind = outcomeKind;
            StoryEventId = storyEventId;
            NodeId = nodeId;
            ChoiceId = choiceId;
            AuthoredChoiceIndex = authoredChoiceIndex;
            EffectIndex = effectIndex;
            IsTimeoutSelection = isTimeoutSelection;
        }

        public StoryEffectContext WithEffectIndex(int effectIndex)
        {
            return new StoryEffectContext(
                Source,
                OutcomeKind,
                StoryEventId,
                NodeId,
                ChoiceId,
                AuthoredChoiceIndex,
                effectIndex,
                IsTimeoutSelection);
        }
    }
}
