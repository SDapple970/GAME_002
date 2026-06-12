using System;
using TMPro;
using UnityEngine;

namespace Game.DemoMission.Runtime
{
    public sealed class MissionObjectiveTracker : MonoBehaviour
    {
        [SerializeField] private DemoMissionRuntime missionRuntime;
        [SerializeField] private MissionCompletionController completionController;
        [SerializeField] private TMP_Text objectiveText;

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
                $"[MissionObjectiveTracker] Progress enemies={missionRuntime.CurrentEnemyKills}, " +
                $"npcRescued={missionRuntime.IsNpcRescued}, complete={missionRuntime.IsMissionComplete()}",
                this
            );

            if (objectiveText != null)
                objectiveText.text = BuildObjectiveText();
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

        private string BuildObjectiveText()
        {
            if (missionRuntime == null || missionRuntime.CurrentMission == null)
                return "임무 정보 없음";

            int requiredKills = Mathf.Max(0, missionRuntime.CurrentMission.requiredEnemyKills);
            string rescued = missionRuntime.IsNpcRescued ? "구출 완료" : "구출 대상 미확보";
            return $"처치 {missionRuntime.CurrentEnemyKills}/{requiredKills}, {rescued}";
        }
    }
}
