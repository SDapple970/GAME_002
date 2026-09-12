using System.Collections.Generic;
using Game.Core;
using Game.Daily;
using Game.NonCombat.Save;
using Game.Reward;
using UnityEngine;

namespace Game.Quest
{
    public sealed class QuestCompletionFlow : MonoBehaviour, ISaveDataConsumer
    {
        [SerializeField] private QuestRuntime questRuntime;
        [SerializeField] private RewardService rewardService;
        [SerializeField] private DaySettlementFlow daySettlementFlow;
        [SerializeField] private bool enterRewardStateOnCompletion;
        [SerializeField] private bool grantRewardOnCompletion = true;
        [SerializeField] private bool notifyDaySettlementOnQuestCompletion;
        [SerializeField] private int fallbackRewardGold;
        [SerializeField] private int fallbackRewardExp;

        private readonly HashSet<string> _claimedQuestIds = new();
        private readonly HashSet<string> _rewardedQuestIds = new();
        private readonly Queue<string> _pendingQuestIds = new();
        private QuestRuntime _subscribedRuntime;
        private GameStateMachine _subscribedStateMachine;
        private bool _missingRewardServiceWarned;
        private bool _missingRewardDefinitionWarned;
        private bool _duplicateRewardWarned;
        private bool _missingDaySettlementFlowWarned;
        private bool _missingFlowControllerWarned;

        private void Awake()
        {
            ResolveReferences();
            LogRuntimeInstances("Awake");
        }

        private void OnEnable()
        {
            ResolveReferences();
            Subscribe();
            LogRuntimeInstances("OnEnable");
            TryProcessPending();
        }

