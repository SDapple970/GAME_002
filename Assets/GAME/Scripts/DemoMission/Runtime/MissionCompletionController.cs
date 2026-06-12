using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Core;
using Game.DemoMission.UI;

namespace Game.DemoMission.Runtime
{
    public sealed class MissionCompletionController : MonoBehaviour
    {
        [SerializeField] private DemoMissionRuntime missionRuntime;
        [SerializeField] private MissionObjectiveTracker objectiveTracker;
        [SerializeField] private MissionCompletePanel completePanel;
        [SerializeField] private bool lockPlayerInputOnComplete = true;
        [SerializeField] private bool setGameStateToUIOnly = true;
        [SerializeField] private List<Behaviour> behavioursToDisableOnComplete = new();

        private readonly Dictionary<Behaviour, bool> _previousBehaviourStates = new();
        private bool _handled;
        private Coroutine _waitRoutine;

        private void Awake()
        {
            if (missionRuntime == null)
                missionRuntime = DemoMissionRuntime.GetOrCreate();
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

            if (objectiveTracker != null)
                objectiveTracker.OnObjectivesCompleted += HandleMissionCompleted;
        }

        private void OnDisable()
        {
            if (missionRuntime != null)
                missionRuntime.OnMissionCompleted -= HandleMissionCompleted;

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
                return;

            if (GameStateMachine.Instance != null && GameStateMachine.Instance.Is(GameState.Combat))
            {
                if (_waitRoutine == null)
                    _waitRoutine = StartCoroutine(Co_WaitUntilNotCombatThenComplete());
                return;
            }

            ShowCompletion();
        }

        private IEnumerator Co_WaitUntilNotCombatThenComplete()
        {
            Debug.Log("[MissionCompletionController] Mission completed during combat. Waiting for combat state to end.", this);

            while (GameStateMachine.Instance != null && GameStateMachine.Instance.Is(GameState.Combat))
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

            if (completePanel != null)
                completePanel.Show();
            else
                Debug.LogWarning("[MissionCompletionController] MissionCompletePanel is not assigned.", this);
        }

        private void ResolveRuntime()
        {
            if (missionRuntime == null)
                missionRuntime = DemoMissionRuntime.GetOrCreate();
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
