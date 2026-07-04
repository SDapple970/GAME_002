using Game.Core;
using Game.Daily;
using Game.Reward;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Quest
{
    public sealed class QuestCompletionFlow : MonoBehaviour
    {
        [SerializeField] private QuestRuntime questRuntime;
        [SerializeField] private RewardService rewardService;
        [SerializeField] private DaySettlementFlow daySettlementFlow;
        [SerializeField] private bool enterRewardStateOnCompletion;
        [SerializeField] private bool grantRewardOnCompletion = true;
        [SerializeField] private bool notifyDaySettlementOnQuestCompletion;
        [SerializeField] private int fallbackRewardGold;
        [SerializeField] private int fallbackRewardExp;

        private readonly HashSet<string> _rewardedQuestIds = new();
        private bool _missingRewardServiceWarned;
        private bool _missingRewardDefinitionWarned;
        private bool _duplicateRewardWarned;
        private bool _missingDaySettlementFlowWarned;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();

            if (questRuntime != null)
                questRuntime.OnQuestCompleted += HandleQuestCompleted;
        }

        private void OnDisable()
        {
            if (questRuntime != null)
                questRuntime.OnQuestCompleted -= HandleQuestCompleted;
        }

        public void CompleteQuest(string questId)
        {
            if (questRuntime == null)
                questRuntime = FindFirstObjectByType<QuestRuntime>();

            questRuntime?.CompleteQuest(questId);
            RewardGrantResult rewardResult = TryGrantQuestReward(questId);
            TryNotifyDaySettlement(questId, rewardResult);

            if (enterRewardStateOnCompletion && GameStateMachine.Instance != null)
                GameStateMachine.Instance.SetState(GameState.Reward);
        }

        private void HandleQuestCompleted(string questId)
        {
            Debug.Log($"[QuestCompletionFlow] Quest completed. questId={questId}", this);
            RewardGrantResult rewardResult = TryGrantQuestReward(questId);
            TryNotifyDaySettlement(questId, rewardResult);

            if (enterRewardStateOnCompletion && GameStateMachine.Instance != null)
                GameStateMachine.Instance.SetState(GameState.Reward);
        }

        private void ResolveReferences()
        {
            if (questRuntime == null)
                questRuntime = FindFirstObjectByType<QuestRuntime>();

            if (rewardService == null)
                rewardService = RewardService.Instance != null
                    ? RewardService.Instance
                    : FindFirstObjectByType<RewardService>();

            if (daySettlementFlow == null)
                daySettlementFlow = DaySettlementFlow.Instance != null
                    ? DaySettlementFlow.Instance
                    : FindFirstObjectByType<DaySettlementFlow>();
        }

        private RewardGrantResult TryGrantQuestReward(string questId)
        {
            if (!grantRewardOnCompletion)
                return RewardGrantResult.Empty;

            if (string.IsNullOrWhiteSpace(questId))
                return RewardGrantResult.Empty;

            if (!_rewardedQuestIds.Add(questId))
            {
                WarnDuplicateRewardBlocked(questId);
                return RewardGrantResult.Empty;
            }

            ResolveReferences();
            if (rewardService == null)
            {
                WarnMissingRewardService(questId);
                return RewardGrantResult.Empty;
            }

            int gold = Mathf.Max(0, fallbackRewardGold);
            int exp = Mathf.Max(0, fallbackRewardExp);
            bool hasDefinitionReward = questRuntime != null &&
                                       questRuntime.TryGetQuestReward(questId, out int definitionGold, out int definitionExp);
            if (hasDefinitionReward)
            {
                gold = definitionGold;
                exp = definitionExp;
            }
            else if (gold <= 0 && exp <= 0)
            {
                WarnMissingRewardDefinition(questId);
                return RewardGrantResult.Empty;
            }

            return rewardService.GrantReward(new RewardGrantRequest(
                RewardSourceType.QuestCompletion,
                questId,
                gold,
                exp));
        }

        private void TryNotifyDaySettlement(string questId, RewardGrantResult rewardResult)
        {
            if (!notifyDaySettlementOnQuestCompletion || string.IsNullOrWhiteSpace(questId))
                return;

            ResolveReferences();
            if (daySettlementFlow == null)
            {
                WarnMissingDaySettlementFlow(questId);
                return;
            }

            daySettlementFlow.PrepareSettlement(DaySettlementRequest.ForQuest(questId, rewardResult));
        }

        private void WarnMissingRewardService(string questId)
        {
            if (_missingRewardServiceWarned)
                return;

            _missingRewardServiceWarned = true;
            Debug.LogWarning($"[QuestCompletionFlow] RewardService is missing. Quest reward was not granted. questId={questId}", this);
        }

        private void WarnMissingRewardDefinition(string questId)
        {
            if (_missingRewardDefinitionWarned)
                return;

            _missingRewardDefinitionWarned = true;
            Debug.LogWarning($"[QuestCompletionFlow] Quest reward definition is missing or empty. Reward grant skipped. questId={questId}", this);
        }

        private void WarnDuplicateRewardBlocked(string questId)
        {
            if (_duplicateRewardWarned)
                return;

            _duplicateRewardWarned = true;
            Debug.LogWarning($"[QuestCompletionFlow] Duplicate quest reward blocked. questId={questId}", this);
        }

        private void WarnMissingDaySettlementFlow(string questId)
        {
            if (_missingDaySettlementFlowWarned)
                return;

            _missingDaySettlementFlowWarned = true;
            Debug.LogWarning($"[QuestCompletionFlow] DaySettlementFlow is missing. Quest completion settlement notification skipped. questId={questId}", this);
        }
    }
}
