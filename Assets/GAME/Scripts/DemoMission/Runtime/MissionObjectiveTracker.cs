using System;
using UnityEngine;

namespace Game.DemoMission.Runtime
{
    public sealed class MissionObjectiveTracker : MonoBehaviour
    {
        [SerializeField] private DemoMissionRuntime missionRuntime;
        [SerializeField] private MissionCompletionController completionController;

        public event Action OnObjectivesCompleted;

        private bool _completedRaised;

        private void Awake()
        {
            if (missionRuntime == null)
                missionRuntime = DemoMissionRuntime.GetOrCreate();

            if (completionController == null)
                completionController = FindFirstObjectByType<MissionCompletionController>();
        }

        private void OnEnable()
        {
            ResolveRuntime();
            if (missionRuntime == null)
                return;

            missionRuntime.OnMissionProgressChanged += HandleProgressChanged;
            missionRuntime.OnMissionCompleted += HandleMissionCompleted;
            HandleProgressChanged();
        }

        private void OnDisable()
        {
            if (missionRuntime == null)
                return;

            missionRuntime.OnMissionProgressChanged -= HandleProgressChanged;
            missionRuntime.OnMissionCompleted -= HandleMissionCompleted;
        }

        public bool AreObjectivesComplete()
        {
            return missionRuntime != null && missionRuntime.IsMissionComplete();
        }

        private void ResolveRuntime()
        {
            if (missionRuntime == null)
                missionRuntime = DemoMissionRuntime.GetOrCreate();
        }

        private void HandleProgressChanged()
        {
            if (missionRuntime == null)
                return;

            Debug.Log(
                $"[MissionObjectiveTracker] Progress enemies={missionRuntime.EnemyDefeatCount}, " +
                $"npcRescued={missionRuntime.IsNpcRescued}, complete={missionRuntime.IsMissionComplete()}",
                this
            );
        }

        private void HandleMissionCompleted()
        {
            if (_completedRaised)
                return;

            _completedRaised = true;
            OnObjectivesCompleted?.Invoke();

            if (completionController != null)
                completionController.HandleMissionCompleted();
            else
                Debug.LogWarning("[MissionObjectiveTracker] MissionCompletionController is not assigned.", this);
        }
    }
}
