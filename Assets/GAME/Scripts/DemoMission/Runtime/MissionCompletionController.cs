using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Core;
using Game.Daily;
using Game.DemoMission.UI;
using Game.Quest;

namespace Game.DemoMission.Runtime
{
    public sealed class MissionCompletionController : MonoBehaviour
    {
        [SerializeField] private DemoMissionRuntime missionRuntime;
        [SerializeField] private QuestRuntime questRuntime;
        [SerializeField] private DaySettlementFlow daySettlementFlow;
        [SerializeField] private MissionObjectiveTracker objectiveTracker;
        [SerializeField] private MissionCompletePanel completePanel;
        [SerializeField] private bool lockPlayerInputOnComplete = true;
        [SerializeField] private bool setGameStateToUIOnly = true;
        [SerializeField] private bool notifyDaySettlementOnMissionComplete;
        [SerializeField] private List<Behaviour> behavioursToDisableOnComplete = new();

        private readonly Dictionary<Behaviour, bool> _previousBehaviourStates = new();
        private bool _handled;
        private bool _duplicateCompletionWarned;
        private bool _missingDaySettlementFlowWarned;
        private Coroutine _waitRoutine;

        private void Awake()
        {
            if (missionRuntime == null)
                missionRuntime = DemoMissionRuntime.GetOrCreate();
            if (questRuntime == null)
                questRuntime = FindFirstObjectByType<QuestRuntime>();
            if (daySettlementFlow == null)
                daySettlementFlow = FindFirstObjectByType<DaySettlementFlow>();
            if (objectiveTracker == null)
                objectiveTracker = FindFirstObjectByType<MissionObjectiveTracker>();
            if (completePanel == null)
                completePanel = FindFirstObjectByType<MissionCompletePanel>();
        }

        private void OnEnable()
        {
            ResolveRuntime();

            if (missionRuntime != null)
                missionRuntime.OnMissionCompleted += HandleMissionCompleted;

            if (questRuntime != null)
                questRuntime.OnQuestCompleted += HandleQuestCompleted;

            if (objectiveTracker != null)
                objectiveTracker.OnObjectivesCompleted += HandleMissionCompleted;
        }

        private void OnDisable()
        {
            if (missionRuntime != null)
                missionRuntime.OnMissionCompleted -= HandleMissionCompleted;

            if (questRuntime != null)
                questRuntime.OnQuestCompleted -= HandleQuestCompleted;

            if (objectiveTracker != null)
                objectiveTracker.OnObjectivesCompleted -= HandleMissionCompleted;

            if (_waitRoutine != null)
            {
                StopCoroutine(_waitRoutine);
                _waitRoutine = null;
            }
        }

        public void HandleMissionCompleted()
        {
            if (_handled)
            {
                WarnDuplicateCompletionBlocked();
                return;
            }

            if (GameStateMachine.Instance != null && GameStateMachine.Instance.IsCombatState())
            {
                if (_waitRoutine == null)
                    _waitRoutine = StartCoroutine(Co_WaitUntilNotCombatThenComplete());
                else
                    WarnDuplicateCompletionBlocked();

                return;
            }

            ShowCompletion();
        }

        private void HandleQuestCompleted(string questId)
        {
            if (missionRuntime != null &&
                !string.IsNullOrWhiteSpace(missionRuntime.CurrentQuestId) &&
                questId != missionRuntime.CurrentQuestId)
            {
                return;
            }

            HandleMissionCompleted();
        }

        private IEnumerator Co_WaitUntilNotCombatThenComplete()
        {
            Debug.Log("[MissionCompletionController] Mission completed during combat. Waiting for combat state to end.", this);

            while (GameStateMachine.Instance != null && GameStateMachine.Instance.IsCombatState())
                yield return null;

            _waitRoutine = null;
            ShowCompletion();
        }

        private void ShowCompletion()
        {
            if (_handled)
                return;

            _handled = true;
            if (lockPlayerInputOnComplete)
                LockConfiguredBehaviours();

            if (setGameStateToUIOnly && GameStateMachine.Instance != null)
                GameStateMachine.Instance.SetState(GameState.UIOnly);

            TryNotifyDaySettlement();

            if (completePanel != null)
                completePanel.Show();
            else
                Debug.LogWarning("[MissionCompletionController] MissionCompletePanel is not assigned.", this);
        }

        private void ResolveRuntime()
        {
            if (missionRuntime == null)
                missionRuntime = DemoMissionRuntime.GetOrCreate();

            if (questRuntime == null)
                questRuntime = FindFirstObjectByType<QuestRuntime>();

            if (daySettlementFlow == null)
                daySettlementFlow = DaySettlementFlow.Instance != null
                    ? DaySettlementFlow.Instance
                    : FindFirstObjectByType<DaySettlementFlow>();
        }

        private void WarnDuplicateCompletionBlocked()
        {
            if (_duplicateCompletionWarned)
                return;

            _duplicateCompletionWarned = true;
            Debug.LogWarning("[MissionCompletionController] Duplicate mission completion blocked.", this);
        }

        private void TryNotifyDaySettlement()
        {
            if (!notifyDaySettlementOnMissionComplete)
                return;

            ResolveRuntime();
            if (daySettlementFlow == null)
            {
                WarnMissingDaySettlementFlow();
                return;
            }

            string missionId = missionRuntime != null ? missionRuntime.CurrentQuestId : string.Empty;
            daySettlementFlow.PrepareSettlement(DaySettlementRequest.ForMission(missionId));
        }

        private void WarnMissingDaySettlementFlow()
        {
            if (_missingDaySettlementFlowWarned)
                return;

            _missingDaySettlementFlowWarned = true;
            Debug.LogWarning("[MissionCompletionController] DaySettlementFlow is missing. Mission settlement notification skipped.", this);
        }

        private void LockConfiguredBehaviours()
        {
            _previousBehaviourStates.Clear();
            for (int i = 0; i < behavioursToDisableOnComplete.Count; i++)
            {
                Behaviour behaviour = behavioursToDisableOnComplete[i];
                if (behaviour == null)
                    continue;

                _previousBehaviourStates[behaviour] = behaviour.enabled;
                behaviour.enabled = false;
            }
        }
    }
}
