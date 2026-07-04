using System;
using TMPro;
using UnityEngine;
using Game.Quest;

namespace Game.DemoMission.Runtime
{
    public sealed class MissionObjectiveTracker : MonoBehaviour
    {
        [SerializeField] private DemoMissionRuntime missionRuntime;
        [SerializeField] private QuestRuntime questRuntime;
        [SerializeField] private MissionCompletionController completionController;
        [SerializeField] private TMP_Text objectiveText;
        [SerializeField] private bool preferQuestRuntime = true;

        public event Action OnObjectivesCompleted;

        private bool _completedRaised;

        private void Awake()
        {
            if (missionRuntime == null)
                missionRuntime = DemoMissionRuntime.GetOrCreate();

            if (questRuntime == null)
                questRuntime = FindFirstObjectByType<QuestRuntime>();

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

            if (questRuntime != null)
            {
                questRuntime.OnObjectiveProgressChanged += HandleQuestObjectiveProgressChanged;
                questRuntime.OnQuestCompleted += HandleQuestCompleted;
            }

            HandleProgressChanged();
        }

        private void OnDisable()
        {
            if (missionRuntime != null)
            {
                missionRuntime.OnMissionProgressChanged -= HandleProgressChanged;
                missionRuntime.OnMissionCompleted -= HandleMissionCompleted;
            }

            if (questRuntime != null)
            {
                questRuntime.OnObjectiveProgressChanged -= HandleQuestObjectiveProgressChanged;
                questRuntime.OnQuestCompleted -= HandleQuestCompleted;
            }
        }

        public bool AreObjectivesComplete()
        {
            if (HasQuestRuntimeProgress())
                return questRuntime.IsQuestComplete(missionRuntime.CurrentQuestId);

            return missionRuntime != null && missionRuntime.IsMissionComplete();
        }

        private void ResolveRuntime()
        {
            if (missionRuntime == null)
                missionRuntime = DemoMissionRuntime.GetOrCreate();

            if (questRuntime == null)
                questRuntime = FindFirstObjectByType<QuestRuntime>();
        }

        private void HandleProgressChanged()
        {
            if (missionRuntime == null)
                return;

            Debug.Log(
                $"[MissionObjectiveTracker] Progress enemies={missionRuntime.CurrentEnemyKills}, " +
                $"npcRescued={missionRuntime.IsNpcRescued}, complete={AreObjectivesComplete()}, " +
                $"source={(HasQuestRuntimeProgress() ? "QuestRuntime" : "DemoMissionRuntime")}",
                this
            );

            if (objectiveText != null)
                objectiveText.text = HasQuestRuntimeProgress() ? BuildQuestObjectiveText() : BuildObjectiveText();
        }

        private void HandleQuestObjectiveProgressChanged(string questId, string objectiveId, int current, int required)
        {
            if (IsCurrentQuest(questId))
                HandleProgressChanged();
        }

        private void HandleQuestCompleted(string questId)
        {
            if (IsCurrentQuest(questId))
                HandleMissionCompleted();
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

        private string BuildQuestObjectiveText()
        {
            string questId = missionRuntime.CurrentQuestId;
            int killRequired = questRuntime.GetObjectiveRequiredCount(questId, DemoMissionRuntime.EnemyDefeatedObjectiveId);
            int killProgress = questRuntime.GetObjectiveProgress(questId, DemoMissionRuntime.EnemyDefeatedObjectiveId);
            int talkRequired = questRuntime.GetObjectiveRequiredCount(questId, DemoMissionRuntime.NpcTalkedObjectiveId);
            int talkProgress = questRuntime.GetObjectiveProgress(questId, DemoMissionRuntime.NpcTalkedObjectiveId);
            int rescueRequired = questRuntime.GetObjectiveRequiredCount(questId, DemoMissionRuntime.NpcRescuedObjectiveId);
            int rescueProgress = questRuntime.GetObjectiveProgress(questId, DemoMissionRuntime.NpcRescuedObjectiveId);

            string text = $"Defeat {killProgress}/{killRequired}";
            if (talkRequired > 0 || talkProgress > 0)
                text += $", Talk {talkProgress}/{Mathf.Max(1, talkRequired)}";

            text += $", Rescue {rescueProgress}/{Mathf.Max(1, rescueRequired)}";
            return text;
        }

        private bool HasQuestRuntimeProgress()
        {
            return preferQuestRuntime &&
                   missionRuntime != null &&
                   questRuntime != null &&
                   questRuntime.HasQuest(missionRuntime.CurrentQuestId);
        }

        private bool IsCurrentQuest(string questId)
        {
            return missionRuntime != null &&
                   !string.IsNullOrWhiteSpace(questId) &&
                   questId == missionRuntime.CurrentQuestId;
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