        private void Start()
        {
            // RuntimeBootstrapper creates the canonical GameStateMachine from its
            // AfterSceneLoad callback. A scene-authored completion flow can therefore
            // be enabled before the singleton exists; retry once after bootstrap so a
            // combat-time completion is released when gameplay returns to Exploration.
            ResolveReferences();
            Subscribe();
            LogRuntimeInstances("Start called");
            TryProcessPending();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void CompleteQuest(string questId)
        {
            ResolveReferences();
            questRuntime?.CompleteQuest(questId);
        }

        public void RestoreSaveData(GameSaveData saveData)
        {
            // These sets are only in-session delivery guards. They must never carry a
            // pre-load completion into the restored snapshot. RewardService owns the
            // persisted reward ledger and remains the authoritative idempotence guard.
            _claimedQuestIds.Clear();
            _rewardedQuestIds.Clear();
            _pendingQuestIds.Clear();
        }

        private void HandleQuestCompleted(string questId)
        {
            Debug.Log(
                $"[QuestRewardTrace] QuestCompletionFlow.HandleQuestCompleted received. questId={questId}, currentGameState={GetCurrentGameState()}, pendingQueueCount={_pendingQuestIds.Count}, questCompletionFlowInstanceId={GetInstanceID()}",
                this);
            if (string.IsNullOrWhiteSpace(questId) || !_claimedQuestIds.Add(questId))
                return;

            _pendingQuestIds.Enqueue(questId);
            Debug.Log(
                $"[QuestRewardTrace] QuestCompletionFlow.HandleQuestCompleted enqueued. questId={questId}, currentGameState={GetCurrentGameState()}, pendingQueueCount={_pendingQuestIds.Count}, questCompletionFlowInstanceId={GetInstanceID()}",
                this);
            TryProcessPending();
        }

        private void HandleStateChanged(GameState previous, GameState next)
        {
            Debug.Log(
                $"[QuestRewardTrace] QuestCompletionFlow.HandleStateChanged. previous={previous}, next={next}, subscribedGameStateMachineInstanceId={GetInstanceId(_subscribedStateMachine)}, questCompletionFlowInstanceId={GetInstanceID()}",
                this);
            if (next == GameState.Exploration)
                TryProcessPending();
        }

        private void TryProcessPending()
        {
            bool safeToProcess = IsSafeToProcessCompletion();
            Debug.Log(
                $"[QuestRewardTrace] QuestCompletionFlow.TryProcessPending. pendingQueueCount={_pendingQuestIds.Count}, isSafeToProcessCompletion={safeToProcess}, currentGameState={GetCurrentGameState()}, gameStateMachineInstanceId={GetInstanceId(GameStateMachine.Instance)}, questCompletionFlowInstanceId={GetInstanceID()}",
                this);

            while (_pendingQuestIds.Count > 0 && safeToProcess)
            {
                string questId = _pendingQuestIds.Dequeue();
                Debug.Log($"[QuestCompletionFlow] Processing quest completion. questId={questId}", this);
                RewardGrantResult rewardResult = TryGrantQuestReward(questId);
                TryNotifyDaySettlement(questId, rewardResult);

                if (!enterRewardStateOnCompletion)
                    continue;

                if (!TryEnterRewardState())
                    continue;

                // Reward is now a blocking state. Remaining completions keep FIFO order
                // until the authoritative flow returns to Exploration.
                break;
            }
        }

        private static bool IsSafeToProcessCompletion()
        {
            return GameStateMachine.Instance == null ||
                   GameStateMachine.Instance.Is(GameState.Exploration);
        }

        private bool TryEnterRewardState()
        {
            if (GameFlowController.Instance != null)
                return GameFlowController.Instance.RequestState(GameState.Reward, nameof(QuestCompletionFlow));

            if (!_missingFlowControllerWarned)
            {
                _missingFlowControllerWarned = true;
                Debug.LogWarning("[QuestCompletionFlow] GameFlowController is missing. Quest Reward state was not entered.", this);
            }

            return false;
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

        private void Subscribe()
        {
            if (_subscribedRuntime != questRuntime)
            {
                if (_subscribedRuntime != null)
                    _subscribedRuntime.OnQuestCompleted -= HandleQuestCompleted;

                _subscribedRuntime = questRuntime;
                if (_subscribedRuntime != null)
                    _subscribedRuntime.OnQuestCompleted += HandleQuestCompleted;
            }

            GameStateMachine stateMachine = GameStateMachine.Instance;
            if (_subscribedStateMachine == stateMachine)
                return;

            if (_subscribedStateMachine != null)
                _subscribedStateMachine.OnStateChanged -= HandleStateChanged;

            _subscribedStateMachine = stateMachine;
            if (_subscribedStateMachine != null)
            {
                _subscribedStateMachine.OnStateChanged += HandleStateChanged;
                Debug.Log(
                    $"[QuestRewardTrace] QuestCompletionFlow.Subscribe GameStateMachine. subscribedGameStateMachineInstanceId={GetInstanceId(_subscribedStateMachine)}, questCompletionFlowInstanceId={GetInstanceID()}",
                    this);
            }
        }

        private void Unsubscribe()
        {
            if (_subscribedRuntime != null)
                _subscribedRuntime.OnQuestCompleted -= HandleQuestCompleted;
            if (_subscribedStateMachine != null)
                _subscribedStateMachine.OnStateChanged -= HandleStateChanged;

            _subscribedRuntime = null;
            _subscribedStateMachine = null;
        }

        private RewardGrantResult TryGrantQuestReward(string questId)
        {
            Debug.Log(
                $"[QuestRewardTrace] QuestCompletionFlow.TryGrantQuestReward begin. questId={questId}, grantRewardOnCompletion={grantRewardOnCompletion}, rewardServiceInstanceId={GetInstanceId(rewardService)}, questCompletionFlowInstanceId={GetInstanceID()}",
                this);
            if (!grantRewardOnCompletion || string.IsNullOrWhiteSpace(questId))
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
            int definitionGold = 0;
            int definitionExp = 0;
            bool hasDefinitionReward = questRuntime != null &&
                                       questRuntime.TryGetQuestReward(questId, out definitionGold, out definitionExp);
            Debug.Log(
                $"[QuestRewardTrace] QuestCompletionFlow.TryGrantQuestReward resolved definition. questId={questId}, tryGetQuestRewardResult={hasDefinitionReward}, definitionGold={definitionGold}, definitionExp={definitionExp}, fallbackGold={fallbackRewardGold}, fallbackExp={fallbackRewardExp}, rewardServiceInstanceId={GetInstanceId(rewardService)}",
                this);
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

            RewardResult reward = rewardService.GrantQuestCompletion(questId, gold, exp);
            Debug.Log(
                $"[QuestRewardTrace] QuestCompletionFlow.TryGrantQuestReward granted. questId={questId}, resolvedGold={gold}, resolvedExp={exp}, rewardServiceInstanceId={GetInstanceId(rewardService)}, appliedGold={reward.Gold}, appliedExp={reward.Exp}, duplicateBlocked={reward.DuplicateBlocked}",
                this);
            return new RewardGrantResult(
                RewardSourceType.QuestCompletion,
                $"quest:{questId}",
                gold,
                exp,
                reward.Gold,
                reward.Exp,
                null,
                0,
                null,
                0,
                reward.DuplicateBlocked,
                reward.PartialFailure,
                false);
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

            string displayTitle = null;
            questRuntime?.TryGetQuestTitle(questId, out displayTitle);
            daySettlementFlow.PrepareSettlement(DaySettlementRequest.ForQuest(questId, rewardResult, displayTitle));
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

        private void LogRuntimeInstances(string phase)
        {
            Debug.Log(
                $"[QuestRewardTrace] QuestCompletionFlow.{phase}. questRuntimeInstanceId={GetInstanceId(questRuntime)}, questCompletionFlowInstanceId={GetInstanceID()}, gameStateMachineInstanceId={GetInstanceId(GameStateMachine.Instance)}, rewardServiceInstanceId={GetInstanceId(rewardService)}, currencyWalletInstanceId={GetInstanceId(Game.NonCombat.Inventory.CurrencyWallet.Instance)}",
                this);
        }

        private static string GetCurrentGameState()
        {
            return GameStateMachine.Instance != null
                ? GameStateMachine.Instance.Current.ToString()
                : "null";
        }

        private static string GetInstanceId(Object value)
        {
            return value != null ? value.GetInstanceID().ToString() : "null";
        }
    }
}
